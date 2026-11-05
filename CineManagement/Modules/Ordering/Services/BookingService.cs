using System.Data;
using CineManagement.Data;
using CineManagement.DTOs;
using CineManagement.Hubs;
using CineManagement.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CineManagement.Modules.Ordering.Services;

public class BookingService : IBookingService
{
    public const int DefaultHoldMinutes = 7;

    private readonly ApplicationDbContext _db;
    private readonly IHubContext<SeatBookingHub> _hubContext;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        ApplicationDbContext db,
        IHubContext<SeatBookingHub> hubContext,
        ILogger<BookingService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<SeatMapDto> GetSeatMapAsync(
        int showtimeId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var showtime = await _db.Showtimes
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId, cancellationToken);

        if (showtime?.Room == null || showtime.Movie == null)
            throw new InvalidOperationException("Suất chiếu không tồn tại.");

        var now = DateTime.UtcNow;

        // Query all ShowtimeSeats for this showtime with Seat and SeatType
        var showtimeSeats = await _db.ShowtimeSeats
            .AsNoTracking()
            .Include(ss => ss.Seat)
                .ThenInclude(s => s!.SeatType)
            .Where(ss => ss.ShowtimeId == showtimeId && ss.Seat != null && ss.Seat.IsActive)
            .ToListAsync(cancellationToken);

        // Active SeatTypes for UI legend
        var seatTypes = await _db.SeatTypes
            .AsNoTracking()
            .Where(st => st.IsActive)
            .OrderBy(st => st.SeatTypeId)
            .ToListAsync(cancellationToken);

        var legend = seatTypes.Select(st => new SeatTypeLegendDto
        {
            SeatTypeId = st.SeatTypeId,
            Code = st.Code,
            Name = st.Name,
            Surcharge = st.Surcharge,
            ColSpan = st.ColSpan,
            ColorCode = st.ColorCode,
            Description = st.Description
        }).ToList();

        DateTime? myHoldExpires = null;
        if (!string.IsNullOrEmpty(userId))
        {
            var myHolds = showtimeSeats
                .Where(ss => ss.HeldByUserId == userId
                          && ss.Status == ShowtimeSeatStatus.Held
                          && ss.HoldExpiresAt > now)
                .ToList();

            if (myHolds.Count > 0)
                myHoldExpires = myHolds.Max(ss => ss.HoldExpiresAt);
        }

        var allCells = showtimeSeats.Select(ss =>
        {
            var seat = ss.Seat!;
            var st = seat.SeatType;

            // Tự động coi các ghế đang Held nhưng HoldExpiresAt <= UtcNow là Available
            var effectiveStatus = ss.Status;
            if (effectiveStatus == ShowtimeSeatStatus.Held && ss.HoldExpiresAt <= now)
            {
                effectiveStatus = ShowtimeSeatStatus.Available;
            }

            var availabilityStatus = effectiveStatus switch
            {
                ShowtimeSeatStatus.Reserved => SeatAvailabilityStatus.Sold,
                ShowtimeSeatStatus.Held => SeatAvailabilityStatus.Held,
                _ => SeatAvailabilityStatus.Available
            };

            var isHeldByMe = availabilityStatus == SeatAvailabilityStatus.Held
                          && !string.IsNullOrEmpty(userId)
                          && ss.HeldByUserId == userId;

            var surcharge = st?.Surcharge ?? 0m;
            var price = showtime.TicketPrice + surcharge;

            return new SeatCellDto
            {
                ShowtimeSeatId = ss.ShowtimeSeatId,
                SeatId = ss.SeatId,
                SeatCode = $"{seat.RowLabel}{seat.SeatNumber}",
                RowLabel = seat.RowLabel,
                SeatNumber = seat.SeatNumber,
                GridRow = seat.GridRow,
                GridCol = seat.GridCol,
                ColSpan = st?.ColSpan ?? 1,
                ColorCode = st?.ColorCode ?? "#6c757d",
                SeatTypeId = seat.SeatTypeId,
                SeatTypeCode = st?.Code ?? "STANDARD",
                SeatTypeName = st?.Name ?? "Thường",
                SeatType = seat.SeatTypeLegacy,
                Status = availabilityStatus,
                IsHeldByCurrentUser = isHeldByMe,
                Price = price
            };
        })
        .OrderBy(c => c.GridRow)
        .ThenBy(c => c.GridCol)
        .ToList();

