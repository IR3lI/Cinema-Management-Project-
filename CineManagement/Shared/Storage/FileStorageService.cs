using Microsoft.AspNetCore.Http;

namespace CineManagement.Modules.Shared.Storage;

/// <summary>
/// Bridge adapter chuyển đổi từ IFileStorageService legacy sang IStorageService (Cloudflare R2 / MinIO S3 API).
/// Tuân thủ nguyên tắc Stateless Infrastructure (Constitution Principle V), cấm ghi đĩa cục bộ wwwroot/uploads.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IStorageService storageService, ILogger<FileStorageService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<string> SaveImageAsync(IFormFile file, string folderName)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File không hợp lệ hoặc rỗng.", nameof(file));

        using var stream = file.OpenReadStream();
        var publicUrl = await _storageService.UploadFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            folder: folderName);

        _logger.LogInformation("Lưu ảnh qua Cloud-Native Storage: {Url}", publicUrl);
        return publicUrl;
    }

    public void DeleteFile(string? relativeOrPublicUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeOrPublicUrl))
            return;

        // Xóa bất đồng bộ qua background task hoặc gọi trực tiếp
        Task.Run(async () =>
        {
            try
            {
                await _storageService.DeleteFileAsync(relativeOrPublicUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi xóa file trên S3: {Url}", relativeOrPublicUrl);
            }
        });
    }

    public bool IsValidImage(IFormFile file, out string? errorMessage)
    {
        return _storageService.IsValidImage(file, out errorMessage);
    }
}
