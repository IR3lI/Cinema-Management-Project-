using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.ViewModels;

namespace CineManagement.Controllers;

[Authorize(Roles = "Admin")]
public class StaffController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public StaffController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ── Index ─────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? search, string? department)
    {
        var query = _context.Staffs
            .Include(s => s.User)
            .Where(s => s.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s =>
                s.User!.FullName.Contains(search) ||
                s.User!.Email!.Contains(search) ||
                (s.Position != null && s.Position.Contains(search)));

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(s => s.Department == department);

        ViewBag.Search = search;
        ViewBag.Department = department;
        ViewBag.Departments = await _context.Staffs
            .Where(s => s.Department != null)
            .Select(s => s.Department!)
            .Distinct()
            .ToListAsync();

        return View(await query.OrderBy(s => s.User!.FullName).ToListAsync());
    }

    // ── Details ───────────────────────────────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var staff = await _context.Staffs.Include(s => s.User).FirstOrDefaultAsync(s => s.StaffId == id);
        if (staff == null) return NotFound();
        return View(staff);
    }

    // ── Create ────────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create() => View(new StaffCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Check duplicate email
        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError("Email", "Email này đã được sử dụng.");
            return View(model);
        }

        // Create identity user
        var user = new ApplicationUser
        {
            FullName = model.FullName,
            UserName = model.Email,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            EmailConfirmed = true,
            CreatedAt = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // Assign role (Staff or Admin)
        var role = model.Role == "Admin" ? "Admin" : "Staff";
        await _userManager.AddToRoleAsync(user, role);

        // Create Staff record
        var staff = new Staff
        {
            UserId = user.Id,
            Position = model.Position,
            Department = model.Department,
            HireDate = model.HireDate,
            Salary = model.Salary,
            Notes = model.Notes,
            IsActive = true
        };

        _context.Staffs.Add(staff);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Thêm nhân viên \"{model.FullName}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var staff = await _context.Staffs.Include(s => s.User).FirstOrDefaultAsync(s => s.StaffId == id);
        if (staff == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(staff.User!);
        var vm = new StaffEditViewModel
        {
            StaffId = staff.StaffId,
            UserId = staff.UserId,
            FullName = staff.User!.FullName,
            PhoneNumber = staff.User.PhoneNumber,
            Position = staff.Position,
            Department = staff.Department,
            HireDate = staff.HireDate,
            Salary = staff.Salary,
            Notes = staff.Notes,
            IsActive = staff.IsActive,
            Role = roles.Contains("Admin") ? "Admin" : "Staff"
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StaffEditViewModel model)
    {
        if (id != model.StaffId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var staff = await _context.Staffs.Include(s => s.User).FirstOrDefaultAsync(s => s.StaffId == id);
        if (staff == null) return NotFound();

        // Update user info
        staff.User!.FullName = model.FullName;
        staff.User.PhoneNumber = model.PhoneNumber;
        await _userManager.UpdateAsync(staff.User);

        // Update role
        var currentRoles = await _userManager.GetRolesAsync(staff.User);
        await _userManager.RemoveFromRolesAsync(staff.User, currentRoles);
        await _userManager.AddToRoleAsync(staff.User, model.Role == "Admin" ? "Admin" : "Staff");

        // Update staff record
        staff.Position = model.Position;
        staff.Department = model.Department;
        staff.HireDate = model.HireDate;
        staff.Salary = model.Salary;
        staff.Notes = model.Notes;
        staff.IsActive = model.IsActive;

        _context.Update(staff);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Cập nhật nhân viên \"{model.FullName}\" thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── Delete (soft) ─────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var staff = await _context.Staffs.Include(s => s.User).FirstOrDefaultAsync(s => s.StaffId == id);
        if (staff == null) return NotFound();

        staff.IsActive = false;
        if (staff.User != null)
            staff.User.IsActive = false;

        _context.Update(staff);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã vô hiệu hóa tài khoản nhân viên!";
        return RedirectToAction(nameof(Index));
    }
}
