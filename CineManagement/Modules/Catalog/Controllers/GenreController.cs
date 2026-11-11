using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;

namespace CineManagement.Controllers;

[Authorize(Roles = "Admin")]
public class GenreController : Controller
{
    private readonly ApplicationDbContext _context;

    public GenreController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var genres = await _context.Genres
            .Include(g => g.Movies.Where(m => m.IsActive))
            .OrderBy(g => g.Name)
            .ToListAsync();
        return View(genres);
    }

    [HttpGet]
    public IActionResult Create() => View(new Genre());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Genre genre)
    {
        if (!ModelState.IsValid) return View(genre);

        _context.Genres.Add(genre);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Thêm thể loại \"{genre.Name}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var genre = await _context.Genres.FindAsync(id);
        if (genre == null) return NotFound();
        return View(genre);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Genre genre)
    {
        if (id != genre.GenreId) return BadRequest();
        if (!ModelState.IsValid) return View(genre);

        _context.Update(genre);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Cập nhật thể loại \"{genre.Name}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var genre = await _context.Genres.FindAsync(id);
        if (genre == null) return NotFound();

        _context.Genres.Remove(genre);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Đã xóa thể loại!";
        return RedirectToAction(nameof(Index));
    }
}
