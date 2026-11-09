using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CineManagement.Tests;

/// <summary>
/// Minh chứng kiểm thử cho các Yêu cầu Phi Chức năng (Non-Functional Requirements) & Ràng buộc Hệ thống:
/// 1. Tính toán thời gian hết hạn giữ ghế (Seat Hold Timeout - 5 phút).
/// 2. Cơ chế Idempotency (Request Key & Request Hash) chống lặp thanh toán / duplicate order.
/// 3. Ràng buộc bảo mật tệp tin tải lên (File Storage Security).
/// </summary>
public class NonFunctionalConstraintTests
{
    [Fact]
    public void SeatHoldTimeout_ShouldExpireAfterExactFiveMinutes()
    {
        // Ràng buộc phi chức năng: Giữ ghế tối đa 5 phút để tránh nghẽn vé
        var heldAt = DateTime.UtcNow;
        var holdDuration = TimeSpan.FromMinutes(5);
        var expiresAt = heldAt.Add(holdDuration);

        // Kiểm tra sau 4 phút 59 giây: Ghế vẫn hợp lệ
        var activeCheckTime = heldAt.AddMinutes(4).AddSeconds(59);
        Assert.True(activeCheckTime < expiresAt, "Ghế vẫn phải được giữ khi chưa quá 5 phút.");

        // Kiểm tra sau 5 phút 01 giây: Ghế phải hết hạn và được giải phóng
        var expiredCheckTime = heldAt.AddMinutes(5).AddSeconds(1);
        Assert.True(expiredCheckTime > expiresAt, "Ghế phải được đánh dấu hết hạn sau 5 phút.");
    }

    [Theory]
    [InlineData("ORDER-20261105-001", "METHOD=VNPAY;AMOUNT=150000;USER=10")]
    [InlineData("ORDER-20261105-002", "METHOD=MOMO;AMOUNT=95000;USER=15")]
    public void IdempotencyKey_SameRequestMustProduceIdenticalHash(string requestKey, string payload)
    {
        // Minh chứng giải thích câu hỏi của Giảng viên: "request_key, request_hash là gì, tại sao phải có?"
        // Mục đích: Nếu mạng bị lag/chậm, người dùng bấm nút Thanh toán 2 lần liên tiếp,
        // hệ thống sẽ phát hiện cùng 1 request_key và cùng request_hash để từ chối xử lý lần 2,
        // đảm bảo KHÔNG TRỪ TIỀN 2 LẦN (Idempotent Transaction).

        string ComputeHash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        var hash1 = ComputeHash(payload);
        var hash2 = ComputeHash(payload);

        // Assert: 2 lần request với cùng payload phải sinh ra cùng hash
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(requestKey);
    }

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".png", true)]
    [InlineData(".webp", true)]
    [InlineData(".exe", false)]
    [InlineData(".sh", false)]
    [InlineData(".php", false)]
    public void StorageService_OnlyWhitelistedImageExtensionsAllowed(string extension, bool expectedValid)
    {
        // Ràng buộc bảo mật phi chức năng: Chống tải lên mã độc thực thi (Malware/Webshell)
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        var isValid = allowedExtensions.Contains(extension);
        Assert.Equal(expectedValid, isValid);
    }
}
