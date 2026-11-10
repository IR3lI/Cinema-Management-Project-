# Implementation Plan: Nâng cấp Toàn diện Codebase chuẩn CLO1

**Branch**: `001-cinema-chain-booking` | **Date**: 2026-11-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-cinema-chain-booking/spec.md` and ratifying Constitution standards (`.specify/memory/constitution.md`).

---

## Summary

Triển khai kế hoạch thiết kế kỹ thuật (Technical Implementation Plan) để nâng cấp toàn diện codebase CineManagement đáp ứng chuẩn chuẩn mực CLO1 thông qua 4 trụ cột kiến trúc cốt lõi:
1. **Tái cấu trúc thư mục/namespace Modular Monolith**: Phân rã hệ thống thành 6 Bounded Contexts độc lập (`Cinemas`, `Catalog`, `Ticketing`, `Ordering`, `Payments`, `Shared`) với ranh giới nghiệp vụ tường minh, triệt tiêu phụ thuộc vòng.
2. **Lưu trữ Cloud-Native (Cloudflare R2 / MinIO)**: Xây dựng module lưu trữ hoàn toàn phi trạng thái (Stateless) thông qua giao diện `IStorageService` và `S3StorageService` sử dụng `AWSSDK.S3`, chuyển toàn bộ ảnh poster, banner, mã QR vé lên S3 Object Storage và cấm ghi đĩa cục bộ `wwwroot/uploads`.
3. **Thuật toán Aisle Guard & Studio Layout Builder**: Phát triển thuật toán phòng vệ hành lang ngăn chặn triệt để ghế đôi Sweetbox (`ColSpan = 2`) đè lên cột lối đi hoặc vượt biên phòng chiếu; nâng cấp giao diện Layout Builder cho Admin chọn nhiều hành lang ngang/dọc, gán loại ghế theo hàng và tự động đánh số ghế chuẩn xác.
4. **Hạ tầng Container hóa & Cloudflare SSL**: Cấu hình Docker Compose đa container (`nginx`, `app`, `mariadb`, `minio`, `minio-init`), NGINX Reverse Proxy hỗ trợ WebSocket cho SignalR Hub, và mã hóa Cloudflare Origin CA TLS 1.3 Strict Mode.

---

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (ASP.NET Core 10 Web App)

**Primary Dependencies**:
- `AWSSDK.S3` (3.7+) — Thư viện giao tiếp S3 API với Cloudflare R2 và MinIO
- `MySql.EntityFrameworkCore` (10.0.9) / `Pomelo.EntityFrameworkCore.MySql` — ORM kết nối MariaDB InnoDB
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (10.0.2) — Quản lý xác thực người dùng & RBAC
- `Microsoft.AspNetCore.SignalR.Core` — Đồng bộ trạng thái ghế thời gian thực qua WebSockets
- `QRCoder` (1.8.0) — Sinh mã QR vé điện tử chuẩn Napas/ISO

**Storage**:
- Cơ sở dữ liệu quan hệ: MariaDB 10.11 / MySQL 8.0 InnoDB (Relational 3NF Schema)
- Lưu trữ tệp tin đối tượng: Cloudflare R2 (Production) / MinIO (Development) tương thích S3 API

**Testing**: xUnit, Moq, `Microsoft.AspNetCore.Mvc.Testing` (Integration Test cho API và SignalR Hub)

**Target Platform**: Linux Docker Containers (Alpine / Debian-based), NGINX 1.25+, Cloudflare Edge Network

**Project Type**: Web Application (Modular Monolith with Razor Views + Web APIs + SignalR Hub)

**Performance Goals**:
- Độ trễ đồng bộ ghế qua SignalR WebSockets: $p95 < 50\text{ms}$
- Thời gian phản hồi API (Booking / Layout): $p95 < 200\text{ms}$
- Chịu tải đồng thời 500 khách tranh ghế cùng suất chiếu với 0 vụ bán trùng ghế (Double-booking = 0)

**Constraints**:
- Kiểm soát tương tranh kép: `IsolationLevel.Serializable` cho giao dịch ghi + `[ConcurrencyCheck] public int Version` trên `ShowtimeSeat`
- Phi trạng thái (Stateless Nodes): Cấm ghi tệp tin vào đĩa cục bộ `wwwroot/uploads`
- Bảo mật: 100% endpoint thay đổi trạng thái (POST, PUT, DELETE) bắt buộc token Anti-CSRF (`[ValidateAntiForgeryToken]`)
- Mô hình dữ liệu: Cố định 12 bảng thực thể theo đặc tả `spec.md`

**Scale/Scope**: Chuỗi rạp chiếu phim đa chi nhánh, quản trị tập trung hàng chục cụm rạp, hàng trăm phòng chiếu, hàng nghìn ghế 2D, hàng nghìn giao dịch vé mỗi ngày.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Nguyên tắc / Tiêu chuẩn hiến pháp | Yêu cầu | Đánh giá thiết kế | Trạng thái |
| :--- | :--- | :--- | :---: |
| **Principle I: Modular Monolith (CLO1)** | Phân chia module rõ ràng: Cinemas, Catalog, Ticketing, Ordering, Payments, Shared. Giao tiếp qua interface / DTOs. | Thiết kế cấu trúc thư mục `Modules/` phân rã 6 bounded contexts, cấm phụ thuộc chéo vòng. | **PASS** |
| **Principle II: Dual-Concurrency (CLO2)** | Giao dịch `IsolationLevel.Serializable` + Token phiên bản `[ConcurrencyCheck] Version` trên `ShowtimeSeat`. | Thiết kế xác nhận thanh toán và giữ ghế đảm bảo 0 vụ double-booking. | **PASS** |
| **Principle III: Real-Time SignalR** | Đồng bộ trạng thái ghế thời gian thực qua WebSockets với độ trễ < 50ms tới group `showtime_{id}`. | NGINX cấu hình `Upgrade` / `Connection` proxying, vô hiệu hóa buffer cho `/seatBookingHub`. | **PASS** |
| **Principle IV: Transactional Outbox** | Không gọi dịch vụ ngoài trong DB transaction. Lưu `outboxmessages` nguyên tử, retry tối đa 3 lần. | Giữ nguyên kiến trúc `OutboxMessageWorker` đọc bảng `outboxmessages` với exponential backoff. | **PASS** |
| **Principle V: Stateless Storage** | Node web phi trạng thái. Toàn bộ ảnh poster, mã QR lưu trên S3 (Cloudflare R2 / MinIO). Cấm ghi `wwwroot/uploads`. | Xây dựng `IStorageService` + `S3StorageService` với `AWSSDK.S3`, thay thế `FileStorageService` cũ. | **PASS** |
| **Business Rule: Aisle Guard Rule** | Ghế đôi Sweetbox (`ColSpan = 2`) không được đè lên cột hành lang dọc (`AisleCols`) hoặc vượt biên. | Thuật toán Aisle Guard kiểm tra cả cột $c$ và $c+1$, từ chối lưu và hiển thị cảnh báo lỗi chi tiết. | **PASS** |
| **Business Rule: IsPrinted Guard Rule** | Khóa in vé vĩnh viễn sau lần in nhiệt đầu tiên, nghiêm cấm in lại. | Vé sau khi in chuyển `IsPrinted = true`, quầy quét lại bị chặn hoàn toàn. | **PASS** |
| **Security: Anti-CSRF & TLS 1.3** | Mọi POST endpoint đều có Anti-CSRF token; mã hóa Cloudflare Origin CA trên Port 443. | Bổ sung `[ValidateAntiForgeryToken]` cho `SaveSeatLayout`; NGINX cấu hình Cloudflare Origin CA. | **PASS** |

---

## Project Structure

### Documentation (this feature)

```text
specs/001-cinema-chain-booking/
├── plan.md              # Kế hoạch triển khai kỹ thuật tổng thể (CLO1)
├── research.md          # Nghiên cứu kiến trúc & các quyết định kỹ thuật
├── data-model.md        # Đặc tả mô hình dữ liệu 12 bảng & Bounded Contexts
├── quickstart.md        # Kịch bản chạy & kiểm thử nghiệm thu 4 trụ cột
├── contracts/           # Các bản giao ước API & hạ tầng
│   ├── storage-service.contract.md        # Giao ước module lưu trữ S3 (R2/MinIO)
│   ├── seat-layout.contract.md            # Giao ước API Visual Layout & Aisle Guard
│   └── docker-infrastructure.contract.md  # Giao ước Docker Compose, NGINX & SSL
└── checklists/
    └── requirements.md  # Bảng kiểm định chất lượng yêu cầu (16/16 Passed)
