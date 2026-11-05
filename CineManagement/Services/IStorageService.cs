namespace CineManagement.Services;

public interface IStorageService
{
    /// <summary>
    /// Tải tệp tin nhị phân (ảnh poster, banner, mã QR) lên S3 Object Storage (Cloudflare R2 / MinIO).
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
    /// Lấy đường dẫn công khai (Public CDN URL) cho tệp.
    /// </summary>
    string GetPublicUrl(string fileKey);

    /// <summary>
    /// Tạo URL ký sẵn (Pre-Signed URL) có thời hạn truy cập tạm thời.
    /// </summary>
    Task<string> GeneratePreSignedUrlAsync(string fileKey, TimeSpan duration);

    /// <summary>
    /// Kiểm tra định dạng và dung lượng tập tin ảnh trước khi tải lên.
    /// </summary>
    bool IsValidImage(IFormFile file, out string? errorMessage);
}
