using CineManagement.DTOs;

namespace CineManagement.Modules.Ordering.Services;

/// <summary>
/// Core booking flow: sơ đồ ghế 2D → giữ ghế (ShowtimeSeats) → tính tiền → tạo đơn & thanh toán.
/// </summary>
public interface IBookingService
{
    /// <summary>Sơ đồ ghế của suất chiếu kèm ma trận lưới 2D, chú thích loại ghế và trạng thái Trống / Đang giữ / Đã bán.</summary>
    Task<SeatMapDto> GetSeatMapAsync(
        int showtimeId,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Giữ ghế tạm thời (ShowtimeSeats với Optimistic Concurrency). Trả về ghế conflict nếu không giữ được.</summary>
    Task<HoldSeatResponseDto> HoldSeatAsync(
        string userId,
        HoldSeatRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Giải phóng ghế đã giữ nếu người dùng hủy hoặc đổi ghế.</summary>
    Task<bool> ReleaseSeatAsync(
        string userId,
        int showtimeId,
        List<int>? seatIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>Tính breakdown vé + F&amp;B + voucher, không ghi đơn.</summary>
    Task<BookingResponseDto> CalculateTotalAmountAsync(
        BookingRequestDto request,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Tạo Booking + Tickets + BookingConcessions, chuyển ShowtimeSeats sang Reserved khi thanh toán.</summary>
    Task<BookingResponseDto> CreateBookingAsync(
        string userId,
        BookingRequestDto request,
        CancellationToken cancellationToken = default);
}
