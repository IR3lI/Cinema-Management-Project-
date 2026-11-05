using System.Text.Json;
using CineManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace CineManagement.Modules.Payments.Services;

/// <summary>
/// Background Worker thực thi Transactional Outbox Pattern:
/// Quét định kỳ mỗi 5 giây bảng OutboxMessages (ProcessedAt == null && RetryCount < 3),
/// giải mã Payload và gửi Email vé QR kèm cơ chế Retry an toàn.
/// </summary>
public sealed class OutboxMessageWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxMessageWorker> _logger;

    public OutboxMessageWorker(
        IServiceProvider serviceProvider,
        ILogger<OutboxMessageWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            await ProcessPendingOutboxMessagesAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessPendingOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var pendingMessages = await db.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.RetryCount < m.MaxRetries)
                .OrderBy(m => m.CreatedAt)
                .Take(10)
                .ToListAsync(cancellationToken);

            if (pendingMessages.Count == 0)
                return;

            foreach (var msg in pendingMessages)
            {
                try
                {
                    if (string.Equals(msg.Type, "TicketEmail", StringComparison.OrdinalIgnoreCase))
                    {
                        using var doc = JsonDocument.Parse(msg.Payload);
                        if (doc.RootElement.TryGetProperty("BookingId", out var bookingIdProp) &&
                            bookingIdProp.TryGetInt32(out var bookingId))
                        {
                            await emailService.SendTicketEmailAsync(bookingId, cancellationToken);
                            msg.ProcessedAt = DateTime.UtcNow;
                            msg.LastError = null;
                            _logger.LogInformation(
                                "[OutboxMessageWorker] Đã xử lý thành công OutboxMessage #{Id} (TicketEmail cho Booking #{BookingId}).",
                                msg.Id, bookingId);
                        }
                        else
                        {
                            msg.RetryCount = msg.MaxRetries;
                            msg.LastError = "Payload thiếu BookingId hợp lệ.";
                        }
                    }
                    else
                    {
                        msg.ProcessedAt = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    msg.RetryCount++;
                    msg.LastError = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
                    _logger.LogWarning(
                        ex,
                        "[OutboxMessageWorker] Lỗi xử lý OutboxMessage #{Id} (Lần thử {Retry}/{Max}): {Error}",
                        msg.Id, msg.RetryCount, msg.MaxRetries, ex.Message);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OutboxMessageWorker] Lỗi chu kỳ quét bảng OutboxMessages.");
        }
    }
}
