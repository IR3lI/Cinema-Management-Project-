# TÀI LIỆU QUY TRÌNH NGHIỆP VỤ HỆ THỐNG (SYSTEM WORKFLOWS)
## HỆ THỐNG QUẢN LÝ CHUỖI RẠP CHIẾU PHIM TẬP TRUNG (CINEMANAGEMENT)

> Hệ thống kiến trúc: 5-Tier Enterprise Architecture  
> Công nghệ cốt lõi: SignalR WebSocket, Dual-Concurrency Engine, Transactional Outbox Pattern  
> Đối tượng vận hành: Khách hàng (Customer), Nhân viên quầy (Staff POS), Hệ thống dịch vụ nền (Background Services)

---

## 1. SƠ ĐỒ WORKFLOW TỔNG THỂ HỆ THỐNG

Sơ đồ biểu diễn dòng chảy dữ liệu và sự tương tác xuyên suốt giữa Khách hàng trực tuyến, Hệ thống xử lý trung tâm và Nhân viên quầy vé Box Office:

```mermaid
flowchart TD
    subgraph KHACH_HANG ["1. KHACH HANG (ONLINE PORTAL)"]
        A1["Truy cap Web / Tra cuu Lich chieu theo Cum rap"] --> A2["Mo So do ghe 2D CSS Grid"]
        A2 --> A3["Bam giu ghe (SignalR khoa ghe 5 phut)"]
        A3 --> A4["Chon Combo Bap Nuoc F&B (Tuy chon)"]
        A4 --> A5["Quet ma VietQR Napas 247 thanh toan"]
        A5 --> A6["Nhan Email ve dien tu E-Ticket kem ma QR"]
    end

    subgraph HE_THONG_LOI ["2. HE THONG LOI VA TIEN TRINH NEN (CINEMANAGEMENT)"]
        B1["SignalR SeatBookingHub: Dong bo trang thai ghe trong 50ms"]
        B2["Dual-Concurrency Engine: Transaction Serializable va Version Token"]
        B3["SeatHoldTimeoutWorker: Quet thu hoi ghe qua han 5 phut"]
        B4["Transactional Outbox Worker: Gui Email E-Ticket qua Gmail SMTP"]
    end

    subgraph QUAY_POS ["3. NHAN VIEN QUAY (BOX OFFICE POS)"]
        C1["Khach xuat trinh ma QR tai quay rap"] --> C2["Camera POS quet ma QR (html5-qrcode)"]
        C2 --> C3{"Kiem tra co IsPrinted?"}
        C3 -- "DA IN TRUOC DO" --> C4["CANH BAO DO: Gian lan dung lai QR! Tu choi in"]
        C3 -- "HOP LE (CHUA IN)" --> C5["Goi y ban kem Combo Bap Nuoc (Upsell F&B)"]
        C5 --> C6["Thu tien mat / Cap nhat IsPrinted = true"]
        C6 --> C7["Xuat lenh in may in nhiet K80: Ve xem phim va Phieu Voucher F&B"]
    end

    A3 <--> B1
    A3 --> B2
    B3 -.->|Tu dong nha ghe neu khong thanh toan| B1
    A5 --> B2
    B2 --> B4
    B4 -.->|Chuyen phat ve| A6

    A6 --> C1
    C2 --> B2
    C6 --> B2
```

---

## 2. WORKFLOW 1: QUY TRÌNH ĐẶT VÉ VÀ GIỮ GHẾ TRỰC TUYẾN (ONLINE BOOKING)

Quy trình xử lý chọn suất chiếu, ma trận ghế 2D, khóa ghế thời gian thực qua SignalR, thanh toán tức thời và xuất vé nguyên tử:

