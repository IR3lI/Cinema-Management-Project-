# Tasks: Hệ thống Quản lý và Đặt vé cho Hãng rạp chiếu phim đa chi nhánh (CLO1 Edition)

**Input**: Design documents from `specs/001-cinema-chain-booking/` (`spec.md`, `plan.md`, `data-model.md`, `contracts/`, `research.md`, `quickstart.md`)  
**Prerequisites**: `spec.md` (required), `plan.md` (required), `constitution.md` (governing principles)

## Format: `[TaskID] [P?] [Story?] Description with exact file path`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User Story tag (`[US1]`, `[US2]`, `[US3]`, `[US4]`, `[US5]`, `[US6]`)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure & Modular Monolith Folders)

**Purpose**: Thiết lập nền tảng dự án, cấu trúc thư mục Modular Monolith và cài đặt các thư viện phụ thuộc bắt buộc.

- [X] T001 Tái cấu trúc thư mục dự án theo chuẩn Modular Monolith tạo các thư mục Bounded Contexts trong `Modules/Cinemas/`, `Modules/Catalog/`, `Modules/Ticketing/`, `Modules/Ordering/`, `Modules/Payments/`, `Modules/Shared/`
- [X] T002 Cài đặt gói thư viện `AWSSDK.S3` (version >= 3.7) vào `CineManagement.csproj` để phục vụ kết nối Cloudflare R2 / MinIO
- [X] T003 [P] Tạo cấu hình Dockerfile multi-stage build cho ASP.NET Core 10 Web App trong `Dockerfile`
- [X] T004 [P] Tạo cấu hình cụm container đa tầng (`nginx`, `app`, `mariadb`, `minio`, `minio-init`) trong `docker-compose.yml`
- [X] T005 [P] Tạo tệp cấu hình NGINX Reverse Proxy hỗ trợ WebSocket cho SignalR Hub và Cloudflare Origin CA TLS 1.3 trong `nginx/conf.d/default.conf`

---

## Phase 2: Foundational (Core Services & Blocking Infrastructure)

**Purpose**: Xây dựng hạ tầng dùng chung và các dịch vụ chặn (blocking prerequisites) trước khi phát triển User Stories.

- [X] T006 Cập nhật entity `ScreeningRoom` bổ sung hai trường `AisleRows` (string?) và `AisleCols` (string?) trong `Modules/Cinemas/Domain/ScreeningRoom.cs`
- [X] T007 Xây dựng giao diện trừu tượng `IStorageService` và lớp `StorageOptions` trong `Modules/Shared/Storage/IStorageService.cs` và `Modules/Shared/Storage/StorageOptions.cs`
- [X] T008 Triển khai `S3StorageService` sử dụng `AmazonS3Client` từ `AWSSDK.S3` tương thích Cloudflare R2 và MinIO trong `Modules/Shared/Storage/S3StorageService.cs`
- [X] T009 Đăng ký DI cho `IAmazonS3` và `IStorageService` trong `Program.cs`, thay thế hoàn toàn `FileStorageService` cũ
- [X] T010 Xây dựng thuật toán phòng vệ hành lang Aisle Guard Core và tự động đánh số ghế trong `Modules/Cinemas/Services/SeatLayoutService.cs`

---

## Phase 3: User Story 1 - Khách hàng đặt vé trực tuyến không bị trùng ghế (Priority: P1) 🎯 MVP

**Goal**: Khách hàng đã đăng nhập chọn suất, giữ ghế thời gian thực (5 phút), chọn Combo F&B và thanh toán VietQR động thành công tức thì không trùng ghế.  
**Independent Test**: Mở cùng một suất chiếu trên 2 tài khoản khác nhau, cùng bấm giữ 1 ghế; kiểm chứng chỉ 1 người giữ thành công, người kia bị từ chối và SignalR đẩy trạng thái ngay.

- [X] T011 [US1] Áp dụng ràng buộc bắt buộc đăng nhập tài khoản trước khi truy cập màn hình chọn ghế trong `Controllers/BookingController.cs` và `Hubs/SeatBookingHub.cs`
- [X] T012 [P] [US1] Xác thực kiểm soát tương tranh kép (`IsolationLevel.Serializable` + `[ConcurrencyCheck] Version`) trong `Services/BookingService.cs`
- [X] T013 [P] [US1] Đảm bảo SignalR WebSockets đẩy sự kiện ghế chuyển sang "Đang giữ" và "Đã bán" trong `Hubs/SeatBookingHub.cs`
- [X] T014 [US1] Tích hợp dịch vụ sinh mã VietQR Napas 247 động và xử lý xác nhận thanh toán tức thì 100% trong `Services/PaymentService.cs`
- [X] T015 [US1] Đảm bảo `SeatHoldTimeoutWorker` tự động quét và giải phóng ghế quá hạn 5 phút trong `Services/SeatHoldTimeoutWorker.cs`

---

