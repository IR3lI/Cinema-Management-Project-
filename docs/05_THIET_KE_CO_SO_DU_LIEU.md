# TÀI LIỆU THIẾT KẾ CƠ SỞ DỮ LIỆU & SƠ ĐỒ THỰC THỂ (3NF ERD)
## HỆ THỐNG QUẢN LÝ CHUỖI RẠP CHIẾU PHIM TẬP TRUNG (CINEMANAGEMENT)

> **Hệ quản trị CSDL**: MariaDB 10.11 / MySQL 8.0 (Storage Engine: InnoDB, Charset: utf8mb4)  
> **Tên Database**: `cinema_db` (Port: 3306)  
> **Tiêu chuẩn chuẩn hóa**: Chuẩn hóa Dạng chuẩn 3 (Third Normal Form - 3NF)  
> **Chiến lược tương tranh**: Khóa phiên bản [ConcurrencyCheck] Version Token + IsolationLevel.Serializable

---

## 1. CHIẾN LƯỢC THIẾT KẾ CSDL CHUẨN HÓA 3NF

Cơ sở dữ liệu của **CineManagement** được thiết kế đáp ứng các nguyên tắc chuẩn hóa 3NF nghiêm ngặt nhằm tránh dư thừa dữ liệu và tối ưu hiệu năng:
1. **Tách biệt Trạng thái Ghế Vật lý và Trạng thái Suất chiếu**:
   * Bảng `seats`: Đại diện cho vị trí phần cứng cố định của phòng chiếu (`GridRow`, `GridCol`, `SeatTypeId`, `IsActive`).
   * Bảng `showtimeseats`: Đại diện cho trạng thái động biến thiên theo từng suất chiếu cụ thể (`Status`: Available, Held, Reserved; `HeldByUserId`, `HoldExpiresAt`, `Version`). Một ghế vật lý có thể tham gia hàng ngàn suất chiếu khác nhau mà không bao giờ gây xung đột dữ liệu.
2. **Snapshot Đơn Giá và Thông Tin Vé**:
   * Bảng `tickets` và `bookingconcessions` lưu trữ snapshot đơn giá (`Price`, `UnitPrice`) tại thời điểm khách hàng thanh toán thành công. Khi quản trị viên thay đổi giá vé hoặc giá bắp nước trong tương lai, lịch sử doanh thu quá khứ không bao giờ bị sai lệch.
3. **Mở rộng Chuỗi Cụm Rạp (Multiplex Cinema Chain)**:
   * Bảng `cinemas` liên kết 1-N với `screeningrooms`, cho phép quản lý chuỗi hàng chục cụm rạp trên toàn quốc (`CS-QT`, `CS-HBT`, `CS-BD`).
4. **Đảm bảo Tính Toàn Vẹn với Transactional Outbox Pattern**:
   * Bảng `outboxmessages` lưu vết các thông điệp sự kiện (`TicketEmail`) trong cùng một Local Transaction với đơn hàng, bảo đảm tính nguyên tử $ACID$.

---

## 2. SƠ ĐỒ QUAN HỆ THỰC THỂ TỔNG THỂ (MERMAID ERD 12 BẢNG)

