<!--
Sync Impact Report:
- Version change: 2.0.0 -> 2.1.0 (Ratified SDD Enterprise Constitution with Explicit Spec-Kit Principles)
- Principles defined:
  1. Kiến trúc Backend: Modular Monolith trên ASP.NET Core 10 (.NET 10), phân chia các module độc lập (Cinemas, Catalog, Ticketing, Ordering, Payments, Shared.Storage).
  2. Database: Pure MariaDB/MySQL (tuyệt đối không dùng SQLite fallback), chuẩn 3NF với 12 bảng, kiểm soát tương tranh bằng [ConcurrencyCheck] int Version.
  3. Lưu trữ đám mây (CLO1): Tích hợp Cloudflare R2 / MinIO (qua AWSSDK.S3) để lưu ảnh Poster phim và mã QR vé.
  4. Kỹ thuật bắt buộc: Real-time đặt ghế bằng SignalR, gửi email vé tin cậy bằng Transactional Outbox Pattern, Quầy POS một cửa (Quét QR đổi vé + Upsell F&B + In vé nhiệt 80mm chống in trùng), sơ đồ ghế 2D đa hành lang không cho phép ghế đôi (ColSpan=2) lấn hành lang.
  5. Triển khai: Docker Compose + NGINX Reverse Proxy + Cloudflare Origin CA SSL. Toàn bộ biểu đồ trong tài liệu phải vẽ bằng Mermaid.
- Ratification Status: APPROVED & RATIFIED
-->

# CineManagement Project Constitution (Hiến Pháp Dự Án)

## 1. Five Core Inviolable Principles (5 Nguyên Tắc Bất Biến Cốt Lõi)

