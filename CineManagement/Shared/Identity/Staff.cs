using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Models;

public class Staff
{
    public int StaffId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Chức vụ")]
    public string? Position { get; set; }

    [StringLength(100)]
    [Display(Name = "Phòng ban")]
    public string? Department { get; set; }

    [Display(Name = "Ngày vào làm")]
    [DataType(DataType.Date)]
    public DateTime HireDate { get; set; } = DateTime.Today;

    [Column(TypeName = "decimal(15,0)")]
    [Range(0, double.MaxValue)]
    [Display(Name = "Lương (VNĐ)")]
    public decimal Salary { get; set; } = 0;

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    // Navigation
    public ApplicationUser? User { get; set; }
}
