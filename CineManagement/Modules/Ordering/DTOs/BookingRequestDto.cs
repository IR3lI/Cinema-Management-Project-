using System.ComponentModel.DataAnnotations;
using CineManagement.Models;

namespace CineManagement.DTOs;

public sealed record FnBLineRequestDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Món F&B không hợp lệ.")]
    public int FoodItemId { get; init; }

    [Required]
    [Range(1, 20, ErrorMessage = "Số lượng F&B phải từ 1 đến 20.")]
    public int Quantity { get; init; } = 1;
}

/// <summary>
/// Request thanh toán / tạo đơn: suất chiếu, ghế, F&amp;B, voucher (SCRUM-16, 18, 19).
/// </summary>
public sealed record BookingRequestDto : IValidatableObject
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Suất chiếu không hợp lệ.")]
    public int ShowtimeId { get; init; }

    [Required(ErrorMessage = "Vui lòng chọn ghế.")]
    [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một ghế.")]
    [MaxLength(10, ErrorMessage = "Không được chọn quá 10 ghế.")]
    public List<int> SeatIds { get; init; } = [];

    public List<FnBLineRequestDto> FnBItems { get; init; } = [];

    [StringLength(50, ErrorMessage = "Mã giảm giá tối đa 50 ký tự.")]
    [RegularExpression(@"^[A-Za-z0-9]*$", ErrorMessage = "Mã giảm giá chỉ gồm chữ và số.")]
    public string? VoucherCode { get; init; }

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Cash;

    [EmailAddress(ErrorMessage = "Email nhận vé không hợp lệ.")]
    [StringLength(256)]
    public string? ContactEmail { get; init; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(20)]
    public string? ContactPhone { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SeatIds.Distinct().Count() != SeatIds.Count)
        {
            yield return new ValidationResult(
                "Danh sách ghế không được trùng.",
                [nameof(SeatIds)]);
        }

        var duplicateFnB = FnBItems
            .GroupBy(x => x.FoodItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateFnB.Count > 0)
        {
            yield return new ValidationResult(
                "Mỗi món F&B chỉ được xuất hiện một dòng.",
                [nameof(FnBItems)]);
        }
    }
}
