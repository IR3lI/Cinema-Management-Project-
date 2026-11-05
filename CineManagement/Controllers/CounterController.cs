using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CineManagement.Common;
using CineManagement.Data;
using CineManagement.Hubs;
using CineManagement.Models;
using CineManagement.Services;

namespace CineManagement.Controllers;

/// <summary>
/// Cổng nghiệp vụ Quầy bán vé POS (Counter POS) dành cho Nhân viên quầy (Staff) và Quản lý rạp (Admin):
/// - Luồng 1: Quét mã QR vé đặt Online từ Email -> Tư vấn Upsell thêm Combo F&B -> Thu thêm tiền -> In vé nhiệt 80mm & Chống in lại 2 lần.
/// - Luồng 2: Bán vé trực tiếp cho khách vãng lai tại quầy (Walk-in POS) -> Chọn suất, ghế 2D realtime, F&B -> Thu tiền -> In vé nhiệt 80mm.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
public class CounterController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBookingService _bookingService;
    private readonly IHubContext<SeatBookingHub> _hubContext;
    private readonly ILogger<CounterController> _logger;

    public CounterController(
        ApplicationDbContext context,
        IBookingService bookingService,
        IHubContext<SeatBookingHub> hubContext,
        ILogger<CounterController> logger)
    {
        _context = context;
        _bookingService = bookingService;
        _hubContext = hubContext;
        _logger = logger;
    }

    // ── DTOs cho Counter POS ──────────────────────────────────────────────
    public record CounterScanQrRequest(string? QrOrCode);

    public record CounterFnbItemDto(int FoodItemId, int Quantity);

    public record ConfirmPrintAndUpsellRequest(
        int BookingId,
        List<CounterFnbItemDto>? UpsellItems,
        PaymentMethod PaymentMethod = PaymentMethod.Cash);

    public record WalkInOrderRequest(
        int ShowtimeId,
        List<int> SeatIds,
        List<CounterFnbItemDto>? FnbItems,
        string? CustomerName,
        string? CustomerPhone,
        PaymentMethod PaymentMethod = PaymentMethod.Cash);

    // ── Giao diện chính Quầy POS (/Counter) ───────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index(int? cinemaId, int? showtimeId)
    {
        var cinemas = await _context.Cinemas
            .Where(c => c.IsActive)
            .OrderBy(c => c.CinemaId)
            .ToListAsync();

        var fromTime = DateTime.Today;
        var showtimesQuery = _context.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
                .ThenInclude(r => r!.Cinema)
            .Where(s => s.Status == ShowtimeStatus.Available && s.StartTime >= fromTime);

        if (cinemaId.HasValue)
        {
            showtimesQuery = showtimesQuery.Where(s => s.Room != null && s.Room.CinemaId == cinemaId.Value);
        }

        var showtimes = await showtimesQuery
            .OrderBy(s => s.StartTime)
            .Take(40)
            .ToListAsync();

        var foodItems = await _context.FoodItems
            .Where(f => f.IsAvailable)
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Price)
            .ToListAsync();

        // Mẫu vé đã thanh toán để 1-Click Test nhanh tại quầy
        var sampleBookings = await _context.Bookings
            .Include(b => b.Tickets)
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room).ThenInclude(r => r!.Cinema)
            .Where(b => (b.Status == BookingStatus.Confirmed || b.PaymentStatus == PaymentStatus.Paid) && b.Tickets.Any())
            .OrderByDescending(b => b.BookingId)
            .Take(8)
            .ToListAsync();

        // Lịch sử in vé trong ca hôm nay
        var recentPrinted = await _context.Bookings
            .Include(b => b.Tickets).ThenInclude(t => t.PrintedBy)
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room)
            .Include(b => b.BookingConcessions)
            .Where(b => b.Tickets.Any(t => t.IsPrinted))
            .OrderByDescending(b => b.UpdatedAt ?? b.BookingTime)
            .Take(15)
            .ToListAsync();

        ViewBag.Cinemas = cinemas;
        ViewBag.SelectedCinemaId = cinemaId;
        ViewBag.Showtimes = showtimes;
        ViewBag.SelectedShowtimeId = showtimeId ?? showtimes.FirstOrDefault()?.ShowtimeId;
        ViewBag.FoodItems = foodItems;
        ViewBag.SampleBookings = sampleBookings;
        ViewBag.RecentPrinted = recentPrinted;

        return View();
    }

    // ── 1. Quét mã QR vé Online tại Quầy (Chưa in ngay, trả về thông tin để Upsell F&B) ──
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanBookingQr(
        [FromBody] CounterScanQrRequest? req,
        [FromForm] string? qrOrCode,
        [FromQuery] string? q)
    {
        var rawCode = (req?.QrOrCode ?? qrOrCode ?? q)?.Trim();
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return Json(new { success = false, message = "Vui lòng quét mã QR hoặc nhập mã đặt vé / mã vé." });
        }

        var lookupCode = rawCode;
        int? parsedBookingOrTicketId = null;

        // Hỗ trợ cả định dạng "cinema-ticket:{id}:{code}"
        if (rawCode.StartsWith("cinema-ticket:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = rawCode.Split(':');
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[1], out var parsedId))
                    parsedBookingOrTicketId = parsedId;
                lookupCode = parts[2];
            }
        }

        // Tìm theo BookingCode trước
        var booking = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room).ThenInclude(r => r!.Cinema)
            .Include(b => b.Tickets).ThenInclude(t => t.Seat).ThenInclude(s => s!.SeatType)
            .Include(b => b.Tickets).ThenInclude(t => t.PrintedBy)
            .Include(b => b.BookingConcessions).ThenInclude(bc => bc.FoodItem)
            .FirstOrDefaultAsync(b => b.BookingCode == lookupCode || b.BookingCode == rawCode);

        // Nếu không khớp BookingCode, tìm theo TicketCode hoặc Id
        if (booking == null)
        {
            var ticket = await _context.Tickets
                .FirstOrDefaultAsync(t =>
                    t.TicketCode == lookupCode ||
                    t.TicketCode == rawCode ||
                    (parsedBookingOrTicketId.HasValue && (t.BookingId == parsedBookingOrTicketId.Value || t.TicketId == parsedBookingOrTicketId.Value)));

            if (ticket != null)
            {
                booking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
                    .Include(b => b.Showtime).ThenInclude(s => s!.Room).ThenInclude(r => r!.Cinema)
                    .Include(b => b.Tickets).ThenInclude(t => t.Seat).ThenInclude(s => s!.SeatType)
                    .Include(b => b.Tickets).ThenInclude(t => t.PrintedBy)
                    .Include(b => b.BookingConcessions).ThenInclude(bc => bc.FoodItem)
                    .FirstOrDefaultAsync(b => b.BookingId == ticket.BookingId);
            }
        }

        if (booking == null || !booking.Tickets.Any())
        {
            return Json(new
            {
                success = false,
                message = $"❌ Không tìm thấy đơn vé nào khớp với mã \"{rawCode}\"."
            });
        }

        // Kiểm tra trạng thái thanh toán của đơn hàng
        bool isPaid = booking.Status == BookingStatus.Confirmed || booking.PaymentStatus == PaymentStatus.Paid;
        if (!isPaid)
        {
            var statusDesc = booking.Status switch
            {
                BookingStatus.Pending => "Đang chờ thanh toán",
                BookingStatus.Cancelled => "Đã bị hủy",
                BookingStatus.Expired => "Đã hết hạn giữ chỗ",
                BookingStatus.Refunded => "Đã hoàn tiền",
                _ => booking.Status.ToString()
            };
            return Json(new
            {
                success = false,
                message = $"❌ Đơn hàng {booking.BookingCode} chưa thanh toán hợp lệ ({statusDesc})! Không thể đổi vé vật lý."
            });
        }

        var tickets = booking.Tickets.OrderBy(t => t.SeatCode).ToList();
        bool isPrinted = tickets.Any(t => t.IsPrinted);
        var printedTicket = tickets.FirstOrDefault(t => t.IsPrinted);
        var printedAtStr = printedTicket?.PrintedAt?.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy");
        var printedByStaff = printedTicket?.PrintedBy?.FullName ?? printedTicket?.PrintedBy?.Email ?? "Nhân viên quầy";

        var seatDetails = tickets.Select(t =>
        {
            var typeName = t.Seat?.SeatType?.Name ?? (t.Seat?.SeatTypeLegacy switch
            {
                SeatTypeEnum.VIP => "Ghế VIP",
                SeatTypeEnum.Couple => "Ghế Đôi",
                _ => "Ghế Thường"
            });
            return new
            {
                ticketId = t.TicketId,
                ticketCode = t.TicketCode,
                seatCode = t.SeatCode,
                seatType = typeName,
                unitPrice = t.UnitPrice,
                colorCode = t.Seat?.SeatType?.ColorCode ?? "#059669",
                display = $"{t.SeatCode} ({typeName})"
            };
        }).ToList();

        var preOrderedFnb = booking.BookingConcessions.Select(bc => new
        {
            bookingConcessionId = bc.BookingConcessionId,
            foodItemId = bc.FoodItemId,
            itemName = bc.ItemName ?? bc.FoodItem?.Name ?? "Combo Bắp Nước",
            quantity = bc.Quantity,
            unitPrice = bc.UnitPrice,
            subTotal = bc.UnitPrice * bc.Quantity
        }).ToList();

        var availableFoodItems = await _context.FoodItems
            .Where(f => f.IsAvailable)
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Price)
            .Select(f => new
            {
                foodItemId = f.FoodItemId,
                name = f.Name,
                category = f.Category.ToString(),
                price = f.Price,
                description = f.Description ?? "Tiêu chuẩn",
                imageUrl = f.ImageUrl
            })
            .ToListAsync();

        var movie = booking.Showtime?.Movie;
        var room = booking.Showtime?.Room;
        var cinema = room?.Cinema;

        return Json(new
        {
            success = true,
            bookingId = booking.BookingId,
            bookingCode = booking.BookingCode,
            customerName = booking.User?.FullName ?? booking.ContactEmail ?? "Khách hàng",
            customerPhone = booking.ContactPhone ?? booking.User?.PhoneNumber ?? "–",
            customerEmail = booking.ContactEmail ?? booking.User?.Email ?? "–",
            cinemaName = cinema?.Name ?? "CineStar Quốc Thanh",
            cinemaAddress = cinema?.Address ?? "271 Nguyễn Trãi, Q.1, TP.HCM",
            roomName = room?.RoomName ?? "Phòng chiếu",
            movieTitle = movie?.Title ?? "Phim chiếu rạp",
            movieFormat = movie?.Format ?? "2D",
            subtitleType = movie?.SubtitleType ?? movie?.Language ?? "Phụ đề Việt",
            ageRating = movie?.AgeRating ?? "P",
            startTime = booking.Showtime?.StartTime.ToVietnameseDateTimeString() ?? "",
            seatSummary = string.Join(", ", tickets.Select(t => t.SeatCode)),
            ticketCount = tickets.Count,
            seatDetails,
            preOrderedFnb,
            ticketAmount = booking.TicketAmount,
            fnbAmount = booking.FnBAmount,
            discountAmount = booking.DiscountAmount,
            finalAmount = booking.FinalAmount,
            isPrinted,
            printedAt = printedAtStr,
            printedByStaff,
            thermalReceiptCode = printedTicket?.ThermalReceiptCode,
            printUrl = $"/Counter/PrintThermalTicket?bookingId={booking.BookingId}",
            availableFoodItems
        });
    }

    // ── 2. Xác nhận Upsell thêm Combo F&B tại quầy & In vé nhiệt 80mm ──────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPrintAndUpsell([FromBody] ConfirmPrintAndUpsellRequest request)
    {
        if (request == null || request.BookingId <= 0)
        {
            return Json(new { success = false, message = "Dữ liệu đơn hàng không hợp lệ." });
        }

        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Tickets).ThenInclude(t => t.PrintedBy)
                .Include(b => b.BookingConcessions)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId);

            if (booking == null || !booking.Tickets.Any())
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Không tìm thấy đơn hàng hoặc vé." });
            }

            // CHẶN CỨNG NẾU VÉ ĐÃ ĐƯỢC IN TRƯỚC ĐÓ (Chống quét lại mã QR lấy vé 2 lần)
            if (booking.Tickets.Any(t => t.IsPrinted))
            {
                var printedTicket = booking.Tickets.First(t => t.IsPrinted);
                var printedTime = printedTicket.PrintedAt?.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy") ?? "Trước đó";
                var staffName = printedTicket.PrintedBy?.FullName ?? printedTicket.PrintedBy?.Email ?? "Nhân viên quầy";

                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    alreadyPrinted = true,
                    message = $"⚠️ CẢNH BÁO ĐỎ: Đơn vé {booking.BookingCode} ĐÃ ĐƯỢC IN VÉ VẬT LÝ lúc {printedTime} bởi {staffName} (Mã phiếu: {printedTicket.ThermalReceiptCode})! Hệ thống từ chối in lại lần 2 để chống gian lận."
                });
            }

            var nowUtc = DateTime.UtcNow;
            var staffUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            decimal upsellAmount = 0m;

            // Xử lý Upsell Combo F&B nếu khách mua thêm tại quầy
            var validUpsellItems = request.UpsellItems?.Where(i => i.FoodItemId > 0 && i.Quantity > 0).ToList() ?? new();
            if (validUpsellItems.Count > 0)
            {
                var foodIds = validUpsellItems.Select(i => i.FoodItemId).Distinct().ToList();
                var foodDict = await _context.FoodItems
                    .Where(f => foodIds.Contains(f.FoodItemId) && f.IsAvailable)
                    .ToDictionaryAsync(f => f.FoodItemId);

                foreach (var item in validUpsellItems)
                {
                    if (!foodDict.TryGetValue(item.FoodItemId, out var food))
                        continue;

                    var lineAmount = food.Price * item.Quantity;
                    upsellAmount += lineAmount;

                    var existingConcession = booking.BookingConcessions
                        .FirstOrDefault(bc => bc.FoodItemId == food.FoodItemId && bc.UnitPrice == food.Price);

                    if (existingConcession != null)
                    {
                        existingConcession.Quantity += item.Quantity;
                    }
                    else
                    {
                        _context.BookingConcessions.Add(new BookingConcession
                        {
                            BookingId = booking.BookingId,
                            FoodItemId = food.FoodItemId,
                            Quantity = item.Quantity,
                            UnitPrice = food.Price,
                            ItemName = $"{food.Name} (Mua thêm tại quầy)",
                            CreatedAt = nowUtc
                        });
                    }
                }

                if (upsellAmount > 0)
                {
                    booking.FnBAmount += upsellAmount;
                    booking.TotalAmount += upsellAmount;
                    booking.FinalAmount += upsellAmount;
                    booking.UpdatedAt = nowUtc;

                    // Tạo bản ghi Payment thu tiền Upsell tại quầy
                    _context.Payments.Add(new Payment
                    {
                        BookingId = booking.BookingId,
                        Amount = upsellAmount,
                        Method = request.PaymentMethod,
                        Provider = request.PaymentMethod == PaymentMethod.Cash ? PaymentProvider.Cash : PaymentProvider.VietQR,
                        TransactionCode = $"POS-UPSELL-{nowUtc:yyMMddHHmmss}{Random.Shared.Next(10, 99)}",
                        Status = PaymentStatus.Paid,
                        PaidAt = nowUtc,
                        GatewayResponse = "Thu tiền Upsell Combo F&B tại quầy POS",
                        CreatedAt = nowUtc
                    });
                }
            }

            // Đánh dấu toàn bộ vé đã in vật lý (IsPrinted = true)
            var thermalCode = $"POS80-{DateTime.Now:yyyyMMdd}-{booking.BookingId:D4}";
            foreach (var ticket in booking.Tickets)
            {
                ticket.IsPrinted = true;
                ticket.PrintedAt = nowUtc;
                ticket.PrintedByUserId = staffUserId;
                ticket.ThermalReceiptCode = thermalCode;
                ticket.Status = TicketStatus.CheckedIn;
                ticket.CheckedInAt = nowUtc;
                ticket.CheckedInByUserId = staffUserId;
                ticket.UpdatedAt = nowUtc;
            }

            booking.UpdatedAt = nowUtc;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "[CounterPOS] Đã xác nhận in vé nhiệt #{ThermalCode} cho đơn #{BookingCode} (Upsell F&B: +{UpsellAmount:N0}đ).",
                thermalCode, booking.BookingCode, upsellAmount);

            return Json(new
            {
                success = true,
                message = upsellAmount > 0
                    ? $"✅ Đã cộng thêm Combo F&B (+{upsellAmount:N0}đ) và xuất lệnh in vé nhiệt #{thermalCode}!"
                    : $"✅ Đã xác nhận đổi vé và xuất lệnh in vé nhiệt #{thermalCode}!",
                bookingId = booking.BookingId,
                bookingCode = booking.BookingCode,
                thermalReceiptCode = thermalCode,
                upsellAmount,
                newFinalAmount = booking.FinalAmount,
                printedAt = nowUtc.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy"),
                printUrl = $"/Counter/PrintThermalTicket?bookingId={booking.BookingId}"
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[CounterPOS] Lỗi khi xác nhận in vé và Upsell cho đơn #{BookingId}", request.BookingId);
            return Json(new { success = false, message = $"Lỗi hệ thống khi xử lý in vé: {ex.Message}" });
        }
    }

    // ── 3. Lấy sơ đồ ghế 2D cho Walk-in POS tại quầy ──────────────────────
    [HttpGet]
    public async Task<IActionResult> GetShowtimeSeatMap(int showtimeId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var seatMap = await _bookingService.GetSeatMapAsync(showtimeId, userId);
            return Json(new { success = true, data = seatMap });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── 4. Bán vé trực tiếp cho khách vãng lai tại quầy (Walk-in POS) ─────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWalkInOrder([FromBody] WalkInOrderRequest request)
    {
        if (request == null || request.ShowtimeId <= 0 || request.SeatIds == null || !request.SeatIds.Any())
        {
            return Json(new { success = false, message = "Vui lòng chọn suất chiếu và ít nhất 1 ghế." });
        }

        var seatIds = request.SeatIds.Distinct().ToList();
        var staffUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var nowUtc = DateTime.UtcNow;

        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.ShowtimeId == request.ShowtimeId);

            if (showtime == null || showtime.Status != ShowtimeStatus.Available)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Suất chiếu không tồn tại hoặc đã đóng." });
            }

            var targetSeats = await _context.ShowtimeSeats
                .Include(ss => ss.Seat)
                    .ThenInclude(s => s!.SeatType)
                .Where(ss => ss.ShowtimeId == request.ShowtimeId && seatIds.Contains(ss.SeatId))
                .ToListAsync();

            if (targetSeats.Count != seatIds.Count)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Một hoặc nhiều ghế không hợp lệ." });
            }

            // Kiểm tra ghế trống (Cho phép bán nếu Available, hoặc Held đã hết hạn, hoặc Held bởi chính nhân viên này)
            var conflictSeats = targetSeats
                .Where(ss => ss.Status == ShowtimeSeatStatus.Reserved ||
                            (ss.Status == ShowtimeSeatStatus.Held && ss.HoldExpiresAt > nowUtc && ss.HeldByUserId != staffUserId))
                .Select(ss => ss.Seat?.SeatCode ?? $"#{ss.SeatId}")
                .ToList();

            if (conflictSeats.Count > 0)
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message = $"Ghế {string.Join(", ", conflictSeats)} vừa có khách đặt hoặc đang được giữ. Vui lòng chọn ghế khác."
                });
            }

            // Tính tiền vé
            decimal ticketAmount = 0m;
            var seatPrices = new Dictionary<int, decimal>();
            foreach (var ss in targetSeats)
            {
                var surcharge = ss.Seat?.SeatType?.Surcharge ?? 0m;
                var price = showtime.TicketPrice + surcharge;
                seatPrices[ss.SeatId] = price;
                ticketAmount += price;

                // Khóa ghế ngay sang Reserved
                ss.Status = ShowtimeSeatStatus.Reserved;
                ss.HeldByUserId = null;
                ss.HoldExpiresAt = null;
                ss.Version++;
                ss.UpdatedAt = nowUtc;
            }

            // Tính tiền F&B
            decimal fnbAmount = 0m;
            var validFnb = request.FnbItems?.Where(i => i.FoodItemId > 0 && i.Quantity > 0).ToList() ?? new();
            var fnbEntities = new List<(FoodItem Food, int Quantity)>();

            if (validFnb.Count > 0)
            {
                var foodIds = validFnb.Select(i => i.FoodItemId).Distinct().ToList();
                var foodDict = await _context.FoodItems
                    .Where(f => foodIds.Contains(f.FoodItemId) && f.IsAvailable)
                    .ToDictionaryAsync(f => f.FoodItemId);

                foreach (var item in validFnb)
                {
                    if (foodDict.TryGetValue(item.FoodItemId, out var food))
                    {
                        fnbAmount += food.Price * item.Quantity;
                        fnbEntities.Add((food, item.Quantity));
                    }
                }
            }

            var totalAmount = ticketAmount + fnbAmount;
            var bookingCode = $"POS-{DateTime.Now:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
            var customerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Khách vãng lai (Walk-in)" : request.CustomerName.Trim();

            var booking = new Booking
            {
                UserId = staffUserId,
                ShowtimeId = request.ShowtimeId,
                BookingCode = bookingCode,
                BookingTime = DateTime.Now,
                TicketAmount = ticketAmount,
                FnBAmount = fnbAmount,
                TotalAmount = totalAmount,
                DiscountAmount = 0m,
                FinalAmount = totalAmount,
                Status = BookingStatus.Confirmed,
                PaymentStatus = PaymentStatus.Paid,
                PaymentMethod = request.PaymentMethod,
                PaymentReference = $"WALKIN-{DateTime.UtcNow:yyMMddHHmmss}",
                PaidAt = nowUtc,
                ContactPhone = request.CustomerPhone?.Trim(),
                Notes = $"Bán trực tiếp tại quầy POS - Khách: {customerName}",
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            var thermalCode = $"POS80-{DateTime.Now:yyyyMMdd}-{booking.BookingId:D4}";

            // Tạo Tickets (Đánh dấu IsPrinted = true ngay vì in trực tiếp tại quầy)
            foreach (var ss in targetSeats.OrderBy(x => x.Seat!.RowLabel).ThenBy(x => x.Seat!.SeatNumber))
            {
                var tCode = $"TCK-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
                var sCode = ss.Seat?.SeatCode ?? $"#{ss.SeatId}";
                _context.Tickets.Add(new Ticket
                {
                    BookingId = booking.BookingId,
                    ShowtimeId = request.ShowtimeId,
                    SeatId = ss.SeatId,
                    ShowtimeSeatId = ss.ShowtimeSeatId,
                    TicketCode = tCode,
                    SeatCode = sCode,
                    UnitPrice = seatPrices[ss.SeatId],
                    Status = TicketStatus.CheckedIn,
                    QrPayload = $"cinema-ticket:{booking.BookingId}:{tCode}",
                    CheckedInAt = nowUtc,
                    CheckedInByUserId = staffUserId,
                    IsPrinted = true,
                    PrintedAt = nowUtc,
                    PrintedByUserId = staffUserId,
                    ThermalReceiptCode = thermalCode,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
            }

            // Tạo BookingConcessions
            foreach (var (food, qty) in fnbEntities)
            {
                _context.BookingConcessions.Add(new BookingConcession
                {
                    BookingId = booking.BookingId,
                    FoodItemId = food.FoodItemId,
                    Quantity = qty,
                    UnitPrice = food.Price,
                    ItemName = food.Name,
                    CreatedAt = nowUtc
                });
            }

            // Tạo Payment
            _context.Payments.Add(new Payment
            {
                BookingId = booking.BookingId,
                Amount = booking.FinalAmount,
                Method = request.PaymentMethod,
                Provider = request.PaymentMethod == PaymentMethod.Cash ? PaymentProvider.Cash : PaymentProvider.VietQR,
                TransactionCode = booking.PaymentReference,
                Status = PaymentStatus.Paid,
                PaidAt = nowUtc,
                GatewayResponse = $"Thu tiền Walk-in POS tại quầy ({customerName})",
                CreatedAt = nowUtc
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Phát sóng SignalR thời gian thực báo các ghế đã bán
            try
            {
                var groupName = SeatBookingHub.GetShowtimeGroup(request.ShowtimeId);
                foreach (var ss in targetSeats)
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("SeatStatusChanged", new
                    {
                        showtimeId = request.ShowtimeId,
                        seatId = ss.SeatId,
                        seatCode = ss.Seat?.SeatCode ?? $"#{ss.SeatId}",
                        status = "Reserved",
                        heldByUserId = (string?)null
                    });
                }
            }
            catch (Exception hubEx)
            {
                _logger.LogWarning(hubEx, "[CounterPOS] Không thể phát SignalR sau khi tạo đơn Walk-in #{BookingId}", booking.BookingId);
            }

            return Json(new
            {
                success = true,
                message = $"✅ Tạo đơn Walk-in {booking.BookingCode} thành công! Đang mở trang in vé nhiệt 80mm...",
                bookingId = booking.BookingId,
                bookingCode = booking.BookingCode,
                thermalReceiptCode = thermalCode,
                finalAmount = booking.FinalAmount,
                printUrl = $"/Counter/PrintThermalTicket?bookingId={booking.BookingId}"
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Json(new { success = false, message = "Ghế vừa được người khác thao tác cùng lúc. Vui lòng thử lại." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[CounterPOS] Lỗi tạo đơn Walk-in POS");
            return Json(new { success = false, message = $"Lỗi xử lý đơn Walk-in: {ex.Message}" });
        }
    }

    // ── 5. Trang In Vé Nhiệt Vật Lý Khổ 80mm (Thermal Ticket 80mm) ─────────
    [HttpGet]
    public async Task<IActionResult> PrintThermalTicket(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room).ThenInclude(r => r!.Cinema)
            .Include(b => b.Tickets).ThenInclude(t => t.Seat).ThenInclude(s => s!.SeatType)
            .Include(b => b.Tickets).ThenInclude(t => t.PrintedBy)
            .Include(b => b.BookingConcessions).ThenInclude(bc => bc.FoodItem)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null)
            return NotFound("Không tìm thấy đơn vé cần in.");

        return View(booking);
    }
}