## Phase 4: User Story 2 - Khách hàng luôn nhận được vé điện tử sau khi thanh toán (Priority: P1)

**Goal**: Đơn hàng thanh toán thành công được ghi nhận cùng lúc với bản ghi `OutboxMessage` trong một giao dịch nguyên tử; worker nền gửi email vé tự động thử lại tối đa 3 lần.  
**Independent Test**: Ngắt kết nối mạng tới SMTP email, thực hiện đặt vé; xác nhận đơn thành công ngay; khôi phục mạng và kiểm chứng email vé kèm QR được gửi tự động.

- [X] T016 [US2] Ghi nhận `Booking`, `Ticket` và `OutboxMessage` trong cùng một database transaction nguyên tử trong `Services/BookingService.cs`
- [X] T017 [P] [US2] Triển khai worker nền `OutboxMessageWorker` quét tin nhắn `Pending`, gửi email vé kèm QR và thử lại tối đa 3 lần trong `Services/OutboxMessageWorker.cs`
- [X] T018 [US2] Xây dựng trang "Vé của tôi" cho phép khách hàng tra cứu toàn bộ vé đã mua kèm mã QR trong `Controllers/AccountController.cs` và `Views/Account/MyTickets.cshtml`
- [X] T019 [US2] Chặn quyền tự hủy vé, đổi suất chiếu hoặc yêu cầu hoàn tiền trực tuyến của khách hàng trong `Controllers/BookingController.cs`

---

## Phase 5: User Story 3 - Quầy vé quét QR, in vé nhiệt 80mm một chạm và khóa vĩnh viễn (Priority: P2)

**Goal**: Nhân viên quầy quét mã QR từ email khách, hệ thống hiển thị nút "In vé", bấm in vé nhiệt K80 và đánh dấu `IsPrinted = true` vĩnh viễn không in lại.  
**Independent Test**: Quét mã QR hợp lệ -> hiện nút "In vé" -> bấm in -> in thành công. Quét lại chính mã đó: hiển thị cảnh báo đỏ và khóa hoàn toàn chức năng in.

- [X] T020 [US3] Tối ưu giao diện quầy POS thành luồng một chạm: quét QR -> hiện nút "In vé" -> xuất vé nhiệt K80 trong `Views/Counter/Index.cshtml`
- [X] T021 [US3] Kiểm tra cờ `IsPrinted` trong transaction `Serializable`, khóa vĩnh viễn và từ chối in lại lần 2 dưới mọi hình thức trong `Controllers/CounterController.cs`
- [X] T022 [P] [US3] Hỗ trợ mời mua thêm Combo F&B (Upsell) tại quầy và in phiếu nhận F&B riêng trong `Controllers/CounterController.cs`
- [X] T023 [US3] Khóa phân quyền Staff theo chi nhánh, từ chối quét mã QR thuộc chi nhánh khác trong `Controllers/CounterController.cs`

---

## Phase 6: User Story 4 - Nhân viên bán vé cho khách vãng lai (Walk-in) (Priority: P2)

**Goal**: Nhân viên bán vé trực tiếp cho khách tại quầy trên cùng sơ đồ ghế thời gian thực, thu tiền và in vé nhiệt ngay.  
**Independent Test**: Bán ghế tại quầy trong khi khách online đang xem cùng suất chiếu; khách online thấy ghế chuyển "Đã bán" ngay lập tức.

- [X] T024 [US4] Tích hợp bán vé Walk-in trên cùng sơ đồ ghế SignalR với kênh Online trong `Controllers/CounterController.cs`
- [X] T025 [US4] Thiết lập đơn hàng Walk-in cho phép `UserId = NULL` nhưng bắt buộc lưu `SoldByStaffId` trong `Controllers/CounterController.cs`
- [X] T026 [US4] Xuất vé nhiệt 80mm ngay sau khi hoàn tất giao dịch Walk-in tại quầy trong `Controllers/CounterController.cs`

---

## Phase 7: User Story 5 - Quản lý rạp và thiết kế sơ đồ 2D đa hành lang (Priority: P2)

**Goal**: Admin thiết kế sơ đồ ghế 2D trực quan, chọn nhiều hành lang dọc/ngang, gán hạng ghế theo hàng, thuật toán Aisle Guard chặn Sweetbox lấn hành lang.  
**Independent Test**: Đặt hành lang Cột 5, thử đặt ghế đôi tại Cột 4-5: hệ thống từ chối ngay; đặt ghế đôi tại Cột 1-2: hệ thống chấp nhận và tự động đánh số ghế.

