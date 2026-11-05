# Research & Architecture Decisions: Nâng cấp Codebase chuẩn CLO1

**Feature**: `001-cinema-chain-booking` | **Date**: 2026-11-04  
**Scope**: Tái cấu trúc Modular Monolith, Lưu trữ Cloudflare R2 / MinIO (AWSSDK.S3), Thuật toán Aisle Guard & Visual Seat Layout Builder, Docker Compose + NGINX + Cloudflare SSL.

---

## 1. Modular Monolith Architecture (CLO1)

### Quyết định kỹ thuật (Decision)
Tái cấu trúc thư mục và namespace của CineManagement theo mô hình **Modular Monolith** dựa trên các Bounded Context rõ ràng:
- `Modules/Cinemas/`: Quản lý cụm rạp (`Cinema`), phòng chiếu (`ScreeningRoom`), ghế vật lý (`Seat`, `SeatType`), và cấu hình ma trận lưới 2D (`AisleRows`, `AisleCols`).
- `Modules/Catalog/`: Quản lý kho phim dùng chung (`Movie`, `Genre`), danh mục bắp nước (`FoodItem`).
- `Modules/Ticketing/`: Quản lý suất chiếu (`Showtime`), trạng thái ghế thời gian thực (`ShowtimeSeat`), phát hành vé (`Ticket`), SignalR Hub (`SeatBookingHub`), và Background Service giải phóng ghế hết hạn (`SeatHoldTimeoutWorker`).
- `Modules/Ordering/`: Quản lý đơn hàng đặt vé và bắp nước (`Booking`, `BookingConcession`).
- `Modules/Payments/`: Mô phỏng thanh toán VietQR động Napas 247, xác nhận giao dịch tức thì $100\%$.
- `Modules/Shared/`:
  - `Shared/Storage/`: Giao diện `IStorageService` và triển khai `S3StorageService` (AWSSDK.S3) cho R2/MinIO.
  - `Shared/Outbox/`: Xử lý gửi email vé không mất mát (`OutboxMessage`, `OutboxMessageWorker`, `IEmailService`).
  - `Shared/Common/`: Base entities, Enums, Result types, DateTime helpers.
- `Modules/Users/`: Tài khoản `ApplicationUser`, phân quyền RBAC (`Customer`, `Staff`, `Admin`), xác thực Identity.

### Lý do lựa chọn (Rationale)
1. **Đáp ứng chuẩn đầu ra CLO1**: Phân tách rõ ràng giữa các phân hệ nghiệp vụ, triệt tiêu sự phụ thuộc vòng (circular dependencies) và tình trạng dồn toàn bộ models, controllers vào thư mục gốc.
2. **Tính toàn vẹn của Monolith**: Toàn bộ các module cùng nằm trong một solution / project hoặc project con nhưng triển khai chung một tiến trình (Single Deployment Unit), không làm phát sinh chi phí vận hành mạng phân tán phức tạp của Microservices.
3. **Giao tiếp liên module chuẩn mực**: Giao tiếp giữa các module diễn ra thông qua Public Service Interfaces, DTOs hoặc Domain Events; không thực hiện truy vấn chéo bảng trực tiếp ngoài phạm vi quản lý.

### Các phương án khác đã xem xét (Alternatives Considered)
- **Microservices độc lập**: Đã loại bỏ vì vượt quá quy mô đề tài, làm phức tạp hóa việc đồng bộ dữ liệu (distributed transactions, saga pattern) và tốn tài nguyên hạ tầng.
- **N-Tier truyền thống (Controllers / Services / Models chung)**: Đã loại bỏ vì gây xáo trộn mã nguồn, khó bảo trì khi dự án mở rộng, và không đạt yêu cầu thiết kế module hóa của CLO1.

---

## 2. Cloud-Native Object Storage (Cloudflare R2 / MinIO via AWSSDK.S3)

### Quyết định kỹ thuật (Decision)
- Xây dựng giao diện trừu tượng `IStorageService` độc lập với hệ thống tệp cục bộ:
  ```csharp
  public interface IStorageService
  {
      Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, string? folder = null, CancellationToken ct = default);
      Task<Stream> DownloadFileAsync(string fileKey, CancellationToken ct = default);
      Task<bool> DeleteFileAsync(string fileKey, CancellationToken ct = default);
      string GetPublicUrl(string fileKey);
      Task<string> GeneratePreSignedUrlAsync(string fileKey, TimeSpan duration);
  }
  ```
- Triển khai `S3StorageService` sử dụng gói thư viện chính thức `AWSSDK.S3` (Amazon S3 API v3):
  - **Môi trường Development**: Kết nối tới container MinIO chạy nội bộ (`http://minio:9000`), bật cờ `ForcePathStyle = true`.
  - **Môi trường Production**: Kết nối tới Cloudflare R2 bucket (`https://<account_id>.r2.cloudflarestorage.com`), cấu hình Public CDN URL (`https://cdn.cinemanagement.vn/...`).
