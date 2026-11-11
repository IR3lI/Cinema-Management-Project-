using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Payments.Domain;

/// <summary>
/// Bảng giao dịch thanh toán (payments) - Lưu vết mọi giao dịch, hỗ trợ retry, webhook/IPN và đối soát.
/// </summary>
public class Payment
{
    [Key]
    public int PaymentId { get; set; }

    [Required]
    [Display(Name = "Mã đơn hàng")]
    public int BookingId { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Số tiền thanh toán")]
    public decimal Amount { get; set; }

    [Display(Name = "Hình thức thanh toán")]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [Display(Name = "Cổng/Kênh thanh toán")]
    public PaymentProvider Provider { get; set; } = PaymentProvider.VietQR;

    [StringLength(100)]
    [Display(Name = "Mã giao dịch / Transaction Ref")]
    public string? TransactionCode { get; set; }

    [Display(Name = "Trạng thái thanh toán")]
    public PaymentStatus Status { get; set; } = PaymentStatus.Unpaid;

    [Display(Name = "Thời gian thanh toán thành công")]
    public DateTime? PaidAt { get; set; }

    [StringLength(2000)]
    [Display(Name = "Phản hồi từ cổng thanh toán")]
    public string? GatewayResponse { get; set; }

    [Display(Name = "Ngày tạo giao dịch")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Booking? Booking { get; set; }
}
