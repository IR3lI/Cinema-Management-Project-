namespace CineManagement.DTOs;

public sealed record DailyRevenueDto
{
    public DateTime Date { get; init; }
    public int BookingCount { get; init; }
    public int TicketCount { get; init; }
    public decimal Revenue { get; init; }
    public decimal TicketRevenue { get; init; }
    public decimal FnBRevenue { get; init; }
    public decimal TotalRevenue { get; init; }
}

public sealed record TopMovieDto
{
    public int MovieId { get; init; }
    public string MovieTitle { get; init; } = string.Empty;
    public string? PosterUrl { get; init; }
    public string? GenreName { get; init; }
    public int TicketsSold { get; init; }
    public decimal TicketRevenue { get; init; }
    public decimal RevenueSharePercent { get; init; }
}

public sealed record TopFoodDto
{
    public int FoodItemId { get; init; }
    public string FoodName { get; init; } = string.Empty;
    public int QuantitySold { get; init; }
    public decimal Revenue { get; init; }
    public decimal RevenueSharePercent { get; init; }
}

public sealed record PaymentMethodReportDto
{
    public string PaymentMethodName { get; init; } = string.Empty;
    public int BookingCount { get; init; }
    public decimal Revenue { get; init; }
    public decimal Percentage { get; init; }
}

public sealed record RoomOccupancyRateDto
{
    public int ShowtimeId { get; init; }
    public int RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public int SoldSeats { get; init; }
    public decimal OccupancyRate { get; init; }
}

public sealed record DetailedAnalyticsDto
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }

    // KPIs
    public decimal TotalRevenue { get; init; }
    public decimal TotalTicketRevenue { get; init; }
    public decimal TotalFnBRevenue { get; init; }
    public decimal TotalDiscount { get; init; }
    public int TotalTicketsSold { get; init; }
    public int TotalBookings { get; init; }
    public decimal AverageOrderValue { get; init; }

    // Highlight
    public TopMovieDto? HighestGrossingMovie { get; init; }

    // Breakdowns
    public IReadOnlyList<DailyRevenueDto> DailyBreakdown { get; init; } = [];
    public IReadOnlyList<TopMovieDto> TopMovies { get; init; } = [];
    public IReadOnlyList<TopFoodDto> TopFoods { get; init; } = [];
    public IReadOnlyList<PaymentMethodReportDto> PaymentMethods { get; init; } = [];
}

public sealed record TicketScanResultDto
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool AlreadyCheckedIn { get; init; }
    public int? TicketId { get; init; }
    public string TicketCode { get; init; } = string.Empty;
    public string SeatCode { get; init; } = string.Empty;
    public string MovieTitle { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public DateTime? StartTime { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public DateTime? CheckedInAt { get; init; }
}
