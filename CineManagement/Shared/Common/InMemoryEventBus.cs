namespace CineManagement.Modules.Shared.Common;

/// <summary>
/// Triển khai In-Memory Event Bus phục vụ giao tiếp bất đồng bộ giữa các Module trong cùng một tiến trình ứng dụng.
/// Đảm bảo tính lỏng lẻo (Decoupled): Module phát sự kiện không cần biết Module nào đang lắng nghe nó.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : class, IEvent
    {
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();

        _logger.LogInformation(
            "Phát sự kiện In-Memory: EventType={EventType}, EventId={EventId}", 
            typeof(TEvent).Name, 
            @event.EventId);

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Lỗi khi xử lý sự kiện: Handler={HandlerName}, EventType={EventType}", 
                    handler.GetType().Name, 
                    typeof(TEvent).Name);
                // Tuỳ chiến lược: log lỗi, không làm dừng luồng chính hoặc đẩy vào Dead Letter/Outbox
            }
        }
    }
}
