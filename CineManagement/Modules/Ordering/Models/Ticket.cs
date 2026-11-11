using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Ordering.Domain;

/// <summary>
/// Vé cho từng ghế, gắn với đơn hàng và suất chiếu.
/// Snapshot giá / mã ghế để lịch sử không đổi khi Seat hoặc Showtime bị sửa sau này.
/// Hỗ trợ quản lý trạng thái in vé vật lý khổ 80mm tại quầy POS (IsPrinted) chống quét lại mã QR.
/// </summary>
public class Ticket
{
    [Key]
    public int TicketId { get; set; }

    [Required]
    public int BookingId { get; set; }

    [Required]
    public int ShowtimeId { get; set; }

    [Required]
    public int SeatId { get; set; }

    /// <summary>
    /// Liên kết trực tiếp tới bản ghi trạng thái ghế của suất chiếu (showtime_seats)
    /// </summary>
    public int? ShowtimeSeatId { get; set; }

    /// <summary>Mã vé duy nhất, dùng cho QR / email (ví dụ: TCK-A1B2C3).</summary>
    [Required]
    [StringLength(30)]
    [Display(Name = "Mã vé")]
    public string TicketCode { get; set; } = string.Empty;

    [StringLength(10)]
    [Display(Name = "Mã ghế (snapshot)")]
    public string SeatCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Giá vé")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Trạng thái vé")]
    public TicketStatus Status { get; set; } = TicketStatus.Held;

    [StringLength(500)]
    [Display(Name = "Nội dung QR")]
    public string? QrPayload { get; set; }

    [Display(Name = "Thời điểm soát vé / đổi vé")]
    public DateTime? CheckedInAt { get; set; }

    [StringLength(450)]
    [Display(Name = "Nhân viên soát vé")]
    public string? CheckedInByUserId { get; set; }

    /// <summary>
    /// Đánh dấu vé đã được in thành vé nhiệt 80mm tại quầy POS (chống quét lại mã QR lấy vé 2 lần).
    /// </summary>
    [Display(Name = "Đã in vé vật lý tại quầy")]
    public bool IsPrinted { get; set; } = false;

    [Display(Name = "Thời điểm in vé")]
    public DateTime? PrintedAt { get; set; }

    [StringLength(450)]
    [Display(Name = "Nhân viên quầy in vé")]
    public string? PrintedByUserId { get; set; }

    [StringLength(50)]
    [Display(Name = "Mã phiếu in nhiệt 80mm")]
    public string? ThermalReceiptCode { get; set; }

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Booking? Booking { get; set; }
    public Showtime? Showtime { get; set; }
    public Seat? Seat { get; set; }
    public ShowtimeSeat? ShowtimeSeat { get; set; }

    [ForeignKey(nameof(CheckedInByUserId))]
    public ApplicationUser? CheckedInBy { get; set; }

    [ForeignKey(nameof(PrintedByUserId))]
    public ApplicationUser? PrintedBy { get; set; }
}
