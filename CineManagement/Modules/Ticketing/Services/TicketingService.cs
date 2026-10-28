using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Modules.Ticketing.Domain;

namespace CineManagement.Modules.Ticketing.Services;

public class TicketingService : ITicketingService
{
    private readonly ApplicationDbContext _context;

    public TicketingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ShowtimeSeat>> GetShowtimeSeatsAsync(int showtimeId, CancellationToken cancellationToken = default)
    {
        return await _context.ShowtimeSeats
            .Include(ss => ss.Seat)
                .ThenInclude(s => s!.SeatType)
            .Where(ss => ss.ShowtimeId == showtimeId)
            .OrderBy(ss => ss.Seat != null ? ss.Seat.GridRow : 0)
            .ThenBy(ss => ss.Seat != null ? ss.Seat.GridCol : 0)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ReleaseExpiredHoldsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredSeats = await _context.ShowtimeSeats
            .Where(s => s.Status == ShowtimeSeatStatus.Held && s.HoldExpiresAt != null && s.HoldExpiresAt < now)
            .ToListAsync(cancellationToken);

        if (!expiredSeats.Any())
            return 0;

        foreach (var seat in expiredSeats)
        {
            seat.Status = ShowtimeSeatStatus.Available;
            seat.HeldByUserId = null;
            seat.HoldExpiresAt = null;
            seat.Version++;
        }

        return await _context.SaveChangesAsync(cancellationToken);
    }
}
