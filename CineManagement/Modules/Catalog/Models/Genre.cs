using System.ComponentModel.DataAnnotations;

namespace CineManagement.Modules.Catalog.Domain;

public class Genre
{
    public int GenreId { get; set; }

    [Required(ErrorMessage = "Tên thể loại không được để trống")]
    [StringLength(100)]
    [Display(Name = "Thể loại")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    // Navigation
    public ICollection<Movie> Movies { get; set; } = new List<Movie>();
}
