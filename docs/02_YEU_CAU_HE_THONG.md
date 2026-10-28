# TÀI LIỆU ĐẶC TẢ YÊU CẦU HỆ THỐNG (SYSTEM REQUIREMENTS SPECIFICATIONS)
## HỆ THỐNG QUẢN LÝ CHUỖI RẠP CHIẾU PHIM TẬP TRUNG (CINEMANAGEMENT)

> **Tiêu chuẩn áp dụng**: Chuẩn đặc tả kỹ thuật phần mềm **IEEE 830**  
> **Mô hình chất lượng phần mềm**: **ISO/IEC 25010:2011** (Systems and software Quality Requirements and Evaluation)  
> **Phiên bản tài liệu**: 2.0 (Cập nhật Buổi 8)

---

## 1. GIỚI THIỆU TỔNG QUAN

Tài liệu này xác định toàn bộ các yêu cầu kỹ thuật đối với hệ thống **CineManagement**, bao gồm:
1. **Yêu cầu chức năng (Functional Requirements - FR)**: Mô tả các hành vi, quy trình nghiệp vụ và chức năng cụ thể mà hệ thống phải thực hiện cho 3 nhóm tác nhân chính: Khách hàng (`Customer`), Nhân viên quầy (`Staff`) và Quản trị viên (`Admin`).
2. **Yêu cầu phi chức năng (Non-Functional Requirements - NFR)**: Định lượng các tiêu chuẩn chất lượng về hiệu năng, độ tin cậy, an toàn bảo mật, tính toàn vẹn dữ liệu và khả năng tương thích phần cứng theo chuẩn quốc tế **ISO/IEC 25010**.

---

## 2. ĐẶC TẢ YÊU CẦU CHỨC NĂNG (FUNCTIONAL REQUIREMENTS - FR)

### 2.1. Phân hệ Khách hàng (`Customer Web Portal`)

| Mã YC | Tên chức năng | Tác nhân | Đầu vào (Input) | Quy trình xử lý nghiệp vụ & Công thức | Đầu ra (Output) |
| :--- | :--- | :---: | :--- | :--- | :--- |
| **FR-CUS-01** | **Tra cứu Lịch chiếu theo Cụm rạp** | Customer | `CinemaId`, Ngày chiếu | Truy vấn bảng `showtimes`, kiểm tra suất chiếu còn hiệu lực (`StartTime > UtcNow`). Phân loại phim theo cụm rạp, định dạng phòng chiếu (2D/3D/IMAX) và nhãn độ tuổi (P, C13, C18). | Danh sách phim, poster, trailer, các khung giờ chiếu |
| **FR-CUS-02** | **Xem Sơ đồ Ghế 2D & Giữ Chỗ Tạm** | Customer | `ShowtimeId`, Danh sách `SeatId[]` | Kiểm tra trạng thái ghế `Available`. Thực hiện Transaction Serializable: Gán `Status = Held`, `HoldExpiresAt = UtcNow + 5m`, tăng `Version++`. Phát sự kiện SignalR tới toàn bộ người đang xem cùng suất. | Ma trận 2D đổi màu ghế, đồng hồ đếm ngược 05:00 |
| **FR-CUS-03** | **Tính Toán Giá Vé & Phụ Thu Ghế** | System | `BasePrice`, `SeatTypeId[]` | Áp dụng công thức tính giá: $$P_{\text{ticket}} = P_{\text{base}} + \text{Surcharge}_{\text{SeatType}}$$ Ghế Sweetbox (`ColSpan = 2`) tính phụ thu theo cặp. | Chi tiết tiền vé từng ghế và tổng tiền vé |
| **FR-CUS-04** | **Đặt Kèm Combo Bắp Nước Online** | Customer | `FoodItemId`, Số lượng $Q_i$ | Kiểm tra món đang mở bán (`IsAvailable = true`). Cộng dồn tiền bắp nước vào đơn hàng: $$T_{\text{final}} = \sum P_{\text{ticket}} + \sum (Q_i \times P_{i,\text{F&B}})$$ | Giỏ hàng vé + F&B cập nhật tức thời |
| **FR-CUS-05** | **Thanh Toán Tức Thời VietQR Napas 247** | Customer | Thông tin thanh toán | Tạo mã VietQR động chứa số tiền chính xác và mã đơn `BK-...`. Khi ngân hàng xác nhận, chuyển `Bookings (Confirmed)`, `Tickets (Issued)`, `ShowtimeSeats (Reserved)`. | Màn hình thông báo thanh toán thành công |
| **FR-CUS-06** | **Nhận Vé Điện Tử E-Ticket qua Email** | Worker | Mã đơn hàng `BookingId` | Ghi sự kiện vào bảng `outboxmessages`. Tiến trình nền gửi email HTML chứa thông tin vé và mã QR cá nhân hóa chứa chuỗi `cinema-ticket:{BookingId}:{TicketCode}`. | Email vé điện tử gửi đến hộp thư khách hàng |

