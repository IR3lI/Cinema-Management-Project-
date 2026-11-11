using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Cinemas.Domain;

/// <summary>
/// Bảng ghế vật lý trong phòng chiếu (seats) - Chứa tọa độ lưới 2D (GridRow, GridCol) và liên kết loại ghế.
/// </summary>
public class Seat
{
    [Key]
    public int SeatId { get; set; }

    [Required]
    [Display(Name = "Phòng chiếu")]
    public int RoomId { get; set; }

    [Required]
    [StringLength(5)]
    [Display(Name = "Tên hàng")]
    public string RowLabel { get; set; } = "A"; // A, B, C...

    [Range(1, 50)]
    [Display(Name = "Số thứ tự ghế trong hàng")]
    public int SeatNumber { get; set; }

    /// <summary>
    /// Tọa độ hàng trên ma trận CSS Grid (bắt đầu từ 1, có tính cả hàng hành lang)
    /// </summary>
    [Range(1, 50)]
    [Display(Name = "Tọa độ hàng lưới (GridRow)")]
    public int GridRow { get; set; } = 1;

    /// <summary>
    /// Tọa độ cột trên ma trận CSS Grid (bắt đầu từ 1, có tính cả cột hành lang)
    /// </summary>
    [Range(1, 50)]
    [Display(Name = "Tọa độ cột lưới (GridCol)")]
    public int GridCol { get; set; } = 1;

    [Display(Name = "Loại ghế")]
    public int? SeatTypeId { get; set; }

    [ForeignKey(nameof(SeatTypeId))]
    public SeatType? SeatType { get; set; }

    /// <summary>
    /// Enum hỗ trợ tương thích ngược cho các controller / view cũ
    /// </summary>
    [Display(Name = "Loại ghế (Legacy)")]
    public SeatTypeEnum SeatTypeLegacy { get; set; } = SeatTypeEnum.Standard;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    // Display name: A1, B5, etc.
    public string SeatCode => $"{RowLabel}{SeatNumber}";

    // Navigation
    public ScreeningRoom? Room { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
}
