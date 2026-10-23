using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Modules.Catalog.Domain;

namespace CineManagement.Modules.Catalog.Services;

public class CatalogService : ICatalogService
{
    private readonly ApplicationDbContext _context;

    public CatalogService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Movie>> GetActiveMoviesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Movies
            .Include(m => m.Genre)
            .Where(m => m.IsActive)
            .OrderByDescending(m => m.ReleaseDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Movie?> GetMovieByIdAsync(int movieId, CancellationToken cancellationToken = default)
    {
        return await _context.Movies
            .Include(m => m.Genre)
            .Include(m => m.Showtimes.Where(s => s.StartTime >= DateTime.Now))
                .ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
    }

    public async Task<List<Showtime>> GetShowtimesByMovieAsync(int movieId, DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Showtimes
            .Include(s => s.Room)
            .Where(s => s.MovieId == movieId);

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(s => s.StartTime.Date == targetDate);
        }

        return await query.OrderBy(s => s.StartTime).ToListAsync(cancellationToken);
    }

    public async Task<bool> HasShowtimeConflictAsync(int roomId, DateTime startTime, DateTime endTime, int? excludeShowtimeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Showtimes
            .Where(s => s.RoomId == roomId && s.StartTime < endTime && s.EndTime > startTime);

        if (excludeShowtimeId.HasValue)
        {
            query = query.Where(s => s.ShowtimeId != excludeShowtimeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
