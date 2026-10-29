using System.ComponentModel.DataAnnotations;

namespace CineManagement.ViewModels;

// ── Create Staff ──────────────────────────────────────────────────────────────
public class StaffCreateViewModel
{
    // Account info
    [Required(ErrorMessage = "Họ tên không được để trống")]
    [StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống")]
    [Phone]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    // Staff info
    [StringLength(100)]
    [Display(Name = "Chức vụ")]
    public string? Position { get; set; }

    [StringLength(100)]
    [Display(Name = "Phòng ban")]
    public string? Department { get; set; }

    [Display(Name = "Ngày vào làm")]
    [DataType(DataType.Date)]
    public DateTime HireDate { get; set; } = DateTime.Today;

    [Range(0, double.MaxValue, ErrorMessage = "Lương không hợp lệ")]
    [Display(Name = "Lương (VNĐ)")]
    public decimal Salary { get; set; } = 0;

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    [Display(Name = "Vai trò")]
    public string Role { get; set; } = "Staff";
}

// ── Edit Staff ────────────────────────────────────────────────────────────────
public class StaffEditViewModel
{
    public int StaffId { get; set; }
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên không được để trống")]
    [StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Chức vụ")]
    public string? Position { get; set; }

    [StringLength(100)]
    [Display(Name = "Phòng ban")]
    public string? Department { get; set; }

    [Display(Name = "Ngày vào làm")]
    [DataType(DataType.Date)]
    public DateTime HireDate { get; set; }

    [Range(0, double.MaxValue)]
    [Display(Name = "Lương (VNĐ)")]
    public decimal Salary { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Vai trò")]
    public string Role { get; set; } = "Staff";
}