```mermaid
erDiagram
    cinemas ||--o{ screeningrooms : "sở hữu (1-N)"
    screeningrooms ||--o{ seats : "chứa (1-N)"
    seattypes ||--o{ seats : "phân loại (1-N)"
    movies ||--o{ showtimes : "lập lịch (1-N)"
    screeningrooms ||--o{ showtimes : "tổ chức tại (1-N)"
    showtimes ||--o{ showtimeseats : "sinh trạng thái ghế (1-N)"
    seats ||--o{ showtimeseats : "ánh xạ vật lý (1-N)"
    aspnetroles ||--o{ aspnetusers : "phân quyền (N-N)"
    aspnetusers ||--o{ bookings : "đặt hàng (1-N)"
    showtimes ||--o{ bookings : "thuộc suất (1-N)"
    bookings ||--o{ tickets : "xuất vé (1-N)"
    showtimeseats ||--o| tickets : "khóa vé (1-1)"
    bookings ||--o{ bookingconcessions : "chứa F&B (1-N)"
    fooditems ||--o{ bookingconcessions : "định nghĩa món (1-N)"
    bookings ||--o{ payments : "thanh toán (1-N)"
    bookings ||--o{ outboxmessages : "phát sự kiện (1-N)"

    aspnetusers {
        string Id PK "Khóa chính GUID Identity"
        string UserName "Tên đăng nhập"
        string Email "Địa chỉ email"
        string PhoneNumber "Số điện thoại"
        string FullName "Họ và tên"
        datetime CreatedAt "Thời điểm tạo"
    }

    aspnetroles {
        string Id PK "Khóa chính vai trò"
        string Name "Admin / Staff / Customer"
    }

    cinemas {
        int CinemaId PK "Khóa chính cụm rạp"
        string Code "Mã rạp (CS-QT, CS-HBT)"
        string Name "Tên cụm rạp"
        string Address "Địa chỉ chi tiết"
        string City "Tỉnh / Thành phố"
        string Hotline "Hotline liên hệ"
        bool IsActive "Trạng thái hoạt động"
    }

    screeningrooms {
        int RoomId PK "Khóa chính phòng chiếu"
        int CinemaId FK "Thuộc cụm rạp nào"
        string RoomName "Tên phòng (Phòng 1, IMAX...)"
        int TotalRows "Số hàng ma trận ghế"
        int TotalCols "Số cột ma trận ghế"
        int Capacity "Sức chứa ghế thực tế"
    }

    seattypes {
        int SeatTypeId PK "Khóa chính hạng ghế"
        string Code "STANDARD / VIP / COUPLE"
        string Name "Ghế Thường / Ghế VIP / Ghế Đôi"
        decimal Surcharge "Phụ thu (0đ, 20.000đ, 50.000đ)"
        int ColSpan "Độ rộng lưới (1 ô hoặc 2 ô)"
        string ColorCode "Mã màu HEX hiển thị"
    }

    seats {
        int SeatId PK "Khóa chính ghế vật lý"
        int RoomId FK "Thuộc phòng chiếu nào"
        int SeatTypeId FK "Thuộc hạng ghế nào"
        int GridRow "Tọa độ hàng ma trận (1..N)"
        int GridCol "Tọa độ cột ma trận (1..M)"
        string RowLabel "Nhãn hàng (A, B, C...)"
        int SeatNumber "Số thứ tự trong hàng (1, 2...)"
        bool IsActive "Khả dụng hay Khóa bảo trì"
    }

    movies {
        int MovieId PK "Khóa chính phim"
        string Title "Tên tác phẩm điện ảnh"
        string Genre "Thể loại phim"
        int DurationMinutes "Thời lượng (phút)"
        date ReleaseDate "Ngày khởi chiếu"
        string AgeRating "Phân loại độ tuổi (P, C13, C18)"
        string PosterUrl "Đường dẫn ảnh poster"
    }

    showtimes {
        int ShowtimeId PK "Khóa chính suất chiếu"
        int MovieId FK "Chiếu bộ phim nào"
        int RoomId FK "Chiếu tại phòng nào"
        datetime StartTime "Thời điểm bắt đầu"
        datetime EndTime "Thời điểm kết thúc"
        decimal TicketPrice "Giá vé cơ sở (VNĐ)"
        int Status "Available / Cancelled / Finished"
    }

    showtimeseats {
        int ShowtimeSeatId PK "Khóa chính trạng thái ghế"
        int ShowtimeId FK "Thuộc suất chiếu nào"
        int SeatId FK "Ghế vật lý tương ứng"
        int SeatTypeId FK "Hạng ghế tương ứng"
        decimal Price "Đơn giá vé thực tế"
        int Status "0: Available, 1: Held, 2: Reserved"
        string HeldByUserId "Mã người dùng đang giữ"
        datetime HoldExpiresAt "Hạn giữ ghế 5 phút"
        int Version "Khóa phiên chống xung đột lạc quan"
    }

    bookings {
        int BookingId PK "Khóa chính đơn hàng"
        string BookingCode "Mã đơn duy nhất (BK-...)"
        string UserId FK "Khách hàng mua vé"
        int ShowtimeId FK "Suất chiếu tương ứng"
        decimal TotalAmount "Tổng tiền thanh toán"
        int Status "0: Pending, 1: Confirmed, 2: Cancelled"
        datetime CreatedAt "Thời điểm tạo đơn"
    }

    tickets {
        int TicketId PK "Khóa chính vé điện tử"
        int BookingId FK "Thuộc đơn hàng nào"
        int SeatId FK "Vị trí ghế tương ứng"
        string TicketCode "Mã vé duy nhất (TK-...)"
        string QrPayload "Chuỗi mã hóa QR vé"
        bool IsPrinted "Cờ kiểm định đã in vé nhiệt K80"
        datetime PrintedAt "Thời điểm in vé cứng tại quầy"
        string PrintedByUserId "Nhân viên thực hiện in vé"
        string ThermalReceiptCode "Mã số phiếu in nhiệt POS80-..."
    }

    bookingconcessions {
        int ConcessionId PK "Khóa chính dịch vụ F&B"
        int BookingId FK "Thuộc đơn hàng nào"
        int FoodItemId FK "Mặt hàng bắp nước"
        int Quantity "Số lượng mua"
        decimal UnitPrice "Đơn giá tại thời điểm mua"
    }

    fooditems {
        int FoodItemId PK "Khóa chính món ăn"
        string Name "Tên món / Combo bắp nước"
        decimal Price "Đơn giá niêm yết"
        string ImageUrl "Đường dẫn ảnh món"
        bool IsAvailable "Còn hàng hay hết hàng"
    }

    payments {
        int PaymentId PK "Khóa chính giao dịch"
        int BookingId FK "Thuộc đơn hàng nào"
        string TransactionCode "Mã giao dịch ngân hàng / POS"
        decimal Amount "Số tiền giao dịch thực tế"
        int Method "Cash / VietQR / Card"
        int Status "Unpaid / Paid / Failed"
        datetime PaidAt "Thời điểm giao dịch thành công"
    }

    outboxmessages {
        guid Id PK "Khóa chính tin nhắn Outbox"
        string Type "Loại sự kiện (TicketEmail)"
        string Payload "Nội dung JSON của sự kiện"
        bool IsProcessed "Cờ đã gửi mail thành công"
        datetime CreatedAt "Thời điểm tạo tin nhắn"
        datetime ProcessedAt "Thời điểm gửi thành công"
        int RetryCount "Số lần đã thử lại khi lỗi"
        string ErrorMessage "Chi tiết lỗi nếu thất bại"
    }
```

