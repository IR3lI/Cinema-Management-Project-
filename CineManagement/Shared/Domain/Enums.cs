using System.ComponentModel.DataAnnotations;

namespace CineManagement.Models;

/// <summary>
/// Trạng thái đơn hàng (Order / Booking).
/// <see cref="BookingStatus"/> giữ nguyên cho luồng Sprint 3 hiện tại.
/// </summary>
public enum OrderStatus
{
    [Display(Name = "Chờ thanh toán")]
    Pending = 0,

    [Display(Name = "Đã thanh toán")]
    Paid = 1,

    [Display(Name = "Đã hủy")]
    Cancelled = 2,

    [Display(Name = "Hết hạn")]
    Expired = 3,

    [Display(Name = "Đã hoàn tiền")]
    Refunded = 4
}

public enum TicketStatus
{
    [Display(Name = "Đã giữ chỗ")]
    Held = 0,

    [Display(Name = "Đã phát hành")]
    Issued = 1,

    [Display(Name = "Đã soát vé")]
    CheckedIn = 2,

    [Display(Name = "Đã hủy")]
    Cancelled = 3,

    [Display(Name = "Hết hạn")]
    Expired = 4,

    [Display(Name = "Đã hoàn")]
    Refunded = 5
}

public enum HoldSeatStatus
{
    [Display(Name = "Đang giữ")]
    Active = 0,

    [Display(Name = "Đã chuyển thành vé")]
    Converted = 1,

    [Display(Name = "Đã giải phóng")]
    Released = 2,

    [Display(Name = "Hết hạn")]
    Expired = 3
}

public enum PaymentStatus
{
    [Display(Name = "Chưa thanh toán")]
    Unpaid = 0,

    [Display(Name = "Thanh toán thành công")]
    Paid = 1,

    [Display(Name = "Thanh toán thất bại")]
    Failed = 2,

    [Display(Name = "Đã hoàn tiền")]
    Refunded = 3
}

public enum DiscountType
{
    [Display(Name = "Theo phần trăm")]
    Percent = 0,

    [Display(Name = "Số tiền cố định")]
    FixedAmount = 1
}

public enum ReviewStatus
{
    [Display(Name = "Chờ duyệt")]
    Pending = 0,

    [Display(Name = "Đã đăng")]
    Published = 1,

    [Display(Name = "Ẩn")]
    Hidden = 2
}

public enum SeatTypeEnum
{
    [Display(Name = "Thường")]
    Standard = 1,

    [Display(Name = "VIP")]
    VIP = 2,

    [Display(Name = "Ghế đôi")]
    Couple = 3
}

public enum ShowtimeSeatStatus
{
    [Display(Name = "Trống")]
    Available = 0,

    [Display(Name = "Đang giữ")]
    Held = 1,

    [Display(Name = "Đã bán")]
    Reserved = 2
}

public enum PaymentProvider
{
    [Display(Name = "VietQR (Napas 247)")]
    VietQR = 0,

    [Display(Name = "Tiền mặt tại quầy")]
    Cash = 1,

    [Display(Name = "Thẻ ngân hàng")]
    BankCard = 2,

    [Display(Name = "Ví MoMo")]
    MoMo = 3,

    [Display(Name = "Cổng PayOS")]
    PayOS = 4
}
