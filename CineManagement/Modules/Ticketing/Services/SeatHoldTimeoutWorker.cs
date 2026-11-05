using CineManagement.Data;
using CineManagement.Hubs;
using CineManagement.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CineManagement.Modules.Ticketing.Services;

/// <summary>
/// Background Service định kỳ quét và giải phóng các ghế giữ quá hạn trên ShowtimeSeats,
/// đồng thời hủy các đơn hàng Pending đã hết hạn giữ chỗ, hoàn trả ghế về Available và phát SignalR realtime.
/// </summary>
public sealed class SeatHoldTimeoutWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<SeatBookingHub> _hubContext;
    private readonly ILogger<SeatHoldTimeoutWorker> _logger;

    public SeatHoldTimeoutWorker(
        IServiceProvider serviceProvider,
        IHubContext<SeatBookingHub> hubContext,
        ILogger<SeatHoldTimeoutWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            await ProcessExpiredSeatHoldsAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessExpiredSeatHoldsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;

            var releasedSeats = new List<ShowtimeSeat>();

            // 1. Quét trực tiếp bảng ShowtimeSeats có Status = Held và đã quá hạn
            var expiredShowtimeSeats = await db.ShowtimeSeats
                .Include(ss => ss.Seat)
                .Where(ss => ss.Status == ShowtimeSeatStatus.Held
                          && ss.HoldExpiresAt != null
                          && ss.HoldExpiresAt <= now)
                .ToListAsync(cancellationToken);

            if (expiredShowtimeSeats.Count > 0)
            {
                foreach (var ss in expiredShowtimeSeats)
                {
                    ss.Status = ShowtimeSeatStatus.Available;
                    ss.HeldByUserId = null;
                    ss.HoldExpiresAt = null;
                    ss.Version++;
                    ss.UpdatedAt = now;
                    releasedSeats.Add(ss);
                }

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("[SeatHoldTimeoutWorker] Đã giải phóng {Count} ghế ShowtimeSeats hết hạn giữ chỗ.", expiredShowtimeSeats.Count);
            }

            // 2. Quét các đơn hàng Pending quá hạn giữ chỗ (10 phút trước)
            var threshold = DateTime.Now.AddMinutes(-10);
            var staleBookings = await db.Bookings
                .Include(b => b.Payments)
                .Include(b => b.Tickets)
                .Where(b => b.Status == BookingStatus.Pending && b.BookingTime <= threshold)
                .ToListAsync(cancellationToken);

            if (staleBookings.Count > 0)
            {
                // Giải phóng toàn bộ ShowtimeSeats gắn với các Tickets của đơn quá hạn
                var staleShowtimeSeatIds = staleBookings
                    .SelectMany(b => b.Tickets)
                    .Where(t => t.ShowtimeSeatId.HasValue)
                    .Select(t => t.ShowtimeSeatId!.Value)
                    .Distinct()
                    .ToList();

                if (staleShowtimeSeatIds.Count > 0)
                {
                    var seatsToRelease = await db.ShowtimeSeats
                        .Include(ss => ss.Seat)
                        .Where(ss => staleShowtimeSeatIds.Contains(ss.ShowtimeSeatId) && ss.Status == ShowtimeSeatStatus.Held)
                        .ToListAsync(cancellationToken);

                    foreach (var ss in seatsToRelease)
                    {
                        ss.Status = ShowtimeSeatStatus.Available;
                        ss.HeldByUserId = null;
                        ss.HoldExpiresAt = null;
                        ss.Version++;
                        ss.UpdatedAt = now;
                        releasedSeats.Add(ss);
                    }
                }

                foreach (var booking in staleBookings)
                {
                    booking.Status = BookingStatus.Expired;
                    booking.UpdatedAt = now;

                    foreach (var ticket in booking.Tickets.Where(t => t.Status == TicketStatus.Held))
                    {
                        ticket.Status = TicketStatus.Cancelled;
                        ticket.UpdatedAt = now;
                    }

                    foreach (var payment in booking.Payments.Where(p => p.Status == PaymentStatus.Unpaid))
                    {
                        payment.Status = PaymentStatus.Failed;
                        payment.UpdatedAt = now;
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("[SeatHoldTimeoutWorker] Đã chuyển {Count} đơn hàng Pending quá hạn sang Expired và giải phóng ghế liên quan.", staleBookings.Count);
            }

            // 3. Phát sóng SignalR báo các ghế hết hạn đã trở về trạng thái Available
            if (releasedSeats.Count > 0)
            {
                foreach (var ss in releasedSeats.DistinctBy(x => x.ShowtimeSeatId))
                {
                    try
                    {
                        var groupName = SeatBookingHub.GetShowtimeGroup(ss.ShowtimeId);
                        await _hubContext.Clients.Group(groupName).SendAsync("SeatStatusChanged", new
                        {
                            showtimeId = ss.ShowtimeId,
                            seatId = ss.SeatId,
                            seatCode = ss.Seat?.SeatCode ?? $"#{ss.SeatId}",
                            status = "Available",
                            heldByUserId = (string?)null
                        }, cancellationToken);
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SeatHoldTimeoutWorker] Lỗi xử lý dọn dẹp ghế giữ quá hạn.");
        }
    }
}
