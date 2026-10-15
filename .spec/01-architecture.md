# THIẾT KẾ KIẾN TRÚC MODULAR MONOLITH & TRIỂN KHAI HỆ THỐNG
## HỆ THỐNG QUẢN TRỊ TẬP TRUNG CHUỖI RẠP CHIẾU PHIM (CINEMANAGEMENT)

> **Tài liệu đặc tả**: `01-architecture.md`  
> **Mục tiêu đáp ứng**: Chuẩn đầu ra **CLO1** (Khả năng phân tích, mô hình hóa và thiết kế kiến trúc hệ thống phần mềm doanh nghiệp)  
> **Phong cách kiến trúc**: Modular Monolith Architecture  
> **Công nghệ nền tảng**: ASP.NET Core 10, C# 13, EF Core 10, SignalR Core, Cloudflare R2 / MinIO S3

---

## 1. TỔNG QUAN KIẾN TRÚC MODULAR MONOLITH

Thay vì áp dụng kiến trúc Microservices phân tán sớm (gây ra độ trễ mạng, phân mảnh transaction phức tạp và chi phí hạ tầng tốn kém) hoặc kiến trúc Traditional Monolith nguyên khối hỗn loạn (gây thắt nút cổ chai và phụ thuộc vòng vèo), **CineManagement** áp dụng kiến trúc **Modular Monolith**.

Kiến trúc này đóng gói ứng dụng trong một đơn vị triển khai duy nhất (Single Deployment Unit) nhưng phân chia mã nguồn bên trong thành các **Mô-đun độc lập (Bounded Contexts)** có ranh giới nghiệp vụ rõ ràng, giao tiếp nội bộ thông qua In-process Method Calls, DTOs và Domain Events.

