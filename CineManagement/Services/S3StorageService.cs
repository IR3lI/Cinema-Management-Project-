using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace CineManagement.Services;

public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly StorageOptions _options;
    private readonly ILogger<S3StorageService> _logger;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public S3StorageService(
        IAmazonS3 s3Client,
        IOptions<StorageOptions> options,
        ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var uniqueFileName = $"{Guid.NewGuid()}{ext}";
        var fileKey = string.IsNullOrWhiteSpace(folder)
            ? uniqueFileName
            : $"{folder.Trim('/')}/{uniqueFileName}";

        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = fileKey,
                InputStream = fileStream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);
            _logger.LogInformation("Đã tải tệp thành công lên S3: Bucket={Bucket}, Key={Key}", _options.BucketName, fileKey);

            return GetPublicUrl(fileKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi upload tệp {FileKey} lên S3 ({ServiceUrl})", fileKey, _options.ServiceUrl);
            throw new InvalidOperationException($"Lỗi lưu trữ tệp lên Cloud-Native S3: {ex.Message}", ex);
        }
    }

    public async Task<Stream> DownloadFileAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        var cleanKey = ExtractKeyFromUrlOrKey(fileKey);
        var getRequest = new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = cleanKey
        };

        var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken);
        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<bool> DeleteFileAsync(string fileKeyOrUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileKeyOrUrl))
            return false;

        var cleanKey = ExtractKeyFromUrlOrKey(fileKeyOrUrl);
        try
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = cleanKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
            _logger.LogInformation("Đã xóa tệp trên S3: Key={Key}", cleanKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể xóa tệp S3 {Key}: {Message}", cleanKey, ex.Message);
            return false;
        }
    }

    public string GetPublicUrl(string fileKey)
    {
        var cleanKey = ExtractKeyFromUrlOrKey(fileKey);
        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{cleanKey}";
    }

    public async Task<string> GeneratePreSignedUrlAsync(string fileKey, TimeSpan duration)
    {
        var cleanKey = ExtractKeyFromUrlOrKey(fileKey);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = cleanKey,
            Expires = DateTime.UtcNow.Add(duration)
        };

        return await _s3Client.GetPreSignedURLAsync(request);
    }

    public bool IsValidImage(IFormFile file, out string? errorMessage)
    {
        errorMessage = null;

        if (file == null || file.Length == 0)
        {
            errorMessage = "Vui lòng chọn tập tin ảnh.";
            return false;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            errorMessage = $"Dung lượng ảnh vượt quá giới hạn tối đa ({MaxFileSizeBytes / (1024 * 1024)}MB).";
            return false;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            errorMessage = $"Định dạng tập tin '{ext}' không được hỗ trợ. Vui lòng chọn file ảnh (.jpg, .jpeg, .png, .webp).";
            return false;
        }

        return true;
    }

    private string ExtractKeyFromUrlOrKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        if (input.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return input.Substring(baseUrl.Length).TrimStart('/');
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.TrimStart('/');
            // Nếu path bắt đầu bằng bucket name trong forcePathStyle
            if (path.StartsWith(_options.BucketName + "/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(_options.BucketName.Length + 1);
            }
            return path;
        }

        return input.TrimStart('/');
    }
}
