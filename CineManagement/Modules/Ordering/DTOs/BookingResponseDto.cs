using System.ComponentModel.DataAnnotations;
using CineManagement.Models;

namespace CineManagement.DTOs;

public sealed record BookingTicketLineDto
{
    public int TicketId { get; init; }

    [Required]
    public string TicketCode { get; init; } = string.Empty;

    public int SeatId { get; init; }

    [Required]
    public string SeatCode { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public TicketStatus Status { get; init; }
}

public sealed record BookingFnBLineDto
{
    public int FoodItemId { get; init; }

    [Required]
    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal SubTotal { get; init; }
}

/// <summary>Tổng tiền, breakdown và trạng thái đơn sau tính giá hoặc sau CreateBooking.</summary>
public sealed record BookingResponseDto
{
    public int BookingId { get; init; }

    [StringLength(20)]
    public string BookingCode { get; init; } = string.Empty;

    public int ShowtimeId { get; init; }

    public BookingStatus Status { get; init; }

    public PaymentStatus PaymentStatus { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    public decimal TicketAmount { get; init; }

    public decimal FnBAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal FinalAmount { get; init; }

    public string? VoucherCode { get; init; }

    public string? VoucherMessage { get; init; }

    public bool VoucherApplied { get; init; }

    public DateTime? HoldExpiresAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public IReadOnlyList<BookingTicketLineDto> Tickets { get; init; } = [];

    public IReadOnlyList<BookingFnBLineDto> FnBItems { get; init; } = [];
}