- Thay thế hoàn toàn `FileStorageService` cũ (ghi trực tiếp vào `wwwroot/uploads`).

### Lý do lựa chọn (Rationale)
1. **Tuân thủ Hiến pháp dự án (Principle V - Stateless Infrastructure)**: Node ứng dụng ASP.NET Core hoàn toàn không lưu trạng thái (stateless) trên đĩa cục bộ, cho phép scale-out nhiều container song song phía sau NGINX mà không bị lệch dữ liệu file tĩnh.
2. **Tối ưu chi phí và hiệu năng**: Cloudflare R2 tương thích hoàn toàn chuẩn S3 API, không tính phí băng thông truyền tải ra ngoài (Zero Egress Fees), tích hợp sẵn CDN toàn cầu.
3. **Tiện lợi khi lập trình cục bộ**: MinIO cung cấp môi trường S3 giả lập 100% giống R2/AWS, hỗ trợ giao diện quản trị Web Console trực quan.

### Các phương án khác đã xem xét (Alternatives Considered)
- **Ghi tệp vào thư mục `wwwroot/uploads` cục bộ**: Bị nghiêm cấm bởi Constitution Principle V do mất tệp khi container khởi động lại hoặc khi chạy nhiều node.
- **Azure Blob Storage SDK**: Bị loại bỏ do không có chuẩn giao thức mở như S3 API, không thể chuyển đổi linh hoạt giữa MinIO cục bộ và Cloudflare R2.

---

## 3. Thuật toán Aisle Guard & Công cụ Visual Seat Layout Builder

### Quyết định kỹ thuật (Decision)
Thiết kế thuật toán phòng vệ hành lang (**Aisle Guard Algorithm**) và tích hợp vào công cụ biên tập sơ đồ 2D trực quan:

#### A. Cấu trúc dữ liệu mở rộng
- `ScreeningRoom`: Bổ sung 2 thuộc tính cấu hình lối đi:
  - `AisleRows`: Chuỗi số hoặc mảng JSON lưu chỉ số các hàng hành lang ngang (ví dụ: `"4,8"`).
  - `AisleCols`: Chuỗi số hoặc mảng JSON lưu chỉ số các cột hành lang dọc (ví dụ: `"5,12"`).
- `SeatType`: Kế thừa `ColSpan` (1 cho Standard/VIP, 2 cho Double/Sweetbox).

#### B. Quy tắc thuật toán Aisle Guard
1. **Biên phòng chiếu**: Ghế $(r, c)$ với độ rộng $ColSpan$ phải thỏa mãn:
   $$1 \le r \le \text{TotalRows} \quad \text{và} \quad 1 \le c \le c + ColSpan - 1 \le \text{TotalCols}$$
2. **Chặn hành lang ngang**: Hàng $r$ không được nằm trong tập $\text{AisleRows}$.
3. **Chặn ghế đơn và ghế đôi đè lên hành lang dọc (Aisle Guard Core)**:
   Mọi cột $k \in [c, c + ColSpan - 1]$ không được trùng với bất kỳ cột hành lang nào trong $\text{AisleCols}$.
   - Ghế đơn ($ColSpan = 1$): $c \notin \text{AisleCols}$.
   - Ghế đôi Sweetbox ($ColSpan = 2$): $c \notin \text{AisleCols}$ **VÀ** $(c + 1) \notin \text{AisleCols}$. Nếu vi phạm, từ chối ngay lập tức kèm thông báo: *"Ghế đôi tại ô ($r$, $c$-$c+1$) đè lên hành lang dọc tại cột $k$"*.
4. **Chống đè ghế lân cận**: Tập tọa độ $\{(r, k) \mid c \le k < c + ColSpan\}$ không được chiếm dụng bởi ghế khác.
5. **Thuật toán tự động đánh số ghế**:
   - Duyệt các hàng $r \in [1, \text{TotalRows}]$, nếu $r \in \text{AisleRows}$ thì bỏ qua không tạo nhãn hàng. Hàng hợp lệ được gắn nhãn chữ: $A, B, C, \dots, Z, AA, \dots$
   - Trong mỗi hàng, duyệt các cột $c \in [1, \text{TotalCols}]$, nếu $c \in \text{AisleCols}$ thì bỏ qua không tăng số thứ tự ghế. Ghế hợp lệ được đánh số thứ tự liên tục $1, 2, 3 \dots$

