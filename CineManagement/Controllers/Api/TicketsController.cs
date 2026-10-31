using System.Security.Claims;
using CineManagement.Data;
using CineManagement.DTOs;
using CineManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineManagement.Controllers.Api;

[ApiController]
[Route("api/tickets")]
[Authorize(Roles = "Staff,Admin")]
public sealed class TicketsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TicketsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost("check-in/{ticketId:int}")]
    [ProducesResponseType(typeof(TicketCheckInResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(int ticketId, CancellationToken cancellationToken)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Showtime)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, cancellationToken);

        if (ticket?.Showtime is null)
            return NotFound(new { message = "Không tìm thấy vé." });

        if (ticket.Showtime.StartTime.Date != DateTime.Today)
            return Conflict(new { message = "Vé chỉ có thể được soát trong ngày chiếu." });

        if (ticket.Status == TicketStatus.CheckedIn)
            return Conflict(new { message = "Vé này đã được soát trước đó.", ticket.CheckedInAt });

        if (ticket.Status != TicketStatus.Issued)
            return Conflict(new { message = "Vé không ở trạng thái có thể soát." });

        ticket.Status = TicketStatus.CheckedIn;
        ticket.CheckedInAt = DateTime.UtcNow;
        ticket.CheckedInByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new TicketCheckInResponseDto
        {
            TicketId = ticket.TicketId,
            TicketCode = ticket.TicketCode,
            Status = ticket.Status,
            CheckedInAt = ticket.CheckedInAt,
            Message = "Soát vé thành công."
        });
    }
}
