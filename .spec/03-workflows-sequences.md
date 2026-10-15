# BIỂU ĐỒ TRÌNH TỰ CÁC LUỒNG NGHIỆP VỤ TRỌNG TÂM (SEQUENCE DIAGRAMS)
## HỆ THỐNG QUẢN TRỊ TẬP TRUNG CHUỖI RẠP CHIẾU PHIM (CINEMANAGEMENT)

> **Tài liệu đặc tả**: `03-workflows-sequences.md`  
> **Mục tiêu**: Chuẩn hóa luồng tương tác thời gian thực, giao dịch tài chính bất biến và quy trình vận hành quầy POS  
> **Công nghệ thực thi**: SignalR WebSockets, EF Core 10 Serializable Isolation, Transactional Outbox Worker, Thermal Print K80

---

## 1. LUỒNG 1: ĐẶT & GIỮ GHẾ REAL-TIME VỚI SIGNALR VÀ DUAL-CONCURRENCY

Biểu đồ mô tả kịch bản hai khách hàng (Khách A và Khách B) cùng truy cập một suất chiếu bom tấn và cùng nhấp chọn ghế `E5`. Cơ chế kết hợp giữa **SignalR Hub** và **Dual-Concurrency Engine** giải quyết triệt để tranh chấp:

```mermaid
sequenceDiagram
    autonumber
    actor UserA as Khách Hàng A
    actor UserB as Khách Hàng B
    participant ClientUI as Trình Duyệt Web (JavaScript)
    participant Hub as SignalR SeatBookingHub
    participant BService as BookingService
    participant DB as MariaDB (cinema_db)
    participant Worker as SeatHoldTimeoutWorker

    Note over UserA, UserB: Khởi tạo kết nối WebSockets ban đầu
    UserA->>Hub: WebSocket: JoinShowtimeGroup(showtimeId = 1)
    UserB->>Hub: WebSocket: JoinShowtimeGroup(showtimeId = 1)
    Hub-->>UserA: Đã tham gia nhóm "showtime_1"
    Hub-->>UserB: Đã tham gia nhóm "showtime_1"

    Note over UserA, DB: Khách A bấm giữ ghế E5 (SeatId: 113) trước Khách B vài mili-giây
    UserA->>BService: POST /Booking/HoldSeats { showtimeId: 1, seatIds: [113] }
    activate BService
    BService->>DB: BEGIN TRANSACTION (IsolationLevel.Serializable)
    BService->>DB: SELECT * FROM showtimeseats WHERE ShowtimeId = 1 AND SeatId = 113
    DB-->>BService: Status = Available (0), Version = 1
    
    Note over BService, DB: Cập nhật giữ ghế & Tăng Version chống xung đột lạc quan
    BService->>DB: UPDATE showtimeseats SET Status = Held (1), HeldByUserId = 'UserA', HoldExpiresAt = UtcNow + 5m, Version = 2, UpdatedAt = NOW() WHERE Version = 1
    DB-->>BService: 1 row updated (Thành công)
    BService->>DB: COMMIT TRANSACTION
    BService-->>UserA: HTTP 200 OK: Giữ ghế thành công (Khóa 5 phút)
    
    Note over BService, Hub: Bắn sự kiện SignalR tới toàn bộ người dùng trong nhóm
    BService->>Hub: Broadcast Group "showtime_1": SeatStatusChanged(seatId: 113, status: Held, user: 'UserA')
    deactivate BService
    Hub-->>UserA: Đổi màu ghế Xanh Dương (Bạn đang chọn) + Kích hoạt đếm ngược 05:00
    Hub-->>UserB: Đổi màu ghế Vàng (Đang bị giữ) + Vô hiệu hóa nút bấm ghế E5 (Disabled)

    Note over UserB, BService: Khách B cố tình click vào ghế E5 cùng tích tắc
    UserB->>BService: POST /Booking/HoldSeats { showtimeId: 1, seatIds: [113] }
    activate BService
    BService->>DB: BEGIN TRANSACTION (IsolationLevel.Serializable)
    BService->>DB: SELECT * FROM showtimeseats WHERE SeatId = 113
    DB-->>BService: Status = Held, HeldByUserId = 'UserA', HoldExpiresAt > UtcNow
    BService->>DB: ROLLBACK TRANSACTION
    BService-->>UserB: HTTP 409 Conflict: "Ghế E5 đang được người khác giữ chỗ!"
    deactivate BService

    Note over Worker, Hub: Tự động thu hồi ghế sau 5 phút nếu Khách A không thanh toán
    loop Định kỳ mỗi 30 giây
        Worker->>DB: SELECT FROM showtimeseats WHERE Status = Held AND HoldExpiresAt <= UtcNow
        DB-->>Worker: Ghế 113 quá hạn 5 phút
        Worker->>DB: UPDATE showtimeseats SET Status = Available (0), HeldByUserId = NULL, Version = Version + 1
        Worker->>Hub: Broadcast Group "showtime_1": SeatStatusChanged(seatId: 113, status: Available)
        Hub-->>UserA: Thông báo: "Hết thời gian giữ vé!"
        Hub-->>UserB: Ghế E5 chuyển lại màu Xám (Sẵn sàng cho người khác đặt vé)
    end
```