- [X] T027 [US5] Thêm nút chọn nhanh hành lang dọc/ngang và công cụ "Gán toàn bộ hàng thành VIP/Couple/Standard" trong `Views/ScreeningRoom/LayoutBuilder.cshtml`
- [X] T028 [US5] Tích hợp thuật toán Aisle Guard phía client (JavaScript) ngăn chặn ghế đôi đè lên hành lang trong `Views/ScreeningRoom/LayoutBuilder.cshtml`
- [X] T029 [US5] Triển khai kiểm tra Aisle Guard và lưu `AisleRows`, `AisleCols` phía Server trong `Controllers/ScreeningRoomController.cs`
- [X] T030 [US5] Chặn xóa hoặc thay đổi vị trí các ghế đã phát sinh vé ở các suất chiếu tương lai trong `Controllers/ScreeningRoomController.cs`

---

## Phase 8: User Story 6 - Quản lý rạp xem báo cáo doanh thu (Priority: P3)

**Goal**: Admin xem bảng điều khiển doanh thu hợp nhất theo cụm rạp, theo phim, theo kênh bán (Online vs Walk-in) và tỷ lệ lấp đầy ghế.  
**Independent Test**: Tạo một số đơn Online và Walk-in ở 2 chi nhánh, mở báo cáo và đối chiếu số liệu với tổng đơn hàng thực tế.

- [X] T031 [US6] Tổng hợp doanh thu vé, F&B tách bạch theo từng chi nhánh và theo kênh bán trong `Services/ReportService.cs`
- [X] T032 [US6] Tính toán tỷ lệ lấp đầy ghế (Seat Occupancy Rate) theo suất chiếu trong `Services/ReportService.cs`
- [X] T033 [US6] Hiển thị báo cáo trực quan cho Admin và chặn quyền truy cập của Staff trong `Controllers/AdminController.cs` và `Views/Admin/Report.cshtml`

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Hoàn thiện bảo mật, kiểm thử nghiệm thu và dọn dẹp mã nguồn.

- [X] T034 [P] Loại bỏ toàn bộ `[IgnoreAntiforgeryToken]` trên các HTTP POST endpoints và bổ sung token Anti-CSRF trong `Controllers/ScreeningRoomController.cs` và `Controllers/CounterController.cs`
- [X] T035 [P] Cập nhật `appsettings.json` bổ sung section cấu hình `Storage` cho S3/MinIO
- [X] T036 Thực hiện kịch bản nghiệm thu 4 trụ cột kỹ thuật theo hướng dẫn `specs/001-cinema-chain-booking/quickstart.md`

---

## Phase 10: Convergence

**Purpose**: Bổ sung các công việc còn thiếu và khắc phục các vi phạm kiến trúc sau khi đối chiếu toàn diện mã nguồn hiện tại với spec.md, plan.md và constitution.md.

- [X] T037 [CRITICAL] Tái cấu trúc phân rã thư mục và namespace mã nguồn theo chuẩn Modular Monolith vào các thư mục Bounded Contexts trong `Modules/Cinemas/`, `Modules/Catalog/`, `Modules/Ticketing/`, `Modules/Ordering/`, `Modules/Payments/`, `Modules/Shared/` per Constitution I (missing)
- [X] T038 [CRITICAL] Cài đặt `AWSSDK.S3`, triển khai `IStorageService` và `S3StorageService` cho Cloudflare R2 / MinIO, loại bỏ hoàn toàn cơ chế ghi đĩa cục bộ trong `Services/FileStorageService.cs` per Constitution V (contradicts)
- [X] T039 [CRITICAL] Loại bỏ `[IgnoreAntiforgeryToken]` và bổ sung xác thực `[ValidateAntiForgeryToken]` trên các endpoint `SaveSeatLayout`, `ConfirmPrintAndUpsell`, `ConfirmWalkInOrder` trong `Controllers/ScreeningRoomController.cs` và `Controllers/CounterController.cs` per Constitution Security 2 (contradicts)
- [X] T040 [HIGH] Bổ sung hai thuộc tính `AisleRows` và `AisleCols` vào `ScreeningRoom` và cài đặt thuật toán Aisle Guard chặn ghế đôi `ColSpan=2` đè lên hành lang trong `Models/ScreeningRoom.cs` và `Controllers/ScreeningRoomController.cs` per Constitution Rule 1 & FR-010 (partial)
- [X] T041 [HIGH] Nâng cấp giao diện Studio `LayoutBuilder.cshtml` với tính năng click tiêu đề cột bật/tắt hành lang dọc, thanh công cụ gán nhanh hạng ghế theo hàng và cảnh báo Aisle Guard phía client per plan.md & US5 (partial)
- [X] T042 [HIGH] Xây dựng `Dockerfile` multi-stage build, `docker-compose.yml` cụm container (`nginx`, `app`, `mariadb`, `minio`, `minio-init`) và cấu hình NGINX WebSocket proxying kèm Cloudflare SSL trong `nginx/conf.d/default.conf` per plan.md (missing)
- [X] T043 [HIGH] Ràng buộc bắt buộc đăng nhập tài khoản trước khi chọn và giữ ghế trực tuyến qua SignalR trong `Controllers/BookingController.cs` và `Hubs/SeatBookingHub.cs` per FR-001 & Clarification 4 (partial)
