# THIẾT KẾ CƠ SỞ DỮ LIỆU & SƠ ĐỒ THỰC THỂ (3NF ERD)
## HỆ THỐNG QUẢN TRỊ TẬP TRUNG CHUỖI RẠP CHIẾU PHIM (CINEMANAGEMENT)

> **Tài liệu đặc tả**: `02-database-erd.md`  
> **Chuẩn hóa**: Third Normal Form (Dạng chuẩn 3 - 3NF)  
> **Hệ quản trị CSDL**: MariaDB 10.11 / MySQL 8.0 (InnoDB Engine, `utf8mb4_unicode_ci`)  
> **Quy tắc đặc thù**: Sơ đồ ghế 2D đa hành lang & Thuật toán chống tràn ghế đôi Sweetbox (Aisle Guard)

---

## 1. SƠ ĐỒ QUAN HỆ THỰC THỂ MERMAID (ERD ĐẦY ĐỦ CÁC CỘT)

Sơ đồ dưới đây đặc tả đầy đủ 12 thực thể chính của hệ thống với chi tiết toàn bộ các trường, kiểu dữ liệu, khóa chính (PK) và khóa ngoại (FK):

```mermaid
erDiagram
    cinemas ||--o{ screeningrooms : "sở hữu (1-N)"
    screeningrooms ||--o{ seats : "chứa (1-N)"
    seattypes ||--o{ seats : "phân loại (1-N)"
    movies ||--o{ showtimes : "lập lịch (1-N)"
    screeningrooms ||--o{ showtimes : "tổ chức tại (1-N)"
    showtimes ||--o{ showtimeseats : "sinh trạng thái ghế (1-N)"
    seats ||--o{ showtimeseats : "ánh xạ vật lý (1-N)"
    aspnetusers ||--o{ bookings : "đặt hàng (1-N)"
    showtimes ||--o{ bookings : "thuộc suất (1-N)"
    bookings ||--o{ tickets : "xuất vé (1-N)"
    showtimeseats ||--o| tickets : "khóa vé (1-1)"
    bookings ||--o{ bookingconcessions : "chứa F&B (1-N)"
    fooditems ||--o{ bookingconcessions : "định nghĩa món (1-N)"
    bookings ||--o{ payments : "thanh toán (1-N)"
    bookings ||--o{ outboxmessages : "phát sự kiện (1-N)"

    cinemas {
        int CinemaId PK "Khóa chính tự tăng"
        string Code UK "Mã cụm rạp duy nhất (CS-QT, CS-HBT)"
        string Name "Tên cụm rạp chi nhánh"
        string Address "Địa chỉ đường, phường/xã"
        string City "Tỉnh/Thành phố (Hà Nội, TP.HCM)"
        string Hotline "Số điện thoại liên hệ"
        bool IsActive "Trạng thái hoạt động"
        datetime CreatedAt "Thời điểm khởi tạo"
    }

    screeningrooms {
        int RoomId PK "Khóa chính tự tăng"
        int CinemaId FK "Thuộc cụm rạp nào"
        string RoomName "Tên phòng (Phòng 1, IMAX 01)"
        string RoomType "Loại phòng (2D, 3D, IMAX, VIP)"
        int TotalRows "Tổng số hàng trong ma trận lưới"
        int TotalCols "Tổng số cột trong ma trận lưới"
        bool IsActive "Trạng thái khả dụng"
        datetime CreatedAt "Thời điểm tạo phòng"
    }

    seattypes {
        int SeatTypeId PK "Khóa chính loại ghế"
        string Name "Tên hạng ghế (Thường, VIP, Sweetbox)"
        string ColorCode "Mã màu hex hiển thị (#3b82f6, #ec4899)"
        decimal Surcharge "Phụ thu trên giá vé cơ bản (VND)"
        int ColSpan "Độ rộng chiếm cột (1 = đơn, 2 = đôi)"
        bool IsActive "Trạng thái áp dụng"
    }

    seats {
        int SeatId PK "Khóa chính ghế vật lý"
        int RoomId FK "Thuộc phòng chiếu nào"
        int SeatTypeId FK "Thuộc loại hạng ghế nào"
        string RowLabel "Nhãn hàng chữ cái (A, B, C...)"
        int SeatNumber "Số thứ tự ghế trong hàng (1, 2, 3...)"
        int GridRow "Tọa độ hàng vật lý trên lưới 2D (1..N)"
        int GridCol "Tọa độ cột vật lý trên lưới 2D (1..N)"
        int ColSpan "Số cột chiếm dụng (1 hoặc 2)"
        bool IsActive "Ghế có sử dụng được không"
    }

    movies {
        int MovieId PK "Khóa chính phim"
        string Title "Tên phim chiếu rạp"
        string Description "Nội dung tóm tắt phim"
        int DurationMinutes "Thời lượng phim tính bằng phút"
        date ReleaseDate "Ngày khởi chiếu"
        date EndDate "Ngày kết thúc dự kiến"
        string Director "Tên đạo diễn"
        string Cast "Danh sách diễn viên chính"
        string PosterUrl "Đường dẫn ảnh Poster (Cloudflare R2)"
        string TrailerUrl "Đường dẫn video trailer YouTube"
        string Country "Quốc gia sản xuất (Việt Nam, Mỹ, Hàn Quốc)"
        string Format "Định dạng chiếu (2D, 3D, IMAX)"
        string SubtitleType "Phụ đề hoặc Lồng tiếng (Sub, Dub)"
        string AgeRating "Phân loại độ tuổi (P, K, T13, T16, T18, C)"
        bool IsActive "Phim đang mở chiếu hay đã đóng"
    }

    showtimes {
        int ShowtimeId PK "Khóa chính suất chiếu"
        int MovieId FK "Chiếu phim nào"
        int RoomId FK "Chiếu tại phòng nào"
        datetime StartTime "Thời điểm bắt đầu chiếu"
        datetime EndTime "Thời điểm kết thúc (StartTime + Duration + Gap)"
        decimal TicketPrice "Giá vé cơ bản của suất chiếu (VND)"
        int Status "Trạng thái (0: Scheduled, 1: Active, 2: Cancelled)"
        datetime CreatedAt "Thời điểm lên lịch"
    }

    showtimeseats {
        int ShowtimeSeatId PK "Khóa chính ghế theo suất"
        int ShowtimeId FK "Thuộc suất chiếu nào"
        int SeatId FK "Ghế vật lý tương ứng"
        int Status "Trạng thái (0: Available, 1: Held, 2: Reserved)"
        string HeldByUserId "ID khách hàng đang giữ ghế tạm"
        datetime HoldExpiresAt "Thời điểm hết hạn giữ ghế (5 phút)"
        decimal Price "Đơn giá vé thực tế sau phụ thu"
        int Version "Token kiểm soát tương tranh lạc quan (Optimistic Concurrency)"
        datetime CreatedAt "Thời điểm sinh ghế"
        datetime UpdatedAt "Thời điểm cập nhật trạng thái gần nhất"
    }

    aspnetusers {
        string Id PK "Khóa chính GUID Identity"
        string UserName "Tên đăng nhập hệ thống"
        string Email "Địa chỉ email nhận vé"
        string PhoneNumber "Số điện thoại di động"
        string FullName "Họ và tên người dùng"
        datetime CreatedAt "Thời điểm đăng ký"
    }

    bookings {
        int BookingId PK "Khóa chính đơn đặt vé"
        string BookingCode UK "Mã đơn duy nhất (BK-20261029-XXXX)"
        string UserId FK "ID khách hàng đặt vé"
        int ShowtimeId FK "Suất chiếu được chọn"
        decimal TotalAmount "Tổng tiền đơn hàng (Vé + Bắp nước)"
        int Status "Trạng thái đơn (0: Pending, 1: Confirmed, 2: Cancelled, 3: Expired)"
        int PaymentStatus "Trạng thái thanh toán (0: Unpaid, 1: Paid, 2: Refunded)"
        datetime CreatedAt "Thời điểm tạo đơn"
        datetime UpdatedAt "Thời điểm cập nhật"
    }

    tickets {
        int TicketId PK "Khóa chính vé xem phim"
        int BookingId FK "Thuộc đơn đặt vé nào"
        int ShowtimeSeatId FK "Khóa ghế suất chiếu tương ứng"
        string TicketCode UK "Mã vé xem phim duy nhất"
        decimal Price "Đơn giá vé tại thời điểm bán (Snapshot)"
        int Status "Trạng thái vé (0: Issued, 1: Used, 2: Cancelled)"
        string QrPayload "Chuỗi dữ liệu sinh mã QR (Mã vé mã hóa)"
        bool IsPrinted "Cờ kiểm soát đã in vé nhiệt tại quầy chưa (IsPrinted Guard)"
        datetime PrintedAt "Thời điểm in vé cứng K80"
        string PrintedByUserId "ID nhân viên quầy thực hiện in vé"
        string ThermalReceiptCode "Mã số phiếu in nhiệt (POS80-XXXX)"
    }

    fooditems {
        int FoodItemId PK "Khóa chính món bắp nước"
        string Name "Tên combo / món (Bắp Caramel L, Coca 32oz)"
        string Description "Mô tả chi tiết món ăn"
        decimal Price "Đơn giá niêm yết (VND)"
        string ImageUrl "Đường dẫn ảnh sản phẩm"
        string Category "Danh mục (Popcorn, Drink, Combo)"
        bool IsAvailable "Còn hàng hay hết hàng"
    }

    bookingconcessions {
        int BookingConcessionId PK "Khóa chính dòng bắp nước"
        int BookingId FK "Thuộc đơn hàng nào"
        int FoodItemId FK "Món bắp nước được chọn"
        int Quantity "Số lượng món đặt mua"
        decimal UnitPrice "Đơn giá tại thời điểm chốt đơn (Snapshot)"
        decimal TotalPrice "Thành tiền (Quantity * UnitPrice)"
    }

    payments {
        int PaymentId PK "Khóa chính giao dịch"
        int BookingId FK "Thanh toán cho đơn hàng nào"
        decimal Amount "Số tiền thực tế giao dịch (VND)"
        string PaymentMethod "Phương thức (VietQR, Cash, POS_Card)"
        int Status "Trạng thái (0: Pending, 1: Paid, 2: Failed)"
        string TransactionCode "Mã giao dịch ngân hàng / POS"
        datetime PaidAt "Thời điểm thanh toán thành công"
    }

    outboxmessages {
        guid Id PK "Mã định danh sự kiện (UUID)"
        string Type "Loại sự kiện nghiệp vụ (TicketEmail, OrderCreated)"
        text Payload "Dữ liệu sự kiện định dạng JSON"
        bool IsProcessed "Cờ đã chuyển phát email thành công chưa"
        datetime ProcessedAt "Thời điểm gửi thư thành công"
        int RetryCount "Số lần đã thử lại khi gặp sự cố mạng (0..3)"
        string ErrorMessage "Chi tiết lỗi SMTP gần nhất nếu thất bại"
        datetime CreatedAt "Thời điểm phát sinh sự kiện"
    }
```

