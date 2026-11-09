using CineManagement.Modules.Cinemas.Services;
using Xunit;

namespace CineManagement.Tests;

/// <summary>
/// Minh chứng kiểm thử tự động (Unit Tests) cho Ràng buộc Nghiệp vụ Aisle Guard:
/// Đảm bảo ghế đôi Sweetbox (ColSpan=2) không bao giờ bị bố trí đè lên hành lang thoát hiểm.
/// </summary>
public class AisleGuardSeatLayoutTests
{
    private readonly SeatLayoutService _service = new();

    [Fact]
    public void ValidateSeatLayout_WhenLayoutIsValid_ShouldReturnSuccess()
    {
        // Arrange: Phòng 10 hàng x 10 cột, hành lang tại cột 5
        var seats = new List<SeatLayoutItem>
        {
            new() { GridRow = 1, GridCol = 1, ColSpan = 1, RowLabel = "A", SeatNumber = 1 },
            new() { GridRow = 1, GridCol = 2, ColSpan = 1, RowLabel = "A", SeatNumber = 2 },
            // Ghế đôi Sweetbox ở cột 6 (ColSpan=2 chiếm cột 6, 7) - Không đè hành lang 5
            new() { GridRow = 10, GridCol = 6, ColSpan = 2, RowLabel = "J", SeatNumber = 1 }
        };

        // Act
        var result = _service.ValidateSeatLayout(10, 10, aisleRowsStr: "", aisleColsStr: "5", seats);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void ValidateSeatLayout_WhenSweetboxOverlapsAisleColumn_ShouldFailWithAisleGuardViolation()
    {
        // Arrange: Hành lang tại cột 4.
        // Ghế đôi Sweetbox đặt tại cột 3 với ColSpan=2 -> Chiếm cột 3 và cột 4 (Đè lên hành lang!)
        var seats = new List<SeatLayoutItem>
        {
            new() { GridRow = 10, GridCol = 3, ColSpan = 2, RowLabel = "J", SeatNumber = 1 }
        };

        // Act
        var result = _service.ValidateSeatLayout(10, 10, aisleRowsStr: "", aisleColsStr: "4", seats);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("AISLE_GUARD_VIOLATION", result.ErrorCode);
        Assert.Contains("đè lên cột hành lang dọc tại cột 4", result.ErrorMessage);
    }

    [Fact]
    public void ValidateSeatLayout_WhenSeatPlacedOnAisleRow_ShouldFailWithAisleRowCollision()
    {
        // Arrange: Hàng hành lang ngang tại hàng 5. Đặt ghế ngay tại hàng 5.
        var seats = new List<SeatLayoutItem>
        {
            new() { GridRow = 5, GridCol = 2, ColSpan = 1, RowLabel = "E", SeatNumber = 2 }
        };

        // Act
        var result = _service.ValidateSeatLayout(10, 10, aisleRowsStr: "5", aisleColsStr: "", seats);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("AISLE_ROW_COLLISION", result.ErrorCode);
    }

    [Fact]
    public void ValidateSeatLayout_WhenSeatExceedsColumnBoundary_ShouldFail()
    {
        // Arrange: Phòng 10 cột, ghế đôi đặt ở cột 10 với ColSpan=2 (vượt ra cột 11)
        var seats = new List<SeatLayoutItem>
        {
            new() { GridRow = 1, GridCol = 10, ColSpan = 2, RowLabel = "A", SeatNumber = 1 }
        };

        // Act
        var result = _service.ValidateSeatLayout(10, 10, aisleRowsStr: "", aisleColsStr: "", seats);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("BOUNDARY_COL_OVERFLOW", result.ErrorCode);
    }

    [Fact]
    public void ValidateSeatLayout_WhenTwoSeatsDuplicateCoordinate_ShouldFail()
    {
        // Arrange: 2 ghế cùng chiếm tọa độ (Hàng 2, Cột 3)
        var seats = new List<SeatLayoutItem>
        {
            new() { GridRow = 2, GridCol = 3, ColSpan = 1, RowLabel = "B", SeatNumber = 1 },
            new() { GridRow = 2, GridCol = 3, ColSpan = 1, RowLabel = "B", SeatNumber = 2 }
        };

        // Act
        var result = _service.ValidateSeatLayout(10, 10, aisleRowsStr: "", aisleColsStr: "", seats);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("COORDINATE_DUPLICATION", result.ErrorCode);
    }
}
