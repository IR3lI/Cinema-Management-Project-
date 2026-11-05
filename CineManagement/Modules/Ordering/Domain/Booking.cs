using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Ordering.Domain;

public enum BookingStatus
{
    [Display(Name = "Chờ thanh toán")]
    Pending,

    [Display(Name = "Đã xác nhận")]
    Confirmed,

    [Display(Name = "Đã hủy")]
    Cancelled,

    [Display(Name = "Hết hạn")]
    Expired,

    [Display(Name = "Đã hoàn tiền")]
    Refunded
}

public enum PaymentMethod
{
    [Display(Name = "Tiền mặt tại quầy")]
    Cash,

    [Display(Name = "Thẻ ngân hàng")]
    BankCard,

    [Display(Name = "Ví điện tử")]
    EWallet
}

/// <summary>
/// Đơn hàng tổng (Order / Booking): vé + F&amp;B + voucher + thanh toán.
/// </summary>
public class Booking
{
    [Key]
    public int BookingId { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int ShowtimeId { get; set; }

    public int? VoucherId { get; set; }

    [StringLength(20)]
    [Display(Name = "Mã đặt vé")]
    public string BookingCode { get; set; } = string.Empty;

    [Display(Name = "Thời gian đặt")]
    public DateTime BookingTime { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Tiền vé")]
    public decimal TicketAmount { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Tiền F&B")]
    public decimal FnBAmount { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Tổng tiền gốc")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Tiền giảm giá")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Thành tiền")]
    public decimal FinalAmount { get; set; }

    [Display(Name = "Trạng thái đơn")]
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    [Display(Name = "Trạng thái thanh toán")]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    [Display(Name = "Phương thức thanh toán")]
    public PaymentMethod? PaymentMethod { get; set; }

    [StringLength(100)]
    [Display(Name = "Mã giao dịch")]
    public string? PaymentReference { get; set; }

    [Display(Name = "Thời gian thanh toán")]
    public DateTime? PaidAt { get; set; }

    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Email nhận vé")]
    public string? ContactEmail { get; set; }

    [StringLength(20)]
    [Display(Name = "SĐT liên hệ")]
    public string? ContactPhone { get; set; }

    [Display(Name = "Đã gửi email vé")]
    public bool TicketEmailSent { get; set; }

    [Display(Name = "Gửi email lúc")]
    public DateTime? TicketEmailSentAt { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ApplicationUser? User { get; set; }
    public Showtime? Showtime { get; set; }
    public Voucher? Voucher { get; set; }

    // 10 Bảng chuẩn: Tickets, Payments, BookingConcessions
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<BookingConcession> BookingConcessions { get; set; } = new List<BookingConcession>();
}