---

### 2.2. Phân hệ Nhân viên Quầy Bán vé (`Staff Box Office POS`)

| Mã YC | Tên chức năng | Tác nhân | Đầu vào (Input) | Quy trình xử lý nghiệp vụ & Công thức | Đầu ra (Output) |
| :--- | :--- | :---: | :--- | :--- | :--- |
| **FR-POS-01** | **Quét Mã QR Vé Bằng Camera** | Staff | Ảnh mã QR từ Camera / Nhập tay `BK-...` | Thư viện `html5-qrcode` giải mã chuỗi trực tiếp từ video stream, gọi API `POST /Counter/ScanBookingQr`. Truy vấn dữ liệu đơn hàng từ bảng `bookings`, `tickets`, `bookingconcessions`. | Hiển thị thông tin khách hàng, phim, ghế và F&B |
| **FR-POS-02** | **Kiểm Định Chống In Vé Lậu (IsPrinted Guard)** | System | Cờ `Ticket.IsPrinted` | Kiểm tra cờ `IsPrinted`. Nếu `true`, lập tức khóa lệnh in, phát chuông báo động và bật Modal Cảnh Báo Đỏ: *"Vé đã được in lúc {PrintedAt} bởi {StaffName}. TỪ CHỐI IN LẠI!"*. Chống gian lận dùng lại ảnh chụp màn hình QR. | Cảnh báo gian lận, chặn đứng việc in lại vé |
| **FR-POS-03** | **Bán Hàng Gia Tăng (Counter Upsell)** | Staff | Món bắp nước chọn thêm, Phương thức thanh toán | Màn hình POS hiển thị bảng gợi ý mua thêm Combo F&B. Nhân viên bấm `[+]` chọn món, hệ thống tính phụ thu, cho phép thu tiền mặt (Cash) hoặc chuyển khoản tại quầy. | Đơn hàng được cộng thêm món, tạo `Payments` |
| **FR-POS-04** | **Xuất Vé Nhiệt K80 (80mm) Rời Từng Ghế** | Staff | Lệnh in từ quầy POS | Hệ thống mở trang in `/Counter/PrintThermalTicket`, kích hoạt `window.print()`. Tách rời phiếu vé cho từng ghế (cỡ chữ số ghế to `32px` dễ nhìn) và in kèm 01 **Phiếu Nhận Bắp Nước (F&B Voucher)** riêng biệt ở cuối cuộn giấy. | Máy in nhiệt K80 nhả cuộn vé cứng |
| **FR-POS-05** | **Khóa Trạng Thái Vé Vật Lý** | System | `BookingId`, `StaffId` | Trong DB Transaction Serializable, cập nhật `tickets.IsPrinted = true`, `tickets.PrintedAt = UtcNow`, `tickets.PrintedByUserId = CurrentStaffId`, gán mã phiếu nhiệt `POS80-...`. | Vé bị khóa vĩnh viễn không thể in lại lần 2 |
| **FR-POS-06** | **Bán Vé Cho Khách Vãng Lai (Walk-in POS)** | Staff | Suất chiếu, Ghế, Combo F&B | Cho phép nhân viên chọn nhanh phim -> suất chiếu -> ghế trên ma trận 2D -> Combo bắp nước -> thu tiền mặt và in vé cứng ngay tại quầy mà không cần đăng ký tài khoản thành viên. | Vé xem phim cứng trao tay khách hàng |