---

## 3. TỪ ĐIỂN DỮ LIỆU CHI TIẾT (DATA DICTIONARY)

### 3.1. Bảng `cinemas` (Cụm rạp chi nhánh)
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Mô Tả |
| :--- | :--- | :---: | :--- |
| `CinemaId` | `INT` | PK, Auto Increment | Định danh duy nhất cụm rạp |
| `Code` | `VARCHAR(30)` | UNIQUE, NOT NULL | Mã định danh rạp (`CS-QT`, `CS-HBT`, `CS-BD`) |
| `Name` | `VARCHAR(150)` | NOT NULL | Tên thương mại cụm rạp |
| `Address` | `VARCHAR(300)` | NOT NULL | Địa chỉ chi tiết rạp chiếu |
| `City` | `VARCHAR(100)` | NOT NULL | Tỉnh / Thành phố trực thuộc |
| `Hotline` | `VARCHAR(30)` | NULL | Số điện thoại đường dây nóng |
| `Email` | `VARCHAR(150)` | NULL | Email liên hệ hỗ trợ |
| `IsActive` | `TINYINT(1)` | NOT NULL, DEFAULT 1 | Trạng thái rạp đang mở cửa |

### 3.2. Bảng `screeningrooms` (Phòng chiếu phim)
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Mô Tả |
| :--- | :--- | :---: | :--- |
| `RoomId` | `INT` | PK, Auto Increment | Định danh duy nhất phòng chiếu |
| `CinemaId` | `INT` | FK -> cinemas(CinemaId) | Thuộc cụm rạp nào |
| `RoomName` | `VARCHAR(100)` | NOT NULL | Tên phòng (`Phòng 1 - Standard`, `IMAX Laser`) |
| `TotalRows` | `INT` | NOT NULL | Số hàng trên lưới ma trận phòng |
| `TotalCols` | `INT` | NOT NULL | Số cột trên lưới ma trận phòng |
| `Capacity` | `INT` | NOT NULL | Tổng sức chứa ghế thực tế của phòng |
| `RoomType` | `INT` | NOT NULL | Phân loại định dạng phòng (0: 2D, 1: 3D, 2: IMAX) |