### I. Kiến Trúc Backend: Modular Monolith trên ASP.NET Core 10 (.NET 10) (NON-NEGOTIABLE)
- Hệ thống áp dụng nghiêm ngặt kiến trúc **Modular Monolith** trên nền tảng .NET 10 (C# 13 LTS).
- Toàn bộ nghiệp vụ được phân rã thành các Bounded Contexts (Module độc lập):
  1. `Cinemas`: Quản lý chuỗi cụm rạp, chi nhánh, phòng chiếu, ma trận ghế.
  2. `Catalog`: Quản lý kho phim dùng chung, thể loại, đạo diễn, diễn viên, thời lượng, bảng giá vé.
  3. `Ticketing`: Quản lý lịch chiếu, đặt và giữ ghế thời gian thực, khóa tương tranh suất chiếu.
  4. `Ordering`: Xử lý đơn hàng, giỏ hàng vé & F&B combo, lưu vết giao dịch.
  5. `Payments`: Cổng thanh toán VietQR / thẻ ngân hàng, xác nhận giao dịch.
  6. `Shared.Storage`: Lớp lưu trữ đối tượng đám mây độc lập dùng chung.
- Giao tiếp liên module bắt buộc thông qua Public Service Interfaces, DTOs hoặc Domain Events; tuyệt đối cấm Circular Dependencies và cấm truy vấn chéo trực tiếp DbContext của module khác.

### II. Database: Pure MariaDB/MySQL chuẩn 3NF (Tuyệt Đối Không SQLite Fallback) (NON-NEGOTIABLE)
- Cơ sở dữ liệu bắt buộc là MariaDB 10.11+ hoặc MySQL 8.0+ chuẩn 3NF với đúng 12 bảng nghiệp vụ quan hệ. Tuyệt đối không dùng SQLite fallback trong bất kỳ môi trường nào (kể cả Development hay Testing).
- Toàn bộ giao dịch ghi dữ liệu hữu hạn (đặt/giữ ghế, thanh toán, in vé) bắt buộc chạy trong Database Transaction với `IsolationLevel.Serializable`.
- Thực thể `ShowtimeSeat` bắt buộc tích hợp cơ chế Optimistic Concurrency Control thông qua token `[ConcurrencyCheck] public int Version { get; set; }` để loại trừ 100% rủi ro Double-Booking.

### III. Lưu Trữ Đám Mây Chuẩn CLO1: Cloudflare R2 / MinIO qua AWSSDK.S3
- Ứng dụng web hoàn toàn Stateless để sẵn sàng mở rộng ngang (Horizontal Scaling) qua multi-container Docker.
- Toàn bộ tài nguyên truyền thông (Poster phim, Banner, ảnh mã QR Code của vé) bắt buộc lưu trữ tại Object Storage tương thích S3 (Cloudflare R2 trong Production, MinIO trong Local Development) thông qua `AWSSDK.S3` và interface `IStorageService`.
- Tuyệt đối cấm ghi file cục bộ vào `wwwroot/uploads` trong luồng vận hành chính thức.

### IV. Bộ Kỹ Thuật Nghiệp Vụ Bắt Buộc (Mandatory Core Techniques)
1. **Real-time đặt ghế bằng SignalR WebSockets**: Trạng thái ghế (`Available`, `Held`, `Reserved`) được phát sóng tức thì đến tất cả máy khách xem cùng suất chiếu (`showtime_{id}`) với độ trễ < 50ms mà không cần F5 tải lại trang.
2. **Gửi email vé tin cậy bằng Transactional Outbox Pattern**: Không gọi dịch vụ email bên thứ ba trực tiếp trong database transaction. Bản ghi sự kiện gửi vé được lưu nguyên tử cùng đơn vé vào bảng `outboxmessages`; tiến trình nền độc lập `OutboxMessageWorker` quét và gửi mail kèm retry 3 lần theo thuật toán Exponential Backoff (chuẩn At-Least-Once Delivery).
3. **Quầy POS Một Cửa (Box Office Single-Counter)**: Gộp luồng Quét camera QR đổi vé (`html5-qrcode`) + Bán chéo thêm bắp nước (Upsell F&B) + In cuộn vé nhiệt 80mm rời từng ghế vào chung một màn hình thao tác 10 giây; cờ `IsPrinted Guard` bắt buộc kiểm tra để chặn đứng 100% hành vi in trùng hoặc sử dụng vé lậu.
4. **Sơ đồ ghế 2D đa hành lang & Thuật toán chặn tràn ghế đôi (Aisle Guard Rule)**: Ghế đôi Sweetbox (`ColSpan = 2`) tuyệt đối không được lấn sang cột hành lang (`AisleCols`) hoặc tràn ra khỏi biên phòng chiếu (`TotalCols`).

### V. Hạ Tầng Triển Khai & Chuẩn Hóa Tài Liệu (Deployment & Documentation Standards)
- **Hạ tầng**: Đóng gói hoàn chỉnh trong Docker Compose đa container (App Nodes + MariaDB Database + NGINX Reverse Proxy) kết hợp Cloudflare Edge SSL / Origin CA Certificates bảo mật TLS 1.3 nghiêm ngặt trên Cổng 443.
- **Biểu đồ trong tài liệu**: Toàn bộ sơ đồ kiến trúc, luồng Use Case, ERD quan hệ thực thể, và Sequence Diagrams trong mọi tài liệu kỹ thuật bắt buộc phải được vẽ bằng cú pháp **Mermaid chuẩn**, đảm bảo khả năng render trực tiếp trên GitHub và các công cụ Markdown Viewer.

## 2. Phân Quyền RBAC & Chính Sách Bảo Mật

1. **Role-Based Access Control (RBAC)**: Phân tách quyền lực nghiêm ngặt giữa 3 Actor:
   - `Customer`: Tra cứu phim, đặt và giữ ghế realtime, mua combo F&B, thanh toán VietQR, nhận vé QR qua email.
   - `Staff`: Đăng nhập phân hệ `/Counter` quầy vé của chi nhánh được gán; quét QR đổi vé, upsell F&B, in vé nhiệt K80 80mm, bán vé Walk-in; bị chặn HTTP 403 Forbidden khỏi `/Admin`.
   - `Admin`: Toàn quyền quản trị chuỗi cụm rạp, dùng Visual Seat Layout Builder cấu hình sơ đồ ghế đa hành lang, quản lý phim, suất chiếu, giá vé, tài khoản nhân viên và báo cáo doanh thu.
2. **Bảo mật Anti-CSRF**: Mọi yêu cầu HTTP thay đổi trạng thái (POST, PUT, DELETE) bắt buộc xác thực token `[ValidateAntiForgeryToken]`.

---
**Phiên bản**: 2.1.0 | **Phê chuẩn**: 2026-11-03 | **Cập nhật SDD Spec-Kit**: 2026-11-05