---

## 2. LUỒNG 2: THANH TOÁN VIETQR & TRANSACTIONAL OUTBOX PATTERN

Biểu đồ mô tả tiến trình thanh toán trực tuyến qua mã VietQR, cam kết tính toàn vẹn giao dịch $ACID$ và bảo đảm 100% email vé điện tử kèm mã QR được gửi thành công mà không làm nghẽn luồng người dùng:

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khách Hàng
    participant WebUI as Giao Diện Thanh Toán
    participant PService as PaymentService
    participant DB as MariaDB (cinema_db)
    participant Hub as SignalR SeatBookingHub
    participant OutboxWorker as OutboxMessageWorker (Background)
    participant SMTP as Google SMTP Mail Server

    Customer->>WebUI: Quét mã VietQR Napas 247 và bấm "Xác nhận đã chuyển khoản"
    WebUI->>PService: POST /Booking/ConfirmPayment { paymentId, transactionCode }
    activate PService

    Note over PService, DB: MỞ LOCAL TRANSACTION NGUYÊN TỬ DUY NHẤT (ACID)
    PService->>DB: BEGIN TRANSACTION (IsolationLevel.Serializable)
    PService->>DB: UPDATE payments SET Status = Paid (1), PaidAt = UtcNow WHERE PaymentId = @pId
    PService->>DB: UPDATE bookings SET Status = Confirmed (1), PaymentStatus = Paid (1)
    PService->>DB: UPDATE showtimeseats SET Status = Reserved (2), Version = Version + 1
    PService->>DB: UPDATE tickets SET Status = Issued (0), QrPayload = 'cinema-ticket:{BookingId}:{TicketCode}'
    
    Note over PService, DB: TRANSACTIONAL OUTBOX: Lưu sự kiện gửi Email vào Database
    PService->>DB: INSERT INTO outboxmessages (Id: UUID, Type: 'TicketEmail', Payload: JSON, IsProcessed: 0, RetryCount: 0)
    PService->>DB: COMMIT TRANSACTION
    
    PService->>Hub: Broadcast Group: SeatStatusChanged (seatId: 113, status: Reserved)
    PService-->>WebUI: Giao dịch thành công! (Phản hồi trong < 100ms)
    deactivate PService
    WebUI-->>Customer: Chuyển hướng tới màn hình: "Đặt vé & Thanh toán thành công!"

    Note over OutboxWorker, SMTP: Tiến trình nền Outbox Worker quét hàng đợi mỗi 5 giây
    loop Quét định kỳ mỗi 5 giây
        OutboxWorker->>DB: SELECT * FROM outboxmessages WHERE IsProcessed = 0 AND RetryCount < 3 LIMIT 10
        activate OutboxWorker
        DB-->>OutboxWorker: Danh sách tin nhắn chờ gửi (Kèm JSON Payload)
        OutboxWorker->>SMTP: Kết nối SMTP Gmail (Port 587 TLS) gửi thư E-Ticket kèm QR Code
        
        alt GỬI EMAIL THÀNH CÔNG (250 OK)
            SMTP-->>OutboxWorker: 250 Message Accepted
            OutboxWorker->>DB: UPDATE outboxmessages SET IsProcessed = 1, ProcessedAt = UtcNow
            Customer-->>Customer: Nhận Email vé xem phim vào hộp thư đến
        else SỰ CỐ MẠNG HOẶC TIMEOUT DỊCH VỤ SMTP
            SMTP-->>OutboxWorker: Connection Timeout / Error 503
            OutboxWorker->>DB: UPDATE outboxmessages SET RetryCount = RetryCount + 1, ErrorMessage = 'Timeout'
            Note over OutboxWorker: Tự động thử lại ở chu kỳ quét tiếp theo (Tối đa 3 lần)
        end
        deactivate OutboxWorker
    end
