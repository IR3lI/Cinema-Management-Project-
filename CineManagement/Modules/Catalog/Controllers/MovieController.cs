using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.ViewModels;
using CineManagement.Services;

namespace CineManagement.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class MovieController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public MovieController(ApplicationDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    // ── Index + Filter ────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? search, int? genreId, MovieStatus? status)
    {
        var query = _context.Movies.Include(m => m.Genre).Where(m => m.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Title.Contains(search) || (m.Director != null && m.Director.Contains(search)));

        if (genreId.HasValue)
            query = query.Where(m => m.GenreId == genreId);

        if (status.HasValue)
            query = query.Where(m => m.Status == status);

        var genres = await _context.Genres.OrderBy(g => g.Name).ToListAsync();

        var vm = new MovieIndexViewModel
        {
            Movies = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(),
            SearchTerm = search,
            GenreId = genreId,
            Status = status,
            Genres = genres.Select(g => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(g.Name, g.GenreId.ToString()))
        };

        return View(vm);
    }

    // ── Details ───────────────────────────────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var movie = await _context.Movies.Include(m => m.Genre).FirstOrDefaultAsync(m => m.MovieId == id);
        if (movie == null) return NotFound();
        return View(movie);
    }

    // ── Create ────────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = await BuildFormViewModel(new MovieFormViewModel());
        return View(vm);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MovieFormViewModel model)
    {
        if (model.PosterFile != null)
        {
            if (!_fileStorageService.IsValidImage(model.PosterFile, out var errorMessage))
            {
                ModelState.AddModelError("PosterFile", errorMessage ?? "Ảnh poster không hợp lệ.");
            }
        }

        if (!ModelState.IsValid)
            return View(await BuildFormViewModel(model));

        var movie = new Movie
        {
            Title = model.Title,
            Description = model.Description,
            Director = model.Director,
            Actors = model.Actors,
            Duration = model.Duration,
            ReleaseDate = model.ReleaseDate,
            EndDate = model.EndDate,
            TrailerUrl = model.TrailerUrl,
            Status = model.Status,
            Rating = model.Rating,
            AgeRating = model.AgeRating,
            Language = model.Language,
            GenreId = model.GenreId,
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        // Handle poster upload
        if (model.PosterFile != null && model.PosterFile.Length > 0)
        {
            movie.PosterUrl = await _fileStorageService.SaveImageAsync(model.PosterFile, "posters");
        }

        _context.Movies.Add(movie);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Thêm phim \"{movie.Title}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var movie = await _context.Movies.FindAsync(id);
        if (movie == null) return NotFound();

        var vm = new MovieFormViewModel
        {
            MovieId = movie.MovieId,
            Title = movie.Title,
            Description = movie.Description,
            Director = movie.Director,
            Actors = movie.Actors,
            Duration = movie.Duration,
            ReleaseDate = movie.ReleaseDate,
            EndDate = movie.EndDate,
            PosterUrl = movie.PosterUrl,
            TrailerUrl = movie.TrailerUrl,
            Status = movie.Status,
            Rating = movie.Rating,
            AgeRating = movie.AgeRating,
            Language = movie.Language,
            GenreId = movie.GenreId
        };

        return View(await BuildFormViewModel(vm));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MovieFormViewModel model)
    {
        if (id != model.MovieId) return BadRequest();

        if (model.PosterFile != null)
        {
            if (!_fileStorageService.IsValidImage(model.PosterFile, out var errorMessage))
            {
                ModelState.AddModelError("PosterFile", errorMessage ?? "Ảnh poster không hợp lệ.");
            }
        }

        if (!ModelState.IsValid)
            return View(await BuildFormViewModel(model));

        var movie = await _context.Movies.FindAsync(id);
        if (movie == null) return NotFound();

        movie.Title = model.Title;
        movie.Description = model.Description;
        movie.Director = model.Director;
        movie.Actors = model.Actors;
        movie.Duration = model.Duration;
        movie.ReleaseDate = model.ReleaseDate;
        movie.EndDate = model.EndDate;
        movie.TrailerUrl = model.TrailerUrl;
        movie.Status = model.Status;
        movie.Rating = model.Rating;
        movie.AgeRating = model.AgeRating;
        movie.Language = model.Language;
        movie.GenreId = model.GenreId;

        if (model.PosterFile != null && model.PosterFile.Length > 0)
        {
            // Delete old poster
            if (!string.IsNullOrEmpty(movie.PosterUrl))
                _fileStorageService.DeleteFile(movie.PosterUrl);

            movie.PosterUrl = await _fileStorageService.SaveImageAsync(model.PosterFile, "posters");
        }

        _context.Update(movie);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Cập nhật phim \"{movie.Title}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var movie = await _context.Movies.FindAsync(id);
        if (movie == null) return NotFound();

        // Soft delete
        movie.IsActive = false;
        _context.Update(movie);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã xóa phim \"{movie.Title}\"!";
        return RedirectToAction(nameof(Index));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<MovieFormViewModel> BuildFormViewModel(MovieFormViewModel vm)
    {
        var genres = await _context.Genres.OrderBy(g => g.Name).ToListAsync();
        vm.Genres = genres.Select(g =>
            new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(g.Name, g.GenreId.ToString()));
        return vm;
    }
}
