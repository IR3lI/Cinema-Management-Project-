using System.Data;
using System.Text.Json;
using CineManagement.Data;
using CineManagement.Hubs;
using CineManagement.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CineManagement.Modules.Payments.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IQrCodeService _qrCodeService;
    private readonly IHubContext<SeatBookingHub> _hubContext;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        ApplicationDbContext db,
        IEmailService emailService,
        IQrCodeService qrCodeService,
        IHubContext<SeatBookingHub> hubContext,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _emailService = emailService;
        _qrCodeService = qrCodeService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<Payment> CreatePaymentAsync(
        int bookingId,
        decimal amount,
        PaymentMethod method,
        PaymentProvider provider = PaymentProvider.VietQR,
        string? clientIp = null,
        CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        if (booking == null)
            throw new InvalidOperationException($"Không tìm thấy đơn hàng #{bookingId}");

        var now = DateTime.UtcNow;
        var transactionCode = $"PAY{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";

        var payment = new Payment
        {
            BookingId = bookingId,
            Amount = amount,
            Method = method,
            Provider = provider,
            TransactionCode = transactionCode,
            Status = PaymentStatus.Unpaid,
            CreatedAt = now
        };

        _db.Payments.Add(payment);
        booking.PaymentReference = transactionCode;
        booking.PaymentMethod = method;
        booking.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task<Payment?> GetPaymentByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        return await _db.Payments
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(p => p.BookingId == bookingId, cancellationToken);
    }

    public async Task<Payment?> GetPaymentByTransactionCodeAsync(string transactionCode, CancellationToken cancellationToken = default)
    {
        return await _db.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.TransactionCode == transactionCode, cancellationToken);
    }

    public async Task<bool> ConfirmPaymentAsync(
        int paymentId,
        string? transactionCode = null,
        string? gatewayResponse = null,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var payment = await _db.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b!.Tickets)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken);

            if (payment == null || payment.Booking == null)
            {
                _logger.LogWarning("Không tìm thấy giao dịch thanh toán #{PaymentId}", paymentId);
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (payment.Status == PaymentStatus.Paid)
            {
                _logger.LogInformation("Giao dịch #{PaymentId} đã được thanh toán trước đó", paymentId);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }

            var now = DateTime.UtcNow;
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = now;
            if (!string.IsNullOrEmpty(transactionCode))
                payment.TransactionCode = transactionCode;
            if (!string.IsNullOrEmpty(gatewayResponse))
                payment.GatewayResponse = gatewayResponse;
            payment.UpdatedAt = now;

            var booking = payment.Booking;
            booking.Status = BookingStatus.Confirmed;
            booking.PaymentStatus = PaymentStatus.Paid;
            booking.PaidAt = now;
            booking.UpdatedAt = now;

            // Chuyển toàn bộ ShowtimeSeats của vé sang Reserved và tăng Version
            var seatIds = booking.Tickets.Select(t => t.SeatId).Distinct().ToList();
            var showtimeSeats = await _db.ShowtimeSeats
                .Where(ss => ss.ShowtimeId == booking.ShowtimeId && seatIds.Contains(ss.SeatId))
                .ToListAsync(cancellationToken);

            foreach (var ss in showtimeSeats)
            {
                ss.Status = ShowtimeSeatStatus.Reserved;
                ss.HeldByUserId = null;
                ss.HoldExpiresAt = null;
                ss.Version++;
                ss.UpdatedAt = now;
            }

            // Đảm bảo vé có trạng thái Issued và sinh mã QR
            foreach (var ticket in booking.Tickets)
            {
                ticket.Status = TicketStatus.Issued;
                if (string.IsNullOrEmpty(ticket.QrPayload))
                {
                    ticket.QrPayload = $"cinema-ticket:{booking.BookingId}:{ticket.TicketCode}";
                }
                ticket.UpdatedAt = now;
            }

            // Transactional Outbox Pattern: Lưu thông điệp gửi Email vé QR trong cùng Database Transaction
            var outboxPayload = JsonSerializer.Serialize(new
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                RecipientEmail = booking.ContactEmail ?? string.Empty
            });

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "TicketEmail",
                Payload = outboxPayload,
                CreatedAt = now,
                RetryCount = 0,
                MaxRetries = 3
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Thanh toán đơn hàng #{BookingId} thành công qua giao dịch #{PaymentId}. Đã ghi OutboxMessage gửi email vé.",
                booking.BookingId, paymentId);

            // Phát sóng SignalR thời gian thực báo các ghế đã chuyển sang Reserved
            try
            {
                var groupName = SeatBookingHub.GetShowtimeGroup(booking.ShowtimeId);
                foreach (var ticket in booking.Tickets)
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("SeatStatusChanged", new
                    {
                        showtimeId = booking.ShowtimeId,
                        seatId = ticket.SeatId,
                        seatCode = ticket.SeatCode,
                        status = "Reserved",
                        heldByUserId = (string?)null
                    }, cancellationToken);
                }
            }
            catch (Exception hubEx)
            {
                _logger.LogWarning(hubEx, "Không thể phát sự kiện SignalR sau khi xác nhận thanh toán đơn #{BookingId}", booking.BookingId);
            }

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Lỗi xác nhận thanh toán #{PaymentId}", paymentId);
            throw;
        }
    }

    public string GenerateVietQrPayload(decimal amount, string transactionCode, string description)
    {
        // Chuỗi payload VietQR Napas 247 tiêu chuẩn
        return $"00020101021238540010A00000072701240006970422011009121112220208QRIBFTTA530370454{amount:0}5802VN62{description.Length + 4:D2}08{description}6304";
    }

    public string GenerateVietQrQuickLink(decimal amount, string transactionCode, string cinemaName = "CineManagement")
    {
        // Quicklink API chuẩn VietQR miễn phí để hiển thị ảnh QR code tức thì trên Web
        var encodedDesc = Uri.EscapeDataString($"{cinemaName} {transactionCode}");
        return $"https://img.vietqr.io/image/970422-0912111222-compact2.png?amount={amount:0}&addInfo={encodedDesc}&accountName=CINEMANAGEMENT";
    }
}
