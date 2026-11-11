using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Catalog.Domain;

public enum ShowtimeStatus
{
    [Display(Name = "Còn vé")]
    Available,

    [Display(Name = "Hết vé")]
    SoldOut,

    [Display(Name = "Đã hủy")]
    Canceled,

    [Display(Name = "Đã kết thúc")]
    Ended
}

/// <summary>
/// Bảng suất chiếu (showtimes) - Quản lý phim, phòng chiếu, giờ chiếu và định dạng chiếu.
/// </summary>
public class Showtime
{
    [Key]
    public int ShowtimeId { get; set; }

    [Required]
    [Display(Name = "Phim")]
    public int MovieId { get; set; }

    [Required]
    [Display(Name = "Phòng chiếu")]
    public int RoomId { get; set; }

    [Required]
    [Display(Name = "Giờ chiếu")]
    public DateTime StartTime { get; set; }

    [Display(Name = "Giờ kết thúc")]
    public DateTime? EndTime { get; set; }

    [Required]
    [Range(0, 10000000)]
    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Giá vé cơ sở (VNĐ)")]
    public decimal TicketPrice { get; set; }

    [Display(Name = "Trạng thái")]
    public ShowtimeStatus Status { get; set; } = ShowtimeStatus.Available;

    [StringLength(20)]
    [Display(Name = "Định dạng chiếu (2D/3D/IMAX)")]
    public string Format { get; set; } = "2D";

    [StringLength(50)]
    [Display(Name = "Ngôn ngữ / Phụ đề")]
    public string SubtitleType { get; set; } = "Phụ đề";

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Movie? Movie { get; set; }
    public ScreeningRoom? Room { get; set; }
    public ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