```

---

## 3. LUỒNG 3: QUẦY VÉ BOX OFFICE POS (QUÉT QR + KIỂM TRA ISPRINTED + UPSELL F&B + IN NHIỆT K80)

Biểu đồ mô tả quy trình phục vụ "Một Cửa" của Nhân viên quầy (`Staff`) tại cổng `/Counter`:

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Khách Hàng Tại Rạp
    actor Staff as Nhân Viên Quầy (Staff)
    participant POS_UI as Màn Hình POS (/Counter/Index)
    participant CounterCtrl as CounterController
    participant DB as MariaDB (cinema_db)
    participant ThermalPrinter as Máy In Hóa Đơn Nhiệt K80 (80mm)

    Customer->>Staff: Đưa màn hình điện thoại hiển thị mã QR vé từ Email
    Staff->>POS_UI: Kích hoạt Camera Quét QR (html5-qrcode)
    POS_UI->>CounterCtrl: POST /Counter/ScanBookingQr { qrCodePayload }
    activate CounterCtrl
    CounterCtrl->>DB: SELECT * FROM bookings INCLUDE Tickets, Concessions WHERE BookingCode = @code
    DB-->>CounterCtrl: Dữ liệu đơn hàng, danh sách vé và món F&B đã mua

    alt TRƯỜNG HỢP VÉ ĐÃ ĐƯỢC IN TRƯỚC ĐÓ (IsPrinted == true)
        CounterCtrl-->>POS_UI: Phản hồi: alreadyPrinted = true, PrintedAt, PrintedBy
        POS_UI-->>Staff: BẬT CẢNH BÁO ĐỎ: "Vé này đã được in lúc 18:30! Từ chối in lại."
        Staff-->>Customer: Chặn lại, từ chối in vé (Phát hiện gian lận mang mã QR dùng lại)
    else TRƯỜNG HỢP VÉ HỢP LỆ (Chưa in cuống vé cứng)
        CounterCtrl-->>POS_UI: Phản hồi hợp lệ: Danh sách ghế và bảng giá F&B
        deactivate CounterCtrl
        POS_UI-->>Staff: Hiển thị thông tin ghế và bảng nút tính tiền Upsell Bắp Nước nhanh
        
        Staff->>Customer: Tư vấn: "Anh/Chị có muốn lấy thêm Combo 1 Bắp Rang Bơ + 2 Nước Ngọt không ạ?"
        Customer->>Staff: Đồng ý mua thêm 1 Bắp Phô Mai + 2 Coca (+120.000đ)
        Staff->>POS_UI: Bấm chọn món Upsell trên màn hình cảm ứng POS
        Staff->>POS_UI: Chọn phương thức thu tiền: Tiền mặt (Cash)
        Staff->>Customer: Thu tiền mặt 120.000đ
        Staff->>POS_UI: Nhấp nút: "Xác nhận & In vé nhiệt 80mm"

        POS_UI->>CounterCtrl: POST /Counter/ConfirmPrintAndUpsell { bookingId, upsellItems, paymentMethod: 'Cash' }
        activate CounterCtrl
        CounterCtrl->>DB: BEGIN TRANSACTION (IsolationLevel.Serializable)
        Note over CounterCtrl, DB: Khóa dòng vé chống Race Condition in trùng giữa 2 quầy
        CounterCtrl->>DB: INSERT INTO bookingconcessions (Món bắp nước mua thêm)
        CounterCtrl->>DB: INSERT INTO payments (Amount: 120000, Method: 'Cash', Status: Paid)
        CounterCtrl->>DB: UPDATE tickets SET IsPrinted = 1, PrintedAt = UtcNow, PrintedByUserId = @staffId, ThermalReceiptCode = 'POS80-20261029-001'
        CounterCtrl->>DB: COMMIT TRANSACTION
        CounterCtrl-->>POS_UI: Thành công: printUrl = /Counter/PrintThermalTicket?bookingId=...
        deactivate CounterCtrl

        POS_UI->>ThermalPrinter: Kích hoạt tự động hàm in window.print() khổ 80mm
        ThermalPrinter-->>Staff: Nhả cuống vé nhiệt 80mm rời từng ghế và phiếu Voucher nhận bắp nước
        Staff-->>Customer: Giao cuống vé cứng vào phòng chiếu và phiếu nhận bắp nước tại quầy bar
    end
```
