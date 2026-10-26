using CineManagement.Models;

namespace CineManagement.DTOs;

/// <summary>Snapshot of a completed Booking, used when delivering an e-ticket.</summary>
public sealed record OrderResponseDto
{
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string MovieTitle { get; init; } = string.Empty;
    public string CinemaName { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public decimal FinalAmount { get; init; }
    public IReadOnlyList<TicketEmailDto> Tickets { get; init; } = [];
}

public sealed record TicketEmailDto
{
    public int TicketId { get; init; }
    public string TicketCode { get; init; } = string.Empty;
    public string SeatCode { get; init; } = string.Empty;
    public TicketStatus Status { get; init; }
    public string? QrCodeBase64 { get; init; }
}
