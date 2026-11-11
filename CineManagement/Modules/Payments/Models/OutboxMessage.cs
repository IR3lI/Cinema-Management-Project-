using System.ComponentModel.DataAnnotations;

namespace CineManagement.Modules.Payments.Domain;

/// <summary>
/// Bảng lưu trữ thông điệp sự kiện theo mẫu Transactional Outbox Pattern (outboxmessages).
/// Được ghi trong cùng Database Transaction với nghiệp vụ thanh toán để đảm bảo At-Least-Once Delivery.
/// </summary>
public class OutboxMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Loại sự kiện")]
    public string Type { get; set; } = "TicketEmail"; // TicketEmail, PaymentReceipt, ...

    /// <summary>
    /// Nội dung dữ liệu JSON (ví dụ: {"BookingId": 12, "BookingCode": "BK-..."})
    /// </summary>
    [Required]
    [Display(Name = "Dữ liệu JSON")]
    public string Payload { get; set; } = string.Empty;

    [Display(Name = "Thời điểm tạo (UTC)")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Thời điểm xử lý thành công (UTC)")]
    public DateTime? ProcessedAt { get; set; }

    [Display(Name = "Số lần thử lại")]
    public int RetryCount { get; set; } = 0;

    [Display(Name = "Số lần thử tối đa")]
    public int MaxRetries { get; set; } = 3;

    [StringLength(2000)]
    [Display(Name = "Lỗi gần nhất")]
    public string? LastError { get; set; }
}
