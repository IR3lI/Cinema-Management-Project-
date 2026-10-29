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
3. **Độ tin cậy giao dịch tuyệt đối (Zero Message Loss)**: Đảm bảo 100% email vé điện tử E-Ticket kèm mã QR cá nhân hóa được chuyển phát tin cậy ngay cả khi máy chủ thư điện tử gặp sự cố mạng tạm thời.
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

### 4.1. Luồng 1: Đặt vé & Giữ ghế Real-Time (SignalR & Concurrency)

Quy trình giữ ghế tạm thời trong 5 phút, đồng bộ trạng thái ghế thời gian thực giữa các người dùng và ngăn chặn xung đột trùng ghế:

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khach Hang
    participant App as Ung Dung Web
    participant Server as Booking Service
    participant DB as Co So Du Lieu
    participant Hub as SignalR Hub
    actor OtherUser as Khach Hang Khac

    Customer->>App: Chon ghe tren so do (E5, E6)
    App->>Server: Yeu cau giu ghe (HoldSeats)
    Server->>DB: Kiem tra trang thai ghe trong DB
    alt Ghe da co nguoi giu
        DB-->>Server: Ghe dang bi khoa
        Server-->>App: Bao loi: Ghe da duoc nguoi khac giu
        App-->>Customer: Hien thi canh bao chon ghe khac
    else Ghe con trong (Hop le)
        DB-->>Server: Ghe hop le
        Server->>DB: Cap nhat trang thai: Giu cho 5 phut
        Server->>Hub: Phat thong bao doi trang thai ghe
        Hub-->>Customer: Khoa ghe Xanh duong & Dem nguoc 05:00
        Hub-->>OtherUser: Doi ghe sang mau Vang & Khoa nut bam
    end
```

---

### 4.2. Luồng 2: Xác nhận Thanh toán & Gửi vé E-Ticket (Checkout & Outbox)

Quy trình xác nhận thanh toán nguyên tử, khóa ghế vĩnh viễn và tiến trình nền Outbox Worker chuyển phát email vé kèm mã QR cho khách hàng:

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khach Hang
    participant App as Ung Dung Web
    participant Server as Payment Service
    participant DB as Co So Du Lieu
    participant Worker as Outbox Worker
    participant Mail as Email Service (SMTP)

    Customer->>App: Quet ma VietQR xac nhan thanh toan
    App->>Server: Gui yeu cau xac nhan don hang
    Server->>DB: Cap nhat don hang: Da thanh toan & Khoa ghe vinh vien
    Server->>DB: Luu thong diep gui ve vao Outbox
    Server-->>App: Thong bao thanh toan thanh cong
    App-->>Customer: Hien thi man hinh Dat ve thanh cong

    Note over DB, Mail: Tien trinh nen tu dong xu ly gui Email
    Worker->>DB: Quet thong diep ve cho gui
    Worker->>Mail: Gui Email ve dien tu kem ma QR
    Mail-->>Customer: Nhan E-Ticket trong hop thu
    Worker->>DB: Danh dau da gui thanh cong
```

---

### 4.3. Luồng 3: Quầy vé Box Office: Soát vé QR & Bán vé In nhiệt K80

Quy trình phục vụ tại quầy gồm soát vé QR (kiểm tra cờ chống gian lận `IsPrinted`), gợi ý bán kèm bắp nước (Upsell F&B) và in cuộn vé nhiệt 80mm:

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khach Hang
    actor Staff as Nhan Vien Quay
    participant POS as He Thong POS
    participant DB as Co So Du Lieu
    participant Printer as May In Nhiet K80

    Customer->>Staff: Xuat trinh ma QR ve tren dien thoai
    Staff->>POS: Quet ma QR bang camera POS
    POS->>DB: Tra cuu thong tin don hang va ve
    alt Ve da in truoc do (IsPrinted = true)
        DB-->>POS: Trang thai: Da in ve
        POS-->>Staff: Canh bao do: Ve da duoc su dung!
        Staff-->>Customer: Tu choi in va chan gian lan
    else Ve hop le (Chua in)
        DB-->>POS: Trang thai: Hop le
        POS-->>Staff: Hien thi thong tin suat chieu & Goi y Bap Nuoc
        Staff->>POS: Xac nhan in ve (kem Combo F&B neu co)
        POS->>DB: Cap nhat trang thai: IsPrinted = true
        POS->>Printer: Gui lenh in K80
        Printer-->>Staff: Nha cuon ve xem phim va phieu Bap Nuoc
        Staff-->>Customer: Giao ve cung va huong dan vao phong chieu
    end
```

---

### 4.4. Luồng 4: Thiết kế Sơ đồ Phòng chiếu (Visual Layout Builder)

Quy trình Quản trị viên cấu hình ma trận ghế 2D, thiết lập hành lang lối đi, phân loại hạng ghế (Thường, VIP, Đôi) và lưu trữ xuống CSDL:

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Quan Tri Vien
    participant UI as Layout Studio UI
    participant Server as Screening Room Service
    participant DB as Co So Du Lieu

    Admin->>UI: Chon phong chieu can thiet ke
    UI->>Server: Lay cau truc phong chieu hien tai
    Server->>DB: Truy van danh sach ghe va hang ghe
    DB-->>Server: Tra ve so do phong
    Server-->>UI: Hien thi ma tran luoi ghe 2D CSS Grid

    Admin->>UI: Thiet lap loi di va gan Hang ghe (Thuong, VIP, Doi)
    UI->>UI: Kiem tra hop le vi tri ghe tren luoi ma tran

    Admin->>UI: Nhan nut Luu cau truc phong chieu
    UI->>Server: Gui ma tran ghe da thiet ke
    Server->>DB: Luu danh sach ghe va cap nhat so do phong
    DB-->>Server: Xac nhan luu thanh cong
    Server-->>UI: Thong bao cap nhat phong chieu thanh cong
    UI-->>Admin: Hien thi tong so ghe va so do hoan chinh
```