---

## 2. ĐẶC TẢ QUY TẮC SƠ ĐỒ GHẾ 2D ĐA HÀNH LANG (2D MULTI-AISLE MATRIX)

Trong kiến trúc của **CineManagement**, sơ đồ phòng chiếu không phải là danh sách hàng ghế phẳng đơn giản mà là một **Ma trận lưới 2D (`CSS Grid Matrix`)** có kích thước `TotalRows x TotalCols`.

### 2.1. Cấu hình đa hành lang (Multi-Aisle Configuration)
Phòng chiếu hiện đại thường có nhiều lối đi (hành lang giữa, hành lang hai bên, lối thoát hiểm):
* **Hành lang dọc (Vertical Aisles)**: Được xác định bởi danh sách các chỉ số cột trống `AisleCols = [Cột 4, Cột 13]`.
* **Hành lang ngang (Horizontal Aisles)**: Được xác định bởi danh sách các chỉ số hàng trống `AisleRows = [Hàng 6]` (ví dụ phân tách khu vực ghế trước và khu vực VIP phía sau).
* **Quy tắc hiển thị**: Tại các tọa độ `(GridRow, GridCol)` thuộc hành lang, hệ thống không tạo thực thể `Seat`, giao diện HTML/CSS render một khoảng trống trong suốt để người dùng dễ định vị.

