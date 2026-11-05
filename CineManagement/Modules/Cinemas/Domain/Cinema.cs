using System.ComponentModel.DataAnnotations;

namespace CineManagement.Modules.Cinemas.Domain;

/// <summary>
/// Bảng cụm rạp / chi nhánh thuộc hãng rạp chiếu phim (cinemas).
/// Liên kết 1-N với các phòng chiếu (ScreeningRooms).
/// </summary>
public class Cinema
{
    [Key]
    public int CinemaId { get; set; }

    [Required(ErrorMessage = "Mã cụm rạp không được để trống")]
    [StringLength(30)]
    [Display(Name = "Mã cụm rạp")]
    public string Code { get; set; } = string.Empty; // CS-QT, CS-HBT, CS-BD

    [Required(ErrorMessage = "Tên cụm rạp không được để trống")]
    [StringLength(150)]
    [Display(Name = "Tên cụm rạp")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ không được để trống")]
    [StringLength(300)]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tỉnh/Thành phố không được để trống")]
    [StringLength(100)]
    [Display(Name = "Tỉnh / Thành phố")]
    public string City { get; set; } = "TP. Hồ Chí Minh";

    [StringLength(30)]
    [Display(Name = "Hotline")]
    public string? Hotline { get; set; }

    [EmailAddress]
    [StringLength(150)]
    [Display(Name = "Email liên hệ")]
    public string? Email { get; set; }

    [StringLength(500)]
    [Display(Name = "Hình ảnh rạp")]
    public string? ImageUrl { get; set; }

    [StringLength(1000)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: 1 Cinema có nhiều ScreeningRooms
    public ICollection<ScreeningRoom> ScreeningRooms { get; set; } = new List<ScreeningRoom>();
}
