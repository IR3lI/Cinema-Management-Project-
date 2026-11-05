using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Hubs;
using CineManagement.Models;
using CineManagement.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

// ── Resolve WebRootPath regardless of current working directory ────────────
var exeDir = AppContext.BaseDirectory;
string? resolvedWebRoot = null;
string[] candidateWebRoots = [
    Path.Combine(exeDir, "wwwroot"),
    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
    Path.Combine(Directory.GetCurrentDirectory(), "CineManagement", "wwwroot"),
    Path.Combine(Directory.GetCurrentDirectory(), "publish", "wwwroot"),
    Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "wwwroot")),
    Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "CineManagement", "wwwroot"))
];

foreach (var cand in candidateWebRoots)
{
    if (Directory.Exists(cand))
    {
        resolvedWebRoot = cand;
        break;
    }
}

if (!string.IsNullOrEmpty(resolvedWebRoot))
{
    builder.Environment.WebRootPath = resolvedWebRoot;
}

// ── Database Configuration (Pure MySQL/MariaDB) ─────────────────────────────
var mySqlConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=cinema_db;Uid=root;Pwd=;CharSet=utf8mb4;";
if (!mySqlConnStr.Contains("CharSet=", StringComparison.OrdinalIgnoreCase))
{
    mySqlConnStr = mySqlConnStr.TrimEnd(';') + ";CharSet=utf8mb4;";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(mySqlConnStr));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ── Ensure wwwroot/uploads exists and contains all posters & FnB images ────
var targetPostersDir = Path.Combine(exeDir, "wwwroot", "uploads", "posters");
var targetFnbDir = Path.Combine(exeDir, "wwwroot", "uploads", "fnb");
Directory.CreateDirectory(targetPostersDir);
Directory.CreateDirectory(targetFnbDir);

string[] uploadSearchDirs = [
    Path.Combine(Directory.GetCurrentDirectory(), "CineManagement", "wwwroot", "uploads"),
    Path.Combine(Directory.GetCurrentDirectory(), "publish", "wwwroot", "uploads"),
    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"),
    Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "CineManagement", "wwwroot", "uploads")),
    Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "wwwroot", "uploads")),
    Path.Combine(exeDir, "wwwroot", "uploads")
];

foreach (var sDir in uploadSearchDirs)
{
    if (!Directory.Exists(sDir)) continue;
    
    var srcPosters = Path.Combine(sDir, "posters");
    if (Directory.Exists(srcPosters))
    {
        foreach (var file in Directory.GetFiles(srcPosters, "poster_*.jpg"))
        {
            var dest = Path.Combine(targetPostersDir, Path.GetFileName(file));
            if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
            {
                try { File.Copy(file, dest, true); } catch { }
            }
        }
    }

    var srcFnb = Path.Combine(sDir, "fnb");
    if (Directory.Exists(srcFnb))
    {
        foreach (var file in Directory.GetFiles(srcFnb, "fnb_*.jpg"))
        {
            var dest = Path.Combine(targetFnbDir, Path.GetFileName(file));
            if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
            {
                try { File.Copy(file, dest, true); } catch { }
            }
        }
    }
}

// ── Identity ────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;  // Easier for dev/college
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Cookie paths ─────────────────────────────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";
});

// ── Modular Monolith Services ─────────────────────────────────────────────
builder.Services.AddSharedModule(builder.Configuration);
builder.Services.AddCinemasModule();
builder.Services.AddCatalogModule();
builder.Services.AddTicketingModule();
builder.Services.AddOrderingModule();
builder.Services.AddPaymentsModule();

// ── Cross-Cutting Support Services ─────────────────────────────────────────
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();

// ── Binding & Network (Allow mobile devices on local Wi-Fi to connect) ───────
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) && !args.Any(a => a.StartsWith("--urls", StringComparison.OrdinalIgnoreCase)))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000");
}

var app = builder.Build();

// ── Seed database ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SEED DATA INFO] Skipping seed on secondary node: {ex.Message}");
    }
}

// ── HTTP Pipeline ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // No HSTS / HTTPS redirect for easy demo without SSL cert
}

app.UseStaticFiles();

// ── Explicit Physical File Provider for /uploads (Posters & FnB) ─────────────
var uploadsPhysical = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(exeDir, "wwwroot"), "uploads");
if (!Directory.Exists(uploadsPhysical))
{
    string[] candidateUploads = [
        Path.Combine(exeDir, "wwwroot", "uploads"),
        Path.Combine(Directory.GetCurrentDirectory(), "publish", "wwwroot", "uploads"),
        Path.Combine(Directory.GetCurrentDirectory(), "CineManagement", "wwwroot", "uploads"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads")
    ];
    foreach (var cu in candidateUploads)
    {
        if (Directory.Exists(cu))
        {
            uploadsPhysical = cu;
            break;
        }
    }
}

if (Directory.Exists(uploadsPhysical))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPhysical),
        RequestPath = "/uploads"
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SeatBookingHub>("/hubs/seatBooking");
app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// ── Print startup URL ──────────────────────────────────────────────────────
app.Lifetime.ApplicationStarted.Register(() =>
{
    var hostName = System.Net.Dns.GetHostName();
    var lanIps = System.Net.Dns.GetHostAddresses(hostName)
        .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        .Select(ip => ip.ToString())
        .ToList();

    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║            🎬 CINEMA MANAGEMENT SYSTEM - SẴN SÀNG!               ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║  🌐 Trên máy tính (PC):  http://localhost:5000                   ║");
    foreach (var ip in lanIps)
    {
        Console.WriteLine($"║  📱 Trên điện thoại (Wi-Fi): http://{ip}:5000{"",-18}║");
    }
    Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  💾 Cơ sở dữ liệu:      {"MySQL (cinema_db:3306)",-37}║");
    Console.WriteLine("║  📊 Báo cáo & Thống kê:  /Admin/Reports                         ║");
    Console.WriteLine("║  🎫 Quầy POS Bán vé/QR:  /Counter                               ║");
    Console.WriteLine("║  Tài khoản Quản trị:    admin@cinema.com / Admin@123            ║");
    Console.WriteLine("║  Tài khoản Nhân viên:   staff@cinema.com / Staff@123            ║");
    Console.WriteLine("║  Tài khoản Khách hàng:  customer@cinema.com / Customer@123      ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
});

app.Run();
