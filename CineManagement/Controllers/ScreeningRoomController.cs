using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CineManagement.Data;
using CineManagement.Models;
using CineManagement.ViewModels;

namespace CineManagement.Controllers;

[Authorize(Roles = "Admin")]
public class ScreeningRoomController : Controller
{
    private readonly ApplicationDbContext _context;

    public ScreeningRoomController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Index ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var rooms = await _context.ScreeningRooms
            .Include(r => r.Cinema)
            .Include(r => r.Seats)
            .Include(r => r.Showtimes)
            .OrderBy(r => r.RoomName)
            .ToListAsync();
        return View(rooms);
    }

    // ── Details (2D Matrix Grid Seat Map) ──────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var room = await _context.ScreeningRooms
            .Include(r => r.Cinema)
            .Include(r => r.Seats.OrderBy(s => s.GridRow).ThenBy(s => s.GridCol))
                .ThenInclude(s => s.SeatType)
            .FirstOrDefaultAsync(r => r.RoomId == id);

        if (room == null) return NotFound();

        ViewBag.SeatTypes = await _context.SeatTypes.OrderBy(st => st.SeatTypeId).ToListAsync();
        ViewBag.FutureShowtimesCount = await _context.Showtimes
            .CountAsync(s => s.RoomId == id && s.StartTime >= DateTime.UtcNow);

        return View(room);
    }

    // ── Visual Seat Layout Builder Studio ──────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> LayoutBuilder(int id)
    {
        var room = await _context.ScreeningRooms
            .Include(r => r.Cinema)
            .Include(r => r.Seats.OrderBy(s => s.GridRow).ThenBy(s => s.GridCol))
                .ThenInclude(s => s.SeatType)
            .FirstOrDefaultAsync(r => r.RoomId == id);

        if (room == null) return NotFound();

        ViewBag.SeatTypes = await _context.SeatTypes.OrderBy(st => st.SeatTypeId).ToListAsync();
        ViewBag.FutureShowtimesCount = await _context.Showtimes
            .CountAsync(s => s.RoomId == id && s.StartTime >= DateTime.UtcNow);

        return View(room);
    }

    // ── DTOs cho Layout Builder ───────────────────────────────────────────
    public class SaveSeatLayoutRequest
    {
        public int RoomId { get; set; }
        public int TotalRows { get; set; }
        public int TotalCols { get; set; }
        public string? AisleRows { get; set; }
        public string? AisleCols { get; set; }
        public List<SeatLayoutItemDto> Seats { get; set; } = new();
    }

    public class SeatLayoutItemDto
    {
        public int GridRow { get; set; }
        public int GridCol { get; set; }
        public int ColSpan { get; set; } = 1;
        public int SeatTypeId { get; set; }
        public string RowLabel { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ── API Lưu Sơ đồ Phòng Chiếu Trực quan ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSeatLayout([FromBody] SaveSeatLayoutRequest request)
    {
        if (request == null || request.RoomId <= 0)
            return Json(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });

        if (request.TotalRows < 3 || request.TotalRows > 30 || request.TotalCols < 4 || request.TotalCols > 40)
            return Json(new { success = false, message = "Kích thước ma trận không hợp lệ (Hàng: 3-30, Cột: 4-40)." });

        var room = await _context.ScreeningRooms
            .Include(r => r.Seats)
            .FirstOrDefaultAsync(r => r.RoomId == request.RoomId);

        if (room == null)
            return Json(new { success = false, message = "Không tìm thấy phòng chiếu." });

        var seatTypesMap = await _context.SeatTypes.ToDictionaryAsync(st => st.SeatTypeId);
        var occupiedCoords = new HashSet<(int Row, int Col)>();

        // Phân tích danh sách hành lang ngang và dọc
        var aisleRowSet = (request.AisleRows ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToHashSet();

        var aisleColSet = (request.AisleCols ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToHashSet();

        foreach (var s in request.Seats)
        {
            if (s.GridRow < 1 || s.GridRow > request.TotalRows)
                return Json(new { success = false, message = $"Ghế {s.RowLabel}{s.SeatNumber} vượt quá giới hạn hàng (1-{request.TotalRows})." });

            if (s.GridCol < 1 || s.GridCol + s.ColSpan - 1 > request.TotalCols)
                return Json(new { success = false, message = $"Ghế {s.RowLabel}{s.SeatNumber} vượt quá giới hạn cột (1-{request.TotalCols})." });

            // 1. Chặn ghế đặt trên hàng hành lang ngang
            if (aisleRowSet.Contains(s.GridRow))
            {
                return Json(new
                {
                    success = false,
                    errorCode = "AISLE_ROW_COLLISION",
                    message = $"Ghế {s.RowLabel}{s.SeatNumber} không thể đặt trên hàng hành lang ngang {s.GridRow}!"
                });
            }

            if (!seatTypesMap.TryGetValue(s.SeatTypeId, out var seatType))
                return Json(new { success = false, message = $"Loại ghế ID {s.SeatTypeId} không tồn tại trong hệ thống." });

            for (int c = s.GridCol; c < s.GridCol + s.ColSpan; c++)
            {
                // 2. Thuật toán Aisle Guard: Chặn ghế (đặc biệt ghế đôi ColSpan=2) đè lên hành lang dọc
                if (aisleColSet.Contains(c))
                {
                    var seatDesc = s.ColSpan > 1 ? "Ghế đôi (Sweetbox)" : "Ghế";
                    return Json(new
                    {
                        success = false,
                        errorCode = "AISLE_GUARD_VIOLATION",
                        message = $"Vi phạm quy tắc an toàn hành lang: {seatDesc} {s.RowLabel}{s.SeatNumber} tại Hàng {s.GridRow} đè lên cột hành lang dọc tại cột {c}!",
                        details = new { gridRow = s.GridRow, gridCol = s.GridCol, colSpan = s.ColSpan, conflictAisleCol = c }
                    });
                }

                if (occupiedCoords.Contains((s.GridRow, c)))
                {
                    return Json(new { success = false, message = $"Phát hiện trùng lặp tọa độ tại Hàng {s.GridRow}, Cột {c}!" });
                }
                occupiedCoords.Add((s.GridRow, c));
            }
        }

        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            room.TotalRows = request.TotalRows;
            room.TotalCols = request.TotalCols;
            room.AisleRows = request.AisleRows;
            room.AisleCols = request.AisleCols;
            _context.ScreeningRooms.Update(room);

            var oldSeats = room.Seats.ToList();
            var oldSeatIds = oldSeats.Select(s => s.SeatId).ToList();

            var seatsWithTickets = await _context.Tickets
                .Where(t => oldSeatIds.Contains(t.SeatId))
                .Select(t => t.SeatId)
                .Distinct()
                .ToListAsync();

            var seatsWithTicketsSet = new HashSet<int>(seatsWithTickets);

            var futureShowtimes = await _context.Showtimes
                .Where(s => s.RoomId == room.RoomId && s.StartTime >= DateTime.UtcNow)
                .Select(s => s.ShowtimeId)
                .ToListAsync();

            var existingSeatMap = oldSeats.ToDictionary(s => (s.GridRow, s.GridCol));
            var newSeatList = new List<Seat>();

            foreach (var s in request.Seats)
            {
                var seatType = seatTypesMap[s.SeatTypeId];
                var legacyType = seatType.Code switch
                {
                    "VIP" => SeatTypeEnum.VIP,
                    "COUPLE" => SeatTypeEnum.Couple,
                    _ => SeatTypeEnum.Standard
                };

                if (existingSeatMap.TryGetValue((s.GridRow, s.GridCol), out var existing))
                {
                    existing.RowLabel = s.RowLabel;
                    existing.SeatNumber = s.SeatNumber;
                    existing.SeatTypeId = s.SeatTypeId;
                    existing.SeatTypeLegacy = legacyType;
                    existing.IsActive = s.IsActive;
                    _context.Seats.Update(existing);
                    newSeatList.Add(existing);
                    existingSeatMap.Remove((s.GridRow, s.GridCol));
                }
                else
                {
                    var newSeat = new Seat
                    {
                        RoomId = room.RoomId,
                        RowLabel = s.RowLabel,
                        SeatNumber = s.SeatNumber,
                        GridRow = s.GridRow,
                        GridCol = s.GridCol,
                        SeatTypeId = s.SeatTypeId,
                        SeatTypeLegacy = legacyType,
                        IsActive = s.IsActive
                    };
                    _context.Seats.Add(newSeat);
                    newSeatList.Add(newSeat);
                }
            }

            foreach (var remaining in existingSeatMap.Values)
            {
                if (seatsWithTicketsSet.Contains(remaining.SeatId))
                {
                    remaining.IsActive = false;
                    _context.Seats.Update(remaining);
                }
                else
                {
                    var unusedShowtimeSeats = await _context.ShowtimeSeats
                        .Where(ss => ss.SeatId == remaining.SeatId && ss.Status == ShowtimeSeatStatus.Available)
                        .ToListAsync();
                    _context.ShowtimeSeats.RemoveRange(unusedShowtimeSeats);
                    _context.Seats.Remove(remaining);
                }
            }

            await _context.SaveChangesAsync();

            if (futureShowtimes.Any())
            {
                var allActiveSeats = await _context.Seats
                    .Where(s => s.RoomId == room.RoomId && s.IsActive)
                    .ToListAsync();

                foreach (var showtimeId in futureShowtimes)
                {
                    var existingShowtimeSeats = await _context.ShowtimeSeats
                        .Where(ss => ss.ShowtimeId == showtimeId)
                        .Select(ss => ss.SeatId)
                        .ToListAsync();

                    var existingSet = new HashSet<int>(existingShowtimeSeats);
                    var missingSeats = allActiveSeats.Where(s => !existingSet.Contains(s.SeatId)).ToList();

                    foreach (var ms in missingSeats)
                    {
                        _context.ShowtimeSeats.Add(new ShowtimeSeat
                        {
                            ShowtimeId = showtimeId,
                            SeatId = ms.SeatId,
                            Status = ShowtimeSeatStatus.Available,
                            Version = 1
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return Json(new
            {
                success = true,
                message = $"✅ Lưu sơ đồ phòng \"{room.RoomName}\" thành công với {request.Seats.Count} ghế!",
                totalSeats = request.Seats.Count,
                capacity = room.Capacity
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Json(new { success = false, message = $"Lỗi khi lưu sơ đồ: {ex.Message}" });
        }
    }

    // ── Toggle Seat Maintenance (AJAX) ────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSeatMaintenance(int seatId)
    {
        var seat = await _context.Seats.Include(s => s.SeatType).FirstOrDefaultAsync(s => s.SeatId == seatId);
        if (seat == null)
            return Json(new { success = false, message = "Không tìm thấy thông tin ghế." });

        seat.IsActive = !seat.IsActive;
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            seatId = seat.SeatId,
            isActive = seat.IsActive,
            seatCode = seat.SeatCode,
            message = seat.IsActive 
                ? $"Ghế {seat.SeatCode} đã được kích hoạt lại sẵn sàng bán vé." 
                : $"Ghế {seat.SeatCode} đã chuyển sang chế độ BẢO TRÌ (tạm ngừng bán vé)."
        });
    }

    // ── Seat Types Configuration ──────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> SeatTypes()
    {
        var types = await _context.SeatTypes
            .Include(st => st.Seats)
            .OrderBy(st => st.SeatTypeId)
            .ToListAsync();
        return View(types);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSeatType(int seatTypeId, string name, decimal surcharge, string colorCode, string? description)
    {
        var type = await _context.SeatTypes.FindAsync(seatTypeId);
        if (type == null) return NotFound();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Tên loại ghế không được để trống.";
            return RedirectToAction(nameof(SeatTypes));
        }

        type.Name = name.Trim();
        type.Surcharge = surcharge >= 0 ? surcharge : type.Surcharge;
        type.ColorCode = !string.IsNullOrWhiteSpace(colorCode) ? colorCode.Trim() : type.ColorCode;
        type.Description = description?.Trim();

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Cập nhật loại ghế \"{type.Name}\" thành công!";
        return RedirectToAction(nameof(SeatTypes));
    }

    // ── Create ────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create() => View(new ScreeningRoomFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScreeningRoomFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var room = new ScreeningRoom
        {
            RoomName    = model.RoomName,
            RoomType    = model.RoomType,
            TotalRows   = model.Rows > 0 ? model.Rows : 10,
            TotalCols   = model.SeatsPerRow > 0 ? model.SeatsPerRow : 16,
            Description = model.Description,
            IsActive    = model.IsActive
        };
        _context.ScreeningRooms.Add(room);
        await _context.SaveChangesAsync();

        // Auto-generate seats with 2D grid
        await GenerateSeatsAsync(room);

        TempData["Success"] = $"Tạo phòng \"{room.RoomName}\" thành công với {room.Capacity} ghế!";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var room = await _context.ScreeningRooms.FindAsync(id);
        if (room == null) return NotFound();

        return View(new ScreeningRoomFormViewModel
        {
            RoomId      = room.RoomId,
            RoomName    = room.RoomName,
            RoomType    = room.RoomType,
            Rows        = room.TotalRows,
            SeatsPerRow = room.TotalCols,
            Description = room.Description,
            IsActive    = room.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScreeningRoomFormViewModel model)
    {
        if (id != model.RoomId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var room = await _context.ScreeningRooms.FindAsync(id);
        if (room == null) return NotFound();

        bool layoutChanged = room.TotalRows != model.Rows || room.TotalCols != model.SeatsPerRow;

        room.RoomName    = model.RoomName;
        room.RoomType    = model.RoomType;
        room.TotalRows   = model.Rows;
        room.TotalCols   = model.SeatsPerRow;
        room.Description = model.Description;
        room.IsActive    = model.IsActive;

        _context.Update(room);
        await _context.SaveChangesAsync();

        // Regenerate seats if layout changed
        if (layoutChanged)
        {
            var oldSeats = _context.Seats.Where(s => s.RoomId == id);
            _context.Seats.RemoveRange(oldSeats);
            await _context.SaveChangesAsync();
            await GenerateSeatsAsync(room);
            TempData["Success"] = $"Cập nhật phòng \"{room.RoomName}\" thành công. Sơ đồ 2D lưới đã được tạo lại!";
        }
        else
        {
            TempData["Success"] = $"Cập nhật phòng \"{room.RoomName}\" thành công!";
        }

        return RedirectToAction(nameof(Index));
    }

    // ── Delete (soft) ─────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await _context.ScreeningRooms.FindAsync(id);
        if (room == null) return NotFound();

        room.IsActive = false;
        _context.Update(room);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã vô hiệu hóa phòng \"{room.RoomName}\"!";
        return RedirectToAction(nameof(Index));
    }

    // ── Helper: Generate seats with 2D Grid aisles & types (Non-overflowing) ──
    private async Task GenerateSeatsAsync(ScreeningRoom room)
    {
        var standardType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Code == "STANDARD");
        var vipType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Code == "VIP");
        var coupleType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Code == "COUPLE");

        int totalRows = room.TotalRows > 0 ? room.TotalRows : 10;
        int totalCols = room.TotalCols > 0 ? room.TotalCols : 16;

        room.TotalRows = totalRows;
        room.TotalCols = totalCols;

        var seats = new List<Seat>();
        var horizontalAisles = new HashSet<int>();
        var verticalAisles = new HashSet<int>();

        // Cấu hình đa hành lang mẫu theo kích thước phòng
        if (totalRows >= 6) horizontalAisles.Add(4);
        if (totalCols >= 10) verticalAisles.Add(4);
        if (totalCols >= 14) verticalAisles.Add(totalCols - 3);

        // Phân tách các block cột độc lập ngăn cách bởi hành lang dọc
        var colBlocks = GetColumnBlocks(totalCols, verticalAisles);

        int rowCharIndex = 0;
        for (int r = 1; r <= totalRows; r++)
        {
            if (horizontalAisles.Contains(r)) continue; // Bỏ qua hàng hành lang ngang

            char rowLetter = (char)('A' + (rowCharIndex++));
            if (rowLetter == 'I') rowLetter = 'J';
            if (rowLetter == 'O') rowLetter = 'P';

            if (r == totalRows && coupleType != null)
            {
                // Hàng cuối: Ghế Couple (ColSpan = 2) xếp gọn trong từng block, TUYỆT ĐỐI không lấn hành lang
                int coupleSeatNum = 1;
                foreach (var block in colBlocks)
                {
                    for (int c = block.StartCol; c + 1 <= block.EndCol; c += 2)
                    {
                        seats.Add(new Seat
                        {
                            RoomId = room.RoomId,
                            RowLabel = rowLetter.ToString(),
                            SeatNumber = coupleSeatNum++,
                            GridRow = r,
                            GridCol = c,
                            SeatTypeId = coupleType.SeatTypeId,
                            SeatTypeLegacy = SeatTypeEnum.Couple,
                            IsActive = true
                        });
                    }
                }
            }
            else
            {
                // Ghế Standard / VIP
                bool isVip = r > (totalRows / 2);
                var currentType = isVip ? vipType : standardType;
                var currentTypeEnum = isVip ? SeatTypeEnum.VIP : SeatTypeEnum.Standard;
                int seatNum = 1;

                foreach (var block in colBlocks)
                {
                    for (int c = block.StartCol; c <= block.EndCol; c++)
                    {
                        seats.Add(new Seat
                        {
                            RoomId = room.RoomId,
                            RowLabel = rowLetter.ToString(),
                            SeatNumber = seatNum++,
                            GridRow = r,
                            GridCol = c,
                            SeatTypeId = currentType?.SeatTypeId,
                            SeatTypeLegacy = currentTypeEnum,
                            IsActive = true
                        });
                    }
                }
            }
        }

        _context.Seats.AddRange(seats);
        await _context.SaveChangesAsync();
    }

    public static List<(int StartCol, int EndCol)> GetColumnBlocks(int totalCols, HashSet<int> verticalAisles)
    {
        var blocks = new List<(int StartCol, int EndCol)>();
        int? blockStart = null;

        for (int c = 1; c <= totalCols; c++)
        {
            bool isAisle = verticalAisles.Contains(c);
            if (!isAisle)
            {
                if (!blockStart.HasValue) blockStart = c;
            }
            else
            {
                if (blockStart.HasValue)
                {
                    blocks.Add((blockStart.Value, c - 1));
                    blockStart = null;
                }
            }
        }

        if (blockStart.HasValue)
        {
            blocks.Add((blockStart.Value, totalCols));
        }

        return blocks;
    }
}