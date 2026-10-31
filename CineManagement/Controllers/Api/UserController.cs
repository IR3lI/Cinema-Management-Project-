using System.Security.Claims;
using CineManagement.Data;
using CineManagement.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineManagement.Controllers.Api;

[ApiController]
[Route("api/users/me")]
[Authorize]
public sealed class UserController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public UserController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<BookingHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookingHistoryDto>>> GetHistory(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var history = await _db.Bookings
            .AsNoTracking()
            .Where(booking => booking.UserId == userId)
            .OrderByDescending(booking => booking.BookingTime)
            .Select(booking => new BookingHistoryDto
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                PurchasedAt = booking.BookingTime,
                Status = booking.Status,
                PaymentStatus = booking.PaymentStatus,
                FinalAmount = booking.FinalAmount,
                MovieTitle = booking.Showtime!.Movie!.Title,
                RoomName = booking.Showtime.Room!.RoomName,
                StartTime = booking.Showtime.StartTime,
                Tickets = booking.Tickets
                    .OrderBy(ticket => ticket.SeatCode)
                    .Select(ticket => new TicketHistoryDto
                    {
                        TicketId = ticket.TicketId,
                        TicketCode = ticket.TicketCode,
                        SeatCode = ticket.SeatCode,
                        Status = ticket.Status,
                        CheckedInAt = ticket.CheckedInAt
                    }).ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(history);
    }
}