---

### 2.3. Phân hệ Quản trị viên (`Admin Control Center Studio`)

| Mã YC | Tên chức năng | Tác nhân | Đầu vào (Input) | Quy trình xử lý nghiệp vụ & Công thức | Đầu ra (Output) |
| :--- | :--- | :---: | :--- | :--- | :--- |
| **FR-ADM-01** | **Quản Lý Chuỗi Cụm Rạp (`cinemas`)** | Admin | Mã rạp, Tên cụm rạp, Địa chỉ, Hotline | Khởi tạo, chỉnh sửa hoặc tạm khóa cụm rạp chi nhánh trong bảng `cinemas`. Cho phép mở rộng quy mô rạp không giới hạn trên toàn quốc. | Danh sách cụm rạp toàn chuỗi cập nhật |
| **FR-ADM-02** | **Visual Seat Layout Builder Studio** | Admin | Kích thước lưới `TotalRows` x `TotalCols` | Công cụ đồ họa thiết kế sơ đồ phòng chiếu: Tùy biến số hàng/cột, chuyển đổi nhanh cả hàng/cột thành hành lang lối đi, dùng cọ gán ghế Standard, VIP, Sweetbox Ghế Đôi, khóa bảo trì và click lại ghế để xóa. | Ma trận phòng chiếu trực quan tùy biến |
| **FR-ADM-03** | **Thuật Toán Chống Tràn Sweetbox vào Hành Lang** | System | Tọa độ `(r, c)` và `ColSpan = 2` | Kiểm tra điều kiện: $$\text{Hợp lệ} \iff \text{ô }(r, c+1)\text{ là ghế} \land (c+1 \ne \text{Cột Hành Lang})$$ Ngăn chặn tuyệt đối ghế đôi lấn sang lối đi hoặc tràn ra biên tường. | Ghế đôi nằm trọn vẹn trong từng block ghế |
| **FR-ADM-04** | **Tự Động Đánh Nhãn Hàng & Số Ghế** | System | Ma trận lưới sau khi bố trí | Bỏ qua các hàng hành lang rỗng để đánh chữ cái hàng (`A, B, C...`). Đánh số thứ tự từ trái sang phải (`1, 2, 3...`) bỏ qua các ô lối đi. Ghế đôi chỉ mang 1 số hiệu kép. | Sơ đồ phòng chiếu có nhãn chuẩn xác |
| **FR-ADM-05** | **Lập Lịch Suất Chiếu Tự Nhân Bản Ghế** | Admin | `MovieId`, `RoomId`, `StartTime`, `TicketPrice` | Kiểm tra phòng chiếu không bị trùng giờ. Khi tạo `Showtimes`, hệ thống tự động nhân bản toàn bộ ghế vật lý sang trạng thái `showtimeseats` sẵn sàng mở bán vé. | Suất chiếu mới mở bán trên hệ thống |
| **FR-ADM-06** | **Quản Lý Bảng Giá Vé & Phụ Thu Hạng Ghế** | Admin | Đơn giá vé cơ bản, Phụ thu hạng ghế | Cấu hình mức phụ thu cho từng loại ghế (`Standard = 0đ`, `VIP = +20.000đ`, `Couple = +50.000đ`). | Bảng giá vé áp dụng cho toàn bộ suất chiếu |
| **FR-ADM-07** | **Quản Lý Danh Mục Combo Bắp Nước (F&B)** | Admin | Tên món, Đơn giá niêm yết, Ảnh minh họa | Quản lý danh mục combo bắp nước, định giá bán, tải ảnh minh họa và bật/tắt trạng thái mở bán (`IsAvailable`). | Menu bắp nước cập nhật trên Web và POS |
| **FR-ADM-08** | **Dashboard KPI & Giám Sát Outbox** | Admin | Bộ lọc thời gian (Ngày/Tháng/Năm) | Thống kê doanh thu vé, doanh thu F&B, tỷ lệ lấp đầy phòng chiếu. Giám sát hàng đợi sự kiện `outboxmessages` và retry khi gửi email gặp sự cố mạng. | Biểu đồ doanh thu Chart.js & Bảng logs |

