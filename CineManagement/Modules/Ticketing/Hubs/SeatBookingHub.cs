using Microsoft.AspNetCore.SignalR;

namespace CineManagement.Hubs;

/// <summary>
/// SignalR Hub quản lý kết nối WebSocket theo từng suất chiếu (Showtime Group).
/// Phát sóng sự kiện SeatStatusChanged thời gian thực khi khách hàng hoặc nhân viên quầy giữ/nhả/mua ghế.
/// </summary>
public class SeatBookingHub : Hub
{
    public static string GetShowtimeGroup(int showtimeId) => $"showtime_{showtimeId}";

    public async Task JoinShowtimeGroup(int showtimeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetShowtimeGroup(showtimeId));
    }

    public async Task LeaveShowtimeGroup(int showtimeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetShowtimeGroup(showtimeId));
    }
}
