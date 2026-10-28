using CineManagement.DTOs;

namespace CineManagement.Services;

public interface IEmailService
{
    Task SendTicketEmailAsync(
        string toEmail,
        OrderResponseDto orderDetails,
        CancellationToken cancellationToken = default);

    Task SendTicketEmailAsync(
        int bookingId,
        CancellationToken cancellationToken = default);
}
