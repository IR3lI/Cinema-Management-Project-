# TÀI LIỆU QUY TRÌNH NGHIỆP VỤ HỆ THỐNG (SYSTEM WORKFLOWS)
## HỆ THỐNG QUẢN LÝ CHUỖI RẠP CHIẾU PHIM TẬP TRUNG (CINEMANAGEMENT)

> Đối tượng sử dụng: Khách hàng (Customer Online) và Nhân viên quầy (Staff POS)  
> Mục tiêu thiết kế: Tối ưu trải nghiệm đặt vé, loại bỏ trùng ghế thời gian thực, quản lý bán vé và soát vé quầy chặt chẽ, chống gian lận.

---

## 1. SƠ ĐỒ WORKFLOW TỔNG THỂ HỆ THỐNG

Sơ đồ tổng quan biểu diễn dòng chảy nghiệp vụ xuyên suốt từ khi khách hàng tìm kiếm suất chiếu trên website/ứng dụng cho đến khi nhận vé và thực hiện check-in tại cụm rạp:

```mermaid
flowchart LR
    subgraph KHACH_HANG ["KHACH HANG TRUC TUYEN"]
        direction TB
        A1["1. Chon Phim va Suat chieu"] --> A2["2. Chon Ghe tren so do 2D"]
        A2 --> A3["3. Chon Bap nuoc F&B (Tuy chon)"]
        A3 --> A4["4. Quet ma VietQR thanh toan"]
        A4 --> A5["5. Nhan Ve dien tu qua Email"]
    end

    subgraph HE_THONG ["HE THONG CINEMANAGEMENT"]
        direction TB
        B1["Giu ghe tam thoi 5 phut"]
        B2["Xac nhan thanh toan va Xuat ve"]
    end

    subgraph QUAY_VE ["QUAY VE VA CHECK-IN"]
        direction TB
        C1["Quet ma QR tren ve"] --> C2{"Kiem tra trang thai ve"}
        C2 -- "Hop le" --> C3["In ve K80 va Vao phong chieu"]
        C2 -- "Da su dung" --> C4["Tu choi vao rap"]
    end

    A2 -->|Yeu cau giu cho| B1
    A4 -->|Xac thuc giao dich| B2
    B2 -->|Gui E-Ticket| A5
    A5 -->|Xuat trinh ve| C1
```

---

## 2. LUỒNG NGHIỆP VỤ 1: ĐẶT VÉ VÀ THANH TOÁN TRỰC TUYẾN (ONLINE BOOKING)

Quy trình khách hàng chọn suất chiếu, chọn vị trí ghế trên sơ đồ 2D trực quan, giữ chỗ tạm thời trong 5 phút và thanh toán tự động qua mã VietQR Napas 247:

```mermaid
flowchart TD
    Start([Bat dau]) --> Step1["1. Chon Phim, Rap va Suat chieu"]
    Step1 --> Step2["2. Chon vi tri ghe tren so do"]
    
    Step2 --> CheckSeat{"Ghe con trong?"}
    CheckSeat -- "Da co nguoi chon" --> MsgSeat["Bao loi: Ghe da duoc giu"]
    MsgSeat --> Step2
    
    CheckSeat -- "Hop le" --> HoldSeat["Giu ghe trong 5 phut va dong bo Realtime"]
    HoldSeat --> Step3{"Chon them Bap nuoc F&B?"}
    
    Step3 -- "Co" --> AddFood["Them Combo Bap nuoc vao don"]
    Step3 -- "Khong" --> Payment["Chuyen sang buoc Thanh toan"]
    AddFood --> Payment
    
    Payment --> ScanQR["Hien thi ma VietQR dong Napas 247"]
    ScanQR --> CheckPay{"Thanh toan trong 5 phut?"}
    
    CheckPay -- "Het thoi gian" --> CancelOrder["Tu dong huy don va nha ghe"]
    CancelOrder --> EndFail([Ket thuc: Dat ve that bai])
    
    CheckPay -- "Thanh toan thanh cong" --> ConfirmOrder["Xac nhan dat ve va Khoa ghe vinh vien"]
    ConfirmOrder --> SendTicket["Xuat ve QR va gui Email cho khach"]
    SendTicket --> EndSuccess([Ket thuc: Dat ve thanh cong])
```

