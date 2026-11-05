namespace CineManagement.Modules.Cinemas.DTOs;

public class SaveSeatLayoutRequest
{
    public int RoomId { get; set; }
    public int TotalRows { get; set; }
    public int TotalCols { get; set; }
    public string? AisleRows { get; set; }
    public string? AisleCols { get; set; }
    public List<SeatLayoutItemDto> Seats { get; set; } = new();
}

public class SeatLayoutItemDto
{
    public int GridRow { get; set; }
    public int GridCol { get; set; }
    public int ColSpan { get; set; } = 1;
    public int SeatTypeId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public bool IsActive { get; set; } = true;
}
