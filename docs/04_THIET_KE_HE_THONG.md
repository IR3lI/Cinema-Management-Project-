# TÀI LIỆU THIẾT KẾ KIẾN TRÚC HỆ THỐNG (SYSTEM ARCHITECTURE DESIGN)
## HỆ THỐNG QUẢN LÝ CHUỖI RẠP CHIẾU PHIM TẬP TRUNG (CINEMANAGEMENT)

> **Mô hình kiến trúc**: 5-Tier Enterprise Architecture  
> **Giao thức truyền thông**: HTTPS / WebSockets (WSS) / TCP Port 3306 / SMTP Port 587  
> **Cơ chế cốt lõi**: SignalR Multiplexing Hub, Transactional Outbox Pattern, Dual-Concurrency Engine, Visual 2D CSS Grid

---

## 1. TỔNG QUAN KIẾN TRÚC HỆ THỐNG

Kiến trúc hệ thống **CineManagement** được thiết kế nhằm đáp ứng các yêu cầu khắt khe của một chuỗi rạp chiếu phim hiện đại:
1. **Khả năng chịu tải đồng thời cao (High-Concurrency)**: Hàng trăm người dùng cùng truy cập chọn ghế một suất chiếu bom tấn mà không xảy ra hiện tượng tranh chấp (Race Condition) hay bán trùng ghế (Double Booking).
2. **Đồng bộ thời gian thực siêu tốc (< 50ms)**: Bất kỳ thay đổi trạng thái ghế nào lập tức được phát sóng đến tất cả các client đang xem cùng suất chiếu qua WebSockets nhị phân.
3. **Độ tin cậy giao dịch tuyệt đối (Zero Message Loss)**: Đảm bảo 100% email vé điện tử được gửi thành công đến khách hàng qua Transactional Outbox Pattern, độc lập với sự cố mạng máy chủ thư điện tử.
4. **Vận hành quầy vé POS "Một Cửa"**: Tích hợp quét mã QR, gợi ý bán kèm bắp nước (Upsell F&B), kiểm tra cờ chống in vé lậu (`IsPrinted Guard`) và xuất lệnh in vé nhiệt K80 80mm.

---

## 2. MÔ HÌNH KIẾN TRÚC PHÂN TẦNG 5 LỚP (5-TIER ENTERPRISE ARCHITECTURE)

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ TẦNG 1: TRẢI NGHIỆM NGƯỜI DÙNG & THIẾT BỊ (PRESENTATION LAYER)                          │
│ • Customer Web Portal: Razor Pages, 2D CSS Grid Matrix, SignalR JavaScript Client      │
│ • Staff Box Office POS: html5-qrcode Scanner, Quick Upsell Calculator, 80mm Thermal    │
│ • Admin Studio: Visual Seat Layout Builder, Chart.js KPI Dashboard, Cinema Manager     │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TẦNG 2: MẠNG, ĐỊNH TUYẾN & BẢO MẬT (NETWORK, ROUTING & GATEWAY LAYER)                 │
│ • Kestrel Application Server (HTTPS / WSS / Reverse Proxy)                             │
│ • ASP.NET Core Middleware Pipeline: Cookie Auth, RBAC Authorization Policy, Anti-CSRF  │
│ • SignalR WebSocket Gateway: Hub Endpoint /hubs/seat, Multiplexing Groups              │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TẦNG 3: NGHIỆP VỤ ỨNG DỤNG & DỊCH VỤ NỀN (APPLICATION & ASYNC WORKERS LAYER)           │
│ • Controllers: BookingController, CounterController, ScreeningRoomController, etc.    │
│ • Domain Services: BookingService, PaymentService, EmailService, QrCodeService         │
│ • Background Workers: SeatHoldTimeoutWorker (5m sweep), OutboxMessageWorker (5s retry) │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TẦNG 4: TRUY XUẤT DỮ LIỆU & KIỂM SOÁT TƯƠNG TRANH (DATA ACCESS & CONCURRENCY)          │
│ • Entity Framework Core 10 (Pomelo MySQL / MariaDB Provider)                           │
│ • Dual-Concurrency Engine: [ConcurrencyCheck] Version Token + Serializable Isolation   │
│ • ASP.NET Core Identity Data Stores (Users, Roles, UserClaims)                         │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TẦNG 5: LƯU TRỮ CƠ SỞ DỮ LIỆU & DỊCH VỤ NGOẠI VI (PERSISTENCE & EXTERNAL LAYER)        │
│ • MariaDB 10.11 InnoDB Server (cinema_db, 12 Relational Tables in 3NF, Port 3306)       │
│ • Google SMTP Server (smtp.gmail.com:587 TLS/SSL)                                      │
│ • Hardware Thermal Receipt Printer (Khổ 80mm ESC/POS / System Print Driver)            │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. SƠ ĐỒ KIẾN TRÚC TỔNG THỂ MERMAID