```mermaid
flowchart TD
    Start([Khach hang truy cap Web/App]) --> Step1["1. Chon Phim, Cum Rap va Ngay Chieu"]
    Step1 --> Step2["2. Chon Khung gio Suat chieu cu the"]
    
    Step2 --> Step3["3. Tai So do phong chieu 2D CSS Grid"]
    Step3 --> Step3_Conn["Ket noi WebSocket: JoinShowtimeGroup(showtime_id)"]
    
    Step3_Conn --> Step4["4. Khach hang click chon ghe"]
    
    Step4 --> Check_Seat{"Kiem tra trang thai ghe trong DB"}
    
    Check_Seat -- "Ghe dang HELD hoac RESERVED" --> Err_Seat["Tu choi: Bao loi ghe da co nguoi chon"]
    Err_Seat --> Step4
    
    Check_Seat -- "Ghe AVAILABLE (Hop le)" --> Lock_DB["Mo DB Transaction Serializable"]
    
    Lock_DB --> Update_Seat["Cap nhat: Status = Held (1)
    HeldByUserId = CurrentUser
    HoldExpiresAt = Now + 5 phut
    Version = Version + 1"]
    
    Update_Seat --> Commit_Hold["COMMIT TRANSACTION"]
    
    Commit_Hold --> Broad_Hold["SignalR Broadcast nhom showtime_id:
    - May khach chon: Ghe doi Xanh Duong va Dem nguoc 05:00
    - May nguoi khac: Ghe doi Vang va Khoa nut bam"]
    
    Broad_Hold --> Step5{"Khach co chon them Bap Nuoc F&B?"}
    Step5 -- "Co" --> Add_FB["Cong don tien Combo Bap Nuoc vao don hang"]
    Step5 -- "Khong" --> Step6["Chuyen sang man hinh Thanh toan Checkout"]
    Add_FB --> Step6
    
    Step6 --> Step7["Sinh ma thanh toan VietQR Napas 247 dong"]
    Step7 --> Check_Payment{"Kiem tra thanh toan trong 5 phut?"}
    
    Check_Payment -- "HET 5 PHUT KHONG TRA TIEN" --> Worker_Release["SeatHoldTimeoutWorker can thiep:
    - UPDATE ghe ve Available (0)
    - Xoa HeldByUserId
    - SignalR doi ghe lai mau xam"]
    Worker_Release --> End_Fail([Huy don hang giu cho])
    
    Check_Payment -- "THANH TOAN THANH CONG" --> Atomic_Trans["Mo Local Transaction duy nhat"]
    
    Atomic_Trans --> Save_Paid["1. Cap nhat Payment: Status = Paid
    2. Cap nhat Booking: Status = Confirmed
    3. Cap nhat Seat: Status = Reserved (2)
    4. Sinh ban ghi Tickets kem ma QR doc ban
    5. Ghi su kien gui Email vao outboxmessages"]
    
    Save_Paid --> Commit_Paid["COMMIT TRANSACTION TOAN BO"]
    
    Commit_Paid --> Broad_Res["SignalR Broadcast: Ghe doi mau Do - Da ban vinh vien"]
    Commit_Paid --> UI_Success["Man hinh Web: Thong bao Dat ve thanh cong"]
    
    Commit_Paid --> Outbox_Send["OutboxWorker quet hang doi gui Email E-Ticket kem ma QR"]
    Outbox_Send --> End_Success([Khach nhan ve qua Email va Hoan tat])
```

---

## 3. WORKFLOW 2: QUY TRÌNH VẬN HÀNH QUẦY VÉ POS VÀ SOÁT VÉ IN NHIỆT K80

Quy trình phục vụ khách tại quầy, kiểm tra cờ chống in lậu vé (`IsPrinted Guard`), gợi ý bán kèm bắp nước (Upsell F&B) và xuất cuộn vé nhiệt 80mm:

```mermaid
flowchart TD
    Start_POS([Khach hang den Quay ve Box Office]) --> Check_Type{"Hinh thuc mua ve?"}
    
    Check_Type -- "Khach vang lai Walk-in" --> POS_Select["Nhan vien chon Phim, Suat chieu, Ghe tren man hinh POS"]
    POS_Select --> POS_Upsell
    
    Check_Type -- "Khach da mua Online" --> POS_Scan["Nhan vien bat Camera quet ma QR tren dien thoai khach"]
    POS_Scan --> POS_Find["He thong tra cuu don hang theo BookingCode"]
    
    POS_Find --> Check_Printed{"Kiem tra co IsPrinted cua ve?"}
    
    Check_Printed -- "IsPrinted == TRUE (Da in truoc do)" --> Warn_Fraud["CANH BAO DO TREN MAN HINH POS:
    Ve da duoc in truoc do!
    Hien thi thoi gian in va Ma nhan vien da in"]
    Warn_Fraud --> Stop_Fraud([Tu choi in ve va Chan gian lan])
    
    Check_Printed -- "IsPrinted == FALSE (Hop le)" --> Show_Detail["Hien thi chi tiet: Ten phim, Phong, Gio chieu, So ghe"]
    
    Show_Detail --> POS_Upsell["Giao dien tu dong bat Bang goi y Upsell Combo Bap Nuoc"]
    
    POS_Upsell --> Ask_Customer{"Khach co mua them Bap Nuoc?"}
    Ask_Customer -- "Dong y mua them" --> Add_Concession["Nhan vien click chon Combo, tong tien tu cong don"]
    Ask_Customer -- "Khong mua" --> POS_Pay["Xac nhan thanh toan"]
    Add_Concession --> POS_Pay
    
    POS_Pay --> POS_Method["Chon phuong thuc: Tien mat hoac Quet the POS"]
    POS_Method --> POS_Trans["Mo DB Transaction bao ve co in"]
    
    POS_Trans --> Update_Print["1. Ghi nhan tien F&B mua them (neu co)
    2. Cap nhat tickets: IsPrinted = TRUE, PrintedAt = Now, PrintedByUserId = StaffId
    3. Sinh ma phieu in nhiet: ThermalReceiptCode"]
    
    Update_Print --> Commit_Print["COMMIT TRANSACTION"]
    
    Commit_Print --> Print_Command["Kich hoat lenh in nhiet K80 kho 80mm"]
    
    Print_Command --> Print_Output["May in nhiet nha cuon giay K80 gom:
    - Phieu ve xem phim (Font so ghe to 32px ro net)
    - Phieu Voucher nhan Bap Nuoc tach roi o cuoi cuon"]
    
    Print_Output --> Handover["Nhan vien giao ve cung va huong dan khach vao phong chieu"]
    Handover --> End_POS([Hoan tat giao dich tai quay])
```