### 3.3. Bảng `seattypes` (Danh mục Hạng ghế)
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Mô Tả |
| :--- | :--- | :---: | :--- |
| `SeatTypeId` | `INT` | PK, Auto Increment | Định danh duy nhất hạng ghế |
| `Code` | `VARCHAR(20)` | UNIQUE, NOT NULL | Mã code (`STANDARD`, `VIP`, `COUPLE`) |
| `Name` | `VARCHAR(50)` | NOT NULL | Tên hiển thị (`Ghế Thường`, `Ghế VIP`, `Ghế Đôi`) |
| `Surcharge` | `DECIMAL(15,0)` | NOT NULL, DEFAULT 0 | Mức phụ thu cộng thêm vào vé gốc (VNĐ) |
| `ColSpan` | `INT` | NOT NULL, DEFAULT 1 | Độ rộng chiếm ô lưới (1: Đơn, 2: Ghế Đôi) |
| `ColorCode` | `VARCHAR(10)` | NOT NULL | Mã màu HEX hiển thị trên sơ đồ (`#ffc107`, `#e83e8c`) |

### 3.4. Bảng `seats` (Ghế vật lý phòng chiếu)
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Mô Tả |
| :--- | :--- | :---: | :--- |
| `SeatId` | `INT` | PK, Auto Increment | Định danh duy nhất ghế vật lý |
| `RoomId` | `INT` | FK -> screeningrooms(RoomId) | Thuộc phòng chiếu nào |
| `SeatTypeId` | `INT` | FK -> seattypes(SeatTypeId) | Hạng ghế tương ứng |
| `GridRow` | `INT` | NOT NULL | Tọa độ hàng trên lưới 2D ($1 \le \text{GridRow} \le \text{TotalRows}$) |
| `GridCol` | `INT` | NOT NULL | Tọa độ cột trên lưới 2D ($1 \le \text{GridCol} \le \text{TotalCols}$) |
| `RowLabel` | `VARCHAR(5)` | NOT NULL | Nhãn hàng hiển thị (`A`, `B`, `C`...) |
| `SeatNumber` | `INT` | NOT NULL | Số thứ tự ghế trong hàng (`1`, `2`...) |
| `IsActive` | `TINYINT(1)` | NOT NULL, DEFAULT 1 | Khả dụng hay đang khóa bảo trì |

### 3.5. Bảng `showtimes` (Lịch suất chiếu)
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Mô Tả |
| :--- | :--- | :---: | :--- |
| `ShowtimeId` | `INT` | PK, Auto Increment | Định danh duy nhất suất chiếu |
| `MovieId` | `INT` | FK -> movies(MovieId) | Chiếu bộ phim nào |
| `RoomId` | `INT` | FK -> screeningrooms(RoomId) | Chiếu tại phòng nào |
| `StartTime` | `DATETIME(6)` | NOT NULL | Thời điểm bắt đầu chiếu |
| `EndTime` | `DATETIME(6)` | NULL | Thời điểm kết thúc chiếu |
| `TicketPrice` | `DECIMAL(15,0)` | NOT NULL | Đơn giá vé cơ bản của suất chiếu (VNĐ) |
| `Status` | `INT` | NOT NULL | Trạng thái (0: Sắp chiếu, 1: Đang chiếu, 2: Hủy) |

