using System.ComponentModel.DataAnnotations;
using CineManagement.Models;

namespace CineManagement.DTOs;

public sealed record CreateReviewRequestDto
{
    [Range(1, int.MaxValue)]
    public int MovieId { get; init; }

    [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5.")]
    public int Rating { get; init; }

    [StringLength(2000, ErrorMessage = "Nhận xét tối đa 2000 ký tự.")]
    public string? Comment { get; init; }
}

public sealed record ReviewResponseDto
{
    public int ReviewId { get; init; }
    public int MovieId { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public ReviewStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}
