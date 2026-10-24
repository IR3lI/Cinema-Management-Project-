using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Cinemas.Domain;

public enum RoomType
{
    [Display(Name = "Tiêu chuẩn")]
    Standard,

    [Display(Name = "VIP")]
    VIP,

    [Display(Name = "IMAX")]
    IMAX
}

/// <summary>
/// Bảng phòng chiếu (auditoriums / screening_rooms) - Quản lý kích thước ma trận lưới 2D và cấu hình phòng.
/// Liên kết N-1 với Cụm rạp (Cinema).
/// </summary>
public class ScreeningRoom
{
    [Key]
    public int RoomId { get; set; }

    [Display(Name = "Cụm rạp")]
    public int? CinemaId { get; set; }

    [ForeignKey(nameof(CinemaId))]
    public Cinema? Cinema { get; set; }

    [Required(ErrorMessage = "Tên phòng không được để trống")]
    [StringLength(100)]
    [Display(Name = "Tên phòng")]
    public string RoomName { get; set; } = string.Empty;

    [Display(Name = "Loại phòng")]
    public RoomType RoomType { get; set; } = RoomType.Standard;

    /// <summary>
    /// Tổng số hàng trong ma trận lưới 2D (bao gồm cả hàng dành cho hành lang đi lại).
    /// </summary>
    [Range(1, 50)]
    [Display(Name = "Tổng số hàng lưới (TotalRows)")]
    public int TotalRows { get; set; } = 10;

    /// <summary>
    /// Tổng số cột trong ma trận lưới 2D (bao gồm cả cột dành cho lối đi / hành lang).
    /// </summary>
    [Range(1, 50)]
    [Display(Name = "Tổng số cột lưới (TotalCols)")]
    public int TotalCols { get; set; } = 16;

    /// <summary>
    /// Danh sách các hàng lối đi / hành lang ngang (ví dụ: "4,8"), ngăn cách bởi dấu phẩy.
    /// </summary>
    [StringLength(255)]
    [Display(Name = "Hàng hành lang ngang (AisleRows)")]
    public string? AisleRows { get; set; }

    /// <summary>
    /// Danh sách các cột lối đi / hành lang dọc (ví dụ: "5,12"), ngăn cách bởi dấu phẩy.
    /// </summary>
    [StringLength(255)]
    [Display(Name = "Cột hành lang dọc (AisleCols)")]
    public string? AisleCols { get; set; }

    // Tương thích ngược với các controller / view cũ
    [Display(Name = "Số hàng ghế vật lý")]
    public int Rows
    {
        get => TotalRows;
        set => TotalRows = value;
    }

    [Display(Name = "Số cột ghế vật lý")]
    public int SeatsPerRow
    {
        get => TotalCols;
        set => TotalCols = value;
    }

    [Display(Name = "Sức chứa")]
    public int Capacity => Seats != null && Seats.Count > 0 ? Seats.Count(s => s.IsActive) : (TotalRows * TotalCols);

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
