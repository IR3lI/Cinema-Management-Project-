using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Ticketing.Domain;

/// <summary>
/// Bảng trạng thái ghế theo suất chiếu (showtime_seats) - Single Source of Truth cho việc giữ và đặt vé.
/// </summary>
public class ShowtimeSeat
{
    [Key]
    public int ShowtimeSeatId { get; set; }

    [Required]
    [Display(Name = "Suất chiếu")]
    public int ShowtimeId { get; set; }

    [Required]
    [Display(Name = "Ghế")]
    public int SeatId { get; set; }

    [Display(Name = "Loại ghế")]
    public int? SeatTypeId { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Giá vé áp dụng")]
    public decimal Price { get; set; }

    [Display(Name = "Trạng thái ghế")]
    public ShowtimeSeatStatus Status { get; set; } = ShowtimeSeatStatus.Available;

    [StringLength(450)]
    [Display(Name = "Mã người dùng giữ ghế")]
    public string? HeldByUserId { get; set; }

    [Display(Name = "Thời điểm hết hạn giữ")]
    public DateTime? HoldExpiresAt { get; set; }

    /// <summary>
    /// Kiểm soát tranh chấp đồng thời (Optimistic Concurrency) tương thích cả MySQL và SQLite.
    /// Mỗi lần update trạng thái, tăng Version lên 1.
    /// </summary>
    [ConcurrencyCheck]
    [Display(Name = "Phiên bản dữ liệu")]
    public int Version { get; set; } = 1;

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Showtime? Showtime { get; set; }
    public Seat? Seat { get; set; }
    public SeatType? SeatType { get; set; }
    public ApplicationUser? HeldByUser { get; set; }
    public Ticket? Ticket { get; set; }
}
