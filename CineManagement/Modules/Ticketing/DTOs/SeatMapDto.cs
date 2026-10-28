using System.ComponentModel.DataAnnotations;
using CineManagement.Models;

namespace CineManagement.DTOs;

public enum SeatAvailabilityStatus
{
    [Display(Name = "Trống")]
    Available = 0,

    [Display(Name = "Đang giữ")]
    Held = 1,

    [Display(Name = "Đã bán")]
    Sold = 2
}

public sealed record SeatTypeLegendDto
{
    public int SeatTypeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Surcharge { get; init; }
    public int ColSpan { get; init; } = 1;
    public string ColorCode { get; init; } = "#6c757d";
    public string? Description { get; init; }
}

public sealed record SeatCellDto
{
    public int ShowtimeSeatId { get; init; }
    public int SeatId { get; init; }

    [Required]
    [StringLength(10)]
    public string SeatCode { get; init; } = string.Empty;

    [Required]
    [StringLength(5)]
    public string RowLabel { get; init; } = string.Empty;

    public int SeatNumber { get; init; }
    public int GridRow { get; init; } = 1;
    public int GridCol { get; init; } = 1;
    public int ColSpan { get; init; } = 1;
    public string ColorCode { get; init; } = "#6c757d";
    public int? SeatTypeId { get; init; }
    public string SeatTypeCode { get; init; } = "STANDARD";
    public string SeatTypeName { get; init; } = "Thường";
    public SeatTypeEnum SeatType { get; init; } = SeatTypeEnum.Standard;

    public SeatAvailabilityStatus Status { get; init; } = SeatAvailabilityStatus.Available;

    /// <summary>True khi ghế đang được chính user hiện tại giữ.</summary>
    public bool IsHeldByCurrentUser { get; init; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; init; }
}

public sealed record SeatRowDto
{
    [Required]
    [StringLength(5)]
    public string RowLabel { get; init; } = string.Empty;

    public int GridRow { get; init; } = 1;

    [Required]
    [MinLength(1)]
    public IReadOnlyList<SeatCellDto> Seats { get; init; } = [];
}

/// <summary>Sơ đồ ghế của một suất chiếu kèm kích thước lưới 2D và trạng thái thời gian thực.</summary>
public sealed record SeatMapDto
{
    [Range(1, int.MaxValue)]
    public int ShowtimeId { get; init; }

    [Range(1, int.MaxValue)]
    public int MovieId { get; init; }

    [Required]
    [StringLength(200)]
    public string MovieTitle { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int RoomId { get; init; }

    [Required]
    [StringLength(100)]
    public string RoomName { get; init; } = string.Empty;

    public int TotalRows { get; init; } = 10;
    public int TotalCols { get; init; } = 16;

    public DateTime StartTime { get; init; }

    [Range(0, double.MaxValue)]
    public decimal BaseTicketPrice { get; init; }

    public int TotalSeats { get; init; }
    public int AvailableSeats { get; init; }

    public DateTime? MyHoldExpiresAt { get; init; }

    public IReadOnlyList<SeatTypeLegendDto> SeatTypes { get; init; } = [];
    public IReadOnlyList<SeatRowDto> Rows { get; init; } = [];
    public IReadOnlyList<SeatCellDto> AllSeats { get; init; } = [];
}
