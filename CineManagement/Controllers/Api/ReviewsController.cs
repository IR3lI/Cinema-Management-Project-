using System.Security.Claims;
using CineManagement.Data;
using CineManagement.DTOs;
using CineManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineManagement.Controllers.Api;

[ApiController]
[Route("api/reviews")]
[Authorize]
public sealed class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ReviewsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReviewRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (!await _db.Movies.AnyAsync(movie => movie.MovieId == request.MovieId, cancellationToken))
            return NotFound(new { message = "Không tìm thấy phim." });

        // Eligibility query joins Ticket -> Booking (Order) -> Showtime. A ticket
        // qualifies only after a completed show, unless staff already checked it in.
        var now = DateTime.Now;
        var canReview = await _db.Tickets.AnyAsync(ticket =>
            ticket.Booking!.UserId == userId
            && ticket.Booking.Status == BookingStatus.Confirmed
            && ticket.Showtime!.MovieId == request.MovieId
            && (ticket.Status == TicketStatus.CheckedIn
                || ticket.Showtime.Status == ShowtimeStatus.Ended
                || (ticket.Showtime.EndTime.HasValue && ticket.Showtime.EndTime <= now)),
            cancellationToken);

        if (!canReview)
        {
            return BadRequest(new
            {
                message = "Bạn chỉ có thể đánh giá sau khi đã mua vé và suất chiếu đã kết thúc hoặc vé đã được soát."
            });
        }

        if (await _db.Reviews.AnyAsync(review => review.UserId == userId && review.MovieId == request.MovieId, cancellationToken))
            return Conflict(new { message = "Bạn đã đánh giá phim này." });

        var review = new Review
        {
            UserId = userId,
            MovieId = request.MovieId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Status = ReviewStatus.Published,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reviews.Add(review);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The database's unique (UserId, MovieId) index protects this case
            // when two browser requests are submitted concurrently.
            return Conflict(new { message = "Bạn đã đánh giá phim này." });
        }

        return StatusCode(StatusCodes.Status201Created, new ReviewResponseDto
        {
            ReviewId = review.ReviewId,
            MovieId = review.MovieId,
            Rating = review.Rating,
            Comment = review.Comment,
            Status = review.Status,
            CreatedAt = review.CreatedAt
        });
    }
}