---

## 4. WORKFLOW 3: QUY TRÌNH TỰ ĐỘNG HÓA NỀN VÀ XỬ LÝ SỰ CỐ (BACKGROUND WORKERS)

Quy trình chạy ngầm bảo đảm tính toàn vẹn hệ thống: Tự động thu hồi ghế quá hạn 5 phút và cơ chế Retry chuyển phát email vé qua Transactional Outbox Pattern:

```mermaid
flowchart TD
    subgraph WORKER_TIMEOUT ["4.1. SEAT HOLD TIMEOUT WORKER (QUET MOI 30 GIAY)"]
        T1([Bo dem 30 giay kich hoat]) --> T2["Quet DB: SELECT FROM showtimeseats WHERE Status = Held AND HoldExpiresAt <= Now"]
        T2 --> T3{"Co ghe bi giu qua han 5 phut?"}
        T3 -- "Khong co" --> T_Sleep["Nghi 30 giay cho chu ky tiep theo"]
        T3 -- "Phat hien ghe qua han" --> T4["Mo Transaction:
        UPDATE showtimeseats
        SET Status = Available (0), HeldByUserId = NULL, Version = Version + 1"]
        T4 --> T5["COMMIT TRANSACTION"]
        T5 --> T6["SignalR phat song nhom showtime_id:
        - Doi mau ghe ve lai mau xam
        - Mo khoa cho nguoi khac dat ngay lap tuc"]
        T6 --> T_Sleep
    end

    subgraph WORKER_OUTBOX ["4.2. TRANSACTIONAL OUTBOX WORKER (QUET MOI 5 GIAY)"]
        O1([Bo dem 5 giay kich hoat]) --> O2["Quet DB: SELECT FROM outboxmessages WHERE IsProcessed = 0 AND RetryCount < 3"]
        O2 --> O3{"Co thong diep ve cho gui?"}
        O3 -- "Hang doi rong" --> O_Sleep["Nghi 5 giay cho chu ky tiep theo"]
        O3 -- "Co thu can gui" --> O4["Giai ma Payload: Lay BookingId, Email khach, Ma ve, QR Code"]
        O4 --> O5["Ket noi Google SMTP Server smtp.gmail.com:587"]
        
        O5 --> Check_SMTP{"Ket noi gui mail thanh cong?"}
        
        Check_SMTP -- "THANH CONG (SMTP 250)" --> O_Success["UPDATE outboxmessages
        SET IsProcessed = 1, ProcessedAt = Now"]
        O_Success --> O_Delivered["Khach nhan duoc E-Ticket trong hop thu"]
        
        Check_SMTP -- "SU CO MANG / TIMEOUT / GMAIL 503" --> O_Fail["UPDATE outboxmessages
        SET RetryCount = RetryCount + 1, ErrorMessage = 'Connection Timeout'"]
        O_Fail --> O_Retry["Ghi nhan loi va Tu dong thu lai o chu ky 5 giay ke tiep"]
        
        O_Success --> O_Sleep
        O_Retry --> O_Sleep
    end
```

---

## 5. BẢNG TỔNG KẾT VÀ CƠ CHẾ ĐẢM BẢO TÍNH TOÀN VẸN

| Quy trình nghiệp vụ | Tác nhân chính | Cơ chế kỹ thuật cốt lõi | Kết quả đảm bảo |
| :--- | :--- | :--- | :--- |
| **Workflow 1: Đặt vé Online** | Khách hàng | IsolationLevel.Serializable, Version Token, SignalR WebSockets | Đặt vé đồng thời không trùng ghế, phản hồi dưới 50ms. |
| **Workflow 2: Quầy POS Box Office** | Nhân viên quầy | Cờ kiểm soát IsPrinted Guard, Camera html5-qrcode, In nhiệt K80 | Soát vé dưới 10 giây, chống in lậu, in vé phim và F&B Voucher. |
| **Workflow 3: Tiến trình nền (Workers)** | Background Services | Transactional Outbox Pattern, Polling định kỳ 30s và 5s | Tự động giải phóng ghế quá hạn, cam kết 100% gửi vé thành công. |
