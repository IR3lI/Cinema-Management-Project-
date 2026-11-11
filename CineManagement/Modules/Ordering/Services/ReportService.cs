using CineManagement.Data;
using CineManagement.DTOs;
using CineManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace CineManagement.Services;

public sealed class ReportService : IReportService
{
    private static readonly TicketStatus[] SoldTicketStatuses =
    [
        TicketStatus.Issued,
        TicketStatus.CheckedIn
    ];

    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DailyRevenueDto>> GetRevenueReportAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var from = fromDate.Date;
        var untilExclusive = toDate.Date.AddDays(1);
        if (untilExclusive <= from)
            throw new ArgumentException("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");

        return await _db.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Confirmed
                           && booking.PaidAt != null
                           && booking.PaidAt >= from
                           && booking.PaidAt < untilExclusive)
            .GroupBy(booking => booking.PaidAt!.Value.Date)
            .OrderBy(group => group.Key)
            .Select(group => new DailyRevenueDto
            {
                Date = group.Key,
                BookingCount = group.Count(),
                TicketCount = group.Sum(b => b.Tickets.Count),
                TicketRevenue = group.Sum(b => b.TicketAmount),
                FnBRevenue = group.Sum(b => b.FnBAmount),
                Revenue = group.Sum(booking => booking.FinalAmount),
                TotalRevenue = group.Sum(booking => booking.FinalAmount)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopMovieDto>> GetTopMoviesAsync(
        int top,
        CancellationToken cancellationToken = default)
    {
        if (top is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(top), "Top phải nằm trong khoảng 1 đến 100.");

        return await _db.Tickets
            .AsNoTracking()
            .Where(ticket => SoldTicketStatuses.Contains(ticket.Status)
                          && ticket.Booking!.Status == BookingStatus.Confirmed)
            .GroupBy(ticket => new { ticket.Showtime!.MovieId, ticket.Showtime.Movie!.Title, ticket.Showtime.Movie.PosterUrl, GenreName = ticket.Showtime.Movie.Genre!.Name })
            .Select(group => new TopMovieDto
            {
                MovieId = group.Key.MovieId,
                MovieTitle = group.Key.Title,
                PosterUrl = group.Key.PosterUrl,
                GenreName = group.Key.GenreName,
                TicketsSold = group.Count(),
                TicketRevenue = group.Sum(ticket => ticket.UnitPrice)
            })
            .OrderByDescending(movie => movie.TicketRevenue)
            .ThenBy(movie => movie.MovieTitle)
            .Take(top)
            .ToListAsync(cancellationToken);
    }

    public async Task<RoomOccupancyRateDto?> GetRoomOccupancyRateAsync(
        int showtimeId,
        CancellationToken cancellationToken = default)
    {
        var showtime = await _db.Showtimes
            .AsNoTracking()
            .Where(item => item.ShowtimeId == showtimeId)
            .Select(item => new
            {
                item.ShowtimeId,
                item.RoomId,
                RoomName = item.Room!.RoomName,
                TotalSeats = item.Room.Seats.Count(seat => seat.IsActive)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (showtime is null)
            return null;

        var soldSeats = await _db.Tickets
            .AsNoTracking()
            .CountAsync(ticket => ticket.ShowtimeId == showtimeId
                               && SoldTicketStatuses.Contains(ticket.Status), cancellationToken);

        return new RoomOccupancyRateDto
        {
            ShowtimeId = showtime.ShowtimeId,
            RoomId = showtime.RoomId,
            RoomName = showtime.RoomName,
            TotalSeats = showtime.TotalSeats,
            SoldSeats = soldSeats,
            OccupancyRate = showtime.TotalSeats == 0
                ? 0
                : Math.Round((decimal)soldSeats / showtime.TotalSeats * 100, 2)
        };
    }

    public async Task<DetailedAnalyticsDto> GetDetailedAnalyticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed);

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(b => (b.PaidAt ?? b.BookingTime) >= from);
        }

        if (toDate.HasValue)
        {
            var until = toDate.Value.Date.AddDays(1);
            query = query.Where(b => (b.PaidAt ?? b.BookingTime) < until);
        }

        var confirmedBookings = await query
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Showtime)
                    .ThenInclude(s => s!.Movie)
                        .ThenInclude(m => m!.Genre)
            .Include(b => b.BookingConcessions)
                .ThenInclude(f => f.FoodItem)
            .OrderByDescending(b => b.PaidAt ?? b.BookingTime)
            .ToListAsync(cancellationToken);

        var totalRevenue = confirmedBookings.Sum(b => b.FinalAmount);
        var totalTicketRevenue = confirmedBookings.Sum(b => b.TicketAmount);
        var totalFnBRevenue = confirmedBookings.Sum(b => b.FnBAmount);
        var totalDiscount = confirmedBookings.Sum(b => b.DiscountAmount);
        var totalBookings = confirmedBookings.Count;
        var allTickets = confirmedBookings.SelectMany(b => b.Tickets).ToList();
        var totalTicketsSold = allTickets.Count;
        var avgOrder = totalBookings > 0 ? Math.Round(totalRevenue / totalBookings, 0) : 0;

        // Daily breakdown (Ticket vs FnB vs Total)
        var dailyBreakdown = confirmedBookings
            .GroupBy(b => (b.PaidAt ?? b.BookingTime).Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyRevenueDto
            {
                Date = g.Key,
                BookingCount = g.Count(),
                TicketCount = g.Sum(b => b.Tickets.Count),
                TicketRevenue = g.Sum(b => b.TicketAmount),
                FnBRevenue = g.Sum(b => b.FnBAmount),
                Revenue = g.Sum(b => b.FinalAmount),
                TotalRevenue = g.Sum(b => b.FinalAmount)
            })
            .ToList();

        // Top movies by revenue
        var topMovies = allTickets
            .Where(t => t.Showtime?.Movie != null)
            .GroupBy(t => new
            {
                t.Showtime!.MovieId,
                t.Showtime.Movie!.Title,
                t.Showtime.Movie.PosterUrl,
                GenreName = t.Showtime.Movie.Genre != null ? t.Showtime.Movie.Genre.Name : "Chung"
            })
            .Select(g =>
            {
                var revenue = g.Sum(t => t.UnitPrice);
                return new TopMovieDto
                {
                    MovieId = g.Key.MovieId,
                    MovieTitle = g.Key.Title,
                    PosterUrl = g.Key.PosterUrl,
                    GenreName = g.Key.GenreName,
                    TicketsSold = g.Count(),
                    TicketRevenue = revenue,
                    RevenueSharePercent = totalTicketRevenue > 0 ? Math.Round((revenue / totalTicketRevenue) * 100, 1) : 0
                };
            })
            .OrderByDescending(m => m.TicketRevenue)
            .ThenByDescending(m => m.TicketsSold)
            .ToList();

        var highestGrossingMovie = topMovies.FirstOrDefault();

        // Top Food & Beverage
        var allFnBs = confirmedBookings.SelectMany(b => b.BookingConcessions).ToList();
        var topFoods = allFnBs
            .GroupBy(f => new
            {
                f.FoodItemId,
                FoodName = !string.IsNullOrEmpty(f.ItemName) ? f.ItemName : (f.FoodItem != null ? f.FoodItem.Name : $"Combo #{f.FoodItemId}")
            })
            .Select(g =>
            {
                var fnbRev = g.Sum(f => f.UnitPrice * f.Quantity);
                return new TopFoodDto
                {
                    FoodItemId = g.Key.FoodItemId,
                    FoodName = g.Key.FoodName,
                    QuantitySold = g.Sum(f => f.Quantity),
                    Revenue = fnbRev,
                    RevenueSharePercent = totalFnBRevenue > 0 ? Math.Round((fnbRev / totalFnBRevenue) * 100, 1) : 0
                };
            })
            .OrderByDescending(f => f.Revenue)
            .ThenByDescending(f => f.QuantitySold)
            .ToList();

        // Payment Methods distribution
        var paymentMethods = confirmedBookings
            .GroupBy(b => b.PaymentMethod ?? PaymentMethod.Cash)
            .Select(g =>
            {
                var name = g.Key switch
                {
                    PaymentMethod.EWallet => "Thanh toán bằng ví điện tử",
                    PaymentMethod.BankCard => "Thẻ ngân hàng",
                    _ => "Tiền mặt tại quầy"
                };
                var rev = g.Sum(b => b.FinalAmount);
                return new PaymentMethodReportDto
                {
                    PaymentMethodName = name,
                    BookingCount = g.Count(),
                    Revenue = rev,
                    Percentage = totalRevenue > 0 ? Math.Round((rev / totalRevenue) * 100, 1) : 0
                };
            })
            .OrderByDescending(p => p.Revenue)
            .ToList();

        return new DetailedAnalyticsDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalRevenue = totalRevenue,
            TotalTicketRevenue = totalTicketRevenue,
            TotalFnBRevenue = totalFnBRevenue,
            TotalDiscount = totalDiscount,
            TotalTicketsSold = totalTicketsSold,
            TotalBookings = totalBookings,
            AverageOrderValue = avgOrder,
            HighestGrossingMovie = highestGrossingMovie,
            DailyBreakdown = dailyBreakdown,
            TopMovies = topMovies,
            TopFoods = topFoods,
            PaymentMethods = paymentMethods
        };
    }
}
