# Contract: Cloud-Native Object Storage (`IStorageService`)

**Interface**: `CineManagement.Modules.Shared.Storage.IStorageService`  
**Standard**: Amazon S3 API v3 (AWSSDK.S3)  
**Engines**: Cloudflare R2 (Production) / MinIO (Development & Testing)

---

## 1. C# Interface Contract

```csharp
namespace CineManagement.Modules.Shared.Storage;

public interface IStorageService
{
    /// <summary>
    /// Tải tệp tin nhị phân (ảnh poster, banner, mã QR) lên Object Storage.
    /// </summary>
    /// <param name="fileStream">Dòng dữ liệu tệp tin</param>
    /// <param name="fileName">Tên gốc của tệp</param>
    /// <param name="contentType">MIME type (image/jpeg, image/png, image/webp)</param>
    /// <param name="folder">Thư mục phân nhóm (movies, qrcodes, concessions)</param>
    /// <param name="cancellationToken">Token hủy tác vụ</param>
    /// <returns>Public URL hoặc Storage Key của tệp vừa lưu</returns>
    Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string? folder = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tải nội dung tệp tin từ Storage về luồng đọc.
    /// </summary>
    /// <param name="fileKey">Khóa định danh tệp trên Storage</param>
    Task<Stream> DownloadFileAsync(string fileKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa tệp vĩnh viễn khỏi Storage.
    /// </summary>
    /// <param name="fileKeyOrUrl">Public URL hoặc Storage Key của tệp</param>
    Task<bool> DeleteFileAsync(string fileKeyOrUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo đường dẫn công khai (Public CDN URL) cho tệp.
    /// </summary>
    string GetPublicUrl(string fileKey);

    /// <summary>
    /// Tạo URL ký sẵn (Pre-Signed URL) có thời hạn truy cập tạm thời.
    /// </summary>
    Task<string> GeneratePreSignedUrlAsync(string fileKey, TimeSpan duration);
}
```

---

## 2. Configuration Options Contract (`appsettings.json`)

```json
{
  "Storage": {
    "Provider": "MinIO", // "MinIO" hoặc "CloudflareR2"
    "ServiceUrl": "http://minio:9000",
    "BucketName": "cinemanagement-media",
    "AccessKeyId": "minioadmin",
    "SecretAccessKey": "minioadmin123",
    "PublicBaseUrl": "http://localhost:9000/cinemanagement-media",
    "ForcePathStyle": true
  }
}
```

### Production Configuration (Cloudflare R2 Example via Environment Variables):
- `Storage__Provider`: `CloudflareR2`
- `Storage__ServiceUrl`: `https://<account_id>.r2.cloudflarestorage.com`
- `Storage__BucketName`: `cinemanagement-prod-media`
- `Storage__AccessKeyId`: `<R2_ACCESS_KEY_ID>`
- `Storage__SecretAccessKey`: `<R2_SECRET_ACCESS_KEY>`
- `Storage__PublicBaseUrl`: `https://media.cinemanagement.vn`
- `Storage__ForcePathStyle`: `false`

---

## 3. Dependency Injection Registration Contract

```csharp
// Program.cs
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    var config = new AmazonS3Config
    {
        ServiceURL = opts.ServiceUrl,
        ForcePathStyle = opts.ForcePathStyle,
        UseHttp = opts.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
    };
    var credentials = new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey);
    return new AmazonS3Client(credentials, config);
});

builder.Services.AddScoped<IStorageService, S3StorageService>();
```
