using System.ComponentModel.DataAnnotations;

namespace CineManagement.Models;

/// <summary>
/// Đánh giá phim của khách (SCRUM-28). Unique (UserId, MovieId): mỗi user 1 review / phim.
/// </summary>
public class Review
{
    [Key]
    public int ReviewId { get; set; }

    [Required]
    public int MovieId { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Range(1, 5)]
    [Display(Name = "Điểm (1–5)")]
    public int Rating { get; set; }

    [StringLength(2000)]
    [Display(Name = "Nhận xét")]
    public string? Comment { get; set; }

    [Display(Name = "Trạng thái")]
    public ReviewStatus Status { get; set; } = ReviewStatus.Published;

    [Display(Name = "Ngày tạo")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Movie? Movie { get; set; }
    public ApplicationUser? User { get; set; }
}
