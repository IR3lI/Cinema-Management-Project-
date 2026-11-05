# THIẾT KẾ KIẾN TRÚC HỆ THỐNG (SYSTEM ARCHITECTURE DESIGN)

> **Đề tài**: Hệ thống Quản trị Tập trung Chuỗi Rạp Chiếu Phim (CineManagement)  
> **Tiêu chuẩn áp dụng**: Chuẩn đầu ra **CLO1 & CLO2** – Kiến trúc Modular Monolith, Docker Compose, Cloudflare R2/SSL, SignalR Real-Time, Dual-Concurrency Control & Transactional Outbox  
> **Tài liệu liên quan**: [`01_PRODUCT_BACKLOG.md`](./01_PRODUCT_BACKLOG.md), [`02_YEU_CAU_HE_THONG.md`](./02_YEU_CAU_HE_THONG.md), [`03_SO_DO_PHAN_RA_CHUC_NANG.md`](./03_SO_DO_PHAN_RA_CHUC_NANG.md), [`05_THIET_KE_CO_SO_DU_LIEU.md`](./05_THIET_KE_CO_SO_DU_LIEU.md)

---

## 1. TỔNG QUAN KIẾN TRÚC HỆ THỐNG

Hệ thống **CineManagement** được thiết kế theo mô hình **Modular Monolith chuẩn doanh nghiệp kết hợp Real-time WebSockets Gateway và Asynchronous Background Workers**, đảm bảo khả năng mở rộng linh hoạt cho toàn chuỗi rạp đa chi nhánh:

1. **Core Backend (`CineManagement` - C# .NET 10)**:
   - Áp dụng kiến trúc **Modular Monolith**: Toàn bộ nghiệp vụ quản trị cụm rạp, phòng chiếu, danh mục phim, suất chiếu, đặt vé, quầy POS và thanh toán được chia thành các module nghiệp vụ độc lập (`Cinemas`, `Catalog`, `Ticketing`, `Ordering`, `Payments`), chạy chung trong một tiến trình `.NET` hiệu năng cao.
   - Giữ ranh giới miền (*Domain Boundaries*) chặt chẽ, giao tiếp nội bộ thông qua Service Contracts, DTOs và In-process Events, sẵn sàng tách thành Microservices độc lập trong tương lai mà không làm vỡ kiến trúc.
2. **Cổng Thời Gian Thực Siêu Tốc (`SignalR SeatBookingHub`)**:
   - Sử dụng **ASP.NET Core SignalR Core** duy trì kết nối song công (Full-Duplex WebSockets) với độ trễ $< 50\text{ ms}$.
   - Phân nhóm kết nối theo từng suất chiếu (`showtime_{id}`), tự động đồng bộ hóa trạng thái ghế (`Available -> Held -> Reserved`) tức thì giữa hàng trăm người dùng đang cùng mở sơ đồ phòng chiếu mà không cần tải lại trang.
3. **Phân Hệ Quầy Vé POS "Một Cửa" (Box Office POS Console)**:
   - Giao diện chuyên biệt cho nhân viên rạp (`/Counter`), tích hợp camera quét mã QR vé (`html5-qrcode`), cờ kiểm soát chống in vé lậu lần 2 (`IsPrinted Guard`), máy tính bán kèm bắp nước nhanh (Upsell F&B) và trình xuất cuộn vé nhiệt K80 80mm rời từng ghế theo giao thức ESC/POS.
4. **Hạ Tầng Lưu Trữ & Đám Mây Phân Tán**:
   - **MariaDB 10.11 Server (InnoDB)**: Lưu trữ dữ liệu giao dịch 12 bảng chuẩn hóa 3NF, bảo đảm tính toàn vẹn $ACID$ với mức cô lập `IsolationLevel.Serializable`.
   - **Cloudflare R2 Storage (Tương thích S3 API)**: Lưu trữ đám mây không tốn phí băng thông tải ra (Zero Egress Fees) cho toàn bộ ảnh Poster phim độ phân giải cao và ảnh mã QR vé điện tử.
   - **Cloudflare DNS & Full (Strict) SSL**: Bảo vệ máy chủ VPS đằng sau lớp lá chắn WAF/DDoS, mã hóa toàn bộ lưu lượng bằng chứng chỉ Cloudflare Origin CA.

---

## 2. SƠ ĐỒ KIẾN TRÚC TỔNG THỂ (HIGH-LEVEL ARCHITECTURE DIAGRAM)

Sơ đồ được thiết kế theo mô hình **phân tầng dọc (Vertical Layered Architecture)** chuẩn doanh nghiệp, sắp xếp các khối thành hàng dọc gọn gàng, luồng dữ liệu truyền thẳng từ trên xuống dưới:

```mermaid
flowchart TB
    %% ==========================================
    %% 1. TẦNG NGƯỜI DÙNG & THIẾT BỊ (XẾP HÀNG DỌC)
    %% ==========================================
    subgraph T1 ["TẦNG 1: NGƯỜI DÙNG & GIAO DIỆN (CLIENT TIER)"]
        direction TB
        Client["👤 Khách Hàng (Web / Mobile Browser)"]
        POS["🖥️ Quầy Vé Box Office (Quét QR & In Vé K80)"]
        Admin["⚙️ Quản Trị Viên (Admin Portal Toàn Chuỗi)"]
    end

    %% ==========================================
    %% 2. TẦNG BẢO MẬT & ĐIỀU PHỐI (XẾP HÀNG DỌC)
    %% ==========================================
    subgraph T2 ["TẦNG 2: CỔNG BẢO MẬT & ĐỊNH TUYẾN (GATEWAY TIER)"]
        direction TB
        CF["🛡️ Cloudflare Edge (WAF, Chống DDoS & SSL 1.3)"]
        Nginx["🌐 NGINX Reverse Proxy (Định Tuyến & WebSockets)"]
        CF ==>|"TLS Origin CA"| Nginx
    end

    %% ==========================================
    %% 3. TẦNG MÁY CHỦ XỬ LÝ (XẾP HÀNG DỌC)
    %% ==========================================
    subgraph T3 ["TẦNG 3: MÁY CHỦ TRUNG TÂM (ASP.NET CORE 10 MODULAR MONOLITH)"]
        direction TB
        API["🌐 Web MVC & REST APIs (Tiếp Nhận & Điều Hướng)"]
        SignalR["⚡ SignalR Real-Time Hub (Giữ Ghế Tức Thì &lt; 50ms)"]
        Modules["📦 5 Modules Nghiệp Vụ (Cinemas • Catalog • Ticketing • Ordering • Payments)"]
        Workers["⏳ Background Workers (Nhả Ghế Quá Hạn & Gửi Vé Outbox)"]
        
        API --> SignalR
        SignalR --> Modules
        Modules --> Workers
    end

    %% ==========================================
    %% 4. TẦNG DỮ LIỆU & DỊCH VỤ NGOÀI (XẾP HÀNG DỌC)
    %% ==========================================
    subgraph T4 ["TẦNG 4: CƠ SỞ DỮ LIỆU & DỊCH VỤ ĐÁM MÂY (DATA & SERVICES)"]
        direction TB
        DB[("🗄️ MariaDB 10.11 Server (12 Bảng 3NF, Giao Dịch ACID)")]
        R2[("☁️ Cloudflare R2 / S3 (Lưu Poster & QR Vé)")]
        Ext["🔌 Dịch Vụ Ngoài (VietQR Banking & Google SMTP)"]
    end

    %% ==========================================
    %% LUỒNG TƯƠNG TÁC THẲNG ĐỨNG TỪ TRÊN XUỐNG DƯỚI (HÀNG DỌC)
    %% ==========================================
    T1 ==>|"HTTPS & WebSockets"| CF
    Nginx ==>|"Chuyển Tiếp HTTP:5000 & WS"| API
    Workers ==>|"Giao Dịch ACID / EF Core"| DB
    Workers -.->|"Upload Poster & QR (S3 API)"| R2
    Workers -.->|"REST VietQR & SMTP TLS 587"| Ext

    %% ==========================================
    %% ĐỊNH DẠNG MÀU SẮC ĐẸP MẮT & DỄ NHÌN
    %% ==========================================
    style T1 fill:#f0f9ff,stroke:#0284c7,stroke-width:2px;
    style T2 fill:#faf5ff,stroke:#9333ea,stroke-width:2px;
    style T3 fill:#f0fdf4,stroke:#16a34a,stroke-width:2px;
    style T4 fill:#fffbeb,stroke:#d97706,stroke-width:2px;

    style SignalR fill:#fef08a,stroke:#ca8a04,stroke-width:2px,color:#713f12,font-weight:bold;
    style DB fill:#f1f5f9,stroke:#475569,stroke-width:2px,color:#0f172a;
    style R2 fill:#f1f5f9,stroke:#475569,stroke-width:2px,color:#0f172a;
    style Ext fill:#f1f5f9,stroke:#475569,stroke-width:2px,color:#0f172a;
```

### 2.1. Bảng Đặc Tả Vai Trò Của Từng Thành Phần Trong Hệ Thống

| Tầng kiến trúc | Thành phần | Vai trò kỹ thuật & Trách nhiệm nghiệp vụ |
| :--- | :--- | :--- |
| **Tầng 1: Clients** | **👤 Khách Hàng (Customer)** | Trình duyệt Web/Mobile cho phép người dùng xem phim, lịch chiếu, chọn ghế trực quan trên sơ đồ 2D thời gian thực (CSS Grid), đặt vé và quét mã VietQR chuyển khoản. Duy trì WebSocket với SignalR để nhận cập nhật ghế tức thì ($< 50\text{ ms}$). |
| | **🖥️ Quầy Vé Box Office (Staff POS)** | Giao diện "một cửa" `/Counter` cho nhân viên rạp: Dùng camera (`html5-qrcode`) quét mã QR vé điện tử của khách, kích hoạt cờ chống in vé lậu (`IsPrinted Guard`), Upsell bắp nước và in vé nhiệt rời từng ghế khổ K80 80mm qua giao thức ESC/POS. |
| | **⚙️ Quản Trị Viên (Admin Portal)** | Giao diện dành riêng cho Admin quản lý toàn bộ chuỗi rạp: Cụm rạp chi nhánh (`Cinemas`), phòng chiếu (`ScreeningRooms`), sơ đồ ghế 2D (Visual Seat Studio với quy tắc Aisle Guard), danh mục phim (`Movies`), lập lịch suất chiếu (`Showtimes`) và báo cáo doanh thu. |
| **Tầng 2: Gateway** | **🛡️ Cloudflare Edge** | Tuyến phòng ngự biên: Ẩn IP máy chủ gốc, lọc tấn công DDoS (L3/L4/L7), WAF chặn bot độc hại, mã hóa SSL/TLS 1.3 tới người dùng, và CDN cache tài nguyên tĩnh/poster. Giao tiếp với VPS thông qua chứng chỉ nội bộ **Cloudflare Origin CA Certificate**. |
| | **🌐 NGINX Reverse Proxy** | Cổng tiếp nhận duy nhất trên VPS: Điều hướng lưu lượng vào container backend qua mạng Docker; xử lý nén (Gzip/Brotli) và cấu hình **WebSocket Upgrade Headers** (`Upgrade $http_upgrade; Connection "upgrade";`) để duy trì kênh song công ổn định cho SignalR. |
| **Tầng 3: Backend** | **🌐 Web MVC & REST APIs** | Điểm tiếp nhận request từ Client: Xác thực người dùng (Identity & Cookie Auth), phân quyền 3 Actor (RBAC), kiểm tra tính hợp lệ của dữ liệu (Validation) và điều hướng đến các Module nghiệp vụ tương ứng. |
| | **⚡ SignalR Real-Time Hub** | "Trái tim" thời gian thực của hệ thống: Phân nhóm kết nối theo từng suất chiếu (`showtime_{id}`). Phát sóng sự kiện `SeatStatusChanged` tức thì ($< 50\text{ ms}$) tới toàn bộ client trong nhóm ngay khi có ghế được giữ/nhả/thanh toán, ngăn chặn xung đột giữ ghế ngay tại giao diện. |
| | **📦 5 Bounded Modules** | Cốt lõi nghiệp vụ theo chuẩn **Modular Monolith**: <br/>• `Cinemas`: Quản trị rạp, phòng chiếu, sơ đồ ghế 2D, quy tắc Sweetbox Aisle Guard.<br/>• `Catalog`: Quản lý danh mục phim, độ tuổi, định dạng, lập lịch suất chiếu.<br/>• `Ticketing`: Quản lý `ShowtimeSeats`, kiểm soát tương tranh bằng `Version Concurrency Token`.<br/>• `Ordering`: Quản lý đơn `Bookings`, sinh vé `Tickets`, bán F&B và xử lý POS `/Counter`.<br/>• `Payments`: Xử lý thanh toán VietQR với `IsolationLevel.Serializable` và Transactional Outbox. |
| | **⏳ Background Workers** | Hai tiến trình nền chạy ngầm độc lập:<br/>• `SeatHoldTimeoutWorker`: Định kỳ quét mỗi 30s để tự động giải phóng ghế `Held` đã quá hạn 5 phút về `Available`, bắn tín hiệu SignalR nhả ghế.<br/>• `OutboxMessageWorker`: Thực thi Transactional Outbox Pattern, định kỳ quét bảng `outboxmessages` mỗi 5s để gửi email vé điện tử kèm mã QR qua Google SMTP với cơ chế Retry 3 lần (*At-Least-Once Delivery*). |
| **Tầng 4: Data & Cloud** | **🗄️ MariaDB 10.11 Server** | Cơ sở dữ liệu quan hệ nguồn (Source of Truth): Lưu trữ 12 bảng chuẩn hóa 3NF, bảo đảm tính toàn vẹn giao dịch $ACID$ với Storage Engine InnoDB. |
| | **☁️ Cloudflare R2 / S3** | Hạ tầng lưu trữ đám mây chuẩn S3 API (Zero Egress Fees): Lưu trữ an toàn Poster phim độ phân giải cao và toàn bộ ảnh mã QR vé điện tử, giải phóng máy chủ web khỏi lưu file cục bộ, hỗ trợ mở rộng scale-out dễ dàng. |
| | **🔌 Dịch Vụ Ngoài (External)** | • **VietQR Napas 247**: Sinh mã QR thanh toán động kèm số tài khoản và cú pháp chuyển khoản đơn hàng.<br/>• **Google SMTP Server**: Cổng gửi email vé điện tử tự động qua cổng 587 bảo mật TLS. |

---

## 3. KIẾN TRÚC NỘI BỘ BACKEND C# .NET (MODULAR MONOLITH)

Mỗi module nghiệp vụ trong hệ thống tuân thủ chặt chẽ kiến trúc **Clean Architecture** gồm 4 phân tầng độc lập:

```mermaid
flowchart LR
    subgraph ModuleStructure ["Cấu trúc chuẩn 1 Module (Ví dụ: CineManagement.Modules.Ticketing)"]
        EP["Endpoints Layer (Controllers, SignalR Hubs)"]
        APP["Application Layer (Services, Use Cases, DTOs, Mappings)"]
        DOM["Domain Layer (Entities, Value Objects, Enums, Concurrency Token)"]
        INF["Infrastructure Layer (EF Configurations, Repositories, Workers)"]

        EP --> APP
        APP --> DOM
        INF --> APP
        INF --> DOM
    end

    SharedKernel["CineManagement.SharedKernel (BaseEntity, IStorageService, OutboxMessage)"]
    DOM -.-> SharedKernel
```

### Nguyên tắc giao tiếp giữa các Module:
1. **Cô lập dữ liệu (Database Isolation)**: Mỗi module quản lý tập thực thể (Entities) của riêng mình. Không có module nào được phép trực tiếp sửa đổi `DbSet` nội bộ của module khác mà phải thông qua Interface hợp đồng công khai.
2. **Kiểm soát tương tranh kép (Dual-Concurrency Engine)**:
   * **Pessimistic Locking**: Mọi giao dịch giữ ghế, thanh toán và in vé quầy POS đều mở Database Transaction ở mức `IsolationLevel.Serializable`.
   * **Optimistic Concurrency**: Thuộc tính `[ConcurrencyCheck] public int Version` trên `ShowtimeSeat` tự động tăng sau mỗi thao tác, ngăn chặn tuyệt đối tình trạng bán trùng vé (Double Booking).
3. **Truyền thông tin cậy qua Transactional Outbox Pattern**:
   * Khi thanh toán thành công, sự kiện `TicketEmail` được ghi vào bảng `outboxmessages` trong cùng một Local Transaction của đơn hàng ($ACID$). Tiến trình nền `OutboxMessageWorker` quét ngầm mỗi 5 giây, bảo đảm nguyên lý *At-Least-Once Delivery* kể cả khi mất mạng.

---

## 4. SƠ ĐỒ TUẦN TỰ CÁC LUỒNG NGHIỆP VỤ TRỌNG TÂM (SEQUENCE DIAGRAMS)

### 4.1. Luồng 1: Đặt & Giữ Ghế Real-Time (SignalR WebSockets & Dual-Concurrency)

Quy trình giữ ghế tạm thời trong 5 phút, khóa ghế hai tầng chống Race Condition và đồng bộ tức thì cho tất cả khán giả qua SignalR:

```mermaid
sequenceDiagram
    autonumber
    actor CustomerA as Khách Hàng A
    actor CustomerB as Khách Hàng B
    participant UI as Giao Diện Web (CSS Grid)
    participant Hub as SignalR SeatBookingHub
    participant API as BookingService
    participant DB as MariaDB (cinema_db)
    participant Worker as SeatHoldTimeoutWorker

    CustomerA->>Hub: WebSocket: JoinShowtimeGroup(showtimeId = 1)
    CustomerB->>Hub: WebSocket: JoinShowtimeGroup(showtimeId = 1)

    Note over CustomerA, API: Khách A bấm giữ ghế E5 trước Khách B vài mili-giây
    CustomerA->>API: POST /Booking/HoldSeats { showtimeId: 1, seatIds: [113] }
    activate API
    API->>DB: BEGIN TRANSACTION (IsolationLevel.Serializable)
    API->>DB: SELECT * FROM showtimeseats WHERE ShowtimeId = 1 AND SeatId = 113
    DB-->>API: Status = Available (0), Version = 1
    API->>DB: UPDATE showtimeseats SET Status = Held (1), HeldByUserId = 'UserA', Version = 2, HoldExpiresAt = UtcNow + 5m
    DB-->>API: 1 row updated (Thành công)
    API->>DB: COMMIT TRANSACTION
    API-->>CustomerA: HTTP 200 OK (Khóa ghế 5 phút thành công)
    API->>Hub: Broadcast Group "showtime_1": SeatStatusChanged(seatId: 113, status: Held, user: 'UserA')
    deactivate API

    Hub-->>CustomerA: Đổi ghế E5 sang Xanh dương (Bạn đang chọn) + Đếm ngược 05:00
    Hub-->>CustomerB: Đổi ghế E5 sang Vàng (Đang có người giữ) + Khóa nút bấm (Disabled)

    Note over CustomerB, API: Khách B cố tình click ghế E5 cùng thời điểm
    CustomerB->>API: POST /Booking/HoldSeats { showtimeId: 1, seatIds: [113] }
    activate API
    API->>DB: BEGIN TRANSACTION (Serializable)
    API->>DB: SELECT * FROM showtimeseats WHERE SeatId = 113
    DB-->>API: Status = Held (Đang bị UserA giữ)
    API->>DB: ROLLBACK TRANSACTION
    API-->>CustomerB: HTTP 409 Conflict: Ghế E5 đang được người khác giữ chỗ!
    deactivate API

    Note over Worker, Hub: Thu hồi ghế sau 5 phút nếu Khách A không thanh toán
    loop Quét định kỳ mỗi 30 giây
        Worker->>DB: SELECT * FROM showtimeseats WHERE Status = Held AND HoldExpiresAt <= UtcNow
        DB-->>Worker: Ghế 113 hết hạn giữ
        Worker->>DB: UPDATE showtimeseats SET Status = Available (0), HeldByUserId = NULL, Version = Version + 1
        Worker->>Hub: Broadcast Group "showtime_1": SeatStatusChanged(seatId: 113, status: Available)
        Hub-->>CustomerB: Ghế E5 chuyển lại màu Xám (Sẵn sàng đặt lại)
    end
```

---

### 4.2. Luồng 2: Xác Nhận Thanh Toán VietQR & Transactional Outbox Pattern

Quy trình thanh toán chuyển khoản Napas 247, khóa vé vĩnh viễn và gửi email E-Ticket kèm mã QR cá nhân hóa không nghẽn luồng:

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khách Hàng
    participant WebUI as Giao Diện Checkout
    participant API as PaymentService
    participant DB as MariaDB (cinema_db)
    participant Hub as SignalR SeatBookingHub
    participant OutboxWorker as OutboxMessageWorker
    participant SMTP as Google SMTP Server

    Customer->>WebUI: Quét mã VietQR Napas 247 và bấm "Xác nhận chuyển khoản"
    WebUI->>API: POST /Booking/ConfirmPayment { paymentId, transactionCode }
    activate API

    Note over API, DB: MỞ LOCAL TRANSACTION NGUYÊN TỬ DUY NHẤT (ACID)
    API->>DB: BEGIN TRANSACTION (IsolationLevel.Serializable)
    API->>DB: UPDATE payments SET Status = Paid (1), PaidAt = UtcNow
    API->>DB: UPDATE bookings SET Status = Confirmed (1), PaymentStatus = Paid (1)
    API->>DB: UPDATE showtimeseats SET Status = Reserved (2), Version = Version + 1
    API->>DB: UPDATE tickets SET Status = Issued (0), QrPayload = 'cinema-ticket:{BookingId}:{TicketCode}'
    
    Note over API, DB: TRANSACTIONAL OUTBOX: Lưu sự kiện gửi vé vào DB
    API->>DB: INSERT INTO outboxmessages (Id: UUID, Type: 'TicketEmail', Payload: JSON, IsProcessed: 0, RetryCount: 0)
    API->>DB: COMMIT TRANSACTION
    
    API->>Hub: Broadcast Group: SeatStatusChanged (seatId: 113, status: Reserved)
    API-->>WebUI: Giao dịch thành công (Phản hồi < 100ms)
    deactivate API
    WebUI-->>Customer: Chuyển hướng tới màn hình "Đặt vé thành công!"

    Note over OutboxWorker, SMTP: Tiến trình nền Outbox Worker quét hàng đợi mỗi 5 giây
    loop Quét định kỳ mỗi 5 giây
        OutboxWorker->>DB: SELECT * FROM outboxmessages WHERE IsProcessed = 0 AND RetryCount < 3
        activate OutboxWorker
        DB-->>OutboxWorker: Danh sách tin nhắn chờ gửi kèm Payload
        OutboxWorker->>SMTP: Kết nối SMTP Gmail (Port 587 TLS) gửi thư E-Ticket kèm QR Code
        alt Gửi Email Thành Công (250 OK)
            SMTP-->>OutboxWorker: 250 Message Accepted
            OutboxWorker->>DB: UPDATE outboxmessages SET IsProcessed = 1, ProcessedAt = UtcNow
            Customer-->>Customer: Nhận Email vé xem phim vào hộp thư đến
        else Sự Cố Mạng / Timeout SMTP Server
            SMTP-->>OutboxWorker: Connection Timeout / Error 503
            OutboxWorker->>DB: UPDATE outboxmessages SET RetryCount = RetryCount + 1, ErrorMessage = 'Timeout'
            Note over OutboxWorker: Tự động thử lại ở chu kỳ tiếp theo (Tối đa 3 lần)
        end
        deactivate OutboxWorker
    end
```

---

### 4.3. Luồng 3: Quầy Vé Box Office POS (Quét QR + IsPrinted Guard + Upsell F&B + In Nhiệt K80)

Quy trình phục vụ "Một Cửa" của nhân viên thu ngân tại rạp:

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khách Hàng Tại Rạp
    actor Staff as Nhân Viên Quầy
    participant POS as Màn Hình POS (/Counter/Index)
    participant API as CounterController
    participant DB as MariaDB (cinema_db)
    participant Printer as Máy In Hóa Đơn Nhiệt K80 (80mm)

    Customer->>Staff: Đưa màn hình điện thoại có mã QR vé từ Email
    Staff->>POS: Bật Camera quét mã QR (html5-qrcode)
    POS->>API: POST /Counter/ScanBookingQr { qrCodePayload }
    activate API
    API->>DB: SELECT * FROM bookings INCLUDE Tickets, Concessions WHERE BookingCode = @code
    DB-->>API: Dữ liệu đơn hàng, danh sách vé và món F&B

    alt VÉ ĐÃ ĐƯỢC IN TRƯỚC ĐÓ (IsPrinted == true)
        API-->>POS: Phản hồi: alreadyPrinted = true, PrintedAt, PrintedBy
        POS-->>Staff: BẬT CẢNH BÁO ĐỎ: "Vé này đã được in lúc 18:30! Từ chối in lại."
        Staff-->>Customer: Chặn lại, từ chối in vé (Phát hiện gian lận mang mã QR dùng lại)
    else VÉ HỢP LỆ (Chưa in cuống vé cứng)
        API-->>POS: Phản hồi hợp lệ: Danh sách ghế và bảng giá F&B
        deactivate API
        POS-->>Staff: Hiển thị thông tin ghế và bảng nút tính tiền Upsell Bắp Nước
        
        Staff->>Customer: Tư vấn: "Anh/Chị có muốn lấy thêm Combo 1 Bắp Rang Bơ + 2 Nước Ngọt không ạ?"
        Customer->>Staff: Đồng ý mua thêm 1 Bắp Phô Mai + 2 Coca (+120.000đ)
        Staff->>POS: Bấm chọn món Upsell trên màn hình POS
        Staff->>Customer: Thu tiền mặt 120.000đ
        Staff->>POS: Nhấp nút: "Xác nhận & In vé nhiệt 80mm"

        POS->>API: POST /Counter/ConfirmPrintAndUpsell { bookingId, upsellItems, paymentMethod: 'Cash' }
        activate API
        API->>DB: BEGIN TRANSACTION (IsolationLevel.Serializable)
        Note over API, DB: Khóa dòng vé chống Race Condition in trùng giữa 2 quầy
        API->>DB: INSERT INTO bookingconcessions (Món bắp nước mua thêm)
        API->>DB: INSERT INTO payments (Amount: 120000, Method: 'Cash', Status: Paid)
        API->>DB: UPDATE tickets SET IsPrinted = 1, PrintedAt = UtcNow, PrintedByUserId = @staffId, ThermalReceiptCode = 'POS80-XXXX'
        API->>DB: COMMIT TRANSACTION
        API-->>POS: Thành công: printUrl = /Counter/PrintThermalTicket?bookingId=...
        deactivate API

        POS->>Printer: Kích hoạt tự động lệnh in window.print() khổ 80mm
        Printer-->>Staff: Nhả cuống vé nhiệt 80mm rời từng ghế và phiếu nhận bắp nước
        Staff-->>Customer: Giao cuống vé cứng vào phòng chiếu và phiếu nhận bắp nước
    end
```

---

### 4.4. Luồng 4: Thiết Kế Sơ Đồ Phòng Chiếu (Visual Seat Studio & Thuật Toán Aisle Guard)

Quy trình quản trị viên cấu hình ma trận ghế 2D, tùy biến lối đi hành lang và kiểm tra an toàn ghế đôi Sweetbox:

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Quản Trị Viên
    participant UI as Layout Studio (/ScreeningRoom/LayoutBuilder)
    participant Engine as SeatLayoutStudio Engine
    participant API as ScreeningRoomController
    participant DB as MariaDB (cinema_db)

    Admin->>UI: Truy cập Studio thiết kế phòng chiếu RoomId = 1
    UI->>API: GET /ScreeningRoom/LayoutBuilder?id=1
    API->>DB: SELECT FROM screeningrooms, seats, seattypes WHERE RoomId = 1
    DB-->>API: Ma trận ghế vật lý và danh mục hạng ghế
    API-->>UI: Render giao diện ma trận 2D CSS Grid (TotalRows x TotalCols)

    Note over Admin, UI: Thao tác 1: Bật cột hành lang lối đi dọc (Cột 4)
    Admin->>UI: Nhấp nút đặt Hành lang Dọc tại Cột 4
    UI->>UI: Đánh dấu Cột 4 là lối đi trống, tự động xóa ghế ở cột này

    Note over Admin, UI: Thao tác 2: Dùng cọ vẽ Ghế đôi Sweetbox (ColSpan = 2)
    Admin->>UI: Chọn cọ Ghế đôi và nhấp vào ô Hàng 10, Cột 3 (sát hành lang Cột 4)
    UI->>Engine: ValidateSweetboxPlacement(row: 10, col: 3)
    activate Engine
    Note over Engine: Kiểm tra: Cột 3 + 1 = Cột 4 (Trùng hành lang lối đi!)
    Engine-->>UI: LỖI: Ghế đôi không được phép lấn sang cột hành lang!
    deactivate Engine
    UI-->>Admin: Hiển thị cảnh báo đỏ và chặn không cho gán ghế đôi

    Admin->>UI: Nhấp vào ô hợp lệ Hàng 10, Cột 5 (Block trung tâm)
    UI->>Engine: ValidateSweetboxPlacement(row: 10, col: 5)
    activate Engine
    Engine-->>UI: HỢP LỆ (Cột 5 và 6 đều là ghế)
    deactivate Engine
    UI->>UI: Gán ghế đôi chiếm Cột 5 và 6 (ColSpan = 2, Màu hồng)

    Note over Admin, UI: Thao tác 3: Lưu sơ đồ phòng chiếu
    Admin->>UI: Bấm nút "Lưu Sơ Đồ Phòng Chiếu"
    UI->>Engine: RecalculateLabelsAndSeatNumbers(matrix)
    Engine-->>UI: Danh sách SeatDto[] tự động đánh nhãn A, B, C và bỏ qua hành lang
    UI->>API: POST /ScreeningRoom/SaveSeatLayout { roomId, totalRows, totalCols, seats[] }
    activate API
    API->>DB: BEGIN TRANSACTION (Serializable)
    API->>DB: Xóa ghế cũ & INSERT danh sách ghế vật lý mới
    API->>DB: COMMIT TRANSACTION
    API-->>UI: Phản hồi thành công: Đã lưu sơ đồ phòng chiếu
    deactivate API
    UI-->>Admin: Cập nhật giao diện và hiển thị tổng số ghế thực tế
```

---

## 5. THIẾT KẾ TRIỂN KHAI HẠ TẦNG, MẠNG & CI/CD (DEPLOYMENT VIEW)

### 5.1. Mô Hình Bảo Mật SSL/TLS qua Cloudflare và Nginx VPS

```mermaid
flowchart LR
    User["Người dùng (Web / POS)"]
    CF["Cloudflare Edge Proxy (DNS & Anycast CDN)"]
    Nginx["Nginx Reverse Proxy (Origin CA Cert)"]
    AppNode1["Backend Node 1 (CineManagement HTTP:5000)"]
    AppNode2["Backend Node 2 (CineManagement HTTP:5000)"]

    User -->|"1. HTTPS / WSS (TLS 1.3 - Cloudflare Universal SSL)"| CF
    CF -->|"2. HTTPS Full Strict (Cloudflare Origin CA Certificate)"| Nginx
    Nginx -->|"3. HTTP Internal & WebSocket Upgrade (/hubs/*)"| AppNode1
    Nginx -->|"4. Load Balancing Round-Robin"| AppNode2
```

### 5.2. Quy Trình Tự Động Hóa CI/CD (GitHub Actions Workflow)

```mermaid
flowchart LR
    Dev["Lập trình viên Push Code (main)"] --> GHA["GitHub Actions Runner"]
    GHA --> Step1["1. Build & Run Tests .NET (xUnit)"]
    Step1 --> Step2["2. Build Docker Multi-Stage Image (< 150MB)"]
    Step2 --> Step3["3. Push Image lên GitHub Container Registry (GHCR)"]
    Step3 --> Step4["4. SSH vào VPS -> docker compose pull & up -d"]
```

---

## 6. BẢNG TỔNG HỢP CÔNG NGHỆ SỬ DỤNG TRONG HỆ THỐNG

| Tầng kiến trúc | Thành phần kỹ thuật | Phiên bản / Công nghệ | Lý do lựa chọn cho Đồ án (Đáp ứng CLO1 & CLO2) |
| :--- | :--- | :--- | :--- |
| **Backend Core** | Framework & Runtime | **C# .NET 10 (ASP.NET Core Web)** | Hiệu năng xử lý cao, hỗ trợ hoàn hảo mô hình Modular Monolith, quản lý phụ thuộc DI rõ ràng, kiểm soát kiểu dữ liệu tĩnh nghiêm ngặt. |
| **Real-time WebSockets** | Giao thức thời gian thực | **ASP.NET Core SignalR Core** | Duy trì kết nối song công hai chiều, đồng bộ trạng thái ghế tức thì $< 50\text{ ms}$, tự động phục hồi kết nối (`withAutomaticReconnect`). |
| **Kiểm soát tương tranh** | Concurrency Engine | **Serializable + Optimistic Concurrency Token** | Khóa mức `Serializable` kết hợp `[ConcurrencyCheck] public int Version` ngăn chặn 100% tình trạng double-booking khi mở bán phim bom tấn. |
| **Giao dịch tin cậy** | Resilient Messaging | **Transactional Outbox Pattern** | Đảm bảo nguyên tử ($ACID$), cam kết *At-Least-Once Delivery* gửi email vé E-Ticket kèm mã QR cá nhân hóa không phụ thuộc vào trạng thái mạng SMTP. |
| **Database** | Cơ sở dữ liệu quan hệ | **MariaDB 10.11 InnoDB (cinema_db)** | Hệ quản trị CSDL chuẩn hóa 12 bảng 3NF, hỗ trợ giao dịch ACID, Foreign Key constraints và kết nối qua connection pooling tốc độ cao. |
| **Frontend UI** | Giao diện Web Khách hàng | **Razor Pages + Bootstrap 5 + CSS Grid Matrix** | Render ma trận sơ đồ ghế 2D sắc nét, tự co giãn Responsive trên thiết bị di động, tích hợp modal thanh toán VietQR Napas 247. |
| **Quầy vé POS** | Quầy vé Một Cửa & In K80 | **html5-qrcode + ESC/POS K80 Thermal Print** | Quét mã QR vé trực tiếp qua camera thiết bị, kiểm tra cờ `IsPrinted Guard`, gợi ý Upsell F&B và in phiếu nhiệt 80mm rời từng ghế. |
| **Object Storage** | Lưu trữ đám mây đối tượng | **Cloudflare R2 (AWSSDK.S3 API) / MinIO** | Tách rời tệp tĩnh, không tốn chi phí băng thông tải ra (Zero Egress Fee), giúp backend hoàn toàn phi trạng thái (Stateless). |
| **Cloud & DevOps** | Đóng gói & Triển khai an toàn | **Docker Compose + Nginx + Cloudflare + GitHub Actions** | Tự động hóa build test và đóng gói image lên GHCR, bảo vệ máy chủ VPS bằng chứng chỉ Cloudflare Origin CA và Edge WAF. |
