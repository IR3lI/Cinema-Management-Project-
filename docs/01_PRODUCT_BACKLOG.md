# TÀI LIỆU PRODUCT BACKLOG & KẾ HOẠCH PHÁT TRIỂN
## HỆ THỐNG QUẢN LÝ CHUỖI RẠP CHIẾU PHIM TẬP TRUNG (CINEMANAGEMENT)

> **Dự án**: CineManagement  
> **Phương pháp quản lý**: Agile / Scrum Framework  
> **Kỹ thuật ước lượng**: Planning Poker / Story Points (Dãy số Fibonacci)  
> **Phân loại ưu tiên**: Mô hình MoSCoW (Must-have, Should-have, Could-have, Won't-have)

---

## 1. TẦM NHÌN SẢN PHẨM (PRODUCT VISION)

Hệ thống **CineManagement** hướng tới xây dựng một nền tảng vận hành rạp chiếu phim hiện đại chuẩn doanh nghiệp:
* **Đối với Khách hàng**: Mang lại trải nghiệm đặt vé mượt mà, sơ đồ ghế 2D trực quan cập nhật thời gian thực qua WebSockets SignalR, tích hợp đặt Combo bắp nước và thanh toán VietQR Napas 247 tức thời với cam kết nhận vé điện tử 100% không mất mát thông báo (Zero Email Loss).
* **Đối với Nhân viên quầy (Box Office POS)**: Cung cấp giải pháp vận hành "Một Cửa", cho phép quét mã QR bằng camera thiết bị, tự động kiểm tra cờ chống in vé lậu lần 2 (`IsPrinted Guard`), hỗ trợ gợi ý bán kèm bắp nước (Upsell F&B) và in cuộn vé nhiệt K80 80mm rời từng ghế chuyên nghiệp.
* **Đối với Quản trị viên (Admin)**: Cung cấp Visual Seat Layout Builder Studio trực quan, cho phép kéo thả/click cấu hình kích thước ma trận hàng cột, tùy biến hành lang lối đi, phân bổ các hạng ghế (Standard, VIP, Couple Sweetbox) với thuật toán chống tràn Sweetbox sang lối đi và giám sát chỉ số doanh thu thời gian thực.

---

## 2. DANH MỤC EPICS (MẢNH GHÉP NGHIỆP VỤ LỚN)

Hệ thống được phân bổ thành **4 Epics trọng tâm**:

| Mã Epic | Tên Epic | Mục tiêu & Phạm vi Nghiệp vụ |
| :---: | :--- | :--- |
| **EPIC-01** | **Cổng Đặt Vé Trực Tuyến Khách Hàng (Customer Booking Experience)** | Tra cứu lịch chiếu theo cụm rạp, chọn ghế CSS Grid 2D thời gian thực qua SignalR, đặt kèm bắp nước, thanh toán VietQR và nhận vé điện tử E-Ticket qua email. |
| **EPIC-02** | **Quầy Bán Vé Box Office POS & Chống Gian Lận (Box Office POS & Anti-Fraud)** | Quét mã QR vé từ camera, kiểm tra chống in vé lần 2 (`IsPrinted`), tư vấn bán kèm bắp nước tại quầy (Upsell F&B), in vé nhiệt K80 khổ 80mm và bán vé cho khách vãng lai. |
| **EPIC-03** | **Visual Seat Layout Studio & Điều Phối Rạp (Visual Studio & Operations)** | Thiết kế sơ đồ phòng chiếu ma trận 2D trực quan, bật tắt hành lang, phân hạng ghế, chống tràn ghế đôi, quản trị phim, cụm rạp, và lập lịch suất chiếu tự động nhân bản ghế. |
| **EPIC-04** | **Báo Cáo Doanh Thu & Hạ Tầng Outbox Pattern (Reporting & Resilient Infrastructure)** | Dashboard KPI doanh thu thời gian thực, quản lý phân quyền RBAC, triển khai Transactional Outbox Pattern đảm bảo độ tin cậy gửi email 100% và cơ chế đa luồng Dual-Concurrency. |

---

## 3. BẢNG CHI TIẾT PRODUCT BACKLOG ITEMS (PBI / USER STORIES)

| Mã US | Thuộc Epic | Tên User Story & Cấu trúc nghiệp vụ | Tiêu chí Chấp thuận (Acceptance Criteria - Given/When/Then) | Story Points | Mức Ưu Tiên (MoSCoW) | Sprint Triển Khai | Trạng Thái |
| :---: | :---: | :--- | :--- | :---: | :---: | :---: | :---: |
| **US-01** | EPIC-01 | **Đăng ký & Đăng nhập Thành viên**<br>*Là một Khách hàng, tôi muốn đăng ký và đăng nhập tài khoản an toàn để lưu lại lịch sử vé.* | **Given**: Người dùng nhập email và mật khẩu hợp lệ.<br>**When**: Bấm nút "Đăng nhập".<br>**Then**: Hệ thống cấp Cookie xác thực bảo mật `HttpOnly`, chuyển hướng về trang chủ và hiển thị họ tên trên thanh menu. | 3 | **Must** | Sprint 1 | ✅ Hoàn thành |
| **US-02** | EPIC-01 | **Tra cứu Lịch Chiếu theo Cụm Rạp**<br>*Là một Khách hàng, tôi muốn lọc suất chiếu theo cụm rạp gần nhất để thuận tiện đi xem.* | **Given**: Khách hàng chọn cụm rạp (CS-QT, CS-HBT, CS-BD).<br>**When**: Chọn ngày chiếu.<br>**Then**: Hệ thống chỉ hiển thị các bộ phim và khung giờ chiếu thuộc cụm rạp đó với định dạng phòng (2D/3D/IMAX). | 5 | **Must** | Sprint 1 | ✅ Hoàn thành |
| **US-03** | EPIC-01 | **Xem Sơ đồ Ghế Ma trận 2D Real-time**<br>*Là một Khách hàng, tôi muốn thấy sơ đồ ghế trực quan với khoảng cách hành lang thực tế để dễ chọn vị trí.* | **Given**: Khách hàng chọn suất chiếu cụ thể.<br>**When**: Mở màn hình chọn ghế.<br>**Then**: Hiển thị lưới CSS Grid 2D với các khu vực ghế cách nhau bởi hành lang lối đi trống, thể hiện rõ ghế trống, ghế đang giữ và ghế đã bán. | 8 | **Must** | Sprint 2 | ✅ Hoàn thành |
| **US-04** | EPIC-01 | **Giữ Ghế 5 Phút & Đồng Bộ SignalR**<br>*Là một Khách hàng, tôi muốn ghế tôi vừa bấm chọn được giữ trong 5 phút và người khác không thể bấm trùng.* | **Given**: Ghế E5 đang ở trạng thái Available.<br>**When**: Khách hàng bấm chọn ghế E5.<br>**Then**: DB Transaction chuyển `Status = Held`, tăng `Version++`, phát sóng SignalR đổi màu ghế sang màu vàng trên máy người khác trong vòng $< 50\text{ ms}$. | 8 | **Must** | Sprint 3 | ✅ Hoàn thành |
| **US-05** | EPIC-01 | **Đặt Kèm Combo Bắp Nước Online**<br>*Là một Khách hàng, tôi muốn chọn thêm bắp rang và nước ngọt để nhận nhanh khi đến rạp.* | **Given**: Khách hàng đã chọn ghế xong.<br>**When**: Chuyển sang bước F&B và chọn Combo bắp phô mai.<br>**Then**: Hệ thống cộng dồn đơn giá bắp nước vào tổng tiền đơn hàng $T_{\text{final}} = \sum P_{\text{ticket}} + \sum P_{\text{F&B}}$. | 5 | **Should** | Sprint 2 | ✅ Hoàn thành |
| **US-06** | EPIC-01 | **Thanh Toán Tức Thời VietQR Napas 247**<br>*Là một Khách hàng, tôi muốn quét mã QR ngân hàng để thanh toán nhanh chóng không cần nhập thẻ.* | **Given**: Khách hàng ở màn hình Checkout.<br>**When**: Chọn thanh toán VietQR.<br>**Then**: Hệ thống hiển thị mã QR động chứa số tiền và mã giao dịch. Khi chuyển khoản thành công, webhook cập nhật đơn sang `Confirmed` và ghế sang `Reserved`. | 8 | **Must** | Sprint 3 | ✅ Hoàn thành |
| **US-07** | EPIC-01 | **Nhận Vé Điện Tử E-Ticket qua Email**<br>*Là một Khách hàng, tôi muốn nhận vé điện tử kèm mã QR về email để lưu trữ và xuất trình tại rạp.* | **Given**: Đơn hàng thanh toán thành công.<br>**When**: Transaction DB commit.<br>**Then**: Outbox Worker gửi email HTML chứa đầy đủ thông tin rạp, suất chiếu, ghế và ảnh mã QR Code duy nhất của từng vé. | 5 | **Must** | Sprint 4 | ✅ Hoàn thành |
| **US-08** | EPIC-02 | **Quét QR Code Vé Bằng Camera tại Quầy**<br>*Là một Nhân viên quầy, tôi muốn dùng camera máy tính quét nhanh mã QR trên điện thoại khách.* | **Given**: Khách hàng xuất trình mã QR trên email/app.<br>**When**: Nhân viên bật camera tại `/Counter`.<br>**Then**: Thư viện `html5-qrcode` quét và giải mã trong vòng 1 giây, tự động gọi API tra cứu chi tiết đơn vé. | 5 | **Must** | Buổi 8 | ✅ Hoàn thành |
| **US-09** | EPIC-02 | **Kiểm Tra Chống In Vé Lậu (IsPrinted Guard)**<br>*Là một Nhân viên quầy, tôi muốn hệ thống cảnh báo nếu mã vé này đã được in ra giấy trước đó.* | **Given**: Đơn vé đã có `IsPrinted = true`.<br>**When**: Bất kỳ ai quét lại mã QR này tại quầy.<br>**Then**: Hệ thống chặn đứng lệnh in, phát chuông cảnh báo và bật Modal Đỏ hiển thị thời gian và nhân viên đã in vé trước đó. | 8 | **Must** | Buổi 8 | ✅ Hoàn thành |
| **US-10** | EPIC-02 | **Bán Kèm Upsell Bắp Nước tại Quầy Vé**<br>*Là một Nhân viên quầy, tôi muốn tư vấn và thêm nhanh bắp nước cho khách khi họ đến đổi vé.* | **Given**: Khách đến in vé nhưng chưa mua bắp nước.<br>**When**: Nhân viên bấm nút `[+]` chọn 1 Bắp + 2 Nước ngọt.<br>**Then**: Màn hình tự cộng tổng tiền phụ thu, ghi nhận hình thức thanh toán tiền mặt và tích hợp phiếu nhận bắp nước. | 5 | **Should** | Buổi 8 | ✅ Hoàn thành |
| **US-11** | EPIC-02 | **In Vé Nhiệt K80 (80mm) Rời Từng Ghế**<br>*Là một Nhân viên quầy, tôi muốn in cuộn vé nhiệt 80mm rời từng ghế kèm 01 phiếu F&B riêng biệt.* | **Given**: Đơn vé gồm 2 vé xem phim và 1 Combo bắp nước.<br>**When**: Nhân viên nhấn "Xác nhận & In vé".<br>**Then**: Máy in nhiệt K80 xuất 2 phiếu vé xem phim (font số ghế to 32px) và 1 phiếu nhận bắp nước (F&B Voucher) tách rời ở cuối cuộn. | 8 | **Must** | Buổi 8 | ✅ Hoàn thành |
| **US-12** | EPIC-02 | **Bán Vé Cho Khách Vãng Lai (Walk-in)**<br>*Là một Nhân viên quầy, tôi muốn bán vé trực tiếp cho khách mua tại rạp không có tài khoản.* | **Given**: Khách hàng mua vé trực tiếp tại quầy.<br>**When**: Nhân viên chọn suất chiếu, chọn ghế trên sơ đồ 2D và thu tiền mặt.<br>**Then**: Hệ thống tạo đơn hàng tức thì, chuyển ghế sang `Reserved` và in cuộn vé nhiệt ngay lập tức. | 5 | **Must** | Buổi 8 | ✅ Hoàn thành |
| **US-13** | EPIC-03 | **Studio Thiết Kế Sơ Đồ Ghế (Visual Layout)**<br>*Là một Quản trị viên, tôi muốn tự do thiết kế ma trận hàng/cột và hành lang phòng chiếu một cách trực quan.* | **Given**: Admin truy cập `/ScreeningRoom/LayoutBuilder?id=1`.<br>**When**: Admin chỉnh kích thước lưới `TotalRows` x `TotalCols` hoặc bấm nút chuyển hàng/cột thành lối đi.<br>**Then**: Hệ thống render lưới 2D phản ánh đúng bố cục thực tế với khoảng cách lối đi tự nhiên. | 13 | **Must** | Buổi 8 | ✅ Hoàn thành |
| **US-14** | EPIC-03 | **Cọ Vẽ Hạng Ghế (Seat Type Painter)**<br>*Là một Quản trị viên, tôi muốn dùng cọ vẽ chọn hạng Standard, VIP, hoặc Sweetbox Ghế Đôi.* | **Given**: Admin chọn cọ "Ghế Đôi (ColSpan = 2)".<br>**When**: Nhấp vào vị trí hàng ghế mong muốn.<br>**Then**: Ô được chọn tự động mở rộng chiếm 2 cột lưới liên tiếp, hiển thị mã màu hồng đặc trưng của Sweetbox. | 8 | **Must** | Buổi 8 | ✅ Hoàn thành |
| **US-15** | EPIC-03 | **Thuật Toán Chống Tràn Sweetbox vào Hành Lang**<br>*Là một Quản trị viên, tôi muốn hệ thống ngăn không cho ghế đôi lấn sang cột hành lang.* | **Given**: Cột 4 là cột hành lang lối đi.<br>**When**: Admin cố tình đặt ghế đôi bắt đầu tại Cột 3 (chiếm cột 3 và 4).<br>**Then**: Hệ thống chặn đứng thao tác, hiển thị cảnh báo không hợp lệ và giữ nguyên tính toàn vẹn của lối đi. | 8 | **Must** | Buổi 8 | ✅ Hoàn thành |
| **US-16** | EPIC-03 | **Tự Động Đánh Nhãn Hàng & Số Ghế**<br>*Là một Quản trị viên, tôi muốn hệ thống tự động đánh mã hàng (A, B, C...) và số ghế sau khi xóa lối đi.* | **Given**: Bố cục ma trận phòng chiếu có các hàng/cột hành lang trống.<br>**When**: Admin lưu sơ đồ phòng chiếu.<br>**Then**: Hệ thống tự động bỏ qua hàng hành lang để đánh nhãn chữ cái và đánh số thứ tự từ trái sang phải chuẩn xác. | 5 | **Must** | Buổi 8 | ✅ Hoàn thành |
| **US-17** | EPIC-03 | **Quản Lý Chuỗi Cụm Rạp Chi Nhánh**<br>*Là một Quản trị viên, tôi muốn quản lý thông tin các cụm rạp trên toàn quốc.* | **Given**: Admin vào quản trị cụm rạp.<br>**When**: Thêm/Sửa cụm rạp với mã rạp, hotline, địa chỉ.<br>**Then**: Thông tin cụm rạp được lưu xuống bảng `cinemas` và hiển thị trên toàn hệ thống đặt vé. | 5 | **Must** | Sprint 3 | ✅ Hoàn thành |
| **US-18** | EPIC-03 | **Lập Lịch Suất Chiếu Tự Nhân Bản Ghế**<br>*Là một Quản trị viên, tôi muốn khi tạo suất chiếu thì toàn bộ ghế phòng đó tự động được nhân bản.* | **Given**: Admin chọn phim, phòng chiếu và thời gian chiếu.<br>**When**: Lưu suất chiếu mới.<br>**Then**: Hệ thống tự động nhân bản toàn bộ ghế vật lý sang bảng `showtimeseats` với trạng thái `Available` sẵn sàng bán vé. | 8 | **Must** | Sprint 2 | ✅ Hoàn thành |
| **US-19** | EPIC-04 | **Dashboard Báo Cáo Doanh Thu Thời Gian Thực**<br>*Là một Quản trị viên, tôi muốn xem biểu đồ doanh thu theo phim, rạp và ngày.* | **Given**: Admin mở trang `/Admin/Reports`.<br>**When**: Chọn khoảng thời gian lọc báo cáo.<br>**Then**: Hiển thị biểu đồ cột và tròn Chart.js phân tích doanh thu vé, doanh thu F&B và tỷ lệ lấp đầy rạp. | 8 | **Should** | Sprint 4 | ✅ Hoàn thành |
| **US-20** | EPIC-04 | **Hạ Tầng Transactional Outbox Pattern**<br>*Là Kiến trúc sư hệ thống, tôi muốn đảm bảo 100% email vé được gửi ngay cả khi SMTP rớt mạng.* | **Given**: Khách thanh toán thành công trong lúc máy chủ SMTP lỗi kết nối.<br>**When**: Transaction DB commit sự kiện vào bảng `outboxmessages`.<br>**Then**: Dịch vụ nền `OutboxMessageWorker` tự động lưu lại và retry gửi thư sau khi mạng phục hồi, không làm mất vé. | 13 | **Must** | Sprint 4 | ✅ Hoàn thành |

---

## 4. KẾ HOẠCH BÀN GIAO SPRINT & PHÂN BỔ NGUỒN LỰC

* **Sprint 1 (Khởi động & Nền tảng)**: Quản lý danh mục phim, cụm rạp, hệ thống xác thực Cookie Identity, phân quyền RBAC (US-01, US-02).
* **Sprint 2 (Lưới Ghế 2D & F&B)**: Ma trận ghế 2D CSS Grid, đặt combo bắp nước online, lập lịch suất chiếu tự nhân bản ghế (US-03, US-05, US-18).
* **Sprint 3 (Thời gian thực & Thanh toán)**: Real-time SignalR WebSockets giữ ghế 5 phút, Dual-Concurrency Control, thanh toán VietQR Napas 247 (US-04, US-06, US-17).
* **Sprint 4 (Outbox & Báo cáo)**: Transactional Outbox Pattern (Zero Email Loss), gửi E-Ticket QR Code, Dashboard báo cáo doanh thu KPI (US-07, US-19, US-20).
* **Buổi 8 (Nâng cấp Hoàn thiện)**: Box Office POS Console, quét QR Camera, kiểm tra cờ chống in lậu `IsPrinted`, Upsell F&B tại quầy, in vé nhiệt K80 80mm rời từng ghế, Visual Seat Layout Builder Studio (US-08, US-09, US-10, US-11, US-12, US-13, US-14, US-15, US-16).
