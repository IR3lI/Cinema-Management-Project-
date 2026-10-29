using CineManagement.DTOs;
using CineManagement.Models;

namespace CineManagement.ViewModels;

// ── SelectSeat ViewModel ────────────────────────────────────────────────────
public class SelectSeatViewModel
{
    public Showtime Showtime { get; set; } = null!;
    public ScreeningRoom Room { get; set; } = null!;

    public int TotalRows => Room?.TotalRows > 0 ? Room.TotalRows : 10;
    public int TotalCols => Room?.TotalCols > 0 ? Room.TotalCols : 16;

    public List<SeatTypeLegendDto> SeatTypes { get; set; } = new();
    public List<SeatInfo> Seats { get; set; } = new();

    // Rows -> Seats (ordered) for backwards compatibility
    public Dictionary<string, List<SeatInfo>> SeatMap { get; set; } = new();

    public class SeatInfo
    {
        public int ShowtimeSeatId { get; set; }
        public int SeatId { get; set; }
        public string SeatCode { get; set; } = "";
        public string RowLabel { get; set; } = "";
        public int SeatNumber { get; set; }
        public int GridRow { get; set; }
        public int GridCol { get; set; }
        public int ColSpan { get; set; } = 1;
        public decimal Price { get; set; }
        public string ColorCode { get; set; } = "#6c757d";
        public string SeatTypeName { get; set; } = "Thường";
        public SeatTypeEnum SeatType { get; set; } = SeatTypeEnum.Standard;
        public SeatStatus Status { get; set; } = SeatStatus.Available;
    }

    public enum SeatStatus { Available, HeldByMe, HeldByOther, Booked }
}

// ── FnB Item selection ───────────────────────────────────────────────────────
public class FnBItemViewModel
{
    public int FoodItemId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public FoodCategory Category { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; } = 0;
}

public class AddFnBViewModel
{
    public int BookingId { get; set; }
    public int ShowtimeId { get; set; }
    public List<FnBItemViewModel> Items { get; set; } = new();
    public decimal SeatTotal { get; set; }
}

// ── Checkout ViewModel ───────────────────────────────────────────────────────
public class CheckoutSeatLine
{
    public string SeatCode { get; set; } = "";
    public string SeatTypeName { get; set; } = "Thường";
    public decimal UnitPrice { get; set; }
}

public class CheckoutViewModel
{
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    // Seat summary
    public List<string> SeatCodes { get; set; } = new();
    public List<CheckoutSeatLine> SeatLines { get; set; } = new();
    public decimal SeatTotal { get; set; }

    // F&B summary
    public decimal FnBTotal { get; set; }

    // Totals
    public decimal SubTotal => SeatTotal + FnBTotal;
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount => SubTotal - DiscountAmount;

    // Voucher
    public string? VoucherCode { get; set; }
    public string? VoucherMessage { get; set; }
    public bool VoucherApplied { get; set; }
    public string? VoucherError { get; set; }

    // Payment & VietQR
    public string PaymentMethod { get; set; } = "VietQR";
    public string? TransactionCode { get; set; }
    public string? VietQrPayload { get; set; }
    public string? VietQrImageUrl { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
}

// ── Confirmation ViewModel ───────────────────────────────────────────────────
public class ConfirmationViewModel
{
    public Booking Booking { get; set; } = null!;
    public List<string> SeatCodes { get; set; } = new();
    public string? QrBase64 { get; set; }
    public string? DirectTicketUrl { get; set; }
}
