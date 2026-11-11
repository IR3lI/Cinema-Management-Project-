using Microsoft.AspNetCore.Http;

namespace CineManagement.Modules.Shared.Storage;

public interface IFileStorageService
{
    /// <summary>
    /// Lưu file ảnh tải lên vào thư mục tương ứng trong wwwroot/uploads/{folderName}
    /// </summary>
    Task<string> SaveImageAsync(IFormFile file, string folderName);

    /// <summary>
    /// Xóa file ảnh trên ổ đĩa dựa trên đường dẫn tương đối (relative URL)
    /// </summary>
    void DeleteFile(string? relativeUrl);

    /// <summary>
    /// Kiểm tra file có đúng định dạng ảnh cho phép hay không (.jpg, .jpeg, .png, .webp)
    /// </summary>
    bool IsValidImage(IFormFile file, out string? errorMessage);
}
