using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.ViewModels;
using CineManagement.Services;

namespace CineManagement.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        IFileStorageService fileStorageService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _fileStorageService = fileStorageService;
    }

    // ── Register ─────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Register()
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            FullName = model.FullName,
            UserName = model.Email,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            DateOfBirth = model.DateOfBirth,
            EmailConfirmed = true,
            CreatedAt = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Customer");
            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["Success"] = "Đăng ký thành công! Chào mừng bạn.";
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Admin"))
                    return RedirectToAction("Dashboard", "Admin");
                if (roles.Contains("Staff"))
                    return RedirectToAction("Index", "Counter");
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    // ── Admin Login (separate page) ────────────────────────────────────────────
    [HttpGet]
    public IActionResult AdminLogin()
    {
        if (_signInManager.IsSignedIn(User))
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole("Staff"))
                return RedirectToAction("Index", "Counter");
        }
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminLogin(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Admin"))
                    return RedirectToAction("Dashboard", "Admin");
                if (roles.Contains("Staff"))
                    return RedirectToAction("Index", "Counter");

                // Not admin/staff – sign out and show error
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Tài khoản không có quyền truy cập hệ thống quản trị / quầy POS.");
                return View(model);
            }
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    // ── Logout ────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ── Profile ───────────────────────────────────────────────────────────────
    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var vm = new ProfileViewModel
        {
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address
        };

        await PopulateProfileStatsAsync(user, vm);
        return View(vm);
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            if (!_fileStorageService.IsValidImage(model.AvatarFile, out var errorMessage))
            {
                ModelState.AddModelError("AvatarFile", errorMessage ?? "Ảnh đại diện không hợp lệ.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateProfileStatsAsync(user, model);
            return View(model);
        }

        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                _fileStorageService.DeleteFile(user.AvatarUrl);
            }
            user.AvatarUrl = await _fileStorageService.SaveImageAsync(model.AvatarFile, "avatars");
        }

        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;
        user.DateOfBirth = model.DateOfBirth;
        user.Address = model.Address;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Cập nhật hồ sơ thành công!";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        await PopulateProfileStatsAsync(user, model);
        return View(model);
    }

    private async Task PopulateProfileStatsAsync(ApplicationUser user, ProfileViewModel vm)
    {
        var roles = await _userManager.GetRolesAsync(user);
        vm.IsAdmin = roles.Contains("Admin");
        vm.IsStaff = roles.Contains("Staff") || vm.IsAdmin;
        vm.RoleName = vm.IsAdmin ? "Quản trị viên" : (vm.IsStaff ? "Nhân viên quầy vé" : "Thành viên thân thiết");
        vm.Email = user.Email;
        vm.CreatedAt = user.CreatedAt;
        vm.AvatarUrl = user.AvatarUrl;

        if (vm.IsStaff)
        {
            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (staff != null)
            {
                vm.Position = !string.IsNullOrEmpty(staff.Position) ? staff.Position : (vm.IsAdmin ? "Quản lý hệ thống" : "Nhân viên quầy POS");
                vm.Department = !string.IsNullOrEmpty(staff.Department) ? staff.Department : "Quầy vé & Dịch vụ F&B";
                vm.HireDate = staff.HireDate;
                vm.StaffCode = $"NV-{staff.StaffId:D4}";
            }
            else
            {
                vm.Position = vm.IsAdmin ? "Tổng Quản trị hệ thống" : "Nhân viên quầy POS";
                vm.Department = "Ban Vận hành Hệ thống";
                vm.StaffCode = vm.IsAdmin ? "ADMIN-01" : $"NV-{user.Id.Substring(0, 4).ToUpper()}";
            }

            vm.CheckedInCount = await _context.Tickets.CountAsync(t => t.PrintedByUserId == user.Id || t.CheckedInByUserId == user.Id);
        }

        var bookings = await _context.Bookings
            .Include(b => b.Tickets)
            .Where(b => b.UserId == user.Id && b.Status == BookingStatus.Confirmed)
            .ToListAsync();

        vm.TotalBookings = bookings.Count;
        vm.TotalSpent = bookings.Sum(b => b.FinalAmount);
        vm.TotalTickets = bookings.Sum(b => b.Tickets.Count);
    }

    // ── Change Password ───────────────────────────────────────────────────────
    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    // ── Access Denied ─────────────────────────────────────────────────────────
    public IActionResult AccessDenied() => View();
}
