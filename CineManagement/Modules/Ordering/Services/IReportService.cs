using CineManagement.DTOs;

namespace CineManagement.Services;

public interface IReportService
{
    Task<IReadOnlyList<DailyRevenueDto>> GetRevenueReportAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopMovieDto>> GetTopMoviesAsync(
        int top,
        CancellationToken cancellationToken = default);

    Task<RoomOccupancyRateDto?> GetRoomOccupancyRateAsync(
        int showtimeId,
        CancellationToken cancellationToken = default);

    Task<DetailedAnalyticsDto> GetDetailedAnalyticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);
}
