using CineManagement.Models;

namespace CineManagement.DTOs;

public sealed record TicketCheckInResponseDto
{
    public int TicketId { get; init; }
    public string TicketCode { get; init; } = string.Empty;
    public TicketStatus Status { get; init; }
    public DateTime? CheckedInAt { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed record BookingHistoryDto
{
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public DateTime PurchasedAt { get; init; }
    public BookingStatus Status { get; init; }
    public PaymentStatus PaymentStatus { get; init; }
    public decimal FinalAmount { get; init; }
    public string MovieTitle { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public IReadOnlyList<TicketHistoryDto> Tickets { get; init; } = [];
}

public sealed record TicketHistoryDto
{
    public int TicketId { get; init; }
    public string TicketCode { get; init; } = string.Empty;
    public string SeatCode { get; init; } = string.Empty;
    public TicketStatus Status { get; init; }
    public DateTime? CheckedInAt { get; init; }
}
