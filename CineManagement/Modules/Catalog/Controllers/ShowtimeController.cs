using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.ViewModels;

namespace CineManagement.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class ShowtimeController : Controller
{
    private readonly ApplicationDbContext _context;

    public ShowtimeController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Index + Filter ────────────────────────────────────────────────────
    public async Task<IActionResult> Index(int? movieId, int? roomId, DateTime? date)
    {
        var query = _context.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
            .AsQueryable();

        if (movieId.HasValue) query = query.Where(s => s.MovieId == movieId);
        if (roomId.HasValue)  query = query.Where(s => s.RoomId  == roomId);
        if (date.HasValue)
        {
            var filterDate = date.Value.Date;
            var nextDate = filterDate.AddDays(1);
            query = query.Where(s => s.StartTime >= filterDate && s.StartTime < nextDate);
        }

        var movies = await _context.Movies.Where(m => m.IsActive).OrderBy(m => m.Title).ToListAsync();
        var rooms  = await _context.ScreeningRooms.Where(r => r.IsActive).OrderBy(r => r.RoomName).ToListAsync();

        var vm = new ShowtimeIndexViewModel
        {
            Showtimes = await query.OrderBy(s => s.StartTime).ToListAsync(),
            MovieId   = movieId,
            RoomId    = roomId,
            Date      = date,
            Movies    = movies.Select(m => new SelectListItem(m.Title, m.MovieId.ToString())),
            Rooms     = rooms.Select(r => new SelectListItem(r.RoomName, r.RoomId.ToString()))
        };
        return View(vm);
    }

    // ── Create ────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(await BuildFormAsync(new ShowtimeFormViewModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShowtimeFormViewModel model)
    {
        if (!ModelState.IsValid) return View(await BuildFormAsync(model));

        // Auto-calculate EndTime based on movie duration
        var movie = await _context.Movies.FindAsync(model.MovieId);
        var showtime = new Showtime
        {
            MovieId     = model.MovieId,
            RoomId      = model.RoomId,
            StartTime   = model.StartTime,
            EndTime     = movie != null ? model.StartTime.AddMinutes(movie.Duration + 20) : null, // +20 min ads
            TicketPrice = model.TicketPrice,
            Status      = model.Status,
            Notes       = model.Notes
        };

        _context.Showtimes.Add(showtime);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Thêm lịch chiếu thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var st = await _context.Showtimes.FindAsync(id);
        if (st == null) return NotFound();

        var vm = new ShowtimeFormViewModel
        {
            ShowtimeId  = st.ShowtimeId,
            MovieId     = st.MovieId,
            RoomId      = st.RoomId,
            StartTime   = st.StartTime,
            TicketPrice = st.TicketPrice,
            Status      = st.Status,
            Notes       = st.Notes
        };
        return View(await BuildFormAsync(vm));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ShowtimeFormViewModel model)
    {
        if (id != model.ShowtimeId) return BadRequest();
        if (!ModelState.IsValid) return View(await BuildFormAsync(model));

        var st = await _context.Showtimes.FindAsync(id);
        if (st == null) return NotFound();

        var movie = await _context.Movies.FindAsync(model.MovieId);
        st.MovieId     = model.MovieId;
        st.RoomId      = model.RoomId;
        st.StartTime   = model.StartTime;
        st.EndTime     = movie != null ? model.StartTime.AddMinutes(movie.Duration + 20) : null;
        st.TicketPrice = model.TicketPrice;
        st.Status      = model.Status;
        st.Notes       = model.Notes;

        _context.Update(st);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Cập nhật lịch chiếu thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Delete ────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var st = await _context.Showtimes.FindAsync(id);
        if (st == null) return NotFound();

        st.Status = ShowtimeStatus.Canceled;
        _context.Update(st);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã hủy lịch chiếu!";
        return RedirectToAction(nameof(Index));
    }

    // ── Helper ────────────────────────────────────────────────────────────
    private async Task<ShowtimeFormViewModel> BuildFormAsync(ShowtimeFormViewModel vm)
    {
        var movies = await _context.Movies.Where(m => m.IsActive).OrderBy(m => m.Title).ToListAsync();
        var rooms  = await _context.ScreeningRooms.Where(r => r.IsActive).OrderBy(r => r.RoomName).ToListAsync();
        vm.Movies  = movies.Select(m => new SelectListItem(m.Title, m.MovieId.ToString()));
        vm.Rooms   = rooms.Select(r => new SelectListItem($"{r.RoomName} ({r.Rows * r.SeatsPerRow} ghế)", r.RoomId.ToString()));
        return vm;
    }
}
