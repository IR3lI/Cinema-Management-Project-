using CineManagement.Models;

namespace CineManagement.Modules.Payments.Services;

public interface IPaymentService
{
    Task<Payment> CreatePaymentAsync(
        int bookingId,
        decimal amount,
        PaymentMethod method,
        PaymentProvider provider = PaymentProvider.VietQR,
        string? clientIp = null,
        CancellationToken cancellationToken = default);

    Task<Payment?> GetPaymentByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<Payment?> GetPaymentByTransactionCodeAsync(string transactionCode, CancellationToken cancellationToken = default);

    Task<bool> ConfirmPaymentAsync(
        int paymentId,
        string? transactionCode = null,
        string? gatewayResponse = null,
        CancellationToken cancellationToken = default);

    string GenerateVietQrPayload(decimal amount, string transactionCode, string description);

    string GenerateVietQrQuickLink(decimal amount, string transactionCode, string cinemaName = "CineManagement");
}
