using CineManagement.Modules.Ticketing.Domain;

namespace CineManagement.Modules.Ticketing.Services;

public interface ITicketingService
{
    Task<List<ShowtimeSeat>> GetShowtimeSeatsAsync(int showtimeId, CancellationToken cancellationToken = default);
    Task<int> ReleaseExpiredHoldsAsync(CancellationToken cancellationToken = default);
}
