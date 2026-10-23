using System.ComponentModel.DataAnnotations;
using CineManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CineManagement.ViewModels;

// ── Create / Edit Movie ───────────────────────────────────────────────────────
public class MovieFormViewModel
{
    public int MovieId { get; set; }

    [Required(ErrorMessage = "Tên phim không được để trống")]
    [StringLength(200)]
    [Display(Name = "Tên phim")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [StringLength(200)]
    [Display(Name = "Đạo diễn")]
    public string? Director { get; set; }

    [StringLength(500)]
    [Display(Name = "Diễn viên")]
    public string? Actors { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập thời lượng")]
    [Range(1, 600, ErrorMessage = "Thời lượng phải từ 1 đến 600 phút")]
    [Display(Name = "Thời lượng (phút)")]
    public int Duration { get; set; }

    [Display(Name = "Ngày khởi chiếu")]
    [DataType(DataType.Date)]
    public DateTime? ReleaseDate { get; set; }

    [Display(Name = "Ngày kết thúc")]
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    public string? PosterUrl { get; set; }

    [Display(Name = "Ảnh bìa phim")]
    public IFormFile? PosterFile { get; set; }

    [StringLength(500)]
    [Display(Name = "Trailer URL (YouTube)")]
    public string? TrailerUrl { get; set; }

    [Display(Name = "Trạng thái")]
    public MovieStatus Status { get; set; } = MovieStatus.ComingSoon;

    [Range(0, 10)]
    [Display(Name = "Điểm đánh giá (0–10)")]
    public decimal Rating { get; set; } = 0;

    [StringLength(10)]
    [Display(Name = "Phân loại độ tuổi")]
    public string? AgeRating { get; set; } = "P";

    [StringLength(100)]
    [Display(Name = "Quốc gia sản xuất")]
    public string? Country { get; set; } = "Việt Nam";

    [StringLength(50)]
    [Display(Name = "Định dạng chiếu")]
    public string? Format { get; set; } = "2D";

    [StringLength(100)]
    [Display(Name = "Hình thức dịch")]
    public string? SubtitleType { get; set; } = "Phụ đề Việt";

    [StringLength(500)]
    [Display(Name = "Hoặc nhập trực tiếp URL Poster")]
    public string? DirectPosterUrl { get; set; }

    [StringLength(100)]
    [Display(Name = "Ngôn ngữ")]
    public string? Language { get; set; } = "Tiếng Việt";

    [Display(Name = "Thể loại")]
    public int? GenreId { get; set; }

    // Select list for dropdown
    public IEnumerable<SelectListItem> Genres { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> AgeRatings { get; set; } = new List<SelectListItem>
    {
        new("P – Phổ biến cho mọi lứa tuổi", "P"),
        new("K – Dưới 13 tuổi xem cùng phụ huynh", "K"),
        new("T13 (C13) – Cấm khán giả dưới 13 tuổi", "T13"),
        new("T16 (C16) – Cấm khán giả dưới 16 tuổi", "T16"),
        new("T18 (C18) – Cấm khán giả dưới 18 tuổi", "T18"),
        new("C – Cấm phổ biến", "C")
    };
}

// ── Movie Index (filter/search) ───────────────────────────────────────────────
public class MovieIndexViewModel
{
    public IEnumerable<Movie> Movies { get; set; } = new List<Movie>();
    public string? SearchTerm { get; set; }
    public int? GenreId { get; set; }
    public MovieStatus? Status { get; set; }
    public IEnumerable<SelectListItem> Genres { get; set; } = new List<SelectListItem>();
}
