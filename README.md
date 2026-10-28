# 🎬 CINEMANAGEMENT - HỆ THỐNG QUẢN LÝ CHUỖI RẠP CHIẾU PHIM TẬP TRUNG

[![.NET Core](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![Database](https://img.shields.io/badge/MariaDB-10.11%20InnoDB-003545?logo=mariadb&logoColor=white)](https://mariadb.org/)
[![Real-Time](https://img.shields.io/badge/SignalR-WebSockets-orange?logo=signalr&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/signalr)
[![Frontend](https://img.shields.io/badge/UI-Bootstrap%205%20%7C%20CSS%20Grid-7952B3?logo=bootstrap&logoColor=white)](https://getbootstrap.com/)
[![Architecture](https://img.shields.io/badge/Architecture-5--Tier%20Enterprise-blue)](./docs/04_THIET_KE_HE_THONG.md)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 📖 GIỚI THIỆU TỔNG QUAN

**CineManagement** là giải pháp phần mềm quản lý và vận hành chuỗi rạp chiếu phim tập trung được xây dựng trên nền tảng **ASP.NET Core 10** và **MariaDB 10.11 InnoDB**. Hệ thống giải quyết triệt để 3 thách thức kỹ thuật lớn nhất trong ngành rạp chiếu:
1. **Tranh chấp giữ ghế đồng thời (High-Concurrency Seat Locking)**: Ứng dụng mô hình tương tranh kép (*Dual-Concurrency Control*) kết hợp WebSockets **SignalR** siêu tốc ($< 50\text{ ms}$).
2. **Độ tin cậy giao dịch tuyệt đối (Zero Email Loss)**: Ứng dụng **Transactional Outbox Pattern** với dịch vụ nền `OutboxMessageWorker` đảm bảo 100% email vé điện tử E-Ticket kèm mã QR cá nhân hóa được chuyển phát tin cậy.
3. **Quy trình Quầy vé POS "Một Cửa" & Chống Gian Lận**: Tích hợp quét mã QR qua Camera, tự động kiểm tra cờ chống in vé lậu lần 2 (`IsPrinted Guard`), gợi ý bán kèm bắp nước (Upsell F&B) và in cuộn vé nhiệt K80 khổ 80mm rời từng ghế.
4. **Trình Thiết Kế Sơ Đồ Phòng Chiếu Trực Quan (Visual Seat Layout Builder Studio)**: Cho phép Ban quản trị tự do điều chỉnh ma trận hàng/cột, cấu hình hành lang ngang/dọc, dùng cọ gán hạng ghế (Standard, VIP, Sweetbox Ghế Đôi với `ColSpan=2`) với thuật toán chống tràn ghế sang lối đi và tự động đánh nhãn ghế chuẩn xác.

---

## 📚 TÀI LIỆU KỸ THUẬT & THIẾT KẾ CHI TIẾT (DOCS)

Toàn bộ tài liệu đặc tả kỹ thuật và thiết kế hệ thống theo chuẩn **IEEE 830** và **ISO/IEC 25010** được lưu trữ chi tiết trong thư mục [`docs/`](./docs/):

* 📋 **[01. Product Backlog & Kế Hoạch Sprint](./docs/01_PRODUCT_BACKLOG.md)**: Danh mục 4 Epics, 20 User Stories chi tiết với tiêu chí nghiệm thu Given-When-Then, Story Points và lộ trình phân bổ Sprint 1 đến Buổi 8.
* 📑 **[02. Đặc Tả Yêu Cầu Hệ Thống (SRS)](./docs/02_YEU_CAU_HE_THONG.md)**: Đặc tả chi tiết 20 Yêu cầu chức năng (FR) cho 3 tác nhân (Customer, Staff, Admin) và 5 nhóm Yêu cầu phi chức năng (NFR theo chuẩn ISO/IEC 25010).
* 🌳 **[03. Sơ Đồ Phân Rã Chức Năng (FDD)](./docs/03_SO_DO_PHAN_RA_CHUC_NANG.md)**: Biểu đồ cây phân rã chức năng 3 cấp độ (Mức 0 -> Mức 1 -> Mức 2 -> Mức 3), bảng mô tả chi tiết 42 chức năng và Ma trận phân quyền RBAC/CRUD.
* 🏛️ **[04. Thiết Kế Kiến Trúc Hệ Thống](./docs/04_THIET_KE_HE_THONG.md)**: Mô hình kiến trúc phân tầng 5 lớp (5-Tier Enterprise Architecture), sơ đồ kiến trúc tổng thể Mermaid, 4 Biểu đồ tuần tự (Sequence Diagrams) cho các luồng nghiệp vụ cốt lõi.
* 🗄️ **[05. Thiết Kế Cơ Sở Dữ Liệu & ERD 3NF](./docs/05_THIET_KE_CO_SO_DU_LIEU.md)**: Sơ đồ thực thể ERD Mermaid 12 bảng chuẩn hóa 3NF, từ điển dữ liệu chi tiết (Data Dictionary), cơ chế khóa tương tranh `Version` và chiến lược Composite Indexing.

---

## 🏛️ KIẾN TRÚC HỆ THỐNG PHÂN TẦNG (5-TIER ARCHITECTURE)

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

## ⚡ HƯỚNG DẪN KHỞI CHẠY HỆ THỐNG

### 1. Yêu cầu Tiên quyết
* **.NET SDK 10.0+**
* **MariaDB 10.11+** hoặc **MySQL 8.0+** chạy tại `localhost:3306`

### 2. Cấu hình Chuỗi Kết Nối
Kiểm tra cấu hình trong [`CineManagement/appsettings.json`](./CineManagement/appsettings.json):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=cinema_db;Uid=root;Pwd=;CharSet=utf8mb4;"
  }
}
```

### 3. Khởi Động Ứng Dụng
```powershell
# Chạy máy chủ MariaDB (nếu dùng binary cục bộ)
.\START-MySQL.bat

# Khởi chạy ứng dụng Web
cd CineManagement
dotnet run --launch-profile http
```
Ứng dụng sẽ khả dụng tại: **`http://localhost:5006`**

### 4. Tài Khoản Quản Trị & Dữ Liệu Demo
* **Tài khoản Quản trị**: `admin@cinema.com` / Mật khẩu: `Admin@123`
* **Dữ liệu Demo đã nạp sẵn**:
  * 3 Cụm rạp chi nhánh: CineStar Quốc Thanh, CineStar Hai Bà Trưng, CineStar Bình Dương
  * 210 Suất chiếu phim từ ngày 28/10/2026 đến 02/11/2026
  * 31,806 Ghế trạng thái đã được khởi tạo
  * 3 Mã đơn hàng test quầy POS:
    * `BK-DEMO-ONLINE-01`: Đơn vé thường + bắp nước chưa in
    * `BK-DEMO-UPSELL-02`: Đơn vé ghế đôi Sweetbox chưa in (test gợi ý mua thêm F&B)
    * `BK-DEMO-PRINTED-03`: Đơn vé ĐÃ IN LÚC 14:20 (test cơ chế chặn in lậu màu đỏ)