### 3.6. Bảng `showtimeseats` (Trạng thái ghế theo suất chiếu)
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Mô Tả |
| :--- | :--- | :---: | :--- |
| `ShowtimeSeatId`| `INT` | PK, Auto Increment | Định danh duy nhất trạng thái ghế |
| `ShowtimeId` | `INT` | FK -> showtimes(ShowtimeId) | Thuộc suất chiếu nào |
| `SeatId` | `INT` | FK -> seats(SeatId) | Ghế vật lý tương ứng |
| `SeatTypeId` | `INT` | FK -> seattypes(SeatTypeId) | Snapshot hạng ghế |
| `Price` | `DECIMAL(15,0)` | NOT NULL | Giá vé thực tế tính theo hạng ghế |
| `Status` | `INT` | NOT NULL, DEFAULT 0 | 0: Available, 1: Held, 2: Reserved |
| `HeldByUserId` | `VARCHAR(450)` | NULL | Mã User/Session đang giữ ghế |
| `HoldExpiresAt` | `DATETIME(6)` | NULL | Thời điểm hết hạn giữ ghế (UtcNow + 5m) |
| `Version` | `INT` | NOT NULL, DEFAULT 1 | **Khóa kiểm tra xung đột tương tranh lạc quan** |

### 3.7. Bảng `tickets` (Vé điện tử & Vé in nhiệt)
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Mô Tả |
| :--- | :--- | :---: | :--- |
| `TicketId` | `INT` | PK, Auto Increment | Định danh duy nhất vé |
| `BookingId` | `INT` | FK -> bookings(BookingId) | Thuộc đơn hàng nào |
| `SeatId` | `INT` | FK -> seats(SeatId) | Vị trí ghế xem phim |
| `TicketCode` | `VARCHAR(50)` | UNIQUE, NOT NULL | Mã vé duy nhất (`TK-...`) |
| `QrPayload` | `VARCHAR(500)` | NOT NULL | Chuỗi mã hóa QR (`cinema-ticket:12:TK-...`) |
| `IsPrinted` | `TINYINT(1)` | NOT NULL, DEFAULT 0 | **Cờ kiểm định vé đã in ra giấy nhiệt tại quầy** |
| `PrintedAt` | `DATETIME(6)` | NULL | Thời điểm in vé nhiệt tại quầy |
| `PrintedByUserId`| `VARCHAR(450)` | NULL | Định danh nhân viên thực hiện in vé |
| `ThermalReceiptCode`| `VARCHAR(50)` | NULL | Mã phiếu in nhiệt (`POS80-...`) |

### 3.8. Bảng `outboxmessages` (Transactional Outbox Events)
| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Mô Tả |
| :--- | :--- | :---: | :--- |
| `Id` | `CHAR(36)` | PK (GUID) | Định danh duy nhất thông điệp sự kiện |
| `Type` | `VARCHAR(100)` | NOT NULL | Loại sự kiện (`TicketEmail`, `SeatReleased`) |
| `Payload` | `LONGTEXT` | NOT NULL | Nội dung chi tiết sự kiện dạng JSON |
| `IsProcessed` | `TINYINT(1)` | NOT NULL, DEFAULT 0 | Cờ trạng thái đã gửi thành công |
| `CreatedAt` | `DATETIME(6)` | NOT NULL | Thời điểm tạo thông điệp sự kiện |
| `ProcessedAt` | `DATETIME(6)` | NULL | Thời điểm gửi email thành công |
| `RetryCount` | `INT` | NOT NULL, DEFAULT 0 | Số lần đã thử lại khi gặp sự cố mạng |
| `ErrorMessage` | `VARCHAR(1000)`| NULL | Nhật ký lỗi nếu quá trình gửi thất bại |

---


CREATE INDEX IX_OutboxMessages_Processed_Retry ON outboxmessages (IsProcessed, RetryCount);

-- 4. Tối ưu quét mã QR và kiểm tra cờ chống in lậu vé tại quầy POS
CREATE INDEX IX_Tickets_Booking_IsPrinted ON tickets (BookingId, IsPrinted);
```
