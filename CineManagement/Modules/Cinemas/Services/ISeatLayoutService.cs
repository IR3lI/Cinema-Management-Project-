using CineManagement.Models;

namespace CineManagement.Modules.Cinemas.Services;

public record AisleGuardValidationResult(bool IsValid, string? ErrorMessage = null, string? ErrorCode = null, object? Details = null);

public interface ISeatLayoutService
{
    /// <summary>
    /// Kiểm tra tính hợp lệ của sơ đồ ghế theo Thuật toán Aisle Guard.
    /// Chặn triệt để ghế đôi (ColSpan = 2) đè lên cột hành lang dọc hoặc vượt biên.
    /// </summary>
    AisleGuardValidationResult ValidateSeatLayout(
        int totalRows,
        int totalCols,
        string? aisleRowsStr,
        string? aisleColsStr,
        IEnumerable<SeatLayoutItem> seats);

    /// <summary>
    /// Tự động đánh số thứ tự ghế liên tục (A, B, C... và 1, 2, 3...) bỏ qua các lối đi hành lang.
    /// </summary>
    void AutoNumberSeats(
        int totalRows,
        int totalCols,
        HashSet<int> aisleRows,
        HashSet<int> aisleCols,
        List<SeatLayoutItem> seats);
}

public class SeatLayoutItem
{
    public int GridRow { get; set; }
    public int GridCol { get; set; }
    public int ColSpan { get; set; } = 1;
    public int SeatTypeId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public bool IsActive { get; set; } = true;
}
