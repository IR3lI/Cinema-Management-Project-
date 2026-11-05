using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.ViewModels;
using Microsoft.AspNetCore.Identity;
using CineManagement.Services;
using CineManagement.DTOs;

namespace CineManagement.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBookingService _bookingService;
    private readonly IEmailService _emailService;
    private readonly IPaymentService _paymentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BookingController> _logger;
    private const int HoldMinutes = 10;

    public BookingController(
        ApplicationDbContext context,
        IBookingService bookingService,
        IEmailService emailService,
        IPaymentService paymentService,
        UserManager<ApplicationUser> userManager,
        ILogger<BookingController> logger)
    {
        _context = context;
        _bookingService = bookingService;
        _emailService = emailService;
        _paymentService = paymentService;
        _userManager = userManager;
        _logger = logger;
    }

    // ── SCRUM-15: Select Seat ─────────────────────────────────────────────────
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> SelectSeat(int showtimeId)
    {
        var showtime = await _context.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId && s.Status == ShowtimeStatus.Available);

        if (showtime == null || showtime.Room == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var seatMapDto = await _bookingService.GetSeatMapAsync(showtimeId, userId);

        var myPendingBooking = await _context.Bookings
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.ShowtimeId == showtimeId
                                   && b.UserId == userId
                                   && b.Status == BookingStatus.Pending);

        ViewBag.PendingBookingId = myPendingBooking?.BookingId;
        ViewBag.HoldUntil = seatMapDto.MyHoldExpiresAt;

        var seatList = seatMapDto.AllSeats.Select(s => new SelectSeatViewModel.SeatInfo
        {
            ShowtimeSeatId = s.ShowtimeSeatId,
            SeatId = s.SeatId,
            SeatCode = s.SeatCode,
            RowLabel = s.RowLabel,
            SeatNumber = s.SeatNumber,
            GridRow = s.GridRow,
            GridCol = s.GridCol,
            ColSpan = s.ColSpan,
            Price = s.Price,
            ColorCode = s.ColorCode,
            SeatTypeName = s.SeatTypeName,
            SeatType = s.SeatType,
            Status = s.Status switch
            {
                SeatAvailabilityStatus.Sold => SelectSeatViewModel.SeatStatus.Booked,
                SeatAvailabilityStatus.Held when s.IsHeldByCurrentUser => SelectSeatViewModel.SeatStatus.HeldByMe,
                SeatAvailabilityStatus.Held => SelectSeatViewModel.SeatStatus.HeldByOther,
                _ => SelectSeatViewModel.SeatStatus.Available
            }
        }).ToList();

        var seatMap = new Dictionary<string, List<SelectSeatViewModel.SeatInfo>>();
        foreach (var seat in seatList)
        {
            if (!seatMap.ContainsKey(seat.RowLabel))
                seatMap[seat.RowLabel] = new List<SelectSeatViewModel.SeatInfo>();
            seatMap[seat.RowLabel].Add(seat);
        }

        return View(new SelectSeatViewModel
        {
            Showtime = showtime,
            Room = showtime.Room,
            SeatTypes = seatMapDto.SeatTypes.ToList(),
            Seats = seatList,
            SeatMap = seatMap
        });
    }

    // ── SCRUM-17: Hold Seats (POST) ───────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> HoldSeats(int showtimeId, int[] selectedSeatIds)
    {
        if (selectedSeatIds.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn ít nhất một ghế.";
            return RedirectToAction(nameof(SelectSeat), new { showtimeId });
        }

        var showtime = await _context.Showtimes
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

        if (showtime == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var now = DateTime.UtcNow;
        var holdUntil = now.AddMinutes(HoldMinutes);

        // Hủy booking Pending cũ nếu có và giải phóng ghế của booking đó
        var oldBooking = await _context.Bookings
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.ShowtimeId == showtimeId
                                   && b.UserId == userId
                                   && b.Status == BookingStatus.Pending);

        if (oldBooking != null)
        {
            oldBooking.Status = BookingStatus.Cancelled;
            var oldShowtimeSeats = await _context.ShowtimeSeats
                .Where(ss => ss.ShowtimeId == showtimeId && ss.HeldByUserId == userId && ss.Status == ShowtimeSeatStatus.Held)
                .ToListAsync();

            foreach (var ss in oldShowtimeSeats)
            {
                ss.Status = ShowtimeSeatStatus.Available;
                ss.HeldByUserId = null;
                ss.HoldExpiresAt = null;
                ss.Version++;
                ss.UpdatedAt = now;
            }
        }

        // Kiểm tra ghế khả dụng trên ShowtimeSeats
        var stSeats = await _context.ShowtimeSeats
            .Include(ss => ss.Seat)
                .ThenInclude(s => s!.SeatType)
            .Where(ss => ss.ShowtimeId == showtimeId && selectedSeatIds.Contains(ss.SeatId))
            .ToListAsync();

        if (stSeats.Count != selectedSeatIds.Length)
        {
            TempData["Error"] = "Một số ghế không tồn tại. Vui lòng chọn lại.";
            return RedirectToAction(nameof(SelectSeat), new { showtimeId });
        }

        var conflicts = stSeats
            .Where(ss => ss.Status == ShowtimeSeatStatus.Reserved
                      || (ss.Status == ShowtimeSeatStatus.Held && ss.HeldByUserId != userId && ss.HoldExpiresAt > now))
            .ToList();

        if (conflicts.Count > 0)
        {
            TempData["Error"] = "Một số ghế bạn chọn đã có người giữ hoặc đã đặt. Vui lòng chọn lại.";
            return RedirectToAction(nameof(SelectSeat), new { showtimeId });
        }

        // Cập nhật trạng thái ShowtimeSeats sang Held (tăng Version)
        foreach (var ss in stSeats)
        {
            ss.Status = ShowtimeSeatStatus.Held;
            ss.HeldByUserId = userId;
            ss.HoldExpiresAt = holdUntil;
            ss.Version++;
            ss.UpdatedAt = now;
        }

        // Tạo đơn đặt vé Pending
        var booking = new Booking
        {
            UserId = userId,
            ShowtimeId = showtimeId,
            BookingTime = DateTime.Now,
            Status = BookingStatus.Pending,
            PaymentStatus = PaymentStatus.Unpaid,
            BookingCode = GenerateBookingCode()
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Tạo danh sách Tickets gắn với ShowtimeSeatId (Status = Held)
        decimal ticketTotal = 0;
        foreach (var ss in stSeats)
        {
            var seat = ss.Seat!;
            var surcharge = seat.SeatType?.Surcharge ?? 0m;
            var price = showtime.TicketPrice + surcharge;
            ticketTotal += price;

            _context.Tickets.Add(new Ticket
            {
                BookingId = booking.BookingId,
                ShowtimeId = showtimeId,
                SeatId = seat.SeatId,
                ShowtimeSeatId = ss.ShowtimeSeatId,
                TicketCode = "TK" + DateTime.UtcNow.ToString("yyMMdd") + Random.Shared.Next(10000, 99999),
                SeatCode = $"{seat.RowLabel}{seat.SeatNumber}",
                UnitPrice = price,
                Status = TicketStatus.Held,
                CreatedAt = DateTime.Now
            });
        }

        booking.TicketAmount = ticketTotal;
        booking.TotalAmount = ticketTotal;
        booking.FinalAmount = ticketTotal;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã giữ {selectedSeatIds.Length} ghế trong {HoldMinutes} phút.";
        return RedirectToAction(nameof(AddFnB), new { bookingId = booking.BookingId });
    }

    // ── SCRUM-16: Add F&B ─────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> AddFnB(int bookingId)
    {
        var booking = await GetMyBookingAsync(bookingId);
        if (booking == null) return NotFound();

        var foodItems = await _context.FoodItems
            .Where(f => f.IsAvailable)
            .OrderBy(f => f.Category).ThenBy(f => f.Name)
            .ToListAsync();

        var existingFnB = await _context.BookingConcessions
            .Where(bf => bf.BookingId == bookingId)
            .ToListAsync();

        var items = foodItems.Select(f => new FnBItemViewModel
        {
            FoodItemId = f.FoodItemId,
            Name = f.Name,
            Description = f.Description,
            Price = f.Price,
            Category = f.Category,
            ImageUrl = f.ImageUrl,
            Quantity = existingFnB.FirstOrDefault(e => e.FoodItemId == f.FoodItemId)?.Quantity ?? 0
        }).ToList();

        var seatTotal = booking.Tickets.Sum(s => s.UnitPrice);

        return View(new AddFnBViewModel
        {
            BookingId = bookingId,
            ShowtimeId = booking.ShowtimeId,
            Items = items,
            SeatTotal = seatTotal
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFnB(int bookingId, Dictionary<int, int> quantities)
    {
        var booking = await GetMyBookingAsync(bookingId);
        if (booking == null) return NotFound();

        // Xóa các món bắp nước đã chọn trước đó
        var existing = _context.BookingConcessions.Where(bf => bf.BookingId == bookingId);
        _context.BookingConcessions.RemoveRange(existing);

        // Thêm các món mới vào BookingConcessions
        decimal fnbTotal = 0;
        var now = DateTime.UtcNow;
        foreach (var (foodItemId, qty) in quantities.Where(q => q.Value > 0))
        {
            var food = await _context.FoodItems.FindAsync(foodItemId);
            if (food == null || !food.IsAvailable) continue;

            _context.BookingConcessions.Add(new BookingConcession
            {
                BookingId = bookingId,
                FoodItemId = foodItemId,
                Quantity = qty,
                UnitPrice = food.Price,
                ItemName = food.Name,
                CreatedAt = now
            });
            fnbTotal += food.Price * qty;
        }

        // Cập nhật tổng tiền đơn hàng
        var seatTotal = booking.Tickets.Sum(s => s.UnitPrice);
        booking.TicketAmount = seatTotal;
        booking.FnBAmount = fnbTotal;
        booking.TotalAmount = seatTotal + fnbTotal;
        booking.FinalAmount = booking.TotalAmount - booking.DiscountAmount;
        _context.Update(booking);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Checkout), new { bookingId });
    }

    // ── SCRUM-18 + 19: Checkout ───────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Checkout(int bookingId)
    {
        var booking = await GetMyBookingAsync(bookingId);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            return RedirectToAction(nameof(Confirmation), new { bookingId });
        }

        // Đảm bảo có bản ghi Payment Pending với mã giao dịch chuẩn
        var payment = await _paymentService.GetPaymentByBookingIdAsync(bookingId);
        if (payment == null)
        {
            payment = await _paymentService.CreatePaymentAsync(bookingId, booking.FinalAmount, PaymentMethod.BankCard, PaymentProvider.VietQR);
        }
        else if (payment.Amount != booking.FinalAmount)
        {
            payment.Amount = booking.FinalAmount;
            await _context.SaveChangesAsync();
        }

        var seatLines = booking.Tickets
            .Select(t => new CheckoutSeatLine
            {
                SeatCode = !string.IsNullOrEmpty(t.SeatCode) ? t.SeatCode : $"Ghế #{t.SeatId}",
                SeatTypeName = t.Seat?.SeatType?.Name ?? (t.Seat?.SeatTypeLegacy == SeatTypeEnum.Couple ? "Ghế đôi" : (t.Seat?.SeatTypeLegacy == SeatTypeEnum.VIP ? "Ghế VIP" : "Ghế thường")),
                UnitPrice = t.UnitPrice
            })
            .OrderBy(c => c.SeatCode)
            .ToList();

        var seatCodes = seatLines.Select(s => s.SeatCode).ToList();
        var seatTotal = booking.Tickets.Sum(bs => bs.UnitPrice);
        var fnbTotal = booking.BookingConcessions.Sum(bf => bf.UnitPrice * bf.Quantity);

        // Tính thời gian hết hạn giữ chỗ dựa trên ShowtimeSeats
        var ticketSeatIds = booking.Tickets.Select(t => t.SeatId).ToList();
        var holdExpiresAt = await _context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == booking.ShowtimeId && ticketSeatIds.Contains(ss.SeatId) && ss.Status == ShowtimeSeatStatus.Held)
            .MaxAsync(ss => (DateTime?)ss.HoldExpiresAt) ?? booking.BookingTime.AddMinutes(HoldMinutes);

        var txCode = payment.TransactionCode ?? $"PAY-{booking.BookingCode}";
        var qrPayload = _paymentService.GenerateVietQrPayload(booking.FinalAmount, txCode, $"Ve xem phim {booking.BookingCode}");
        var qrImageUrl = _paymentService.GenerateVietQrQuickLink(booking.FinalAmount, txCode);

        return View(new CheckoutViewModel
        {
            BookingId = bookingId,
            Booking = booking,
            SeatCodes = seatCodes,
            SeatLines = seatLines,
            SeatTotal = seatTotal,
            FnBTotal = fnbTotal,
            DiscountAmount = booking.DiscountAmount,
            VoucherCode = booking.Voucher?.Code,
            VoucherApplied = booking.VoucherId.HasValue,
            TransactionCode = txCode,
            VietQrPayload = qrPayload,
            VietQrImageUrl = qrImageUrl,
            HoldExpiresAt = holdExpiresAt
        });
    }

    // ── SCRUM-18: Apply Voucher (AJAX) ────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyVoucher(int bookingId, string code)
    {
        var booking = await GetMyBookingAsync(bookingId);
        if (booking == null) return NotFound();

        var now = DateTime.Now;
        var voucher = await _context.Vouchers
            .FirstOrDefaultAsync(v => v.Code == code.ToUpper().Trim()
                                    && v.IsActive
                                    && v.ValidFrom <= now
                                    && v.ValidTo >= now);

        if (voucher == null)
            return Json(new { success = false, message = "Mã giảm giá không hợp lệ hoặc đã hết hạn." });

        if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit)
            return Json(new { success = false, message = "Mã giảm giá đã được sử dụng hết." });

        var subtotal = booking.TotalAmount;
        var discount = Math.Round(subtotal * voucher.DiscountPercent / 100, 0);
        if (voucher.MaxDiscountAmount.HasValue)
            discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);

        booking.VoucherId = voucher.VoucherId;
        booking.DiscountAmount = discount;
        booking.FinalAmount = subtotal - discount;
        _context.Update(booking);
        await _context.SaveChangesAsync();

        // Cập nhật lại số tiền trên bản ghi Payment
        var payment = await _paymentService.GetPaymentByBookingIdAsync(bookingId);
        if (payment != null)
        {
            payment.Amount = booking.FinalAmount;
            await _context.SaveChangesAsync();
        }

        return Json(new
        {
            success = true,
            message = $"Áp dụng thành công! Giảm {discount:N0} VNĐ",
            discountAmount = discount,
            finalAmount = booking.FinalAmount
        });
    }

    // ── SCRUM-19: Pay / Confirm Payment ───────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int bookingId, string paymentMethod)
    {
        var booking = await GetMyBookingAsync(bookingId);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Đơn đặt vé này không còn hiệu lực.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var now = DateTime.UtcNow;
        var seatIds = booking.Tickets.Select(t => t.SeatId).Distinct().ToList();

        // Kiểm tra giữ chỗ trên ShowtimeSeats còn hiệu lực không
        var invalidSeats = await _context.ShowtimeSeats
            .AnyAsync(ss => ss.ShowtimeId == booking.ShowtimeId
                         && seatIds.Contains(ss.SeatId)
                         && (ss.Status != ShowtimeSeatStatus.Held || ss.HeldByUserId != booking.UserId || ss.HoldExpiresAt <= now));

        if (invalidSeats)
        {
            booking.Status = BookingStatus.Expired;
            _context.Update(booking);
            await _context.SaveChangesAsync();
            TempData["Error"] = "Thời gian giữ ghế đã hết. Vui lòng đặt lại.";
            return RedirectToAction("Index", "Home");
        }

        var method = paymentMethod switch
        {
            "BankCard" => PaymentMethod.BankCard,
            "EWallet" => PaymentMethod.EWallet,
            _ => PaymentMethod.Cash
        };

        var payment = await _paymentService.GetPaymentByBookingIdAsync(bookingId);
        if (payment == null)
        {
            payment = await _paymentService.CreatePaymentAsync(bookingId, booking.FinalAmount, method, PaymentProvider.VietQR);
        }
        else
        {
            payment.Method = method;
            payment.Amount = booking.FinalAmount;
            _context.Update(payment);
            await _context.SaveChangesAsync();
        }

        // Xác nhận thanh toán qua PaymentService (chuyển ShowtimeSeats sang Reserved, Tickets sang Issued, gửi Email)
        var confirmed = await _paymentService.ConfirmPaymentAsync(payment.PaymentId, payment.TransactionCode);
        if (!confirmed)
        {
            TempData["Error"] = "Không thể hoàn tất thanh toán. Vui lòng thử lại.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        if (booking.VoucherId.HasValue)
        {
            var voucher = await _context.Vouchers.FindAsync(booking.VoucherId.Value);
            if (voucher != null)
            {
                voucher.UsedCount++;
                await _context.SaveChangesAsync();
            }
        }

        TempData["Success"] = "Đặt vé & thanh toán thành công! Vé điện tử đã được tự động gửi tới Gmail của bạn.";
        return RedirectToAction(nameof(Confirmation), new { bookingId });
    }

    // ── AJAX: Kiểm tra trạng thái thanh toán VietQR (Polling) ─────────────────
    [HttpGet]
    public async Task<IActionResult> CheckPaymentStatus(int bookingId)
    {
        var booking = await GetMyBookingAsync(bookingId);
        if (booking == null) return Json(new { success = false });

        var isPaid = booking.Status == BookingStatus.Confirmed && booking.PaymentStatus == PaymentStatus.Paid;
        return Json(new { success = true, isPaid = isPaid });
    }

    // ── Confirmation ──────────────────────────────────────────────────────────
    public async Task<IActionResult> Confirmation(int bookingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var booking = await _context.Bookings
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room)
            .Include(b => b.Tickets).ThenInclude(t => t.Seat)
            .Include(b => b.BookingConcessions).ThenInclude(bf => bf.FoodItem)
            .Include(b => b.Voucher)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null) return NotFound();

        var seatCodes = booking.Tickets
            .Select(t => !string.IsNullOrEmpty(t.SeatCode) ? t.SeatCode : (t.Seat != null ? (t.Seat.RowLabel + t.Seat.SeatNumber.ToString()) : $"Ghế #{t.SeatId}"))
            .OrderBy(c => c).ToList();

        return View(new ConfirmationViewModel { Booking = booking, SeatCodes = seatCodes });
    }

    // ── Resend Ticket Email ───────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmail(int bookingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var fullBooking = await _context.Bookings
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room)
            .Include(b => b.Tickets)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && (b.UserId == userId || User.IsInRole("Admin") || User.IsInRole("Staff")));

        if (fullBooking == null)
            return Json(new { success = false, message = "Không tìm thấy đơn đặt vé." });

        try
        {
            await _emailService.SendTicketEmailAsync(bookingId);
            var recipientEmail = fullBooking.ContactEmail ?? fullBooking.User?.Email;
            return Json(new { success = true, message = $"Vé điện tử đã được gửi lại thành công tới Gmail ({recipientEmail})!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi lại email vé {BookingCode}", fullBooking.BookingCode);
            return Json(new { success = false, message = "Không thể gửi email lúc này: " + ex.Message });
        }
    }

    // ── My Bookings ───────────────────────────────────────────────────────────
    public async Task<IActionResult> MyBookings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bookings = await _context.Bookings
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room)
            .Include(b => b.Tickets).ThenInclude(t => t.Seat)
            .Include(b => b.BookingConcessions).ThenInclude(bf => bf.FoodItem)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookingTime)
            .ToListAsync();

        return View(bookings);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<Booking?> GetMyBookingAsync(int bookingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await _context.Bookings
            .Include(b => b.Showtime).ThenInclude(s => s!.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s!.Room)
            .Include(b => b.Tickets).ThenInclude(t => t.Seat)
            .Include(b => b.BookingConcessions).ThenInclude(bf => bf.FoodItem)
            .Include(b => b.Voucher)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);
    }

    private static string GenerateBookingCode()
        => "BK" + DateTime.Now.ToString("yyMMdd") + Random.Shared.Next(1000, 9999).ToString();
}
