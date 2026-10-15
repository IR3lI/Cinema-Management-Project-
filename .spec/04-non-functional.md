# ĐẶC TẢ CÁC YÊU CẦU PHI CHỨC NĂNG (NON-FUNCTIONAL REQUIREMENTS)
## HỆ THỐNG QUẢN TRỊ TẬP TRUNG CHUỖI RẠP CHIẾU PHIM (CINEMANAGEMENT)

> **Tài liệu đặc tả**: `04-non-functional.md`  
> **Tiêu chuẩn áp dụng**: ISO/IEC 25010 (Hệ thống & Chất lượng phần mềm)  
> **Mục tiêu đáp ứng**: CLO1 (Mô hình hóa kiến trúc chịu tải, bảo mật) & CLO2 (Hiện thực hóa & Kiểm thử chuẩn công nghiệp)

---

## 1. TÍNH TOÀN VẸN GIAO DỊCH VÀ KIỂM SOÁT TƯƠNG TRANH (ACID & CONCURRENCY)

### 1.1. Mức cô lập giao dịch Serializable (Pessimistic Concurrency)
* **Yêu cầu kỹ thuật**: Mọi thao tác tranh chấp tài nguyên trọng yếu (bao gồm `HoldSeatAsync`, `ConfirmPaymentAsync`, và `ConfirmPrintAndUpsell`) **BẮT BUỘC** phải được bao bọc trong một Database Transaction với mức cô lập nghiêm ngặt nhất:
  ```csharp
  using var transaction = await _db.Database.BeginTransactionAsync(
      IsolationLevel.Serializable, 
      cancellationToken);
  ```
* **Mục tiêu**: Loại bỏ 100% các hiện tượng đọc bẩn (Dirty Read), đọc không lặp lại (Non-repeatable Read) và dữ liệu bóng ma (Phantom Read). Ngăn chặn tuyệt đối tình trạng hai khách hàng cùng mua thành công một ghế (Zero Double-Booking Guarantee).

### 1.2. Khóa phiên bản lạc quan (Optimistic Concurrency Control - OCC)
* **Yêu cầu kỹ thuật**: Trên thực thể `ShowtimeSeat`, trường `Version` được đánh dấu kiểm soát tương tranh tự động:
  ```csharp
  [ConcurrencyCheck]
  public int Version { get; set; } = 1;
  ```
* **Mục tiêu**: Mỗi khi trạng thái ghế thay đổi (`Available -> Held -> Reserved`), trường `Version` tự động tăng 1 đơn vị. Nếu có hai luồng đọc cùng lúc và cùng cố ghi đè, luồng đến sau sẽ bị từ chối bằng ngoại lệ `DbUpdateConcurrencyException`, trả về mã lỗi `HTTP 409 Conflict`.

---

## 2. ĐỘ TIN CẬY CHUYỂN PHÁT THÔNG ĐIỆP (AT-LEAST-ONCE DELIVERY)

### 2.1. Kiến trúc Transactional Outbox Pattern
* **Nguyên tắc**: Tuyệt đối **KHÔNG ĐƯỢC** gọi trực tiếp các API dịch vụ bên ngoài (như SMTP Server, SMS Gateway, Payment Webhook) bên trong cùng một Local Transaction của cơ sở dữ liệu.
* **Cơ chế**:
  * Lưu trữ bản ghi sự kiện `outboxmessages` cùng lúc với cập nhật trạng thái đơn vé trong cùng một Transaction nguyên tử ($ACID$).
  * Dữ liệu payload của sự kiện được tuần tự hóa (Serialize) dưới định dạng JSON chuẩn.

### 2.2. Tiến trình nền chuyển phát có khả năng chịu lỗi (Resilient Worker)
* **Tần suất quét**: Tiến trình `OutboxMessageWorker` quét bảng `outboxmessages` mỗi **5 giây** một lần.
* **Cơ chế Retry (Exponential Backoff)**:
  * Nếu kết nối SMTP Google gián đoạn, tin nhắn không bị mất mà được tăng trường `RetryCount = RetryCount + 1` và ghi nhận `ErrorMessage`.
  * Số lần thử lại tối đa: **3 lần**.
  * Cam kết mức độ dịch vụ: **At-Least-Once Delivery** (Đảm bảo 100% email vé điện tử E-Ticket kèm mã QR được chuyển phát thành công tới hộp thư khách hàng khi hạ tầng mạng phục hồi).

---

## 3. HIỆU NĂNG VÀ ĐỒNG BỘ THỜI GIAN THỰC (REAL-TIME PERFORMANCE)

### 3.1. Độ trễ phát sóng SignalR WebSockets (< 200ms)
* **Chỉ số KPI**: Thời gian từ khi Khách A bấm giữ ghế trên giao diện đến khi ghế đổi sang màu vàng trên màn hình Khách B phải đạt **dưới 200 mili-giây** (trong điều kiện mạng tiêu chuẩn, trung bình thực tế đạt **dưới 50 mili-giây**).
* **Chiến lược phân nhóm (SignalR Groups Multiplexing)**:
  * Kết nối WebSockets được gán theo nhóm Suất chiếu: `showtime_{showtimeId}`.
  * Chỉ các client đang mở cùng suất chiếu mới nhận bản tin `SeatStatusChanged`. Các suất chiếu khác không bị ảnh hưởng, tiết kiệm đến 90% băng thông máy chủ.