```mermaid
graph TB
    subgraph T1 ["TẦNG 1: TRẢI NGHIỆM NGƯỜI DÙNG & THIẾT BỊ (PRESENTATION LAYER)"]
        ClientCus["Customer Web Portal<br/>• Razor Pages / Bootstrap 5 Dark Theme<br/>• 2D CSS Grid Matrix Renderer<br/>• SignalR WebSockets JavaScript Client<br/>• VietQR Checkout Modal"]
        ClientStaff["Staff Box Office POS Console<br/>• Màn hình Quầy vé (/Counter/Index)<br/>• Camera QR Scanner (html5-qrcode)<br/>• Bộ tính Upsell Bắp Nước nhanh<br/>• Trình xuất phiếu vé nhiệt 80mm K80"]
        ClientAdmin["Admin Studio & Management<br/>• Visual Seat Layout Builder Studio<br/>• Trình vẽ ma trận hàng/cột & hành lang<br/>• Biểu đồ KPI Doanh thu (Chart.js)<br/>• Quản lý Cụm rạp & Lập lịch phim"]
    end

    subgraph T2 ["TẦNG 2: MẠNG, ĐỊNH TUYẾN & BẢO MẬT (NETWORK & GATEWAY LAYER)"]
        Kestrel["Kestrel Web Server / Nginx Reverse Proxy<br/>• SSL/TLS Termination (HTTPS / WSS)<br/>• Nén dữ liệu Gzip / Brotli"]
        AuthSec["Security & Authorization Pipeline<br/>• ASP.NET Core Identity & Cookie Authentication<br/>• Phân quyền theo vai trò (RBAC Policies)<br/>• Anti-CSRF Token Validation"]
        Router["Routing & Endpoints Dispatcher<br/>• MVC Controller Routing Engine<br/>• SignalR Hub Endpoint Mapping (/hubs/seat)"]
    end

    subgraph T3 ["TẦNG 3: NGHIỆP VỤ ỨNG DỤNG & DỊCH VỤ NỀN (APPLICATION & WORKERS LAYER)"]
        subgraph RealtimeHub ["Real-Time WebSocket Gateway"]
            HubSeat["SignalR SeatBookingHub<br/>• Grouping: 'showtime_{id}'<br/>• Broadcast: 'SeatStatusChanged'<br/>• Đồng bộ đa người dùng < 50ms"]
        end

        subgraph AppServices ["Domain Application Services"]
            S_Booking["BookingService<br/>• HoldSeatAsync (Gia hạn 5p)<br/>• ReleaseSeatAsync<br/>• Tính giá vé & Phụ thu ghế"]
            S_Payment["PaymentService<br/>• ConfirmPaymentAsync<br/>• Sinh mã VietQR Napas 247<br/>• Lưu OutboxMessage"]
            S_Layout["SeatLayoutStudio Engine<br/>• Sinh tọa độ GridRow/GridCol<br/>• Aisle Guard chống tràn Sweetbox<br/>• Tự động đánh nhãn RowLabel/SeatNo"]
        end

        subgraph BgWorkers ["Asynchronous Background Workers"]
            W_Outbox["OutboxMessageWorker<br/>• Polling bảng outboxmessages (5s/lần)<br/>• Gửi Email vé điện tử qua SMTP Gmail<br/>• Cơ chế Retry tối đa 3 lần"]
            W_Cleanup["SeatHoldTimeoutWorker<br/>• Quét các ghế Held quá hạn 5 phút<br/>• Chuyển trạng thái về Available<br/>• Kích hoạt SignalR mở khóa ghế"]
        end
    end

    subgraph T4 ["TẦNG 4: TRUY XUẤT DỮ LIỆU & KIỂM SOÁT TƯƠNG TRANH (DATA ACCESS & CONCURRENCY)"]
        EFCore["Entity Framework Core 10 (ORM)<br/>ApplicationDbContext • Connection Pooling • Identity Stores"]
        ConcurrencyControl["Dual-Concurrency Engine<br/>• Pessimistic Locking: IsolationLevel.Serializable<br/>• Optimistic Concurrency: [ConcurrencyCheck] public int Version"]
    end

    subgraph T5 ["TẦNG 5: CƠ SỞ DỮ LIỆU & DỊCH VỤ NGOẠI VI (PERSISTENCE & EXTERNAL LAYER)"]
        MariaDB[("MariaDB 10.11 InnoDB Server<br/>Port 3306 • Database: cinema_db<br/>12 Bảng Chuẩn hóa 3NF<br/>31,806 Ghế Suất Chiếu Demo")]
        ExtSMTP["Google SMTP Server<br/>smtp.gmail.com:587<br/>Gửi Email E-Ticket kèm QR"]
        ExtPrinter["Máy In Nhiệt POS Khổ 80mm<br/>In vé xem phim & Phiếu F&B rời"]
    end

    %% Giao tiếp giữa các tầng
    T1 ==>|HTTPS / WSS| T2
    Kestrel --> AuthSec --> Router
    Router --> RealtimeHub
    Router --> AppServices

    AppServices <--> RealtimeHub
    AppServices --> EFCore
    BgWorkers --> EFCore
    BgWorkers --> RealtimeHub
    BgWorkers -.->|Gửi Mail Bất đồng bộ| ExtSMTP

    EFCore --> ConcurrencyControl
    ConcurrencyControl ==>|TCP Port 3306| MariaDB
    ClientStaff -.->|Lệnh in nhiệt K80| ExtPrinter
```