---

## 3. ĐẶC TẢ YÊU CẦU PHI CHỨC NĂNG (NON-FUNCTIONAL REQUIREMENTS - NFR)

Tuân thủ nghiêm ngặt mô hình chất lượng phần mềm quốc tế **ISO/IEC 25010**:

### 3.1. Hiệu năng & Khả năng Mở rộng (Performance Efficiency - NFR-PERF)
* **NFR-PERF-01 (Độ trễ phát sóng SignalR)**: Thời gian từ khi một ghế được khóa giữ chỗ (`HoldSeatAsync`) đến khi tất cả các trình duyệt client khác đang xem cùng suất chiếu nhận được gói tin WebSocket đổi màu ghế phải đạt:
  $$t_{\text{broadcast}} \le 50\text{ ms}$$
* **NFR-PERF-02 (Thời gian phản hồi API tra cứu)**: Thời gian phản hồi của các API tra cứu lịch chiếu, thông tin phòng chiếu và chi tiết đơn vé:
  $$t_{\text{response}} \le 200\text{ ms}$$
* **NFR-PERF-03 (Tốc độ Render Giao diện Sơ đồ Ghế)**: Ma trận ghế 2D CSS Grid hiển thị mượt mà với tốc độ **60 FPS** trên cả máy tính để bàn lẫn thiết bị di động, không bị giật lag khi số lượng ghế lên tới 500 ghế/phòng chiếu.
* **NFR-PERF-04 (Tối ưu hóa Truy vấn Cơ sở Dữ liệu)**: 100% các câu truy vấn tra cứu trạng thái ghế theo suất chiếu (`showtimeseats`) và kiểm tra mã đơn (`bookings`) phải sử dụng chỉ mục phức hợp (*Composite Indexes*), thời gian thực thi tại MariaDB đạt $t \le 20\text{ ms}$.
* **NFR-PERF-05 (Thông lượng xử lý đồng thời)**: Hệ thống chịu tải tối thiểu $\mathbf{500\text{ requests/sec}}$ trong các khung giờ cao điểm mở bán vé phim bom tấn mà không bị nghẽn cổ chai kết nối.

### 3.2. Độ tin cậy & Khả năng Phục hồi Lỗi (Reliability - NFR-REL)
* **NFR-REL-01 (Transactional Outbox Pattern - Zero Message Loss)**: Nghiệp vụ thanh toán thành công và việc ghi sự kiện gửi vé vào bảng `outboxmessages` phải diễn ra trong **cùng 01 Transaction nguyên tử duy nhất**. Tuyệt đối không thất thoát thông báo vé ngay cả khi mất điện đột ngột hoặc rớt mạng kết nối máy chủ thư điện tử:
  $$\text{Tỷ lệ mất mát thông báo vé (Email Loss)} = \mathbf{0\%}$$
* **NFR-REL-02 (Chính sách Thử lại Tự động - Retry with Exponential Backoff)**: Dịch vụ ngầm `OutboxMessageWorker` tự động quét định kỳ mỗi 5 giây. Nếu gửi email thất bại do lỗi mạng, worker tự động ghi lại `LastError`, tăng `RetryCount` lên tối đa 3 lần trước khi đánh dấu lỗi để quản trị viên can thiệp.
* **NFR-REL-03 (Tự Động Thu Hồi Ghế Quá Hạn - Auto Seat Release)**: Dịch vụ ngầm `SeatHoldTimeoutWorker` chạy chu kỳ 30 giây, tự động giải phóng toàn bộ các ghế có trạng thái `Held` mà quá hạn 5 phút về trạng thái `Available`, đồng thời broadcast WebSocket mở khóa ghế cho khách hàng khác đặt.