```

### Source Code Structure (Modular Monolith)

```text
CineManagement/
├── Modules/
│   ├── Cinemas/                           # Bounded Context: Quản lý Cụm rạp & Phòng chiếu
│   │   ├── Domain/                        # Thực thể nghiệp vụ
│   │   │   ├── Cinema.cs
│   │   │   ├── ScreeningRoom.cs           # Chứa TotalRows, TotalCols, AisleRows, AisleCols
│   │   │   ├── Seat.cs                    # Chứa GridRow, GridCol, RowLabel, SeatNumber
│   │   │   └── SeatType.cs                # Chứa ColSpan (1 hoặc 2), Surcharge, ColorCode
│   │   ├── DTOs/
│   │   │   ├── SaveSeatLayoutRequest.cs   # DTO lưu sơ đồ phòng chiếu từ Builder
│   │   │   └── SeatLayoutItemDto.cs
│   │   ├── Services/
│   │   │   ├── ICinemaService.cs
│   │   │   ├── IScreeningRoomService.cs
│   │   │   ├── ISeatLayoutService.cs      # Thực thi Thuật toán Aisle Guard & Auto Numbering
│   │   │   └── SeatLayoutService.cs
│   │   └── Controllers/
│   │       ├── CinemaController.cs
│   │       └── ScreeningRoomController.cs # Quản trị phòng chiếu & Studio LayoutBuilder
│   │
│   ├── Catalog/                           # Bounded Context: Kho Phim & Bắp Nước F&B
│   │   ├── Domain/
│   │   │   ├── Movie.cs
│   │   │   ├── Genre.cs
│   │   │   └── FoodItem.cs                # Danh mục F&B Combo (IsActive menu toggle)
│   │   ├── Services/
│   │   │   ├── IMovieService.cs
│   │   │   └── IFoodService.cs
│   │   └── Controllers/
│   │       ├── MovieController.cs
│   │       └── FoodController.cs
│   │
│   ├── Ticketing/                         # Bounded Context: Suất Chiếu, Giữ Ghế & Phát Hành Vé
│   │   ├── Domain/
│   │   │   ├── Showtime.cs
│   │   │   ├── ShowtimeSeat.cs            # Status (Available/Held/Reserved), Version token
│   │   │   └── Ticket.cs                  # QrCodeData, IsPrinted, PrintedAt
│   │   ├── Hubs/
│   │   │   └── SeatBookingHub.cs          # SignalR WebSockets thời gian thực
│   │   ├── BackgroundWorkers/
│   │   │   └── SeatHoldTimeoutWorker.cs   # Quét giải phóng ghế hết hạn 5 phút
│   │   ├── Services/
│   │   │   ├── IShowtimeService.cs
│   │   │   ├── ISeatLockManager.cs        # Dual-concurrency control (Optimistic + Serializable)
│   │   │   └── ITicketService.cs
│   │   └── Controllers/
│   │       ├── ShowtimeController.cs
│   │       └── CounterController.cs       # Màn hình POS quầy vé (quét QR, in vé nhiệt K80)
│   │
│   ├── Ordering/                          # Bounded Context: Quản Lý Đơn Hàng
│   │   ├── Domain/
│   │   │   ├── Booking.cs                 # Đơn đặt vé online / Walk-in
│   │   │   └── BookingConcession.cs       # Món ăn bắp nước trong đơn
│   │   ├── Services/
│   │   │   ├── IBookingService.cs
│   │   │   └── BookingService.cs
│   │   └── Controllers/
│   │       └── BookingController.cs
│   │
│   ├── Payments/                          # Bounded Context: Thanh Toán VietQR
│   │   ├── Services/
│   │   │   ├── IPaymentService.cs         # Sinh mã VietQR Napas 247 động & xác nhận tức thì
│   │   │   └── PaymentService.cs
│   │   └── Controllers/
│   │       └── PaymentController.cs
│   │
│   ├── Shared/                            # Bounded Context: Hạ Tầng Dùng Chung
│   │   ├── Storage/                       # Module lưu trữ Cloud-Native (S3 API)
│   │   │   ├── IStorageService.cs         # Giao diện trừu tượng lưu trữ
│   │   │   ├── S3StorageService.cs        # Triển khai AWSSDK.S3 cho R2 / MinIO
│   │   │   └── StorageOptions.cs          # Cấu hình Bucket, ServiceUrl, CDN URL
│   │   ├── Outbox/                        # Transactional Outbox Pattern
│   │   │   ├── OutboxMessage.cs
│   │   │   ├── OutboxMessageWorker.cs     # Worker gửi email vé nền độc lập
│   │   │   └── IEmailService.cs
│   │   └── Common/
│   │       ├── VietnameseDateHelper.cs
│   │       └── Enums.cs
│   │
│   └── Users/                             # Bounded Context: Phân Quyền & Người Dùng
│       ├── Domain/
│       │   └── ApplicationUser.cs         # Phân quyền Customer, Staff, Admin
│       └── Controllers/
│           ├── AccountController.cs
│           ├── StaffController.cs
│           └── AdminController.cs
│
├── Data/                                  # Tầng Dữ Liệu Tập Trung (Single DbContext)
│   ├── ApplicationDbContext.cs            # Khai báo 12 DbSets, cấu hình quan hệ Fluent API
│   └── SeedData.cs                        # Dữ liệu mẫu khởi tạo rạp, phim, loại ghế
│
├── Views/                                 # Giao diện Razor Views
│   ├── ScreeningRoom/
│   │   └── LayoutBuilder.cshtml           # Studio thiết kế sơ đồ ghế 2D trực quan
│   ├── Counter/                           # Giao diện quầy vé POS in nhiệt K80
│   └── Shared/
│
├── nginx/                                 # Cấu hình Reverse Proxy & SSL
│   ├── nginx.conf
│   ├── conf.d/
│   │   └── default.conf                   # Cấu hình SSL Strict & SignalR WebSockets
│   └── certs/
│       ├── origin.crt                     # Cloudflare Origin CA Certificate
│       └── origin.key                     # Cloudflare Origin CA Private Key
│
├── Dockerfile                             # Multi-stage build .NET 10 Web App
├── docker-compose.yml                     # Cụm container: NGINX, App, MariaDB, MinIO
└── CineManagement.csproj                  # Cấu hình dự án & gói thư viện NuGet
```

**Structure Decision**: Lựa chọn mô hình **Single-Project Modular Monolith with Feature Folders (`Modules/*`)**. Toàn bộ mã nguồn nằm trong một dự án web duy nhất nhưng phân tách tuyệt đối các ranh giới miền (Domain, DTOs, Services, Controllers). Giải pháp này vừa đảm bảo tính đóng gói của mô hình Monolith (dễ biên dịch, triển khai một container duy nhất), vừa đáp ứng trọn vẹn tiêu chuẩn phân tách trách nhiệm cao của CLO1.

---

## Complexity Tracking

| Thành phần kỹ thuật | Lý do bắt buộc sử dụng | Phương án đơn giản hơn bị từ chối vì |
| :--- | :--- | :--- |
| **Gói `AWSSDK.S3` thay vì ghi ổ đĩa cục bộ** | Đảm bảo tính phi trạng thái (Stateless Node) của web application, cho phép chạy cụm container nhiều node phía sau NGINX mà không bị mất hoặc lệch tệp media. | Ghi vào `wwwroot/uploads` vi phạm trực tiếp Hiến pháp dự án (Principle V), tệp sẽ bị xóa sạch khi container recreate hoặc deploy bản mới. |
| **Modular Monolith thay vì Microservices** | Giữ trọn vẹn sự đơn giản của một triển khai nguyên khối (Single Deployment Unit), không tốn chi phí quản lý mạng lưới microservices, service mesh hoặc distributed tracing. | Microservices làm tăng độ phức tạp quá mức cần thiết đối với quy mô đồ án sinh viên và gây khó khăn cho việc bảo vệ đồ án. |
| **Cloudflare Origin CA thay vì Let's Encrypt Certbot** | Tích hợp hoàn hảo với Cloudflare Edge Proxy (Chế độ Full Strict SSL), chứng chỉ có hiệu lực 15 năm, không bao giờ lo hết hạn hoặc lỗi ACME Challenge. | Certbot tự động renew thường xuyên bị chặn hoặc lỗi xác thực HTTP-01 khi tên miền nằm sau mạng lưới CDN/WAF của Cloudflare. |
