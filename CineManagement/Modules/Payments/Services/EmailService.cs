using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using CineManagement.Common;
using CineManagement.Data;
using CineManagement.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CineManagement.Services;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "CineStar Cinema";
    public string CinemaName { get; init; } = "CineStar Cinema";
}

public sealed class EmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<SmtpOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SendTicketEmailAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var booking = await db.Bookings
                .AsNoTracking()
                .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime).ThenInclude(s => s!.Room)
                .Include(b => b.Tickets)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

            if (booking == null)
            {
                _logger.LogWarning("Không tìm thấy đơn hàng #{BookingId} để gửi email vé.", bookingId);
                return;
            }

            var toEmail = booking.ContactEmail ?? booking.User?.Email;
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Đơn hàng #{BookingId} không có email nhận vé.", bookingId);
                return;
            }

            var orderDto = new OrderResponseDto
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                MovieTitle = booking.Showtime?.Movie?.Title ?? "Phim chiếu rạp",
                CinemaName = _options.CinemaName ?? "CineStar Cinema",
                RoomName = booking.Showtime?.Room?.RoomName ?? "Phòng chiếu",
                StartTime = booking.Showtime?.StartTime ?? DateTime.Now,
                FinalAmount = booking.FinalAmount,
                Tickets = booking.Tickets.Select(t => new TicketEmailDto
                {
                    TicketId = t.TicketId,
                    TicketCode = t.TicketCode,
                    SeatCode = t.SeatCode,
                    Status = t.Status
                }).ToList()
            };

            await SendTicketEmailAsync(toEmail, orderDto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xử lý gửi email vé đơn #{BookingId}", bookingId);
        }
    }

    public async Task SendTicketEmailAsync(
        string toEmail,
        OrderResponseDto orderDetails,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromEmail) || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning("Chưa cấu hình đầy đủ SMTP Gmail trong appsettings.json. Bỏ qua gửi email thực tế cho đơn {BookingCode}.", orderDetails.BookingCode);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = $"Vé điện tử {orderDetails.BookingCode} - {orderDetails.MovieTitle}",
            Body = BuildTicketHtml(orderDetails),
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(toEmail));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.UserName, _options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Sent e-ticket for booking {BookingCode} to {Email}.", orderDetails.BookingCode, toEmail);
    }

    private static string BuildTicketHtml(OrderResponseDto order)
    {
        static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);

        var seatList = string.Join(", ", order.Tickets.Select(t => t.SeatCode));
        var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={Uri.EscapeDataString(order.BookingCode)}";

        var ticketCardHtml = $"""
            <div style="background-color:#1c1917;border:2px solid #059669;border-radius:16px;padding:22px 18px;margin-bottom:18px;text-align:center;">
                <div style="display:inline-block;background-color:#059669;color:#ffffff;font-weight:800;font-size:14px;padding:6px 22px;border-radius:24px;letter-spacing:1px;margin-bottom:14px;">
                    GHẾ: {Encode(seatList)} ({order.Tickets.Count} VÉ)
                </div>
                <div style="background-color:#ffffff;padding:12px;border-radius:14px;display:inline-block;margin:0 auto 12px;box-shadow:0 4px 14px rgba(0,0,0,0.45);">
                    <img src="{qrUrl}" alt="QR {Encode(order.BookingCode)}" width="170" height="170" style="display:block;border:0;outline:none;" />
                </div>
                <div style="font-family:Consolas,monospace,sans-serif;font-size:18px;font-weight:900;color:#34d399;letter-spacing:3px;">
                    {Encode(order.BookingCode)}
                </div>
                <div style="font-size:12px;color:#a1a1aa;margin-top:8px;">
                    📱 Quét mã QR duy nhất này tại cửa phòng chiếu để vào rạp (áp dụng cho toàn bộ {order.Tickets.Count} ghế)
                </div>
            </div>
            """;

        return $"""
            <!doctype html>
            <html lang="vi">
            <head><meta charset="utf-8"></head>
            <body style="font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;background-color:#0c0a09;margin:0;padding:24px;color:#f8fafc;">
              <div style="max-width:560px;margin:0 auto;background-color:#18181b;border-radius:18px;overflow:hidden;border:1px solid #27272a;box-shadow:0 12px 30px rgba(0,0,0,0.6);">
                <!-- Header -->
                <div style="background:linear-gradient(135deg,#059669,#10b981);padding:24px 20px;text-align:center;">
                  <h1 style="margin:0;color:#ffffff;font-size:22px;letter-spacing:1px;">🎬 CINESTAR CINEMA</h1>
                  <p style="margin:6px 0 0;color:#d1fae5;font-size:14px;font-weight:500;">Vé Điện Tử Xác Nhận Đặt Chỗ Thành Công</p>
                </div>
                <!-- Content -->
                <div style="padding:24px 28px;">
                  <div style="background-color:#09090b;border-radius:14px;padding:16px 20px;margin-bottom:20px;border:1px dashed #3f3f46;text-align:center;">
                    <div style="font-size:11px;color:#a1a1aa;text-transform:uppercase;letter-spacing:1.5px;font-weight:700;">Mã Đặt Vé (Booking Code)</div>
                    <div style="font-size:28px;font-weight:900;color:#34d399;letter-spacing:3px;margin-top:4px;font-family:Consolas,monospace;">{Encode(order.BookingCode)}</div>
                  </div>

                  <table style="width:100%;border-collapse:collapse;font-size:14px;color:#d4d4d8;margin-bottom:22px;">
                    <tr>
                      <td style="padding:8px 0;color:#a1a1aa;width:35%;">Phim:</td>
                      <td style="padding:8px 0;font-weight:700;color:#ffffff;font-size:16px;">{Encode(order.MovieTitle)}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px 0;color:#a1a1aa;">Phòng chiếu:</td>
                      <td style="padding:8px 0;font-weight:600;color:#ffffff;">{Encode(order.RoomName)} ({Encode(order.CinemaName)})</td>
                    </tr>
                    <tr>
                      <td style="padding:8px 0;color:#a1a1aa;">Suất chiếu:</td>
                      <td style="padding:8px 0;font-weight:700;color:#fbbf24;">{order.StartTime.ToVietnameseDateTimeString()}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px 0;color:#a1a1aa;">Tổng thanh toán:</td>
                      <td style="padding:8px 0;font-weight:800;color:#34d399;font-size:17px;">{order.FinalAmount:N0} đ</td>
                    </tr>
                  </table>

                  <div style="margin-top:20px;">
                    <div style="font-size:12px;font-weight:800;color:#a1a1aa;text-transform:uppercase;letter-spacing:1px;margin-bottom:12px;text-align:center;">
                      🎟️ VÉ ĐIỆN TỬ &amp; MÃ QR VÀO RẠP ({order.Tickets.Count} vé)
                    </div>
                    {ticketCardHtml}
                  </div>

                  <div style="margin-top:24px;padding:14px;background-color:#27272a;border-radius:12px;font-size:13px;color:#cbd5e1;line-height:1.6;border-left:4px solid #10b981;">
                    💡 <strong>Hướng dẫn vào rạp:</strong> Vui lòng có mặt trước giờ chiếu 15 phút. Xuất trình mã QR duy nhất trên trực tiếp từ màn hình điện thoại cho nhân viên soát vé tại cửa phòng chiếu để check-in cho tất cả ghế.
                  </div>
                </div>
                <!-- Footer -->
                <div style="background-color:#09090b;padding:16px;text-align:center;font-size:12px;color:#71717a;border-top:1px solid #27272a;">
                  Hệ thống Rạp chiếu phim CineStar Cinema<br/>Email này được gửi tự động đến hộp thư của bạn.
                </div>
              </div>
            </body>
            </html>
            """;
    }
}
