using System.ComponentModel.DataAnnotations;

namespace CineManagement.DTOs;

/// <summary>Request khi user chọn ghế trên sơ đồ (SCRUM-15 / SCRUM-17).</summary>
public sealed record HoldSeatRequestDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Suất chiếu không hợp lệ.")]
    public int ShowtimeId { get; init; }

    [Required(ErrorMessage = "Vui lòng chọn ít nhất một ghế.")]
    [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một ghế.")]
    [MaxLength(10, ErrorMessage = "Không được chọn quá 10 ghế trong một lần đặt.")]
    public List<int> SeatIds { get; init; } = [];

    /// <summary>Thời gian giữ ghế (phút). Mặc định 10 — khớp HoldMinutes hiện tại.</summary>
    [Range(1, 30, ErrorMessage = "Thời gian giữ ghế phải từ 1 đến 30 phút.")]
    public int HoldMinutes { get; init; } = 10;
}

/// <summary>Kết quả giữ ghế — cần cho chữ ký HoldSeatAsync.</summary>
public sealed record HoldSeatResponseDto
{
    public bool Success { get; init; }

    [StringLength(300)]
    public string? Message { get; init; }

    public int? BookingId { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public IReadOnlyList<int> HeldSeatIds { get; init; } = [];

    public IReadOnlyList<int> ConflictSeatIds { get; init; } = [];
}