---

## 4. BIỂU ĐỒ TRÌNH TỰ CÁC LUỒNG NGHIỆP VỤ TRỌNG TÂM (SEQUENCE DIAGRAMS)

### 4.1. Luồng 1: Đặt vé & Giữ ghế Real-Time với SignalR & Dual-Concurrency

```mermaid
sequenceDiagram
    autonumber
    actor UserA as Khách Hàng A
    actor UserB as Khách Hàng B
    participant Hub as SignalR SeatBookingHub
    participant BService as BookingService
    participant DB as MariaDB (cinema_db)
    participant Worker as SeatHoldTimeoutWorker

    UserA->>Hub: Kết nối WebSocket: JoinShowtimeGroup(showtimeId)
    UserB->>Hub: Kết nối WebSocket: JoinShowtimeGroup(showtimeId)

    Note over UserA, DB: Khách hàng A bấm chọn giữ ghế E5, E6
    UserA->>BService: POST /Booking/HoldSeats { showtimeId: 1, seatIds: [113, 114] }
    activate BService
    BService->>DB: BEGIN TRANSACTION (IsolationLevel.Serializable)
    BService->>DB: SELECT * FROM showtimeseats WHERE ShowtimeId = 1 AND SeatId IN (113, 114)
    DB-->>BService: Status = Available (0), Version = 1
    
    Note over BService, DB: Cập nhật giữ ghế & Tăng Version chống xung đột lạc quan
    BService->>DB: UPDATE showtimeseats SET Status = Held (1), HeldByUserId = 'UserA', HoldExpiresAt = UtcNow + 5m, Version = 2, UpdatedAt = NOW() WHERE Version = 1
    DB-->>BService: 2 rows updated
    BService->>DB: COMMIT TRANSACTION
    deactivate BService

    BService->>Hub: Broadcast to Group "showtime_1": SeatStatusChanged(seatId, status: Held, heldByUser: UserA)
    Hub-->>UserA: Đổi màu ghế Xanh Dương (Đang chọn) + Kích hoạt đếm ngược 05:00
    Hub-->>UserB: Đổi màu ghế Vàng (Đang bị giữ) + Vô hiệu hóa nút bấm ghế E5, E6

    Note over UserB, BService: Khách B cố tình click vào ghế E5 cùng thời điểm
    UserB->>BService: POST /Booking/HoldSeats { showtimeId: 1, seatIds: [113] }
    activate BService
    BService->>DB: BEGIN TRANSACTION (Serializable)
    BService->>DB: SELECT * FROM showtimeseats WHERE SeatId = 113
    DB-->>BService: Status = Held, HeldByUserId = 'UserA', HoldExpiresAt > UtcNow
    BService->>DB: ROLLBACK TRANSACTION
    BService-->>UserB: HTTP 409 Conflict: "Ghế E5 đang được người khác giữ chỗ!"
    deactivate BService

    Note over Worker, DB: Xử lý Tự Động Thu Hồi Ghế Quá Hạn sau 5 phút (Nếu UserA không thanh toán)
    loop Định kỳ mỗi 30 giây
        Worker->>DB: SELECT * FROM showtimeseats WHERE Status = Held AND HoldExpiresAt <= UtcNow
        DB-->>Worker: Danh sách ghế hết hạn
        Worker->>DB: UPDATE showtimeseats SET Status = Available, HeldByUserId = NULL, Version = Version + 1
        Worker->>Hub: Broadcast to Group "showtime_1": SeatStatusChanged(seatId, status: Available)
        Hub-->>UserA: Thông báo: "Hết thời gian giữ vé!"
        Hub-->>UserB: Ghế E5, E6 chuyển lại màu xám sẵn sàng đặt vé
    end
```

