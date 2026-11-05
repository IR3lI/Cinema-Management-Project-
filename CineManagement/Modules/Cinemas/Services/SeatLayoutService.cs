namespace CineManagement.Modules.Cinemas.Services;

public class SeatLayoutService : ISeatLayoutService
{
    private static readonly string[] ValidLetters =
    {
        "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M",
        "N", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
    };

    public AisleGuardValidationResult ValidateSeatLayout(
        int totalRows,
        int totalCols,
        string? aisleRowsStr,
        string? aisleColsStr,
        IEnumerable<SeatLayoutItem> seats)
    {
        if (totalRows < 3 || totalRows > 30 || totalCols < 4 || totalCols > 40)
        {
            return new AisleGuardValidationResult(false, "Kích thước ma trận không hợp lệ (Hàng: 3-30, Cột: 4-40).", "INVALID_DIMENSIONS");
        }

        var aisleRows = (aisleRowsStr ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToHashSet();

        var aisleCols = (aisleColsStr ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToHashSet();

        var occupied = new HashSet<(int Row, int Col)>();

        foreach (var s in seats)
        {
            // 1. Kiểm tra biên phòng chiếu
            if (s.GridRow < 1 || s.GridRow > totalRows)
            {
                return new AisleGuardValidationResult(
                    false,
                    $"Ghế {s.RowLabel}{s.SeatNumber} vượt quá giới hạn hàng (1-{totalRows}).",
                    "BOUNDARY_ROW_OVERFLOW");
            }

            if (s.GridCol < 1 || s.GridCol + s.ColSpan - 1 > totalCols)
            {
                return new AisleGuardValidationResult(
                    false,
                    $"Ghế {s.RowLabel}{s.SeatNumber} vượt quá giới hạn cột (1-{totalCols}).",
                    "BOUNDARY_COL_OVERFLOW");
            }

            // 2. Chặn đặt ghế trên hàng hành lang ngang
            if (aisleRows.Contains(s.GridRow))
            {
                return new AisleGuardValidationResult(
                    false,
                    $"Ghế {s.RowLabel}{s.SeatNumber} không thể đặt trên hàng hành lang ngang {s.GridRow}!",
                    "AISLE_ROW_COLLISION");
            }

            // 3. Thuật toán Aisle Guard: Chặn ghế (đặc biệt ghế đôi ColSpan=2) đè lên hành lang dọc
            for (int c = s.GridCol; c < s.GridCol + s.ColSpan; c++)
            {
                if (aisleCols.Contains(c))
                {
                    var seatDesc = s.ColSpan > 1 ? "Ghế đôi (Sweetbox)" : "Ghế";
                    return new AisleGuardValidationResult(
                        false,
                        $"Vi phạm quy tắc an toàn hành lang: {seatDesc} {s.RowLabel}{s.SeatNumber} tại Hàng {s.GridRow} đè lên cột hành lang dọc tại cột {c}!",
                        "AISLE_GUARD_VIOLATION",
                        new { gridRow = s.GridRow, gridCol = s.GridCol, colSpan = s.ColSpan, conflictAisleCol = c });
                }

                if (occupied.Contains((s.GridRow, c)))
                {
                    return new AisleGuardValidationResult(
                        false,
                        $"Phát hiện trùng lặp tọa độ ghế tại Hàng {s.GridRow}, Cột {c}!",
                        "COORDINATE_DUPLICATION");
                }
                occupied.Add((s.GridRow, c));
            }
        }

        return new AisleGuardValidationResult(true);
    }

    public void AutoNumberSeats(
        int totalRows,
        int totalCols,
        HashSet<int> aisleRows,
        HashSet<int> aisleCols,
        List<SeatLayoutItem> seats)
    {
        var rowGroups = seats
            .GroupBy(s => s.GridRow)
            .OrderBy(g => g.Key);

        int letterIdx = 0;
        foreach (var group in rowGroups)
        {
            if (aisleRows.Contains(group.Key))
                continue; // Bỏ qua hàng hành lang

            var letter = ValidLetters[letterIdx % ValidLetters.Length];
            letterIdx++;

            int seatNum = 1;
            foreach (var seat in group.OrderBy(s => s.GridCol))
            {
                seat.RowLabel = letter;
                seat.SeatNumber = seatNum;
                seatNum += seat.ColSpan; // Tăng theo độ rộng ghế (ghế đôi chiếm 2 số hoặc bước nhảy)
            }
        }
    }
}