### 3.3. An toàn Thông tin & Phòng ngừa Gian lận (Security & Anti-Fraud - NFR-SEC)
* **NFR-SEC-01 (Cảnh báo Chống Quét Lại Vé - IsPrinted Guard)**: Hệ thống sử dụng cờ boolean `IsPrinted` kết hợp khóa `PrintedAt` và `PrintedByUserId`. Khi một vé đã được in ra giấy nhiệt tại quầy, việc quét lại mã QR này sẽ bị từ chối 100% và kích hoạt cảnh báo đỏ trên màn hình POS, ngăn chặn hoàn toàn việc chụp ảnh QR chia sẻ cho người khác.
* **NFR-SEC-02 (Phân quyền Nghiêm ngặt theo Vai trò - RBAC Matrix)**:
  * Khách hàng (`Customer`): Chỉ được thao tác trên đơn hàng của chính mình.
  * Nhân viên quầy (`Staff`): Tự động điều hướng vào màn hình Quầy vé `/Counter`, bị chặn lỗi 403 nếu cố tình truy cập vào URL báo cáo doanh thu (`/Admin/Reports`) hoặc bảng điều khiển (`/Admin/Dashboard`).
  * Quản trị viên (`Admin`): Toàn quyền cấu hình cụm rạp, phòng chiếu, giá vé, nhân sự và báo cáo.
* **NFR-SEC-03 (Bảo vệ Biểu mẫu & Chống Giả mạo - CSRF & XSS Guard)**: 100% các yêu cầu HTTP POST/PUT/DELETE bắt buộc phải có `[ValidateAntiForgeryToken]` hợp lệ. Toàn bộ chuỗi đầu vào được mã hóa HTML khi render ra giao diện. Cookie phiên được cấu hình cờ `HttpOnly = true` và `SameSite = SameSiteMode.Lax`.

### 3.4. Kiểm soát Tương tranh & Tính Toàn vẹn (ACID & Concurrency Control - NFR-CON)
* **NFR-CON-01 (Dual-Concurrency Engine)**:
  * **Optimistic Concurrency Control**: Bảng `showtimeseats` sở hữu thuộc tính `[ConcurrencyCheck] public int Version { get; set; } = 1;`. Khi cập nhật trạng thái, câu lệnh SQL tự động sinh mệnh đề `WHERE Version = @oldVersion`. Nếu có xung đột, EF Core ném `DbUpdateConcurrencyException`.
  * **Pessimistic Serializable Transaction**: Các tác vụ then chốt (`HoldSeatAsync`, `ConfirmPaymentAsync`, `ConfirmPrintAndUpsell`, `CreateWalkInOrder`) được thực thi với `IsolationLevel.Serializable`, đảm bảo không xảy ra hiện tượng đọc dữ liệu bẩn (Dirty Read) hoặc bán trùng ghế:
    $$\text{Tỷ lệ bán trùng ghế (Double-Booking)} = \mathbf{0\%}$$

### 3.5. Tính Khả dụng & Tương thích Thiết bị POS (Usability & POS Compatibility - NFR-USE)
* **NFR-USE-01 (Mẫu In Vé Nhiệt Khổ 80mm Chuẩn Quốc Tế)**: Thiết kế chuẩn xác theo kích thước giấy in nhiệt POS 80mm (`width: 80mm; font-family: monospace;`), áp dụng `@media print` ẩn hoàn toàn các thành phần trang web thừa và tự động kích hoạt hộp thoại in `window.print()`.
* **NFR-USE-02 (Tách rời Phiếu Vé & Phiếu Bắp Nước)**: Mỗi ghế xem phim được in thành một phiếu vé nhiệt riêng biệt (nhấn mạnh số ghế với cỡ chữ `32px` nổi bật), cuối cùng in kèm 01 Phiếu Nhận Bắp Nước (F&B Voucher) để khách hàng cầm riêng sang quầy đồ ăn, không làm rách cuộn vé xem phim.
