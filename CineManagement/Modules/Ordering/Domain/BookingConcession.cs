using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Ordering.Domain;

/// <summary>
/// Chi tiết bắp nước trong đơn hàng (booking_concessions) - Hợp nhất chuẩn thay thế cho OrderFnB & BookingFoodItem.
/// </summary>
public class BookingConcession
{
    [Key]
    public int BookingConcessionId { get; set; }

    [Required]
    [Display(Name = "Đơn hàng")]
    public int BookingId { get; set; }

    [Required]
    [Display(Name = "Món F&B")]
    public int FoodItemId { get; set; }

    [Range(1, 100)]
    [Display(Name = "Số lượng")]
    public int Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Đơn giá (snapshot)")]
    public decimal UnitPrice { get; set; }

    [StringLength(200)]
    [Display(Name = "Tên món (snapshot)")]
    public string? ItemName { get; set; }

    [NotMapped]
    [Display(Name = "Thành tiền")]
    public decimal SubTotal => UnitPrice * Quantity;

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Booking? Booking { get; set; }
    public FoodItem? FoodItem { get; set; }
}
