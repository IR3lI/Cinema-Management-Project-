using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Cinemas.Domain;

/// <summary>
/// Bảng phân loại ghế (seat_types) - Quản lý cấu hình loại ghế động, phụ thu và số cột lưới chiếm dụng.
/// </summary>
public class SeatType
{
    [Key]
    public int SeatTypeId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Mã loại ghế")]
    public string Code { get; set; } = "STANDARD"; // STANDARD, VIP, COUPLE, BED

    [Required]
    [StringLength(100)]
    [Display(Name = "Tên loại ghế")]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Phụ thu (VNĐ)")]
    public decimal Surcharge { get; set; } = 0;

    /// <summary>
    /// Số cột chiếm dụng trên CSS Grid:
    /// 1 cho ghế đơn (Standard, VIP)
    /// 2 cho ghế đôi (Couple), ghế giường nằm (Bed)
    /// </summary>
    [Range(1, 4)]
    [Display(Name = "Số cột lưới (ColSpan)")]
    public int ColSpan { get; set; } = 1;

    [Required]
    [StringLength(20)]
    [Display(Name = "Mã màu Hex")]
    public string ColorCode { get; set; } = "#6c757d";

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
}