        var rows = allCells
            .GroupBy(c => c.RowLabel)
            .OrderBy(g => g.First().GridRow)
            .Select(g => new SeatRowDto
            {
                RowLabel = g.Key,
                GridRow = g.First().GridRow,
                Seats = g.ToList()
            })
            .ToList();

        var totalSeats = allCells.Count;
        var availableSeats = allCells.Count(c => c.Status == SeatAvailabilityStatus.Available);

        return new SeatMapDto
        {
            ShowtimeId = showtime.ShowtimeId,
            MovieId = showtime.MovieId,
            MovieTitle = showtime.Movie.Title,
            RoomId = showtime.RoomId,
            RoomName = showtime.Room.RoomName,
            TotalRows = showtime.Room.TotalRows,
            TotalCols = showtime.Room.TotalCols,
            StartTime = showtime.StartTime,
            BaseTicketPrice = showtime.TicketPrice,
            TotalSeats = totalSeats,
            AvailableSeats = availableSeats,
            MyHoldExpiresAt = myHoldExpires,
            SeatTypes = legend,
            Rows = rows,
            AllSeats = allCells
        };
    }

    public async Task<HoldSeatResponseDto> HoldSeatAsync(
        string userId,
        HoldSeatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var seatIds = request.SeatIds.Distinct().ToList();
        if (seatIds.Count == 0)
        {
            return new HoldSeatResponseDto
            {
                Success = false,
                Message = "Vui lòng chọn ít nhất một ghế."
            };
        }

        var holdMinutes = request.HoldMinutes > 0 ? request.HoldMinutes : DefaultHoldMinutes;
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(holdMinutes);

        using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var showtime = await _db.Showtimes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowtimeId == request.ShowtimeId, cancellationToken);

            if (showtime == null || showtime.Status != ShowtimeStatus.Available)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new HoldSeatResponseDto
                {
                    Success = false,
                    Message = "Suất chiếu không khả dụng."
                };
            }

            // Truy vấn trực tiếp ShowtimeSeats cần giữ (có ConcurrencyCheck Version)
            var targetSeats = await _db.ShowtimeSeats
                .Include(ss => ss.Seat)
                .Where(ss => ss.ShowtimeId == request.ShowtimeId && seatIds.Contains(ss.SeatId))
                .ToListAsync(cancellationToken);

            if (targetSeats.Count != seatIds.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new HoldSeatResponseDto
                {
                    Success = false,
                    Message = "Một số ghế không tồn tại trong suất chiếu này."
                };
            }

            var conflictSeats = new List<string>();
            var conflictSeatIds = new List<int>();

            foreach (var ss in targetSeats)
            {
                // Nếu đã bán
                if (ss.Status == ShowtimeSeatStatus.Reserved)
                {
                    var code = ss.Seat != null ? $"{ss.Seat.RowLabel}{ss.Seat.SeatNumber}" : $"#{ss.SeatId}";
                    conflictSeats.Add(code);
                    conflictSeatIds.Add(ss.SeatId);
                }
                // Nếu đang bị người khác giữ còn hạn
                else if (ss.Status == ShowtimeSeatStatus.Held && ss.HeldByUserId != userId && ss.HoldExpiresAt > now)
                {
                    var code = ss.Seat != null ? $"{ss.Seat.RowLabel}{ss.Seat.SeatNumber}" : $"#{ss.SeatId}";
                    conflictSeats.Add(code);
                    conflictSeatIds.Add(ss.SeatId);
                }
            }

            if (conflictSeats.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new HoldSeatResponseDto
                {
                    Success = false,
                    Message = $"Ghế {string.Join(", ", conflictSeats)} đang được người khác giữ hoặc đã đặt.",
                    ConflictSeatIds = conflictSeatIds
                };
            }

            // Giải phóng các ghế trước đó user đang giữ ở suất này mà không nằm trong request mới
            var previousHolds = await _db.ShowtimeSeats
                .Include(ss => ss.Seat)
                .Where(ss => ss.ShowtimeId == request.ShowtimeId
                          && ss.HeldByUserId == userId
                          && ss.Status == ShowtimeSeatStatus.Held
                          && !seatIds.Contains(ss.SeatId))
                .ToListAsync(cancellationToken);

            foreach (var prev in previousHolds)
            {
                prev.Status = ShowtimeSeatStatus.Available;
                prev.HeldByUserId = null;
                prev.HoldExpiresAt = null;
                prev.Version++;
                prev.UpdatedAt = now;
            }

            // Cập nhật trạng thái giữ ghế mới
            foreach (var ss in targetSeats)
            {
                ss.Status = ShowtimeSeatStatus.Held;
                ss.HeldByUserId = userId;
                ss.HoldExpiresAt = expiresAt;
                ss.Version++;
                ss.UpdatedAt = now;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Broadcast SignalR thời gian thực cho các client đang mở cùng suất chiếu
            try
            {
                var groupName = SeatBookingHub.GetShowtimeGroup(request.ShowtimeId);
                foreach (var prev in previousHolds)
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("SeatStatusChanged", new
                    {
                        showtimeId = request.ShowtimeId,
                        seatId = prev.SeatId,
                        seatCode = prev.Seat?.SeatCode ?? $"#{prev.SeatId}",
                        status = "Available",
                        heldByUserId = (string?)null
                    }, cancellationToken);
                }

                foreach (var ss in targetSeats)
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("SeatStatusChanged", new
                    {
                        showtimeId = request.ShowtimeId,
                        seatId = ss.SeatId,
                        seatCode = ss.Seat?.SeatCode ?? $"#{ss.SeatId}",
                        status = "Held",
                        heldByUserId = userId
                    }, cancellationToken);
                }
            }
            catch (Exception hubEx)
            {
                _logger.LogWarning(hubEx, "Không thể phát SignalR khi giữ ghế suất {ShowtimeId}", request.ShowtimeId);
            }

            return new HoldSeatResponseDto
            {
                Success = true,
                Message = $"Đã giữ {seatIds.Count} ghế trong {holdMinutes} phút.",
                ExpiresAt = expiresAt,
                HeldSeatIds = seatIds
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "DbUpdateConcurrencyException khi giữ ghế suất {ShowtimeId}", request.ShowtimeId);
            return new HoldSeatResponseDto
            {
                Success = false,
                Message = "Ghế vừa được người khác giữ. Vui lòng chọn ghế khác.",
                ConflictSeatIds = seatIds
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Lỗi giữ ghế suất {ShowtimeId}", request.ShowtimeId);
            throw;
        }
    }

    public async Task<bool> ReleaseSeatAsync(
        string userId,
        int showtimeId,
        List<int>? seatIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ShowtimeSeats
            .Include(ss => ss.Seat)
            .Where(ss => ss.ShowtimeId == showtimeId
                      && ss.HeldByUserId == userId
                      && ss.Status == ShowtimeSeatStatus.Held);

        if (seatIds != null && seatIds.Count > 0)
        {
            query = query.Where(ss => seatIds.Contains(ss.SeatId));
        }

        var seats = await query.ToListAsync(cancellationToken);
        if (seats.Count == 0) return true;

        var now = DateTime.UtcNow;
        foreach (var ss in seats)
        {
            ss.Status = ShowtimeSeatStatus.Available;
            ss.HeldByUserId = null;
            ss.HoldExpiresAt = null;
            ss.Version++;
            ss.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var groupName = SeatBookingHub.GetShowtimeGroup(showtimeId);
            foreach (var ss in seats)
            {
                await _hubContext.Clients.Group(groupName).SendAsync("SeatStatusChanged", new
                {
                    showtimeId,
                    seatId = ss.SeatId,
                    seatCode = ss.Seat?.SeatCode ?? $"#{ss.SeatId}",
                    status = "Available",
                    heldByUserId = (string?)null
                }, cancellationToken);
            }
        }
        catch (Exception hubEx)
        {
            _logger.LogWarning(hubEx, "Không thể phát SignalR khi nhả ghế suất {ShowtimeId}", showtimeId);
        }

        return true;
    }

    public async Task<BookingResponseDto> CalculateTotalAmountAsync(
        BookingRequestDto request,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var quote = await BuildQuoteAsync(request, userId, persistVoucherCheck: false, cancellationToken);
        return MapQuoteToResponse(quote, booking: null, holdExpiresAt: null);
    }

    public async Task<BookingResponseDto> CreateBookingAsync(
        string userId,
        BookingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var seatIds = request.SeatIds.Distinct().ToList();
        if (seatIds.Count == 0)
            throw new InvalidOperationException("Vui lòng chọn ít nhất một ghế.");

        using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var now = DateTime.UtcNow;

            // Kiểm tra các ghế phải đang được user giữ hợp lệ trên ShowtimeSeats
            var showtimeSeats = await _db.ShowtimeSeats
                .Include(ss => ss.Seat)
                    .ThenInclude(s => s!.SeatType)
                .Where(ss => ss.ShowtimeId == request.ShowtimeId && seatIds.Contains(ss.SeatId))
                .ToListAsync(cancellationToken);

            if (showtimeSeats.Count != seatIds.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("Một hoặc nhiều ghế không hợp lệ.");
            }

            var invalidSeats = showtimeSeats
                .Where(ss => ss.Status != ShowtimeSeatStatus.Held
                          || ss.HeldByUserId != userId
                          || ss.HoldExpiresAt <= now)
                .ToList();

            if (invalidSeats.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("Ghế chưa được giữ hoặc thời gian giữ chỗ đã hết. Vui lòng chọn lại ghế.");
            }

            var quote = await BuildQuoteAsync(request, userId, persistVoucherCheck: true, cancellationToken);

            var booking = new Booking
            {
                UserId = userId,
                ShowtimeId = request.ShowtimeId,
                VoucherId = quote.Voucher?.VoucherId,
                BookingCode = await GenerateUniqueBookingCodeAsync(cancellationToken),
                BookingTime = now,
                TicketAmount = quote.TicketAmount,
                FnBAmount = quote.FnBAmount,
                TotalAmount = quote.TotalAmount,
                DiscountAmount = quote.DiscountAmount,
                FinalAmount = quote.FinalAmount,
                Status = BookingStatus.Pending,
                PaymentStatus = PaymentStatus.Unpaid,
                PaymentMethod = request.PaymentMethod,
                PaymentReference = $"PAY-{Guid.NewGuid():N}"[..20],
                PaidAt = null,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                CreatedAt = now
            };

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync(cancellationToken);

            // 1. Tạo Tickets với ShowtimeSeatId (Status = Held khi chưa thanh toán)
            var seatLookup = showtimeSeats.ToDictionary(ss => ss.SeatId);
            foreach (var line in quote.TicketLines)
            {
                var ss = seatLookup[line.SeatId];
                var ticketCode = GenerateTicketCode();
                _db.Tickets.Add(new Ticket
                {
                    BookingId = booking.BookingId,
                    ShowtimeId = request.ShowtimeId,
                    SeatId = line.SeatId,
                    ShowtimeSeatId = ss.ShowtimeSeatId,
                    TicketCode = ticketCode,
                    SeatCode = line.SeatCode,
                    UnitPrice = line.UnitPrice,
                    Status = TicketStatus.Held,
                    QrPayload = ticketCode,
                    CreatedAt = now
                });
            }

            // 2. Tạo BookingConcessions
            foreach (var line in quote.FnBLines)
            {
                _db.BookingConcessions.Add(new BookingConcession
                {
                    BookingId = booking.BookingId,
                    FoodItemId = line.FoodItemId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    ItemName = line.ItemName,
                    CreatedAt = now
                });
            }

            // 3. Gia hạn trạng thái Held trên ShowtimeSeats trong 10 phút chờ thanh toán (tăng Version)
            var holdExpiresAt = now.AddMinutes(10);
            foreach (var ss in showtimeSeats)
            {
                ss.Status = ShowtimeSeatStatus.Held;
                ss.HeldByUserId = userId;
                ss.HoldExpiresAt = holdExpiresAt;
                ss.Version++;
                ss.UpdatedAt = now;
            }

            // 4. Tạo bản ghi Payment chuẩn ở trạng thái Unpaid (chờ thanh toán VietQR)
            _db.Payments.Add(new Payment
            {
                BookingId = booking.BookingId,
                Amount = booking.FinalAmount,
                Method = request.PaymentMethod,
                Provider = PaymentProvider.VietQR,
                TransactionCode = booking.PaymentReference,
                Status = PaymentStatus.Unpaid,
                PaidAt = null,
                CreatedAt = now
            });

            // 5. Cập nhật lượt dùng Voucher nếu có
            if (quote.Voucher != null)
            {
                var voucher = await _db.Vouchers
                    .FirstAsync(v => v.VoucherId == quote.Voucher.VoucherId, cancellationToken);

                if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new InvalidOperationException("Mã giảm giá vừa hết lượt sử dụng.");
                }

                voucher.UsedCount++;
                voucher.UpdatedAt = now;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            booking.Tickets = await _db.Tickets
                .Where(t => t.BookingId == booking.BookingId)
                .ToListAsync(cancellationToken);
            booking.BookingConcessions = await _db.BookingConcessions
                .Where(o => o.BookingId == booking.BookingId)
                .ToListAsync(cancellationToken);
            booking.Voucher = quote.Voucher;

            return MapQuoteToResponse(quote, booking, holdExpiresAt: holdExpiresAt);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "DbUpdateConcurrencyException khi tạo booking suất {ShowtimeId}", request.ShowtimeId);
            throw new InvalidOperationException("Ghế vừa được người khác đặt hoặc đã thay đổi trạng thái. Vui lòng chọn lại.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Quote> BuildQuoteAsync(
        BookingRequestDto request,
        string? userId,
        bool persistVoucherCheck,
        CancellationToken cancellationToken)
    {
        var seatIds = request.SeatIds.Distinct().ToList();

        var showtime = await _db.Showtimes
            .AsNoTracking()
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.ShowtimeId == request.ShowtimeId, cancellationToken);

        if (showtime?.Room == null)
            throw new InvalidOperationException("Suất chiếu không tồn tại.");

        var showtimeSeats = await _db.ShowtimeSeats
            .AsNoTracking()
            .Include(ss => ss.Seat)
                .ThenInclude(s => s!.SeatType)
            .Where(ss => ss.ShowtimeId == request.ShowtimeId && seatIds.Contains(ss.SeatId))
            .ToListAsync(cancellationToken);

        if (showtimeSeats.Count != seatIds.Count)
            throw new InvalidOperationException("Một hoặc nhiều ghế không hợp lệ.");

        var ticketLines = showtimeSeats
            .OrderBy(ss => ss.Seat!.RowLabel)
            .ThenBy(ss => ss.Seat!.SeatNumber)
            .Select(ss =>
            {
                var seat = ss.Seat!;
                var surcharge = seat.SeatType?.Surcharge ?? 0m;
                var unitPrice = showtime.TicketPrice + surcharge;

                return new BookingTicketLineDto
                {
                    SeatId = ss.SeatId,
                    SeatCode = $"{seat.RowLabel}{seat.SeatNumber}",
                    UnitPrice = unitPrice,
                    Status = TicketStatus.Held
                };
            })
            .ToList();

        var ticketAmount = ticketLines.Sum(t => t.UnitPrice);

        var fnbLines = new List<BookingFnBLineDto>();
        foreach (var item in request.FnBItems.Where(x => x.Quantity > 0))
        {
            var food = await _db.FoodItems
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FoodItemId == item.FoodItemId && f.IsAvailable, cancellationToken);

            if (food == null)
                throw new InvalidOperationException($"Món F&B #{item.FoodItemId} không còn phục vụ.");

            fnbLines.Add(new BookingFnBLineDto
            {
                FoodItemId = food.FoodItemId,
                ItemName = food.Name,
                Quantity = item.Quantity,
                UnitPrice = food.Price,
                SubTotal = food.Price * item.Quantity
            });
        }

        var fnbAmount = fnbLines.Sum(x => x.SubTotal);
        var total = ticketAmount + fnbAmount;

        var voucherResult = await TryApplyVoucherAsync(
            request.VoucherCode,
            total,
            userId,
            persistVoucherCheck,
            cancellationToken);

        var discount = voucherResult.Discount;
        var finalAmount = Math.Max(0, total - discount);

        return new Quote
        {
            ShowtimeId = request.ShowtimeId,
            PaymentMethod = request.PaymentMethod,
            TicketAmount = ticketAmount,
            FnBAmount = fnbAmount,
            TotalAmount = total,
            DiscountAmount = discount,
            FinalAmount = finalAmount,
            Voucher = voucherResult.Voucher,
            VoucherCode = voucherResult.Code,
            VoucherMessage = voucherResult.Message,
            VoucherApplied = voucherResult.Applied,
            TicketLines = ticketLines,
            FnBLines = fnbLines
        };
    }

    private async Task<(Voucher? Voucher, string? Code, string? Message, bool Applied, decimal Discount)> TryApplyVoucherAsync(
        string? code,
        decimal subtotal,
        string? userId,
        bool strictUsage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (null, null, null, false, 0);

        var normalized = code.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        var voucher = await _db.Vouchers
            .FirstOrDefaultAsync(v => v.Code == normalized, cancellationToken);

        if (voucher == null || !voucher.IsActive)
            return (null, normalized, "Mã giảm giá không hợp lệ hoặc đã hết hạn.", false, 0);

        if (voucher.ValidFrom > now || voucher.ValidTo.Date < now.Date)
            return (null, normalized, "Mã giảm giá không hợp lệ hoặc đã hết hạn.", false, 0);

        if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value)
            return (null, normalized, "Mã giảm giá đã được sử dụng hết.", false, 0);

        if (voucher.MinOrderAmount > 0 && subtotal < voucher.MinOrderAmount)
        {
            return (null, normalized,
                $"Đơn tối thiểu {voucher.MinOrderAmount:N0} VNĐ để dùng mã này.",
                false, 0);
        }

        if (!string.IsNullOrEmpty(userId) && voucher.PerUserLimit > 0)
        {
            var usedByUser = await _db.Bookings.CountAsync(
                b => b.UserId == userId
                  && b.VoucherId == voucher.VoucherId
                  && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending),
                cancellationToken);

            if (usedByUser >= voucher.PerUserLimit)
                return (null, normalized, "Bạn đã dùng hết lượt cho mã này.", false, 0);
        }

        decimal discount;
        if (voucher.DiscountType == DiscountType.FixedAmount)
            discount = voucher.DiscountAmount;
        else
            discount = Math.Round(subtotal * voucher.DiscountPercent / 100, 0, MidpointRounding.AwayFromZero);

        if (voucher.MaxDiscountAmount.HasValue)
            discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);

        discount = Math.Min(discount, subtotal);

        _ = strictUsage;
        return (voucher, voucher.Code, $"Áp dụng thành công! Giảm {discount:N0} VNĐ", true, discount);
    }

    private static BookingResponseDto MapQuoteToResponse(Quote quote, Booking? booking, DateTime? holdExpiresAt)
    {
        var tickets = booking?.Tickets.Count > 0
            ? booking.Tickets.Select(t => new BookingTicketLineDto
            {
                TicketId = t.TicketId,
                TicketCode = t.TicketCode,
                SeatId = t.SeatId,
                SeatCode = t.SeatCode,
                UnitPrice = t.UnitPrice,
                Status = t.Status
            }).ToList()
            : quote.TicketLines;

        var fnb = booking?.BookingConcessions.Count > 0
            ? booking.BookingConcessions.Select(o => new BookingFnBLineDto
            {
                FoodItemId = o.FoodItemId,
                ItemName = o.ItemName ?? string.Empty,
                Quantity = o.Quantity,
                UnitPrice = o.UnitPrice,
                SubTotal = o.UnitPrice * o.Quantity
            }).ToList()
            : quote.FnBLines;

        return new BookingResponseDto
        {
            BookingId = booking?.BookingId ?? 0,
            BookingCode = booking?.BookingCode ?? string.Empty,
            ShowtimeId = quote.ShowtimeId,
            Status = booking?.Status ?? BookingStatus.Pending,
            PaymentStatus = booking?.PaymentStatus ?? PaymentStatus.Unpaid,
            PaymentMethod = booking?.PaymentMethod ?? quote.PaymentMethod,
            TicketAmount = quote.TicketAmount,
            FnBAmount = quote.FnBAmount,
            TotalAmount = quote.TotalAmount,
            DiscountAmount = quote.DiscountAmount,
            FinalAmount = quote.FinalAmount,
            VoucherCode = quote.VoucherCode,
            VoucherMessage = quote.VoucherMessage,
            VoucherApplied = quote.VoucherApplied,
            HoldExpiresAt = holdExpiresAt,
            PaidAt = booking?.PaidAt,
            Tickets = tickets,
            FnBItems = fnb
        };
    }

    private async Task<string> GenerateUniqueBookingCodeAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 8; i++)
        {
            var code = "BK" + DateTime.UtcNow.ToString("yyMMdd") + Random.Shared.Next(1000, 9999);
            var exists = await _db.Bookings.AnyAsync(b => b.BookingCode == code, cancellationToken);
            if (!exists)
                return code;
        }

        return "BK" + DateTime.UtcNow.ToString("yyMMddHHmmss") + Random.Shared.Next(10, 99);
    }

    private static string GenerateTicketCode()
        => "TCK" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private sealed class Quote
    {
        public int ShowtimeId { get; init; }
        public PaymentMethod PaymentMethod { get; init; }
        public decimal TicketAmount { get; init; }
        public decimal FnBAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal FinalAmount { get; init; }
        public Voucher? Voucher { get; init; }
        public string? VoucherCode { get; init; }
        public string? VoucherMessage { get; init; }
        public bool VoucherApplied { get; init; }
        public List<BookingTicketLineDto> TicketLines { get; init; } = [];
        public List<BookingFnBLineDto> FnBLines { get; init; } = [];
    }
}