### Các bước thực hiện chi tiết:
1. **Lựa chọn thông tin suất chiếu**: Khách hàng tìm kiếm theo cụm rạp, bộ phim và khung giờ chiếu mong muốn.
2. **Chọn vị trí ghế ngồi**: Giao diện hiển thị sơ đồ phòng chiếu 2D trực quan (Ghế thường, Ghế VIP, Ghế đôi). Trạng thái ghế được cập nhật theo thời gian thực (Realtime).
3. **Giữ chỗ tạm thời**: Ghế được hệ thống khóa tạm thời trong 5 phút cho riêng khách hàng, ngăn chặn tình trạng nhiều người cùng bấm một ghế.
4. **Bắp nước (F&B) & Thanh toán**: Khách hàng có thể chọn thêm combo bắp nước hoặc chuyển thẳng đến màn hình thanh toán VietQR.
5. **Xử lý kết quả giao dịch**:
   - Nếu quá 5 phút chưa hoàn tất thanh toán: Hệ thống tự động hủy đơn và trả ghế về trạng thái sẵn sàng để người khác đặt.
   - Nếu thanh toán thành công: Đơn hàng được xác nhận, ghế chuyển sang trạng thái đã bán, mã vé QR và thông tin hóa đơn được gửi đến Email của khách hàng.

---

## 3. LUỒNG NGHIỆP VỤ 2: BÁN VÉ VÀ SOÁT VÉ TẠI QUẦY (BOX OFFICE POS)

Quy trình phục vụ tại quầy vé của rạp bao gồm 2 nghiệp vụ chính: Check-in / in vé cho khách đã đặt Online và Bán vé trực tiếp cho khách vãng lai:

```mermaid
flowchart TD
    Start([Khach den quay Box Office]) --> CheckType{"Hinh thuc mua ve?"}
    
    CheckType -- "Khach da mua Online" --> ScanQR["Nhan vien quet ma QR tren dien thoai"]
    ScanQR --> CheckTicket{"Kiem tra trang thai ve"}
    CheckTicket -- "Ve da in hoac da check-in" --> Reject["Canh bao: Ve khong hop le, tu choi in"]
    Reject --> EndReject([Tu choi])
    
    CheckTicket -- "Ve hop le" --> UpsellOnline["Goi y mua them Combo Bap nuoc"]
    
    CheckType -- "Khach mua truc tiep tai quay" --> SelectSeat["Nhan vien chon Suat chieu va Ghe"]
    SelectSeat --> UpsellPOS["Goi y mua Combo Bap nuoc"]
    
    UpsellOnline --> ChooseFood{"Khach dong y mua F&B?"}
    UpsellPOS --> ChooseFood
    
    ChooseFood -- "Co" --> AddFB["Them bap nuoc vao don hang"]
    ChooseFood -- "Khong" --> PayPOS["Thanh toan"]
    AddFB --> PayPOS
    
    PayPOS --> CashCard["Thu tien (Tien mat / The POS)"]
    CashCard --> PrintTicket["In ve nhiet K80 va Phieu nhan Bap nuoc"]
    PrintTicket --> Handover["Giao ve va huong dan khach vao phong chieu"]
    Handover --> EndPOS([Hoan tat giao dich])
```

### Các bước thực hiện chi tiết:
1. **Tiếp nhận khách hàng**:
   - **Khách hàng đã đặt Online**: Nhân viên dùng máy quét camera POS quét mã QR trên điện thoại khách hàng. Hệ thống kiểm tra cờ trạng thái (`IsPrinted`). Nếu vé đã in hoặc đã qua cổng trước đó, hệ thống sẽ đưa ra cảnh báo từ chối để chống gian lận.
   - **Khách hàng mua trực tiếp (Walk-in)**: Nhân viên chọn nhanh phim, suất chiếu và vị trí ghế theo yêu cầu của khách trên màn hình cảm ứng POS.
2. **Bán gia tăng dịch vụ (Upsell F&B)**: Màn hình gợi ý các gói combo bắp nước đang có ưu đãi để nhân viên tư vấn nhanh cho khách.
3. **Thanh toán & Xuất vé**: Nhân viên thu tiền mặt hoặc quẹt thẻ POS. Máy in nhiệt K80 in ngay cuộn vé xem phim cùng phiếu nhận bắp nước tách rời.

---

## 4. TỔNG KẾT VÀ ĐẶC ĐIỂM TRỌNG TÂM CỦA 2 LUỒNG NGHIỆP VỤ

| Tiêu chí | Luồng 1: Đặt vé Trực tuyến (Online Booking) | Luồng 2: Quầy vé Box Office (POS & Check-in) |
| :--- | :--- | :--- |
| **Đối tượng thao tác** | Khách hàng thao tác trên Web/Mobile Browser | Nhân viên thu ngân/soát vé thao tác trên máy POS |
| **Giao diện chính** | Sơ đồ ghế 2D, cổng thanh toán VietQR động | Màn hình bán vé nhanh POS, camera quét mã QR vé |
| **Cơ chế kiểm soát** | Giữ chỗ 5 phút, tự động hủy nếu quá hạn | Kiểm tra cờ IsPrinted, chống tái sử dụng vé gian lận |
| **Đầu ra nghiệp vụ** | Mã vé điện tử E-Ticket, QR Code qua Email | Cuộn vé in nhiệt K80 (Vé xem phim + Voucher F&B) |
| **Thời gian xử lý** | Dưới 2 phút / lượt giao dịch | Dưới 15 giây / lượt soát vé hoặc bán vé tại quầy |