#### C. Visual Seat Layout Builder UI
- **Gán hạng ghế theo hàng (Row-level Assignment)**: Bổ sung thanh công cụ cho phép Admin click chọn 1 hàng và bấm: "Gán toàn bộ hàng thành VIP", "Gán toàn bộ hàng thành Đôi", "Gán toàn bộ hàng thành Thường", hoặc "Chuyển toàn bộ hàng thành Lối đi ngang".
- **Chọn nhiều hành lang dọc (Column Aisle Toggle)**: Click trực tiếp vào tiêu đề cột trên ma trận để bật/tắt hành lang dọc (cột hành lang hiển thị hiệu ứng lối đi có đèn neon dẫn hướng).
- **Xác thực 2 lớp**: Kiểm tra chặn lỗi ngay trên giao diện JavaScript (phản hồi thời gian thực < 10ms) và kiểm tra lại nghiêm ngặt ở Server-side (`SaveSeatLayout`) trong transaction `Serializable`.

### Lý do lựa chọn (Rationale)
Triệt tiêu hoàn toàn khả năng phát sinh ghế "ma" hoặc ghế đè lên lối thoát hiểm / lối đi của rạp. Đảm bảo dữ liệu sơ đồ đồng nhất tuyệt đối giữa màn hình quản trị và màn hình đặt vé của khách hàng.

---

## 4. Docker Compose, NGINX Reverse Proxy & Cloudflare SSL

### Quyết định kỹ thuật (Decision)
Thiết lập kiến trúc cụm container đa tầng (`docker-compose.yml`):

```text
[Internet / Khách hàng / Quầy vé]
               │
               ▼ HTTPS (Port 443 - TLS 1.3)
      [Cloudflare Edge / WAF]
               │
               ▼ Origin CA SSL (Strict)
       [NGINX Reverse Proxy]
       ├── Port 80  ──> 301 Redirect HTTPS
       ├── Port 443 ──> SSL Termination (Cloudflare Origin CA)
       │                ├── Static Caching / Security Headers
       │                └── WebSocket Upgrade (/seatBookingHub)
       ▼
 ┌────────────────────────────────────────────────────────┐
 │ cinemanagement-net (Docker Bridge Network)             │
 │                                                        │
 │  ┌───────────────────────┐   ┌──────────────────────┐  │
 │  │ cinemanagement-app    │   │ mariadb:10.11        │  │
 │  │ (.NET 10 Web App)     │──>│ (InnoDB utf8mb4)     │  │
 │  │ Port: 8080 (internal) │   │ Port: 3306 (internal)│  │
 │  └───────────────────────┘   └──────────────────────┘  │
 │              │                                         │
 │              ▼                                         │
 │  ┌───────────────────────┐   ┌──────────────────────┐  │
 │  │ minio:latest          │   │ minio-init           │  │
 │  │ (S3 Object Storage)   │<──│ (Auto Create Bucket) │  │
 │  │ API: 9000, Web: 9001  │   └──────────────────────┘  │
 │  └───────────────────────┘                             │
 └────────────────────────────────────────────────────────┘
```

#### Chi tiết cấu hình NGINX:
1. **SSL Strict**: Sử dụng chứng chỉ Cloudflare Origin CA (`origin.crt` và `origin.key`) có hiệu lực dài hạn (15 năm), giúp loại bỏ hoàn toàn sự cố hết hạn chứng chỉ của Let's Encrypt.
2. **WebSocket Proxy cho SignalR Hub**:
   - `proxy_http_version 1.1;`
   - `proxy_set_header Upgrade $http_upgrade;`
   - `proxy_set_header Connection "upgrade";`
   - `proxy_read_timeout 3600s;`
   - `proxy_buffering off;` (chống buffer gây chậm sự kiện ghế thời gian thực).
3. **Bảo mật**: Enforce HSTS (`max-age=31536000`), X-Frame-Options SAMEORIGIN, X-Content-Type-Options nosniff.

#### Chi tiết Dockerfile cho ASP.NET Core 10:
- Multi-stage build tối ưu kích thước:
  - Stage 1: `mcr.microsoft.com/dotnet/sdk:10.0` biên dịch và `dotnet publish -c Release -o /app/publish`.
  - Stage 2: `mcr.microsoft.com/dotnet/aspnet:10.0` (chỉ chứa runtime nhẹ, non-root user `app`, an toàn bảo mật).

---

## 5. Kết luận & Danh sách việc cần làm (Checklist)

- [x] Kiến trúc Modular Monolith xác định rõ 6 Bounded Contexts.
- [x] Giao diện `IStorageService` và cấu hình S3 cho R2 / MinIO hoàn thiện.
- [x] Thuật toán Aisle Guard cho ghế đôi và công cụ Layout Builder đã đặc tả chi tiết.
- [x] Cấu hình Docker Compose, NGINX, Cloudflare SSL đã chuẩn hóa.
- [x] Không còn mục nào rơi vào trạng thái `NEEDS CLARIFICATION`. Sẵn sàng cho Phase 1 (Data Model & Contracts).