### 2.2. Thuật toán đánh nhãn ghế tự động (Auto-Indexing & Labeling)
Hệ thống tự động tính toán tên hàng (`RowLabel`) và số thứ tự ghế (`SeatNumber`) dựa trên tọa độ ma trận:
1. **Nhãn hàng (`RowLabel`)**: Đánh theo thứ tự chữ cái `A, B, C...` từ hàng trên cùng xuống dưới cùng. Nếu một hàng là hành lang ngang (toàn bộ ô đều trống), hàng đó **bị bỏ qua**, hàng ghế kế tiếp mới được tăng chữ cái.
2. **Số thứ tự ghế (`SeatNumber`)**: Đánh số `1, 2, 3...` từ trái qua phải trên từng hàng. Nếu gặp cột hành lang dọc, bộ đếm số ghế **không tăng ở cột hành lang** mà tiếp tục đánh số liên tục cho ghế kế tiếp.
   * *Ví dụ*: Cột 1 là `A1`, Cột 2 là `A2`, Cột 3 là `A3`. Cột 4 là Hành lang lối đi. Cột 5 sẽ là `A4` (không bị nhảy cóc thành A5).

---

## 3. QUY TẮC NGHIÊM NGẶT CHỐNG TRÀN GHẾ ĐÔI SWEETBOX (AISLE GUARD)

