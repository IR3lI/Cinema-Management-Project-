using QRCoder;

namespace CineManagement.Services;

/// <summary>Creates QR images from a server-issued ticket identity payload.</summary>
public sealed class QrCodeService : IQrCodeService
{
    public string CreateTicketPayload(int ticketId, string ticketCode)
        => $"cinema-ticket:{ticketId}:{ticketCode}";

    public string GeneratePngBase64(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return Convert.ToBase64String(png.GetGraphic(20));
    }
}
