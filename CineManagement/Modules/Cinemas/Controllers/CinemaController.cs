using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;

namespace CineManagement.Controllers;

/// <summary>
/// Quản lý chuỗi Cụm rạp / Chi nhánh thuộc hãng rạp (cinemas).
/// Chỉ dành cho Quản lý rạp / Quản trị viên (Admin).
/// </summary>
[Authorize(Roles = "Admin")]
public class CinemaController : Controller
{
    private readonly ApplicationDbContext _context;

    public CinemaController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Index ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var cinemas = await _context.Cinemas
            .Include(c => c.ScreeningRooms)
            .OrderBy(c => c.CinemaId)
            .ToListAsync();
        return View(cinemas);
    }

    // ── Create ────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create() => View(new Cinema());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Cinema model)
    {
        if (!ModelState.IsValid) return View(model);

        model.Code = model.Code.Trim().ToUpperInvariant();
        if (await _context.Cinemas.AnyAsync(c => c.Code == model.Code))
        {
            ModelState.AddModelError("Code", $"Mã cụm rạp \"{model.Code}\" đã tồn tại.");
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        _context.Cinemas.Add(model);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã tạo cụm rạp \"{model.Name}\" ({model.Code}) thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var cinema = await _context.Cinemas.FindAsync(id);
        if (cinema == null) return NotFound();
        return View(cinema);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Cinema model)
    {
        if (id != model.CinemaId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var cinema = await _context.Cinemas.FindAsync(id);
        if (cinema == null) return NotFound();

        var newCode = model.Code.Trim().ToUpperInvariant();
        if (await _context.Cinemas.AnyAsync(c => c.Code == newCode && c.CinemaId != id))
        {
            ModelState.AddModelError("Code", $"Mã cụm rạp \"{newCode}\" đã được sử dụng bởi chi nhánh khác.");
            return View(model);
        }

        cinema.Code = newCode;
        cinema.Name = model.Name.Trim();
        cinema.Address = model.Address.Trim();
        cinema.City = model.City.Trim();
        cinema.Hotline = model.Hotline?.Trim();
        cinema.Email = model.Email?.Trim();
        cinema.ImageUrl = model.ImageUrl?.Trim();
        cinema.Description = model.Description?.Trim();
        cinema.IsActive = model.IsActive;

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã cập nhật cụm rạp \"{cinema.Name}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Toggle Status ─────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var cinema = await _context.Cinemas.FindAsync(id);
        if (cinema == null) return NotFound();

        cinema.IsActive = !cinema.IsActive;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã {(cinema.IsActive ? "kích hoạt" : "tạm ngưng")} cụm rạp \"{cinema.Name}\".";
        return RedirectToAction(nameof(Index));
    }
}