Ghế đôi Sweetbox (`SeatType` có `ColSpan = 2`) mang đặc thù chiếm 2 ô cột liên tiếp trên cùng một hàng vật lý: `[GridCol, GridCol + 1]`.

```
Ô Cột 3 [Ghế Đôi Trái]  +  Ô Cột 4 [Ghế Đôi Phải]  ===>  HỢP LỆ (Nếu Cột 4 là ghế bình thường)
Ô Cột 4 [Ghế Đôi Trái]  +  Ô Cột 5 [HÀNH LANG LỐI ĐI]  ===>  VI PHẠM AISLE GUARD (BỊ CHẶN!)
```

### 3.1. Ràng buộc bất biến (Invariants)
1. **Không được lấn sang Cột Hành Lang**: Ô thứ hai của ghế đôi (`GridCol + 1`) tuyệt đối không được trùng với bất kỳ chỉ số cột nào trong danh sách `AisleCols`.
2. **Không được vượt quá Biên Phòng Chiếu**: `GridCol + ColSpan - 1 <= TotalCols`. Ghế đôi đặt ở cột cuối cùng của phòng chiếu là bất hợp lệ.
3. **Tính nguyên tử trong Suất Chiếu**:
   * Khi khách hàng bấm chọn một ghế Sweetbox, hệ thống phải tự động giữ đồng thời cả 2 vị trí cột liên kết và chỉ tính một giá vé trọn gói cho cặp ghế.
   * Khi một nửa của ghế đôi bị giữ, toàn bộ thực thể ghế đôi lập tức chuyển trạng thái sang `Held` trên SignalR Hub.

### 3.2. Thuật toán kiểm tra (Aisle Guard Algorithm)
```csharp
public static bool ValidateSweetboxPlacement(int gridRow, int gridCol, int colSpan, int totalCols, HashSet<int> aisleCols)
{
    // Kiểm tra tràn biên phải của ma trận
    if (gridCol + colSpan - 1 > totalCols)
    {
        return false; // Lỗi: Ghế đôi vượt quá độ rộng phòng chiếu
    }

    // Kiểm tra xem bất kỳ ô nào thuộc độ rộng của ghế có rơi vào hành lang không
    for (int offset = 0; offset < colSpan; offset++)
    {
        int checkCol = gridCol + offset;
        if (aisleCols.Contains(checkCol))
        {
            return false; // Lỗi: Ghế đôi lấn vào cột hành lang lối đi
        }
    }

    return true; // Hợp lệ
}
```
Mọi hành vi vẽ sơ đồ phòng chiếu tại `Visual Seat Layout Builder Studio` vi phạm thuật toán trên đều bị chặn ngay tại Client (JavaScript Validation) và xác thực lại lần 2 tại Backend (`ScreeningRoomController`), bảo đảm tính toàn vẹn 100% của cơ sở dữ liệu.
