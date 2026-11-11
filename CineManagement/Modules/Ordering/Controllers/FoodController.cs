using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.ViewModels;
using CineManagement.Services;

namespace CineManagement.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class FoodController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public FoodController(ApplicationDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    // ── Index ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(FoodCategory? category, bool? available)
    {
        var query = _context.FoodItems.AsQueryable();

        if (category.HasValue) query = query.Where(f => f.Category == category);
        if (available.HasValue) query = query.Where(f => f.IsAvailable == available);

        ViewBag.Category  = category;
        ViewBag.Available = available;
        return View(await query.OrderBy(f => f.Category).ThenBy(f => f.Name).ToListAsync());
    }

    // ── Create ────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create() => View(new FoodFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FoodFormViewModel model)
    {
        if (model.ImageFile != null)
        {
            if (!_fileStorageService.IsValidImage(model.ImageFile, out var errorMessage))
            {
                ModelState.AddModelError("ImageFile", errorMessage ?? "Ảnh không hợp lệ.");
            }
        }

        if (!ModelState.IsValid) return View(model);

        var item = new FoodItem
        {
            Name        = model.Name,
            Description = model.Description,
            Price       = model.Price,
            Category    = model.Category,
            IsAvailable = model.IsAvailable
        };

        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            item.ImageUrl = await _fileStorageService.SaveImageAsync(model.ImageFile, "food");
        }

        _context.FoodItems.Add(item);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Thêm món \"{item.Name}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.FoodItems.FindAsync(id);
        if (item == null) return NotFound();

        return View(new FoodFormViewModel
        {
            FoodItemId  = item.FoodItemId,
            Name        = item.Name,
            Description = item.Description,
            Price       = item.Price,
            Category    = item.Category,
            ImageUrl    = item.ImageUrl,
            IsAvailable = item.IsAvailable
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FoodFormViewModel model)
    {
        if (id != model.FoodItemId) return BadRequest();

        if (model.ImageFile != null)
        {
            if (!_fileStorageService.IsValidImage(model.ImageFile, out var errorMessage))
            {
                ModelState.AddModelError("ImageFile", errorMessage ?? "Ảnh không hợp lệ.");
            }
        }

        if (!ModelState.IsValid) return View(model);

        var item = await _context.FoodItems.FindAsync(id);
        if (item == null) return NotFound();

        item.Name        = model.Name;
        item.Description = model.Description;
        item.Price       = model.Price;
        item.Category    = model.Category;
        item.IsAvailable = model.IsAvailable;

        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            // Xóa ảnh cũ nếu có
            if (!string.IsNullOrEmpty(item.ImageUrl))
            {
                _fileStorageService.DeleteFile(item.ImageUrl);
            }
            item.ImageUrl = await _fileStorageService.SaveImageAsync(model.ImageFile, "food");
        }

        _context.Update(item);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Cập nhật món \"{item.Name}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Toggle Availability ───────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAvailable(int id)
    {
        var item = await _context.FoodItems.FindAsync(id);
        if (item == null) return NotFound();

        item.IsAvailable = !item.IsAvailable;
        _context.Update(item);
        await _context.SaveChangesAsync();

        TempData["Success"] = item.IsAvailable
            ? $"Món \"{item.Name}\" đã được kích hoạt trở lại!"
            : $"Món \"{item.Name}\" đã tạm ngừng phục vụ!";
        return RedirectToAction(nameof(Index));
    }

    // ── Delete ────────────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.FoodItems.FindAsync(id);
        if (item == null) return NotFound();

        // Xóa file ảnh đính kèm khỏi ổ đĩa
        if (!string.IsNullOrEmpty(item.ImageUrl))
        {
            _fileStorageService.DeleteFile(item.ImageUrl);
        }

        _context.FoodItems.Remove(item);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã xóa món \"{item.Name}\"!";
        return RedirectToAction(nameof(Index));
    }
}
