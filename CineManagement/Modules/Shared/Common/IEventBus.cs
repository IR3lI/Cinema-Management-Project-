namespace CineManagement.Modules.Shared.Common;

/// <summary>
/// Đánh dấu một sự kiện miền nghiệp vụ (Domain Event) trong kiến trúc Modular Monolith.
/// </summary>
public interface IEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}

/// <summary>
/// Interface trung tâm định nghĩa phần phát sự kiện (Event Publisher / Event Bus).
/// Được định nghĩa tại Common/Shared Kernel để mọi Module có thể phát tán sự kiện mà không phụ thuộc vào nhau.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Phát tán sự kiện miền nghiệp vụ đến các Module đăng ký lắng nghe (In-Memory).
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : class, IEvent;
}

/// <summary>
/// Interface cho các Module muốn xử lý (lắng nghe) một Domain Event cụ thể.
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : class, IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
