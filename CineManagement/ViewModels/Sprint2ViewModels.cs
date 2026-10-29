using System.ComponentModel.DataAnnotations;
using CineManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CineManagement.ViewModels;

// ── Screening Room Form ────────────────────────────────────────────────────
public class ScreeningRoomFormViewModel
{
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Tên phòng không được để trống")]
    [StringLength(100)]
    [Display(Name = "Tên phòng")]
    public string RoomName { get; set; } = string.Empty;

    [Display(Name = "Loại phòng")]
    public RoomType RoomType { get; set; } = RoomType.Standard;

    [Required]
    [Range(1, 26, ErrorMessage = "Số hàng từ 1 đến 26")]
    [Display(Name = "Số hàng (tối đa 26)")]
    public int Rows { get; set; } = 8;

    [Required]
    [Range(1, 30, ErrorMessage = "Số ghế mỗi hàng từ 1 đến 30")]
    [Display(Name = "Số ghế mỗi hàng")]
    public int SeatsPerRow { get; set; } = 12;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
}

// ── Showtime Form ──────────────────────────────────────────────────────────
public class ShowtimeFormViewModel
{
    public int ShowtimeId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phim")]
    [Display(Name = "Phim")]
    public int MovieId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng chiếu")]
    [Display(Name = "Phòng chiếu")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giờ chiếu")]
    [Display(Name = "Giờ chiếu")]
    public DateTime StartTime { get; set; } = DateTime.Today.AddHours(9);

    [Required(ErrorMessage = "Vui lòng nhập giá vé")]
    [Range(0, 10000000, ErrorMessage = "Giá vé không hợp lệ")]
    [Display(Name = "Giá vé (VNĐ)")]
    public decimal TicketPrice { get; set; } = 90000;

    [Display(Name = "Trạng thái")]
    public ShowtimeStatus Status { get; set; } = ShowtimeStatus.Available;

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    // Dropdowns
    public IEnumerable<SelectListItem> Movies { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> Rooms  { get; set; } = new List<SelectListItem>();
}

// ── Showtime Index Filter ──────────────────────────────────────────────────
public class ShowtimeIndexViewModel
{
    public IEnumerable<Showtime> Showtimes { get; set; } = new List<Showtime>();
    public int? MovieId { get; set; }
    public int? RoomId  { get; set; }
    public DateTime? Date { get; set; }
    public IEnumerable<SelectListItem> Movies { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> Rooms  { get; set; } = new List<SelectListItem>();
}

// ── Food Form ─────────────────────────────────────────────────────────────
public class FoodFormViewModel
{
    public int FoodItemId { get; set; }

    [Required(ErrorMessage = "Tên món không được để trống")]
    [StringLength(200)]
    [Display(Name = "Tên món")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giá")]
    [Range(0, 10000000)]
    [Display(Name = "Giá (VNĐ)")]
    public decimal Price { get; set; }

    [Display(Name = "Danh mục")]
    public FoodCategory Category { get; set; } = FoodCategory.Combo;

    public string? ImageUrl { get; set; }

    [Display(Name = "Ảnh món ăn")]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Còn phục vụ")]
    public bool IsAvailable { get; set; } = true;
}