```mermaid
flowchart TB
    %% ==========================================
    %% 1. TẦNG NGƯỜI DÙNG & THIẾT BỊ (XẾP HÀNG DỌC)
    %% ==========================================
    subgraph T1 ["TẦNG 1: NGƯỜI DÙNG & GIAO DIỆN (CLIENT TIER)"]
        direction TB
        Client["Khách Hàng (Web / Mobile Browser)"]
        POS["Quầy Vé Box Office (Quét QR & In Vé K80)"]
        Admin["Quản Trị Viên (Admin Portal Toàn Chuỗi)"]
    end

    %% ==========================================
    %% 2. TẦNG BẢO MẬT & ĐIỀU PHỐI (XẾP HÀNG DỌC)
    %% ==========================================
    subgraph T2 ["TẦNG 2: CỔNG BẢO MẬT & ĐỊNH TUYẾN (GATEWAY TIER)"]
        direction TB
        CF["Cloudflare Edge (WAF, Chống DDoS & SSL 1.3)"]
        Nginx["NGINX Reverse Proxy (Định Tuyến & WebSockets)"]
        CF ==>|"TLS Origin CA"| Nginx
    end

    %% ==========================================
    %% 3. TẦNG MÁY CHỦ XỬ LÝ (XẾP HÀNG DỌC)
    %% ==========================================
    subgraph T3 ["TẦNG 3: MÁY CHỦ TRUNG TÂM (ASP.NET CORE 10 MODULAR MONOLITH)"]
        direction TB
        API["Web MVC & REST APIs (Tiếp Nhận & Điều Hướng)"]
        SignalR["SignalR Real-Time Hub (Giữ Ghế Tức Thì &lt; 50ms)"]
        Modules["5 Modules Nghiệp Vụ (Cinemas, Catalog, Ticketing, Ordering, Payments)"]
        Workers["Background Workers (Nhả Ghế Quá Hạn & Gửi Vé Outbox)"]
        
        API --> SignalR
        SignalR --> Modules
        Modules --> Workers
    end

    %% ==========================================
    %% 4. TẦNG DỮ LIỆU & DỊCH VỤ NGOÀI (XẾP HÀNG DỌC)
    %% ==========================================
    subgraph T4 ["TẦNG 4: CƠ SỞ DỮ LIỆU & DỊCH VỤ ĐÁM MÂY (DATA & SERVICES)"]
        direction TB
        DB[("MariaDB 10.11 Server (12 Bảng 3NF, Giao Dịch ACID)")]
        R2[("Cloudflare R2 / S3 (Lưu Poster & QR Vé)")]
        Ext["Dịch Vụ Ngoài (VietQR Banking & Google SMTP)"]
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

---

## 2. ĐẶC TẢ CHI TIẾT CÁC MÔ-ĐUN NGHIỆP VỤ (MODULE BOUNDARIES)

### 2.1. Module `Cinemas` (Quản trị Cụm rạp & Phòng chiếu)
* **Trách nhiệm cốt lõi**:
  * Quản lý thực thể Cụm rạp chi nhánh (`Cinemas`): Mã rạp (`CS-QT`, `CS-HBT`), Tên, Địa chỉ, Số hotline.
  * Quản lý Phòng chiếu (`ScreeningRooms`): Định dạng phòng (2D Standard, 3D, IMAX Laser), tổng số hàng, tổng số cột.
  * **Visual Seat Layout Studio**: Quản lý các ghế vật lý (`Seats`), phân loại hạng ghế (`SeatTypes`), tọa độ ma trận (`GridRow`, `GridCol`), độ rộng ghế (`ColSpan = 1` hoặc `2`).
  * Thực thi quy tắc **Aisle Guard**: Ngăn chặn cấu hình ghế đôi Sweetbox lấn sang cột hành lang hoặc vượt biên phòng chiếu.

### 2.2. Module `Catalog` (Danh mục Phim & Lập lịch Suất chiếu)
* **Trách nhiệm cốt lõi**:
  * Quản lý thông tin Phim (`Movies`): Tên phim, thời lượng, đạo diễn, diễn viên, quốc gia (`Country`), định dạng (`Format`), phụ đề/lồng tiếng (`SubtitleType`), giới hạn độ tuổi (`AgeRating: P, K, T13, T16, T18, C`).
  * Quản lý Suất chiếu (`Showtimes`): Lập lịch chiếu phim theo phòng và khung giờ; thuật toán kiểm tra xung đột thời gian chiếu (ngăn 2 phim chiếu trùng một phòng cùng khung giờ).
  * Kích hoạt sự kiện sinh ma trận ghế suất chiếu tự động khi một suất chiếu mới được tạo.

### 2.3. Module `Ticketing` (Điều phối Ghế Suất chiếu & Real-time WebSockets)
* **Trách nhiệm cốt lõi**:
  * Quản lý bảng trạng thái biến thiên `ShowtimeSeats`: Trạng thái ghế (`Available = 0, Held = 1, Reserved = 2`), ID người giữ (`HeldByUserId`), thời điểm hết hạn giữ (`HoldExpiresAt`), mã phiên bản kiểm soát tương tranh (`Version`).
  * **SignalR WebSocket Gateway (`SeatBookingHub`)**:
    * Phân nhóm kết nối theo từng suất chiếu (`showtime_{id}`).
    * Phát sóng sự kiện `SeatStatusChanged` tức thì (< 50ms) cho mọi client trong nhóm khi có ghế bị giữ, nhả hoặc đã thanh toán.
  * **Background Worker (`SeatHoldTimeoutWorker`)**: Định kỳ quét mỗi 30 giây để thu hồi các ghế `Held` đã quá hạn 5 phút, trả về `Available` và bắn thông báo SignalR giải phóng ghế.

### 2.4. Module `Ordering` (Quản lý Đặt vé, Bắp nước & Quầy vé Box Office POS)
* **Trách nhiệm cốt lõi**:
  * Quản lý đơn hàng đặt vé trực tuyến (`Bookings`) và các vé xem phim thành phần (`Tickets`).
  * Quản lý bán kèm Bắp nước F&B (`BookingConcessions` và `FoodItems`).
  * **Box Office POS Engine (`/Counter`)**:
    * Phân tích mã QR từ camera nhân viên (`html5-qrcode`).
    * **Kiểm tra IsPrinted Guard**: Kiểm tra cờ `IsPrinted`. Nếu vé đã in cứng trước đó, chặn đứng hành vi in lại để ngăn gian lận.
    * Gợi ý Upsell bắp nước và cộng dồn hóa đơn tại quầy.
    * Xuất lệnh in phiếu nhiệt chuẩn 80mm K80 (ESC/POS) cho từng ghế và phiếu nhận combo bắp nước riêng biệt.

### 2.5. Module `Payments` (Thanh toán & Transactional Outbox)
* **Trách nhiệm cốt lõi**:
  * Tích hợp thanh toán trực tuyến tự động qua chuẩn **VietQR Napas 247** (tạo mã QR động kèm số tiền và nội dung chuyển khoản theo cú pháp đơn hàng).
  * Xử lý xác nhận thanh toán (`ConfirmPaymentAsync`) dưới cơ chế `IsolationLevel.Serializable`.
  * **Transactional Outbox Engine**:
    * Lưu bản ghi sự kiện `TicketEmail` vào bảng `outboxmessages` trong cùng một Local Transaction của đơn hàng ($ACID$).
    * **OutboxMessageWorker**: Tiến trình nền chạy độc lập mỗi 5 giây, quét các message chưa xử lý, kết nối Google SMTP gửi email E-Ticket kèm mã QR cá nhân hóa với cơ chế Retry tối đa 3 lần.

### 2.6. Module `Shared.Storage` (Lưu trữ Đám mây Cloudflare R2 / MinIO S3)
* **Trách nhiệm cốt lõi**:
  * Thay thế việc lưu trữ file trực tiếp trên ổ cứng web server (`wwwroot/uploads`), giúp hệ thống có thể mở rộng nhiều container mà không bị mất file.
  * Sử dụng thư viện chuẩn **`AWSSDK.S3`** giao tiếp qua giao thức S3-compatible API:
    * Môi trường Production: Kết nối **Cloudflare R2 Storage** (Zero Egress Fees, CDN toàn cầu siêu tốc).
    * Môi trường Development/Local: Kết nối cụm **MinIO S3** container hóa.
  * Lưu trữ và phân phối Poster phim độ phân giải cao, Banner sự kiện và ảnh QR Code vé điện tử.

---

## 3. SƠ ĐỒ GIAO TIẾP NỘI BỘ GIỮA CÁC MODULE (IN-PROCESS COLLABORATION)

```mermaid
graph TD
    subgraph UI ["TẦNG GIAO DIỆN (PRESENTATION)"]
        WebPortal["Customer Web Portal"]
        PosUI["Staff Box Office POS"]
        AdminStudio["Admin Studio"]
    end

    subgraph CoreModules ["CÁC MODULE LOGIC NGHIỆP VỤ (MODULAR MONOLITH)"]
        M_Cinemas["Module Cinemas<br/>• Quản lý Cụm rạp & Phòng<br/>• Sơ đồ ghế 2D Grid"]
        M_Catalog["Module Catalog<br/>• Quản lý Phim<br/>• Lập lịch Suất chiếu"]
        M_Ticketing["Module Ticketing<br/>• ShowtimeSeats State<br/>• SignalR WebSockets Hub<br/>• SeatHoldTimeoutWorker"]
        M_Ordering["Module Ordering<br/>• Bookings & Tickets<br/>• Counter POS Console<br/>• In vé nhiệt 80mm K80"]
        M_Payments["Module Payments<br/>• VietQR Payment Engine<br/>• Transactional Outbox<br/>• OutboxMessageWorker"]
        M_Storage["Module Shared.Storage<br/>• AWSSDK.S3 Integration<br/>• Cloudflare R2 / MinIO"]
    end

    subgraph Infra ["HẠ TẦNG DỮ LIỆU & NGOẠI VI"]
        DB[(MariaDB 10.11 InnoDB)]
        SMTP[Google SMTP Mail Server]
        R2[(Cloudflare R2 Bucket)]
    end

    %% Tương tác UI
    WebPortal --> M_Catalog
    WebPortal --> M_Ticketing
    WebPortal --> M_Ordering
    PosUI --> M_Ordering
    AdminStudio --> M_Cinemas
    AdminStudio --> M_Catalog
    AdminStudio --> M_Storage

    %% Tương tác giữa các module
    M_Catalog -.->|Sinh ma trận ghế khi tạo suất| M_Ticketing
    M_Ordering -->|Giữ/Nhả/Khóa ghế| M_Ticketing
    M_Ordering -->|Tạo giao dịch thanh toán| M_Payments
    M_Payments -.->|Phát SignalR khi thanh toán xong| M_Ticketing
    M_Catalog -->|Upload Poster| M_Storage
    M_Ordering -->|Upload mã QR vé| M_Storage

    %% Tương tác hạ tầng
    M_Cinemas --> DB
    M_Catalog --> DB
    M_Ticketing --> DB
    M_Ordering --> DB
    M_Payments --> DB
    M_Payments -.->|Gửi Mail vé| SMTP
    M_Storage -.->|S3 API| R2
