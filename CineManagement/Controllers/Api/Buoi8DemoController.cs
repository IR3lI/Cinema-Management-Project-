using Microsoft.AspNetCore.Mvc;

namespace CineManagement.Controllers.Api;

[ApiController]
[Route("api/demo")]
public sealed class Buoi8DemoController : ControllerBase
{
    private readonly ILogger<Buoi8DemoController> _logger;

    public Buoi8DemoController(ILogger<Buoi8DemoController> logger)
    {
        _logger = logger;
    }

    [HttpGet("node-info")]
    public IActionResult GetNodeInfo()
    {
        var envNode = Environment.GetEnvironmentVariable("NODE_INSTANCE_ID");
        var localPort = HttpContext.Connection.LocalPort;
        var nodeId = !string.IsNullOrEmpty(envNode) 
            ? envNode 
            : $"Backend-Node-{localPort}";

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var clientIp = !string.IsNullOrEmpty(realIp) 
            ? realIp 
            : (!string.IsNullOrEmpty(forwardedFor) ? forwardedFor : HttpContext.Connection.RemoteIpAddress?.ToString());

        var host = Request.Headers["Host"].FirstOrDefault() ?? Request.Host.Value;
        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;

        _logger.LogInformation("[DEMO BUỔI 8] Request received by {NodeId} from Client IP: {ClientIp} (Host: {Host})",
            nodeId, clientIp, host);

        return Ok(new
        {
            success = true,
            nodeId,
            clientIp = clientIp ?? "Unknown",
            xRealIp = realIp ?? "(chưa có - Nginx chưa truyền)",
            xForwardedFor = forwardedFor ?? "(chưa có)",
            hostHeader = host,
            protocol = scheme,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            message = $"Request được xử lý thành công bởi node: {nodeId}"
        });
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit] // Allow backend to accept large body so Nginx's client_max_body_size is what actually enforces the limit
    public async Task<IActionResult> TestUpload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            // If body has raw data without form-data
            if (Request.ContentLength.HasValue && Request.ContentLength > 0)
            {
                return Ok(new
                {
                    success = true,
                    sizeBytes = Request.ContentLength.Value,
                    sizeMB = Math.Round((double)Request.ContentLength.Value / (1024 * 1024), 2),
                    message = $"Nhận thành công body dữ liệu {Request.ContentLength.Value} bytes qua Nginx!"
                });
            }

            return BadRequest(new { success = false, message = "Vui lòng chọn một file hoặc gửi dữ liệu payload." });
        }

        var sizeInMb = Math.Round((double)file.Length / (1024 * 1024), 2);
        _logger.LogInformation("[DEMO BUỔI 8] Upload received: {FileName} ({SizeMB} MB)", file.FileName, sizeInMb);

        return Ok(new
        {
            success = true,
            fileName = file.FileName,
            sizeBytes = file.Length,
            sizeMB = sizeInMb,
            message = $"Tải lên thành công file '{file.FileName}' dung lượng {sizeInMb} MB vượt qua NGINX an toàn!"
        });
    }
}