---

### 4.2. Luồng 2: Thanh toán Thành công & Transactional Outbox Pattern

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khách Hàng
    participant Controller as BookingController
    participant PService as PaymentService
    participant DB as MariaDB (cinema_db)
    participant Hub as SignalR SeatBookingHub
    participant OutboxWorker as OutboxMessageWorker (Background)
    participant SMTP as Google SMTP Mail Server

    Customer->>Controller: Xác nhận Thanh Toán Đơn Vé (Checkout)
    activate Controller
    Controller->>PService: ConfirmPaymentAsync(paymentId, transactionCode)
    activate PService

    Note over PService, DB: MỞ LOCAL TRANSACTION DUY NHẤT (Serializable)
    PService->>DB: BEGIN TRANSACTION (Serializable)
    PService->>DB: UPDATE payments SET Status = Paid, PaidAt = UtcNow
    PService->>DB: UPDATE bookings SET Status = Confirmed, PaymentStatus = Paid
    PService->>DB: UPDATE showtimeseats SET Status = Reserved, Version = Version + 1
    PService->>DB: UPDATE tickets SET Status = Issued, QrPayload = 'cinema-ticket:{BookingId}:{TicketCode}'
    
    Note over PService, DB: TRANSACTIONAL OUTBOX: Lưu sự kiện gửi Email vé vào DB
    PService->>DB: INSERT INTO outboxmessages (Id, Type: 'TicketEmail', Payload: JSON, IsProcessed: 0, RetryCount: 0)
    PService->>DB: COMMIT TRANSACTION (Toàn bộ dữ liệu được chốt 100%)
    
    PService->>Hub: Broadcast to Group: SeatStatusChanged (status: Reserved)
    PService-->>Controller: Giao dịch thành công
    deactivate PService
    Controller-->>Customer: HTTP 200 OK: Trả về Màn hình Đặt Vé Thành Công (E-Ticket) trong tích tắc (< 150ms)
    deactivate Controller

    Note over OutboxWorker, SMTP: Tiến trình nền Outbox Worker quét hàng đợi mỗi 5 giây
    loop Quét định kỳ mỗi 5 giây
        OutboxWorker->>DB: SELECT * FROM outboxmessages WHERE IsProcessed = 0 AND RetryCount < 3
        activate OutboxWorker
        DB-->>OutboxWorker: Danh sách tin nhắn chờ gửi (kèm Payload BookingId, Email)
        OutboxWorker->>SMTP: Kết nối SMTP Gmail (Port 587) gửi thư E-Ticket kèm QR Code
        alt Gửi Email Thành Công
            SMTP-->>OutboxWorker: 250 Message Accepted
            OutboxWorker->>DB: UPDATE outboxmessages SET IsProcessed = 1, ProcessedAt = UtcNow
            Customer-->>Customer: Nhận Email vé xem phim vào hộp thư đến
        else Sự Cố Mạng / Timeout SMTP Server
            SMTP-->>OutboxWorker: Connection Timeout / Error 503
            OutboxWorker->>DB: UPDATE outboxmessages SET RetryCount = RetryCount + 1, ErrorMessage = 'Timeout'
            Note over OutboxWorker: Tự động thử lại ở chu kỳ 5 giây tiếp theo (Tối đa 3 lần)
        end
        deactivate OutboxWorker
    end
