using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.ViewModels;

namespace CineManagement.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Public Home (SCRUM-10 – search & filter) ──────────────────────────────
    public async Task<IActionResult> Index(string? search, int? genreId, MovieStatus? status)
    {
        var query = _context.Movies
            .Include(m => m.Genre)
            .Where(m => m.IsActive)
            .AsQueryable();

        // Default: show Now Showing + Coming Soon
        if (!status.HasValue)
            query = query.Where(m => m.Status == MovieStatus.NowShowing || m.Status == MovieStatus.ComingSoon);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m =>
                m.Title.Contains(search) ||
                (m.Director != null && m.Director.Contains(search)) ||
                (m.Actors != null && m.Actors.Contains(search)));

        if (genreId.HasValue)
            query = query.Where(m => m.GenreId == genreId);

        if (status.HasValue)
            query = query.Where(m => m.Status == status);

        var genres = await _context.Genres.OrderBy(g => g.Name).ToListAsync();

        var vm = new MovieIndexViewModel
        {
            Movies = await query.OrderBy(m => m.Status).ThenByDescending(m => m.ReleaseDate).ToListAsync(),
            SearchTerm = search,
            GenreId = genreId,
            Status = status,
            Genres = genres.Select(g => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(g.Name, g.GenreId.ToString()))
        };

        return View(vm);
    }

    // ── Movie Detail (public) – SCRUM-14 ──────────────────────────────────────
    public async Task<IActionResult> MovieDetail(int id)
    {
        var movie = await _context.Movies
            .Include(m => m.Genre)
            .FirstOrDefaultAsync(m => m.MovieId == id && m.IsActive);

        if (movie == null) return NotFound();

        // Load upcoming showtimes (today onwards), grouped by date
        var showtimes = await _context.Showtimes
            .Include(s => s.Room)
            .Where(s => s.MovieId == id
                     && s.StartTime >= DateTime.Now
                     && s.Status == ShowtimeStatus.Available)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        var showtimesByDate = showtimes
            .GroupBy(s => s.StartTime.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.ShowtimesByDate = showtimesByDate;

        // ── Reviews & Ratings ──
        var reviews = await _context.Reviews
            .Include(r => r.User)
            .Where(r => r.MovieId == id && r.Status == ReviewStatus.Published)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Review? myReview = null;
        bool hasVerifiedTicket = false;

        if (!string.IsNullOrEmpty(currentUserId))
        {
            myReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.MovieId == id && r.UserId == currentUserId);

            hasVerifiedTicket = await _context.Tickets.AnyAsync(t =>
                t.Booking != null
                && t.Booking.UserId == currentUserId
                && t.Showtime != null
                && t.Showtime.MovieId == id
                && t.Booking.Status == BookingStatus.Confirmed);
        }

        // Identify which reviewers have verified tickets
        var reviewerUserIds = reviews.Select(r => r.UserId).Distinct().ToList();
        var verifiedReviewerIds = await _context.Tickets
            .Where(t => t.Showtime != null
                     && t.Showtime.MovieId == id
                     && t.Booking != null
                     && reviewerUserIds.Contains(t.Booking.UserId)
                     && t.Booking.Status == BookingStatus.Confirmed)
            .Select(t => t.Booking!.UserId)
            .Distinct()
            .ToListAsync();

        var starCounts = new int[6]; // index 1..5
        foreach (var r in reviews)
        {
            if (r.Rating >= 1 && r.Rating <= 5)
                starCounts[r.Rating]++;
        }

        decimal avgRating = reviews.Any()
            ? Math.Round((decimal)reviews.Average(r => r.Rating), 1)
            : movie.Rating;

        ViewBag.Reviews = reviews;
        ViewBag.AverageRating = avgRating;
        ViewBag.TotalReviews = reviews.Count;
        ViewBag.StarCounts = starCounts;
        ViewBag.MyReview = myReview;
        ViewBag.HasVerifiedTicket = hasVerifiedTicket;
        ViewBag.VerifiedUserIds = new HashSet<string>(verifiedReviewerIds);

        return View(movie);
    }

    // ── Submit / Update Review ────────────────────────────────────────────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(int movieId, int rating, string? comment)
    {
        var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId && m.IsActive);
        if (movie == null)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "Không tìm thấy phim." });
            return NotFound();
        }

        if (rating < 1 || rating > 5)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "Vui lòng chọn số sao từ 1 đến 5." });
            TempData["ReviewError"] = "Số sao đánh giá phải từ 1 đến 5.";
            return RedirectToAction(nameof(MovieDetail), new { id = movieId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.MovieId == movieId && r.UserId == userId);

        bool isUpdate = review != null;
        if (review == null)
        {
            review = new Review
            {
                MovieId = movieId,
                UserId = userId,
                Rating = rating,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                Status = ReviewStatus.Published,
                CreatedAt = DateTime.Now
            };
            _context.Reviews.Add(review);
        }
        else
        {
            review.Rating = rating;
            review.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            review.Status = ReviewStatus.Published;
            review.UpdatedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        // Recalculate movie rating
        var publishedReviews = await _context.Reviews
            .Where(r => r.MovieId == movieId && r.Status == ReviewStatus.Published)
            .ToListAsync();

        if (publishedReviews.Any())
        {
            movie.Rating = Math.Round((decimal)publishedReviews.Average(r => r.Rating), 1);
            await _context.SaveChangesAsync();
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new
            {
                success = true,
                message = isUpdate ? "Đã cập nhật đánh giá của bạn thành công!" : "Cảm ơn bạn đã gửi đánh giá!",
                rating = review.Rating,
                comment = review.Comment,
                avgRating = movie.Rating,
                totalReviews = publishedReviews.Count
            });
        }

        TempData["ReviewSuccess"] = isUpdate ? "Đã cập nhật đánh giá thành công!" : "Cảm ơn bạn đã gửi đánh giá!";
        return RedirectToAction(nameof(MovieDetail), new { id = movieId });
    }

    // ── Delete My Review ──────────────────────────────────────────────────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int movieId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var review = await _context.Reviews.FirstOrDefaultAsync(r => r.MovieId == movieId && r.UserId == userId);
        if (review != null)
        {
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            var movie = await _context.Movies.FindAsync(movieId);
            if (movie != null)
            {
                var remaining = await _context.Reviews
                    .Where(r => r.MovieId == movieId && r.Status == ReviewStatus.Published)
                    .ToListAsync();
                movie.Rating = remaining.Any() ? Math.Round((decimal)remaining.Average(r => r.Rating), 1) : 0;
                await _context.SaveChangesAsync();
            }
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Đã xóa đánh giá của bạn." });

        TempData["ReviewSuccess"] = "Đã xóa đánh giá thành công.";
        return RedirectToAction(nameof(MovieDetail), new { id = movieId });
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new CineManagement.Models.ErrorViewModel
        {
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
