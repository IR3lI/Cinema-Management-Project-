using CineManagement.Modules.Catalog.Domain;

namespace CineManagement.Modules.Catalog.Services;

public interface ICatalogService
{
    Task<List<Movie>> GetActiveMoviesAsync(CancellationToken cancellationToken = default);
    Task<Movie?> GetMovieByIdAsync(int movieId, CancellationToken cancellationToken = default);
    Task<List<Showtime>> GetShowtimesByMovieAsync(int movieId, DateTime? date = null, CancellationToken cancellationToken = default);
    Task<bool> HasShowtimeConflictAsync(int roomId, DateTime startTime, DateTime endTime, int? excludeShowtimeId = null, CancellationToken cancellationToken = default);
}