```

---

### 4.3. Luồng 3: Quầy Vé Box Office POS: Quét QR + Upsell F&B + In Vé Nhiệt 80mm

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khách Hàng
    actor Staff as Nhân Viên Quầy (Staff)
    participant POS_UI as Giao Diện POS (/Counter/Index)
    participant CounterCtrl as CounterController
    participant DB as MariaDB (cinema_db)
    participant ThermalPrinter as Máy In Hóa Đơn Nhiệt K80 (80mm)

    Customer->>Staff: Đưa màn hình điện thoại có mã QR Vé
    Staff->>POS_UI: Bật Camera Quét QR (hoặc nhập mã đơn BK-DEMO-...)
    POS_UI->>CounterCtrl: POST /Counter/ScanBookingQr { code }
    activate CounterCtrl
    CounterCtrl->>DB: SELECT * FROM bookings INCLUDE Tickets, Concessions WHERE BookingCode = @code
    DB-->>CounterCtrl: Thông tin đơn hàng, danh sách vé và F&B

    alt TRƯỜNG HỢP VÉ ĐÃ ĐƯỢC IN TRƯỚC ĐÓ (IsPrinted == true)
        CounterCtrl-->>POS_UI: { success: false, alreadyPrinted: true, printedAt, printedByStaff }
        POS_UI-->>Staff: ⚠️ BẬT MODAL CẢNH BÁO ĐỎ RỰC:<br/>"VÉ NÀY ĐÃ ĐƯỢC IN VÀO LÚC {PrintedAt} BỞI {StaffName}! TỪ CHỐI IN LẠI!"
        Staff-->>Customer: Chặn lại, từ chối in vé (Phát hiện gian lận dùng lại ảnh chụp màn hình QR)
    else TRƯỜNG HỢP VÉ HỢP LỆ (Chưa in vé cứng)
        CounterCtrl-->>POS_UI: { success: true, booking, tickets, concessions, availableFoods }
        deactivate CounterCtrl
        POS_UI-->>Staff: Hiển thị bảng chi tiết ghế + Bảng gợi ý "🍿 Mời khách mua thêm Bắp Nước (Upsell)"
        
        Staff->>Customer: "Anh/Chị có muốn dùng thêm Combo Bắp Rang Phô Mai & Nước ngọt lớn không ạ?"
        Customer->>Staff: "Cho mình 1 Bắp Phô Mai và 2 Coca Lớn"
        Staff->>POS_UI: Bấm nút [+] chọn thêm 1 Bắp Phô Mai & 2 Coca (Tự động cộng +120.000đ)
        Staff->>POS_UI: Chọn phương thức thu tiền: Tiền mặt tại quầy (Cash)
        Staff->>Customer: Thu tiền mặt 120.000đ
        Staff->>POS_UI: Bấm nút "🖨️ XÁC NHẬN & IN VÉ NHIỆT (80MM)"

        POS_UI->>CounterCtrl: POST /Counter/ConfirmPrintAndUpsell { bookingId, upsellItems, paymentMethod: 'Cash' }
        activate CounterCtrl
        CounterCtrl->>DB: BEGIN TRANSACTION (Serializable)
        Note over CounterCtrl, DB: Kiểm tra lại cờ IsPrinted trong Transaction chống Race Condition
        CounterCtrl->>DB: INSERT INTO bookingconcessions (món bắp nước mua thêm)
        CounterCtrl->>DB: INSERT INTO payments (Amount: 120k, Method: Cash, Status: Paid)
        CounterCtrl->>DB: UPDATE tickets SET IsPrinted = true, PrintedAt = UtcNow, PrintedByUserId = @staffId, ThermalReceiptCode = 'POS80-...'
        CounterCtrl->>DB: COMMIT TRANSACTION
        CounterCtrl-->>POS_UI: { success: true, printUrl: '/Counter/PrintThermalTicket?bookingId=...' }
        deactivate CounterCtrl

        POS_UI->>ThermalPrinter: Kích hoạt in tự động window.print()
        ThermalPrinter-->>Staff: Nhả các phiếu vé nhiệt 80mm rời từng ghế (font số ghế 32px) + 01 Phiếu Nhận Bắp Nước (F&B Voucher)
        Staff-->>Customer: Giao vé cứng vào phòng chiếu & Phiếu nhận bắp nước
    end
```

---

