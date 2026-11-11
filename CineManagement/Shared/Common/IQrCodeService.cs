namespace CineManagement.Services;

public interface IQrCodeService
{
    string CreateTicketPayload(int ticketId, string ticketCode);
    string GeneratePngBase64(string payload);
}