### 3.2. Cơ chế tự động kết nối lại (Automatic Reconnection)
* Thư viện SignalR phía Client được cấu hình mảng thời gian chờ thử lại thông minh:
  ```javascript
  const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/seatBooking")
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();
  ```
  Nếu mạng 4G/Wifi của khách bị ngắt quãng ngắn, client tự động kết nối lại và đồng bộ lại trạng thái ghế mới nhất mà không yêu cầu khách phải F5 tải lại toàn bộ trang.

---

## 4. LƯU TRỮ ĐÁM MÂY PHÂN TÁN (S3 COMPATIBLE / CLOUDFLARE R2)

### 4.1. Nguyên tắc phi trạng thái (Stateless Application)
* **Vấn đề**: Việc lưu trữ ảnh Poster và file tải lên trực tiếp vào thư mục cục bộ `wwwroot/uploads` khiến ứng dụng không thể mở rộng nhiều Container (Replicas) trên Docker hoặc Kubernetes.
* **Giải pháp**: Tách rời hoàn toàn tầng lưu trữ tĩnh sang **Dịch vụ Lưu trữ Hướng Đối tượng (Object Storage)** tương thích chuẩn Amazon S3 API thông qua thư viện `AWSSDK.S3`.

### 4.2. Môi trường triển khai
1. **Production (Môi trường vận hành thực tế)**:
   * Kết nối tới **Cloudflare R2 Storage Bucket**.
   * Ưu điểm: Phí truyền dữ liệu ra ngoài bằng 0 (Zero Egress Fees), tích hợp CDN toàn cầu của Cloudflare với thời gian phản hồi hình ảnh Poster < 30ms.
2. **Development / Local (Môi trường phát triển nội bộ)**:
   * Kết nối cụm **MinIO S3** container hóa chạy độc lập trong Docker Compose, giả lập hoàn hảo giao thức S3 trên máy cục bộ của lập trình viên.

---

## 5. ĐÓNG GÓI CONTAINER VÀ TỰ ĐỘNG HÓA CI/CD (DOCKER & DEVOPS)

### 5.1. Kiến trúc Docker Compose Multi-Node
* Hệ thống được đóng gói thành các dịch vụ độc lập trong tệp `docker-compose.yml`:
  1. `cinema-backend-1` & `cinema-backend-2`: Hai node ứng dụng ASP.NET Core 10 chạy song song để cân bằng tải và dự phòng nóng.
  2. `cinema-nginx-proxy`: NGINX Alpine làm Reverse Proxy, xử lý SSL Termination và định tuyến WebSocket.
  3. `cinema-mariadb`: MariaDB 10.11 Server với cơ chế lưu trữ bền vững **Persistent Named Volume** (`mariadb_data`), bảo đảm dữ liệu không bị mất khi container khởi động lại.

### 5.2. Tích hợp liên tục & Triển khai liên tục (CI/CD Pipeline)
* **GitHub Actions Workflow**:
  * Mỗi khi commit được push lên nhánh `main`:
    1. Tự động chạy lệnh kiểm thử `dotnet test` (Unit & Integration Tests).
    2. Tự động đóng gói Docker Image đa tầng (Multi-stage Build) tối ưu dung lượng (< 150MB).
    3. Đẩy image lên **GitHub Container Registry (GHCR)**: `ghcr.io/ir3li/cinemanagement:latest`.
    4. Kích hoạt Webhook cập nhật tự động (Watchtower hoặc SSH Deployment) trên máy chủ VPS.

---

## 6. AN TOÀN VÀ BẢO MẬT HỆ THỐNG (SECURITY STANDARDS)

| Tiêu chuẩn an toàn | Giải pháp hiện thực hóa trong mã nguồn |
| :--- | :--- |
| **Bảo vệ biểu mẫu (Anti-CSRF)** | Tự động tiêm và xác thực thẻ `__RequestVerificationToken` trên toàn bộ các phương thức POST/PUT/DELETE. |
| **Phân quyền vai trò (RBAC)** | Sử dụng thuộc tính `[Authorize(Roles = "Admin")]` và `[Authorize(Roles = "Staff")]` để bảo vệ chặt chẽ các Action. Nhân viên Staff bị từ chối 403 nếu truy cập Admin. |
| **Mã hóa dữ liệu truyền tải** | Cưỡng chế giao thức **HTTPS & WSS (TLS 1.3)** từ Edge Cloudflare tới NGINX VPS bằng chứng chỉ Cloudflare Origin CA. |
| **Bảo vệ mã QR vé (Anti-Tampering)** | Dữ liệu mã QR được mã hóa theo mẫu `cinema-ticket:{BookingId}:{TicketCode}` kèm chữ ký hash, ngăn chặn việc tạo mã QR giả mạo. |
| **Chống gian lận in lại (IsPrinted Guard)** | Trường `tickets.IsPrinted` được cập nhật nguyên tử trong Serializable Transaction, chặn đứng hành vi in 1 mã QR thành nhiều vé cứng tại các quầy POS khác nhau. |