```

---

## 4. SƠ ĐỒ KIẾN TRÚC TRIỂN KHAI VẬT LÝ (DEPLOYMENT ARCHITECTURE)

Hệ thống được thiết kế theo tiêu chuẩn triển khai an toàn của doanh nghiệp, với lớp phòng thủ biên **Cloudflare Edge**, máy chủ ảo **VPS Linux**, **NGINX Reverse Proxy** và đóng gói toàn bộ dịch vụ thông qua **Docker Compose**:

```mermaid
graph TB
    subgraph InternetClients ["NGƯỜI DÙNG INTERNET & THIẾT BỊ"]
        Browser["Trình duyệt Khách hàng (HTTPS / WSS)"]
        StaffPOS["Máy tính Quầy vé & Máy quét QR (POS)"]
        Thermal["Máy In Nhiệt K80 (80mm ESC/POS)"]
    end

    subgraph CloudflareEdge ["LỚP PHÒNG THỦ BIÊN (CLOUDFLARE EDGE NETWORK)"]
        CF_DNS["Cloudflare DNS & CDN Caching"]
        CF_WAF["Cloudflare WAF & DDoS Shield"]
        CF_SSL["Edge SSL/TLS Termination (Full Strict)"]
    end

    subgraph HostVPS ["MÁY CHỦ ẢO ĐỘC LẬP (UBUNTU LINUX VPS)"]
        subgraph NginxProxy ["NGINX REVERSE PROXY"]
            OriginCert["Cloudflare Origin CA Certificate (15 năm)"]
            ProxyConfig["Nginx Reverse Proxy & Brotli/Gzip Compression"]
            WS_Proxy["WebSocket Upgrade Gateway (/hubs/*)"]
        end

        subgraph DockerComposeEngine ["DOCKER COMPOSE ORCHESTRATION"]
            AppNode1["Container CineManagement Backend (ASP.NET Core 10)"]
            AppNode2["Container CineManagement Replica (Dự phòng tải)"]
            MariaDBEngine[("Container MariaDB 10.11 Server<br/>Port 3306 - Persistent Volume")]
        end
    end

    subgraph ExternalCloudServices ["DỊCH VỤ ĐÁM MÂY NGOẠI VI"]
        CloudflareR2[("Cloudflare R2 Object Storage<br/>(Lưu Poster Phim & QR Code)")]
        GmailSMTP["Google SMTP Server (Port 587 TLS)"]
    end

    %% Luồng truyền thông
    Browser ==>|HTTPS / WSS| CF_SSL
    StaffPOS ==>|HTTPS / WSS| CF_SSL
    StaffPOS -.->|USB / LAN Driver| Thermal

    CF_SSL --> CF_WAF --> CF_DNS
    CF_DNS ==>|HTTPS Port 443 with Origin CA| OriginCert

    OriginCert --> ProxyConfig
    ProxyConfig --> WS_Proxy
    WS_Proxy -->|Load Balancing HTTP:5000| AppNode1
    WS_Proxy -->|Load Balancing HTTP:5000| AppNode2

    AppNode1 ==>|TCP Port 3306| MariaDBEngine
    AppNode2 ==>|TCP Port 3306| MariaDBEngine

    AppNode1 -.->|AWSSDK.S3 API| CloudflareR2
    AppNode1 -.->|SMTP TLS 587| GmailSMTP
```

### Đặc tả luồng bảo mật mạng:
1. **Bảo vệ toàn diện trước DDoS/Attacks**: Toàn bộ địa chỉ IP gốc của VPS được ẩn giấu (Proxied) đằng sau mạng lưới Anycast của Cloudflare. Mọi request độc hại, quét cổng botnet đều bị chặn tại Cloudflare WAF.
2. **Chứng chỉ SSL/TLS Full (Strict)**: 
   * Chiều từ Client đến Cloudflare: Mã hóa bằng chứng chỉ Universal SSL của Cloudflare.
   * Chiều từ Cloudflare đến NGINX VPS: Xác thực bằng **Cloudflare Origin CA Certificate** có thời hạn 15 năm, bảo đảm không bị tấn công Man-In-The-Middle (MITM).
3. **Cấu hình NGINX hỗ trợ WebSockets**:
   * Khối cấu hình NGINX định tuyến các endpoint `/hubs/*` với các header bắt buộc:
     `Upgrade $http_upgrade; Connection "upgrade";` bảo đảm đường truyền song công của SignalR không bao giờ bị ngắt kết nối đột ngột.
