using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.Services;
using CineManagement.DTOs;
using CineManagement.Common;

namespace CineManagement.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IReportService _reportService;

    public AdminController(ApplicationDbContext context, IReportService reportService)
    {
        _context = context;
        _reportService = reportService;
    }

    // ── Dashboard (Strict RBAC: Admin Only) ────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.Today;

        ViewBag.TotalCinemas   = await _context.Cinemas.CountAsync(c => c.IsActive);
        ViewBag.TotalMovies    = await _context.Movies.CountAsync(m => m.IsActive);
        ViewBag.NowShowing     = await _context.Movies.CountAsync(m => m.Status == MovieStatus.NowShowing && m.IsActive);
        ViewBag.ComingSoon     = await _context.Movies.CountAsync(m => m.Status == MovieStatus.ComingSoon && m.IsActive);
        ViewBag.TotalStaff     = await _context.Staffs.CountAsync(s => s.IsActive);
        ViewBag.TotalRooms     = await _context.ScreeningRooms.CountAsync(r => r.IsActive);
        ViewBag.TotalShowtimes = await _context.Showtimes.CountAsync(s => s.StartTime >= today && s.Status == ShowtimeStatus.Available);
        ViewBag.TotalFood      = await _context.FoodItems.CountAsync(f => f.IsAvailable);

        var tomorrow = today.AddDays(1);
        ViewBag.TodayBookings  = await _context.Bookings.CountAsync(b => b.BookingTime >= today && b.BookingTime < tomorrow && b.Status == BookingStatus.Confirmed);
        ViewBag.TodayRevenue   = await _context.Bookings
            .Where(b => b.BookingTime >= today && b.BookingTime < tomorrow && b.Status == BookingStatus.Confirmed)
            .SumAsync(b => (decimal?)b.FinalAmount) ?? 0;

        var recentMovies = await _context.Movies
            .Include(m => m.Genre)
            .Where(m => m.IsActive)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .ToListAsync();

        return View(recentMovies);
    }

    // ── Admin Bookings list (Admin & Staff can view orders) ────────────────
    public async Task<IActionResult> Bookings(BookingStatus? status, DateTime? date, string? search)
    {
        var query = _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room).ThenInclude(r => r!.Cinema)
            .Include(b => b.Tickets)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(b => b.Status == status);
        if (date.HasValue)
        {
            var filterDate = date.Value.Date;
            var nextDate = filterDate.AddDays(1);
            query = query.Where(b => b.BookingTime >= filterDate && b.BookingTime < nextDate);
        }
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => b.BookingCode.Contains(search) || (b.User != null && b.User.Email != null && b.User.Email.Contains(search)));

        ViewBag.Status = status;
        ViewBag.Date   = date;
        ViewBag.Search = search;

        return View(await query.OrderByDescending(b => b.BookingTime).ToListAsync());
    }

    // ── Comprehensive Reports & Statistics (Strict RBAC: Admin Only) ──────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reports(DateTime? fromDate, DateTime? toDate, string? period)
    {
        var today = DateTime.Today;

        if (!string.IsNullOrWhiteSpace(period))
        {
            switch (period.ToLowerInvariant())
            {
                case "today":
                    fromDate = today;
                    toDate = today;
                    break;
                case "7days":
                    fromDate = today.AddDays(-6);
                    toDate = today;
                    break;
                case "30days":
                    fromDate = today.AddDays(-29);
                    toDate = today;
                    break;
                case "thismonth":
                    fromDate = new DateTime(today.Year, today.Month, 1);
                    toDate = today;
                    break;
                case "all":
                    fromDate = null;
                    toDate = null;
                    break;
            }
        }
        else if (!fromDate.HasValue && !toDate.HasValue)
        {
            period = "all";
            fromDate = null;
            toDate = null;
        }

        ViewBag.Period = period ?? "custom";
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        var analytics = await _reportService.GetDetailedAnalyticsAsync(fromDate, toDate);
        return View(analytics);
    }

    // ── Staff QR Ticket Scanner (Legacy route -> Supported alongside /Counter) ──
    [HttpGet]
    public async Task<IActionResult> ScanTicket()
    {
        ViewBag.SampleTickets = await _context.Tickets
            .Include(t => t.Showtime).ThenInclude(s => s!.Movie)
            .Include(t => t.Showtime).ThenInclude(s => s!.Room)
            .Include(t => t.Booking)
            .Where(t => t.Booking != null && (t.Booking.Status == BookingStatus.Confirmed || t.Booking.PaymentStatus == PaymentStatus.Paid))
            .OrderByDescending(t => t.TicketId)
            .Take(8)
            .Select(t => new
            {
                t.TicketId,
                t.TicketCode,
                t.SeatCode,
                t.Status,
                t.IsPrinted,
                MovieTitle = t.Showtime != null && t.Showtime.Movie != null ? t.Showtime.Movie.Title : "Chưa rõ",
                RoomName = t.Showtime != null && t.Showtime.Room != null ? t.Showtime.Room.RoomName : "Chưa rõ",
                StartTime = t.Showtime != null ? t.Showtime.StartTime : DateTime.Now
            })
            .ToListAsync();

        return View();
    }

    public record ScanTicketRequest(string? Code);

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ProcessTicketScan([FromBody] ScanTicketRequest? request, [FromForm] string? code, [FromQuery] string? q)
    {
        var rawCode = (request?.Code ?? code ?? q)?.Trim();
        if (string.IsNullOrWhiteSpace(rawCode))
            return Json(new { success = false, message = "Vui lòng nhập hoặc quét mã vé." });

        var ticketCode = rawCode;
        int? parsedId = null;

        // Parse format "cinema-ticket:12:TCK-ABC"
        if (rawCode.StartsWith("cinema-ticket:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = rawCode.Split(':');
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[1], out var tid)) parsedId = tid;
                ticketCode = parts[2];
            }
        }

        // 1. Check if rawCode matches a BookingCode directly (multi-seat scan)
        var booking = await _context.Bookings
            .Include(b => b.Tickets).ThenInclude(t => t.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Tickets).ThenInclude(t => t.Showtime).ThenInclude(s => s!.Room).ThenInclude(r => r!.Cinema)
            .Include(b => b.Tickets).ThenInclude(t => t.Seat).ThenInclude(s => s!.SeatType)
            .Include(b => b.BookingConcessions).ThenInclude(bc => bc.FoodItem)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.BookingCode == rawCode);

        List<Ticket> tickets = new();
        if (booking != null && booking.Tickets.Any())
        {
            tickets = booking.Tickets.OrderBy(t => t.SeatCode).ToList();
        }
        else
        {
            // 2. Look up by TicketId or TicketCode
            var query = _context.Tickets
                .Include(t => t.Showtime).ThenInclude(s => s!.Movie)
                .Include(t => t.Showtime).ThenInclude(s => s!.Room).ThenInclude(r => r!.Cinema)
                .Include(t => t.Seat).ThenInclude(s => s!.SeatType)
                .Include(t => t.Booking).ThenInclude(b => b!.User)
                .Include(t => t.Booking).ThenInclude(b => b!.BookingConcessions).ThenInclude(bc => bc.FoodItem)
                .AsQueryable();

            Ticket? ticket = null;
            if (parsedId.HasValue)
            {
                ticket = await query.FirstOrDefaultAsync(t => t.TicketId == parsedId.Value);
            }

            if (ticket == null)
            {
                ticket = await query.FirstOrDefaultAsync(t => t.TicketCode == ticketCode || t.TicketCode == rawCode);
            }

            if (ticket != null)
            {
                if (ticket.BookingId > 0)
                {
                    // Fetch all sibling tickets in the same booking
                    tickets = await _context.Tickets
                        .Include(t => t.Showtime).ThenInclude(s => s!.Movie)
                        .Include(t => t.Showtime).ThenInclude(s => s!.Room).ThenInclude(r => r!.Cinema)
                        .Include(t => t.Seat).ThenInclude(s => s!.SeatType)
                        .Include(t => t.Booking).ThenInclude(b => b!.User)
                        .Include(t => t.Booking).ThenInclude(b => b!.BookingConcessions).ThenInclude(bc => bc.FoodItem)
                        .Where(t => t.BookingId == ticket.BookingId)
                        .OrderBy(t => t.SeatCode)
                        .ToListAsync();
                    booking = tickets.FirstOrDefault()?.Booking;
                }
                else
                {
                    tickets.Add(ticket);
                    booking = ticket.Booking;
                }
            }
        }

        if (!tickets.Any())
        {
            return Json(new { success = false, message = $"Không tìm thấy vé trong hệ thống với mã: \"{rawCode}\"" });
        }

        var sampleTicket = tickets.First();
        var movieTitle = sampleTicket.Showtime?.Movie?.Title ?? "Chưa rõ phim";
        var roomName = sampleTicket.Showtime?.Room?.RoomName ?? "Chưa rõ phòng";
        var cinemaName = sampleTicket.Showtime?.Room?.Cinema?.Name ?? "CineStar Cinema";
        var seatCode = string.Join(", ", tickets.Select(t => t.SeatCode)) + (tickets.Count > 1 ? $" ({tickets.Count} vé)" : "");
        var startTime = sampleTicket.Showtime?.StartTime.ToVietnameseDateTimeString() ?? "";
        var customerName = sampleTicket.Booking?.User?.FullName ?? sampleTicket.Booking?.ContactEmail ?? "Khách hàng";
        var customerEmail = sampleTicket.Booking?.User?.Email ?? sampleTicket.Booking?.ContactEmail ?? "";
        var codeDisplay = booking?.BookingCode ?? sampleTicket.TicketCode;

        // Verify Booking payment status
        var relatedBooking = booking ?? sampleTicket.Booking;
        if (relatedBooking != null)
        {
            bool isPaid = relatedBooking.Status == BookingStatus.Confirmed || relatedBooking.PaymentStatus == PaymentStatus.Paid;
            if (!isPaid)
            {
                var statusDesc = relatedBooking.Status switch
                {
                    BookingStatus.Pending => "Đang chờ thanh toán",
                    BookingStatus.Cancelled => "Đã bị hủy",
                    BookingStatus.Expired => "Đã hết hạn giữ chỗ",
                    BookingStatus.Refunded => "Đã hoàn tiền",
                    _ => relatedBooking.Status.ToString()
                };
                return Json(new
                {
                    success = false,
                    message = $"❌ Vé thuộc đơn hàng {relatedBooking.BookingCode} chưa thanh toán thành công (Trạng thái đơn: {statusDesc})! Không thể in vé.",
                    ticketCode = codeDisplay
                });
            }
        }

        // Check if all tickets already checked in / printed
        if (tickets.All(t => t.Status == TicketStatus.CheckedIn || t.IsPrinted))
        {
            var checkedInTicket = tickets.FirstOrDefault(t => t.PrintedAt.HasValue || t.CheckedInAt.HasValue) ?? sampleTicket;
            var timeVal = checkedInTicket.PrintedAt ?? checkedInTicket.CheckedInAt;
            var localCheckedInTime = timeVal.HasValue
                ? timeVal.Value.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy")
                : "Trước đó";

            return Json(new
            {
                success = false,
                alreadyCheckedIn = true,
                message = $"⚠️ VÉ ĐÃ ĐƯỢC IN / ĐỔI TẠI QUẦY TRƯỚC ĐÓ vào lúc {localCheckedInTime}!",
                ticketCode = codeDisplay,
                movieTitle,
                roomName,
                cinemaName,
                seatCode,
                startTime,
                customerName,
                customerEmail,
                checkedInAt = localCheckedInTime
            });
        }

        // Cancelled or invalid
        if (tickets.Any(t => t.Status == TicketStatus.Cancelled))
        {
            return Json(new
            {
                success = false,
                message = "❌ Vé này ĐÃ BỊ HỦY hoặc không còn giá trị sử dụng!",
                ticketCode = codeDisplay
            });
        }

        // Successful check-in & print mark for all tickets
        var nowUtc = DateTime.UtcNow;
        var staffUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var thermalCode = $"POS-{DateTime.Now:yyyyMMdd}-{relatedBooking?.BookingId ?? sampleTicket.TicketId:D4}";
        foreach (var t in tickets)
        {
            t.Status = TicketStatus.CheckedIn;
            t.CheckedInAt = nowUtc;
            t.CheckedInByUserId = staffUserId;
            t.IsPrinted = true;
            t.PrintedAt = nowUtc;
            t.PrintedByUserId = staffUserId;
            t.ThermalReceiptCode = thermalCode;
            t.UpdatedAt = nowUtc;
        }
        await _context.SaveChangesAsync();

        var seatDetails = tickets.Select(t =>
        {
            var typeName = t.Seat?.SeatType?.Name;
            if (string.IsNullOrEmpty(typeName))
            {
                typeName = t.Seat?.SeatTypeLegacy switch
                {
                    SeatTypeEnum.VIP => "Ghế VIP",
                    SeatTypeEnum.Couple => "Ghế Đôi",
                    _ => "Ghế Thường"
                };
            }
            return new
            {
                seatCode = t.SeatCode,
                seatType = typeName,
                display = $"{t.SeatCode} ({typeName})",
                colorCode = t.Seat?.SeatType?.ColorCode ?? "#6c757d"
            };
        }).ToList();

        var fnbList = (relatedBooking?.BookingConcessions ?? new List<BookingConcession>())
            .Select(bc => new
            {
                itemName = bc.ItemName ?? bc.FoodItem?.Name ?? "Sản phẩm F&B",
                quantity = bc.Quantity,
                display = $"{bc.ItemName ?? bc.FoodItem?.Name ?? "Sản phẩm"} (×{bc.Quantity})"
            }).ToList();

        var successMsg = tickets.Count > 1
            ? $"✅ XÁC THỰC & IN VÉ THÀNH CÔNG! Đã cấp {tickets.Count} ghế: {string.Join(", ", tickets.Select(t => t.SeatCode))}."
            : "✅ XÁC THỰC & IN VÉ THÀNH CÔNG! Chúc quý khách xem phim vui vẻ.";

        return Json(new
        {
            success = true,
            message = successMsg,
            ticketCode = codeDisplay,
            bookingId = relatedBooking?.BookingId,
            thermalReceiptCode = thermalCode,
            printUrl = relatedBooking != null ? $"/Counter/PrintThermalTicket?bookingId={relatedBooking.BookingId}" : null,
            movieTitle,
            roomName,
            cinemaName,
            seatCode,
            seatDetails,
            fnbList,
            startTime,
            customerName,
            customerEmail,
            checkedInAt = nowUtc.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy")
        });
    }

    // ── Admin Reviews Management (Strict RBAC: Admin Only) ────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reviews(int? movieId, int? rating, ReviewStatus? status, string? search)
    {
        var query = _context.Reviews
            .Include(r => r.Movie)
            .Include(r => r.User)
            .AsQueryable();

        if (movieId.HasValue)
            query = query.Where(r => r.MovieId == movieId.Value);

        if (rating.HasValue)
            query = query.Where(r => r.Rating == rating.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                (r.Comment != null && r.Comment.Contains(term)) ||
                (r.Movie != null && r.Movie.Title.Contains(term)) ||
                (r.User != null && (r.User.FullName.Contains(term) || (r.User.Email != null && r.User.Email.Contains(term))))
            );
        }

        ViewBag.Movies = await _context.Movies.OrderBy(m => m.Title).ToListAsync();
        ViewBag.MovieId = movieId;
        ViewBag.Rating = rating;
        ViewBag.Status = status;
        ViewBag.Search = search;

        var list = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(list);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleReviewStatus(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        review.Status = review.Status == ReviewStatus.Published ? ReviewStatus.Hidden : ReviewStatus.Published;
        review.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        var movie = await _context.Movies.FindAsync(review.MovieId);
        if (movie != null)
        {
            var published = await _context.Reviews
                .Where(r => r.MovieId == review.MovieId && r.Status == ReviewStatus.Published)
                .ToListAsync();
            movie.Rating = published.Any() ? Math.Round((decimal)published.Average(r => r.Rating), 1) : 0;
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = $"Đã cập nhật trạng thái bình luận sang: {(review.Status == ReviewStatus.Published ? "Đã hiển thị" : "Đã ẩn")}.";
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReviewAdmin(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        int movieId = review.MovieId;
        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        var movie = await _context.Movies.FindAsync(movieId);
        if (movie != null)
        {
            var published = await _context.Reviews
                .Where(r => r.MovieId == movieId && r.Status == ReviewStatus.Published)
                .ToListAsync();
            movie.Rating = published.Any() ? Math.Round((decimal)published.Average(r => r.Rating), 1) : 0;
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Đã xóa bình luận thành công.";
        return RedirectToAction(nameof(Reviews));
    }
}
