using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Ordering.Domain;

public enum FoodCategory
{
    [Display(Name = "Bắp rang")]
    Popcorn,

    [Display(Name = "Đồ uống")]
    Drink,

    [Display(Name = "Combo")]
    Combo,

    [Display(Name = "Snack")]
    Snack,

    [Display(Name = "Khác")]
    Other
}

/// <summary>
/// Bảng món bắp nước / combo (concessions / food_items)
/// </summary>
public class FoodItem
{
    [Key]
    public int FoodItemId { get; set; }

    [Required(ErrorMessage = "Tên món không được để trống")]
    [StringLength(200)]
    [Display(Name = "Tên món")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required]
    [Range(0, 10000000)]
    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Giá (VNĐ)")]
    public decimal Price { get; set; }

    [Display(Name = "Danh mục")]
    public FoodCategory Category { get; set; } = FoodCategory.Combo;

    [StringLength(500)]
    [Display(Name = "Ảnh món ăn")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Còn phục vụ")]
    public bool IsAvailable { get; set; } = true;

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<BookingConcession> BookingConcessions { get; set; } = new List<BookingConcession>();
}
