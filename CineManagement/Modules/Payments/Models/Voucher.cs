using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Payments.Domain;

public class Voucher
{
    [Key]
    public int VoucherId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Mã giảm giá")]
    public string Code { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Loại giảm")]
    public DiscountType DiscountType { get; set; } = DiscountType.Percent;

    [Range(0, 100)]
    [Display(Name = "Phần trăm giảm (%)")]
    public decimal DiscountPercent { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Số tiền giảm cố định")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Giảm tối đa (VNĐ)")]
    public decimal? MaxDiscountAmount { get; set; }

    [Column(TypeName = "decimal(15,0)")]
    [Display(Name = "Đơn tối thiểu (VNĐ)")]
    public decimal MinOrderAmount { get; set; }

    [Display(Name = "Hiệu lực từ")]
    public DateTime ValidFrom { get; set; } = DateTime.Today;

    [Display(Name = "Hết hạn")]
    public DateTime ValidTo { get; set; } = DateTime.Today.AddDays(30);

    [Display(Name = "Giới hạn lượt dùng (toàn hệ thống)")]
    public int? UsageLimit { get; set; }

    [Display(Name = "Giới hạn mỗi user")]
    public int PerUserLimit { get; set; } = 1;

    [Display(Name = "Đã sử dụng")]
    public int UsedCount { get; set; }

    [Display(Name = "Kích hoạt")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