### 4.4. Luồng 4: Trình Thiết Kế Sơ Đồ Phòng Chiếu (Visual Seat Layout Builder Studio)

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Quản Trị Viên (Admin)
    participant StudioUI as Layout Studio (/ScreeningRoom/LayoutBuilder)
    participant RoomCtrl as ScreeningRoomController
    participant Engine as SeatLayoutStudio Engine
    participant DB as MariaDB (cinema_db)

    Admin->>StudioUI: Truy cập Studio thiết kế phòng chiếu RoomId = 1
    StudioUI->>RoomCtrl: GET /ScreeningRoom/LayoutBuilder?id=1
    RoomCtrl->>DB: SELECT * FROM screeningrooms WHERE RoomId = 1; SELECT * FROM seats; SELECT * FROM seattypes;
    DB-->>RoomCtrl: Dữ liệu cấu trúc lưới và danh mục hạng ghế
    RoomCtrl-->>StudioUI: Render giao diện ma trận 2D CSS Grid (TotalRows x TotalCols)

    Note over Admin, StudioUI: Thao tác 1: Bật / Tắt Hành lang Lối đi (Aisle Configurator)
    Admin->>StudioUI: Nhấp vào nút "🚶 Đặt Hành lang Dọc" ở tiêu đề Cột 4 và Cột 13
    StudioUI->>StudioUI: Đánh dấu Cột 4 & 13 là lối đi trống, tự động xóa các ghế ở cột này

    Note over Admin, StudioUI: Thao tác 2: Dùng cọ vẽ Hạng Ghế (Seat Type Painter)
    Admin->>StudioUI: Chọn cọ "💕 Ghế Đôi Sweetbox (ColSpan = 2)"
    Admin->>StudioUI: Nhấp vào ô (Hàng 10, Cột 3) - nằm sát trước hành lang Cột 4
    StudioUI->>Engine: ValidateCoupleSeatPlacement(row: 10, col: 3)
    activate Engine
    Note over Engine: Kiểm tra: Cột tiếp theo là Cột 4 (Cột Hành lang) -> Không hợp lệ!
    Engine-->>StudioUI: { valid: false, error: "Ghế đôi chiếm 2 ô không được lấn vào cột hành lang!" }
    deactivate Engine
    StudioUI-->>Admin: Hiển thị tooltip cảnh báo & Chặn không cho gán ghế đôi tại vị trí này

    Admin->>StudioUI: Nhấp vào ô hợp lệ (Hàng 10, Cột 5)
    StudioUI->>Engine: ValidateCoupleSeatPlacement(row: 10, col: 5)
    activate Engine
    Note over Engine: Cột 5 và Cột 6 đều thuộc Block trung tâm -> Hợp lệ!
    Engine-->>StudioUI: { valid: true }
    deactivate Engine
    StudioUI->>StudioUI: Gán ghế đôi J2 tại Cột 5 với ColSpan = 2 (chiếm Cột 5 và 6)

    Note over Admin, StudioUI: Thao tác 3: Click lại để xóa ghế
    Admin->>StudioUI: Nhấp vào một ghế đã có sẵn một lần nữa
    StudioUI->>StudioUI: Ghế chuyển thành ô trống (Xóa ghế)

    Note over Admin, StudioUI: Thao tác 4: Lưu Sơ đồ Phòng chiếu xuống Database
    Admin->>StudioUI: Bấm "💾 LƯU CẤU TRÚC PHÒNG CHIẾU"
    StudioUI->>Engine: RecalculateLabelsAndSeatNumbers(matrix)
    activate Engine
    Note over Engine: Bỏ qua hàng hành lang để đánh nhãn RowLabel (A, B, C...)<br/>Đánh số ghế SeatNumber từ trái qua phải bỏ qua lối đi
    Engine-->>StudioUI: Danh sách ghế SeatDto[] đã chuẩn hóa
    deactivate Engine

    StudioUI->>RoomCtrl: POST /ScreeningRoom/SaveSeatLayout { roomId, totalRows, totalCols, seats[] }
    activate RoomCtrl
    RoomCtrl->>DB: BEGIN TRANSACTION (Serializable)
    RoomCtrl->>DB: DELETE FROM seats WHERE RoomId = @roomId AND SeatId NOT IN (đã có vé)
    RoomCtrl->>DB: INSERT INTO seats (GridRow, GridCol, RowLabel, SeatNumber, ColSpan, SeatTypeId)
    RoomCtrl->>DB: Đồng bộ an toàn bảng showtimeseats cho các suất chiếu tương lai
    RoomCtrl->>DB: COMMIT TRANSACTION
    RoomCtrl-->>StudioUI: { success: true, message: "Đã lưu sơ đồ phòng chiếu thành công!" }
    deactivate RoomCtrl
    StudioUI-->>Admin: Hiển thị thông báo thành công & Cập nhật số ghế thực tế
```
