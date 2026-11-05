using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineManagement.Modules.Catalog.Domain;

public enum MovieStatus
{
    [Display(Name = "Sắp chiếu")]
    ComingSoon,

    [Display(Name = "Đang chiếu")]
    NowShowing,

    [Display(Name = "Đã kết thúc")]
    Ended
}

public class Movie
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

    [StringLength(100)]
    [Display(Name = "Quốc gia")]
    public string? Country { get; set; } = "Mỹ";

    [Range(1, 600)]
    [Display(Name = "Thời lượng (phút)")]
    public int Duration { get; set; }

    [Display(Name = "Ngày khởi chiếu")]
    [DataType(DataType.Date)]
    public DateTime? ReleaseDate { get; set; }

    [Display(Name = "Ngày kết thúc")]
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    [StringLength(500)]
    [Display(Name = "Ảnh bìa")]
    public string? PosterUrl { get; set; }

    [StringLength(500)]
    [Display(Name = "Trailer URL")]
    public string? TrailerUrl { get; set; }

    [Display(Name = "Trạng thái")]
    public MovieStatus Status { get; set; } = MovieStatus.ComingSoon;

    [Range(0, 10)]
    [Column(TypeName = "decimal(3,1)")]
    [Display(Name = "Đánh giá")]
    public decimal Rating { get; set; } = 0;

    [StringLength(10)]
    [Display(Name = "Phân loại (P/C13/C16/C18)")]
    public string? AgeRating { get; set; } = "P";

    [StringLength(100)]
    [Display(Name = "Ngôn ngữ")]
    public string? Language { get; set; } = "Tiếng Việt";

    [StringLength(50)]
    [Display(Name = "Định dạng (2D/3D/IMAX)")]
    public string? Format { get; set; } = "2D";

    [StringLength(100)]
    [Display(Name = "Phụ đề / Lồng tiếng")]
    public string? SubtitleType { get; set; } = "Phụ đề Việt";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;

    [Display(Name = "Ngày cập nhật")]
    public DateTime? UpdatedAt { get; set; }

    // FK
    [Display(Name = "Thể loại")]
    public int? GenreId { get; set; }
    public Genre? Genre { get; set; }

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
