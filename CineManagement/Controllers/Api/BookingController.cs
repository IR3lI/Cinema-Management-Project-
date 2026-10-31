using System.Security.Claims;
using CineManagement.Data;
using CineManagement.DTOs;
using CineManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CineManagement.Controllers.Api;

[ApiController]
[Route("api/bookings")]
public sealed class BookingApiController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly IEmailService _emailService;
    private readonly IQrCodeService _qrCodeService;
    private readonly ApplicationDbContext _db;
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<BookingApiController> _logger;

    public BookingApiController(
        IBookingService bookingService,
        IEmailService emailService,
        IQrCodeService qrCodeService,
        ApplicationDbContext db,
        IOptions<SmtpOptions> smtpOptions,
        ILogger<BookingApiController> logger)
    {
        _bookingService = bookingService;
        _emailService = emailService;
        _qrCodeService = qrCodeService;
        _db = db;
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    [HttpGet("showtimes/{id:int}/seats")]
    [ProducesResponseType(typeof(SeatMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatMap(int id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _bookingService.GetSeatMapAsync(id, userId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("hold-seat")]
    [ProducesResponseType(typeof(HoldSeatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HoldSeatResponseDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> HoldSeat(
        [FromBody] HoldSeatRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _bookingService.HoldSeatAsync(GetUserId(), request, cancellationToken);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [Authorize]
    [HttpPost("calculate-price")]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CalculatePrice(
        [FromBody] BookingRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bookingService.CalculateTotalAmountAsync(
                request, GetUserId(), cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(
        [FromBody] BookingRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bookingService.CreateBookingAsync(
                GetUserId(), request, cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.ContactEmail))
                await TrySendTicketEmailAsync(result.BookingId, request.ContactEmail, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidOperationException ex)
        {
            // A hold timeout, an already-sold seat, or exhausted voucher usage
            // is a conflict with current server state, not a malformed request.
            return Conflict(new { message = ex.Message });
        }
    }

    private string GetUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? throw new UnauthorizedAccessException("Không thể xác định người dùng từ token.");

    private async Task TrySendTicketEmailAsync(
        int bookingId,
        string recipient,
        CancellationToken cancellationToken)
    {
        try
        {
            var booking = await _db.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime).ThenInclude(s => s!.Room)
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

            if (booking?.Showtime?.Movie is null || booking.Showtime.Room is null)
            {
                _logger.LogWarning("Skipped e-ticket email because booking {BookingId} lacks showtime details.", bookingId);
                return;
            }

            var emailOrder = new OrderResponseDto
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                MovieTitle = booking.Showtime.Movie.Title,
                CinemaName = _smtpOptions.CinemaName,
                RoomName = booking.Showtime.Room.RoomName,
                StartTime = booking.Showtime.StartTime,
                FinalAmount = booking.FinalAmount,
                Tickets = booking.Tickets.Select(ticket => new TicketEmailDto
                {
                    TicketId = ticket.TicketId,
                    TicketCode = ticket.TicketCode,
                    SeatCode = ticket.SeatCode,
                    Status = ticket.Status,
                    QrCodeBase64 = _qrCodeService.GeneratePngBase64(
                        ticket.QrPayload ?? _qrCodeService.CreateTicketPayload(ticket.TicketId, ticket.TicketCode))
                }).ToList()
            };

            await _emailService.SendTicketEmailAsync(recipient, emailOrder, cancellationToken);
            booking.TicketEmailSent = true;
            booking.TicketEmailSentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A completed sale must not fail because a mail provider is unavailable.
            _logger.LogError(ex, "Unable to send e-ticket email for booking {BookingId}.", bookingId);
        }
    }
}
