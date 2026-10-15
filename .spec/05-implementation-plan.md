# KẾ HOẠCH HIỆN THỰC HÓA & ĐỒNG BỘ MÃ NGUỒN (IMPLEMENTATION PLAN)
## HỆ THỐNG QUẢN TRỊ TẬP TRUNG CHUỖI RẠP CHIẾU PHIM (CINEMANAGEMENT)

> **Tài liệu đặc tả**: `05-implementation-plan.md`  
> **Quy trình áp dụng**: Spec-Driven Development (SDD) & Spec-Kit Implementation  
> **Mục tiêu**: Lập lộ trình kỹ thuật cụ thể để đồng bộ 100% codebase hiện tại với bộ đặc tả kỹ thuật trong thư mục `.spec/`

---

## 1. TỔNG QUAN LỘ TRÌNH KỸ THUẬT (ROADMAP PHASES)

Lộ trình đồng bộ hóa được chia thành 4 giai đoạn logic chặt chẽ:

```
┌────────────────────────────────────────────────────────────────────────┐
│ GIAI ĐOẠN 1: TÁI CẤU TRÚC MODULAR MONOLITH & CORE DOMAIN (TASK-01, 02)  │
├────────────────────────────────────────────────────────────────────────┤
│ GIAI ĐOẠN 2: TÍCH HỢP CLOUDFLARE R2 / MINIO S3 STORAGE (TASK-03, 04)   │
├────────────────────────────────────────────────────────────────────────┤
│ GIAI ĐOẠN 3: GIA CỐ THUẬT TOÁN AISLE GUARD & POS BOX OFFICE (TASK-05,06)│
├────────────────────────────────────────────────────────────────────────┤
│ GIAI ĐOẠN 4: KIỂM THỬ TƯƠNG TRANH & ĐÓNG GÓI DOCKER NGINX (TASK-07, 08) │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. DANH MỤC CÁC TASK KỸ THUẬT CHI TIẾT (ACTIONABLE WORK ITEMS)

### TASK-01: Tái cấu trúc mã nguồn theo chuẩn Modular Monolith
* **Độ ưu tiên**: `P1 - High`
* **Mục tiêu**: Gom nhóm các Controllers, Services, Models, ViewModels hiện tại vào các ranh giới module mạch lạc theo đúng tài liệu `01-architecture.md`:
  * `Modules/Cinemas`: Cụm rạp, Phòng chiếu, Sơ đồ ghế 2D.
  * `Modules/Catalog`: Quản lý Phim, Suất chiếu.
  * `Modules/Ticketing`: ShowtimeSeats, SignalR `SeatBookingHub`, `SeatHoldTimeoutWorker`.
  * `Modules/Ordering`: Đơn vé, Quầy Box Office POS `/Counter`, In vé nhiệt 80mm.
  * `Modules/Payments`: VietQR, Transactional Outbox, `OutboxMessageWorker`.
  * `Modules/Shared.Storage`: Dịch vụ lưu trữ tệp đám mây.
* **Tệp tin tác động**:
  * `CineManagement/Program.cs`
  * `CineManagement/Controllers/*`
  * `CineManagement/Services/*`
* **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  * Dự án build thành công (`dotnet build`) không có lỗi biên dịch.
  * Dependency Injection đăng ký dịch vụ theo từng Module Extension method (`AddCinemasModule()`, `AddOrderingModule()`, v.v.).

---

### TASK-02: Đồng bộ cấu trúc dữ liệu & Thuộc tính mở rộng trong EF Core
* **Độ ưu tiên**: `P1 - High`
* **Mục tiêu**: Đảm bảo tất cả các trường mới trong tài liệu `02-database-erd.md` được phản ánh đầy đủ trong Model C# và `ApplicationDbContext`:
  * `Movie`: Thêm `Country`, `Format`, `SubtitleType`, `AgeRating`.
  * `SeatType`: Thêm `ColSpan` (mặc định 1, Sweetbox = 2).
  * `Seat`: Xác nhận đủ `GridRow`, `GridCol`, `ColSpan`.
  * `ShowtimeSeat`: Giữ nguyên và gia cố thuộc tính `[ConcurrencyCheck] public int Version { get; set; }`.
  * `Ticket`: Đảm bảo đủ các trường kiểm soát quầy POS: `IsPrinted`, `PrintedAt`, `PrintedByUserId`, `ThermalReceiptCode`.
  * `OutboxMessage`: Đảm bảo đủ `Id`, `Type`, `Payload`, `IsProcessed`, `RetryCount`, `ErrorMessage`.
* **Tệp tin tác động**:
  * `CineManagement/Models/*.cs`
  * `CineManagement/Data/ApplicationDbContext.cs`
* **Tiêu chí nghiệm thu**:
  * Chạy `dotnet ef migrations add UpdateSchemaV2` thành công, không phát sinh lỗi mismatch kiểu dữ liệu.

---

### TASK-03: Tích hợp thư viện AWSSDK.S3 kết nối Cloudflare R2 / MinIO
* **Độ ưu tiên**: `P1 - High`
* **Mục tiêu**: Xây dựng phân hệ lưu trữ đối tượng đám mây (`Shared.Storage`) thay thế lưu trữ file cục bộ vào thư mục `wwwroot/uploads`.
* **Các bước triển khai**:
  1. Cài đặt Package NuGet:
     ```bash
     dotnet add package AWSSDK.S3 --version 3.7.*
     ```
  2. Định nghĩa giao diện lưu trữ `IStorageService`:
     ```csharp
     public interface IStorageService
     {
         Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder = "posters");
         Task DeleteFileAsync(string fileUrl);
     }
     ```
  3. Hiện thực hóa lớp `S3StorageService` sử dụng `AmazonS3Client` với cấu hình linh hoạt:
     * `ServiceURL`: `https://<account_id>.r2.cloudflarestorage.com` (Cloudflare R2) hoặc `http://localhost:9000` (MinIO Local).
     * `ForcePathStyle = true`, xác thực bằng `AccessKey` và `SecretKey`.
  4. Bổ sung section cấu hình trong `appsettings.json`:
     ```json
     "Storage": {
       "Provider": "CloudflareR2",
       "ServiceUrl": "https://your-account-id.r2.cloudflarestorage.com",
       "BucketName": "cinemanagement-media",
       "PublicBaseUrl": "https://media.cinemanagement.vn",
       "AccessKey": "YOUR_R2_ACCESS_KEY",
       "SecretKey": "YOUR_R2_SECRET_KEY"
     }
     ```
* **Tệp tin tác động**:
  * `CineManagement/Services/IStorageService.cs` (Tạo mới)
  * `CineManagement/Services/S3StorageService.cs` (Tạo mới)
  * `CineManagement/appsettings.json`
* **Tiêu chí nghiệm thu**:
  * Upload thử nghiệm một file poster phim, nhận về đường dẫn CDN công khai hợp lệ.

---

### TASK-04: Cập nhật Movie Controller & Ticket Service sử dụng S3 Storage
* **Độ ưu tiên**: `P2 - Medium`
* **Mục tiêu**: Chuyển đổi mã nguồn xử lý upload file ảnh poster phim và sinh ảnh mã QR vé xem phim sang sử dụng `IStorageService`.
* **Tệp tin tác động**:
  * `CineManagement/Controllers/MovieController.cs`
  * `CineManagement/Services/BookingService.cs`
* **Tiêu chí nghiệm thu**:
  * Khi tạo/sửa phim, file poster được đẩy thẳng lên Cloudflare R2 / MinIO, đường dẫn `movies.PosterUrl` trỏ tới URL S3.
  * Không còn phát sinh file mới lưu vào thư mục `wwwroot/uploads` trên máy chủ web.

---

### TASK-05: Gia cố Thuật toán Chống Tràn Ghế Đôi (Aisle Guard) trong Layout Studio
* **Độ ưu tiên**: `P1 - High`
* **Mục tiêu**: Kiểm tra và ngăn chặn tuyệt đối tình trạng vẽ ghế đôi Sweetbox (`ColSpan = 2`) lấn sang cột hành lang hoặc vượt quá độ rộng phòng chiếu.
* **Các bước triển khai**:
  1. Trong `LayoutBuilder.cshtml`: Khi Admin chọn cọ vẽ ghế đôi và click vào ô `(row, col)`, JavaScript kiểm tra trước: nếu `col + 1` thuộc danh sách `aisleCols` hoặc `col + 1 > totalCols`, lập tức hiện thông báo cảnh báo đỏ và chặn không cho đổi class ghế.
  2. Trong `ScreeningRoomController.SaveSeatLayout`: Bổ sung kiểm tra backend xác thực mảng ghế `SeatDto[]`. Nếu có bất kỳ ghế nào có `ColSpan == 2` mà `GridCol + 1` trùng với cột hành lang, hủy bỏ Transaction và trả về `BadRequest("Ghế đôi không được phép lấn vào cột hành lang!")`.
* **Tệp tin tác động**:
  * `CineManagement/Views/ScreeningRoom/LayoutBuilder.cshtml`
  * `CineManagement/Controllers/ScreeningRoomController.cs`
* **Tiêu chí nghiệm thu**:
  * Thử vẽ ghế đôi sát trước cột hành lang lối đi $\rightarrow$ Hệ thống chặn thành công cả ở giao diện và API.

---

### TASK-06: Hoàn thiện & Kiểm thử Cơ chế Chống In Lậu Vé (IsPrinted Guard) tại Quầy POS
* **Độ ưu tiên**: `P1 - High`
* **Mục tiêu**: Bảo đảm tính toàn vẹn của nghiệp vụ soát vé và in vé nhiệt 80mm tại cổng `/Counter`.
* **Các bước triển khai**:
  1. Trong `CounterController.ScanBookingQr`: Kiểm tra cờ `tickets.Any(t => t.IsPrinted)`. Nếu `true`, trả về trạng thái cảnh báo gian lận kèm thời điểm và nhân viên đã in trước đó.
  2. Trong `CounterController.ConfirmPrintAndUpsell`: Đặt trong `IsolationLevel.Serializable` transaction. Kiểm tra lại cờ `IsPrinted` trước khi cập nhật.
  3. Cập nhật giao diện in nhiệt `PrintThermalTicket.cshtml`: Chuẩn hóa CSS `@media print` cho khổ giấy cuộn 80mm (K80): chiều rộng cố định 80mm, viền cắt đứt nét (dashed lines), mã vạch/QR rõ nét, font chữ đơn sắc rõ ràng.
* **Tệp tin tác động**:
  * `CineManagement/Controllers/CounterController.cs`
  * `CineManagement/Views/Counter/Index.cshtml`
  * `CineManagement/Views/Counter/PrintThermalTicket.cshtml`
* **Tiêu chí nghiệm thu**:
  * Quét mã vé đã in lần 1 $\rightarrow$ Màn hình hiện cảnh báo đỏ từ chối in lần 2.
  * In vé lần đầu $\rightarrow$ Nhả phiếu in nhiệt 80mm định dạng sắc nét.

---

### TASK-07: Viết Bộ Kiểm Thử Tự Động Xử Lý Tương Tranh (Concurrency Unit & Integration Tests)
* **Độ ưu tiên**: `P1 - High`
* **Mục tiêu**: Viết bộ test tự động chứng minh năng lực đáp ứng CLO2 của sinh viên trước hội đồng bảo vệ.
* **Kịch bản kiểm thử**:
  1. **Test Case 1 (Race Condition)**: Khởi tạo 10 tác vụ (Task) bất đồng bộ cùng gọi `HoldSeatAsync` vào duy nhất 1 ghế `E5` cùng một thời điểm.
     * *Kết quả kỳ vọng*: Đúng 1 tác vụ trả về `Success = true`, 9 tác vụ còn lại trả về `Success = false` và không có ngoại lệ unhandled crash.
  2. **Test Case 2 (Aisle Guard)**: Gọi hàm lưu sơ đồ phòng chiếu với 1 ghế đôi tại `Col 4` (nơi `Col 5` là hành lang).
     * *Kết quả kỳ vọng*: Trả về `BadRequest`.
  3. **Test Case 3 (Transactional Outbox)**: Xác nhận thanh toán thành công đơn hàng.
     * *Kết quả kỳ vọng*: Bản ghi `outboxmessages` được tạo với `IsProcessed = 0`, sau đó `OutboxMessageWorker` xử lý đổi thành `IsProcessed = 1`.
* **Tệp tin tác động**:
  * `CineManagement.Tests/BookingConcurrencyTests.cs` (Tạo mới)
  * `CineManagement.Tests/AisleGuardTests.cs` (Tạo mới)
* **Tiêu chí nghiệm thu**:
  * Chạy lệnh `dotnet test` vượt qua 100% test cases (All Green).

---

### TASK-08: Chuẩn hóa Docker Compose & Cấu hình NGINX WebSocket Proxy
* **Độ ưu tiên**: `P2 - Medium`
* **Mục tiêu**: Chuẩn hóa tệp `docker-compose.yml` và cấu hình reverse proxy đáp ứng kiến trúc triển khai trong `01-architecture.md`.
* **Các bước triển khai**:
  1. Cấu hình 2 replica container backend (`backend-node1`, `backend-node2`).
  2. Cấu hình container `mariadb-10.11` gắn persistent volume `mariadb_data:/var/lib/mysql`.
  3. Bổ sung cấu hình `nginx.conf`:
     ```nginx
     location /hubs/ {
         proxy_pass http://cinema-backend;
         proxy_http_version 1.1;
         proxy_set_header Upgrade $http_upgrade;
         proxy_set_header Connection "upgrade";
         proxy_set_header Host $host;
         proxy_cache_bypass $http_upgrade;
     }
     ```
* **Tệp tin tác động**:
  * `docker-compose.yml`
  * `nginx/nginx-docker.conf`
* **Tiêu chí nghiệm thu**:
  * Khởi động toàn bộ stack bằng `docker compose up -d` thành công, truy cập web qua NGINX và kết nối SignalR WebSockets ổn định.
