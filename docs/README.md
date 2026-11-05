# THƯ MỤC TÀI LIỆU KỸ THUẬT & THIẾT KẾ HỆ THỐNG
## HỆ THỐNG QUẢN LÝ CHUỖI RẠP CHIẾU PHIM TẬP TRUNG (CINEMANAGEMENT)

Thư mục `docs/` chứa toàn bộ bộ tài liệu đặc tả kỹ thuật, thiết kế kiến trúc, phân rã chức năng và cơ sở dữ liệu của dự án **CineManagement**, tuân thủ tiêu chuẩn đặc tả phần mềm quốc tế **IEEE 830** và mô hình chất lượng phần mềm **ISO/IEC 25010**.

---

## 📚 MỤC LỤC TỔNG HỢP CÁC TÀI LIỆU

| STT | Tài Liệu | Nội Dung Trọng Tâm | Tệp Tin |
| :---: | :--- | :--- | :--- |
| **01** | **Product Backlog & Kế Hoạch Sprint** | Tầm nhìn sản phẩm, danh mục 4 Epics lớn, 20 User Stories chi tiết với tiêu chí nghiệm thu Given-When-Then, Story Points, độ ưu tiên MoSCoW và phân bổ Sprint 1 đến Buổi 8. | [01_PRODUCT_BACKLOG.md](./01_PRODUCT_BACKLOG.md) |
| **02** | **Đặc Tả Yêu Cầu Hệ Thống (SRS)** | Đặc tả chi tiết 20 Yêu cầu chức năng (FR) cho 3 tác nhân (Customer, Staff, Admin) và 5 nhóm Yêu cầu phi chức năng (NFR) theo chuẩn ISO/IEC 25010 (Hiệu năng, Độ tin cậy, Bảo mật, ACID, Tương thích POS). | [02_YEU_CAU_HE_THONG.md](./02_YEU_CAU_HE_THONG.md) |
| **03** | **Sơ Đồ Phân Rã Chức Năng (FDD)** | Biểu đồ cây phân rã chức năng 3 cấp độ (Mức 0 -> Mức 1 -> Mức 2 -> Mức 3), bảng mô tả chi tiết 42 chức năng nghiệp vụ và Ma trận phân quyền CRUD theo vai trò (RBAC Access Matrix). | [03_SO_DO_PHAN_RA_CHUC_NANG.md](./03_SO_DO_PHAN_RA_CHUC_NANG.md) |
| **04** | **Thiết Kế Kiến Trúc Hệ Thống** | Mô hình kiến trúc phân tầng 5 lớp (5-Tier Enterprise Architecture), sơ đồ kiến trúc tổng thể Mermaid, 4 Biểu đồ tuần tự (Sequence Diagrams) cho các luồng cốt lõi: SignalR giữ ghế, Outbox thanh toán, POS quét QR in vé nhiệt K80 và Visual Seat Studio. | [04_THIET_KE_HE_THONG.md](./04_THIET_KE_HE_THONG.md) |
| **05** | **Thiết Kế Cơ Sở Dữ Liệu & ERD 3NF** | Sơ đồ quan hệ thực thể (ERD Mermaid) 12 bảng chuẩn hóa 3NF, từ điển dữ liệu chi tiết (Data Dictionary), chiến lược khóa phiên bản tương tranh lạc quan `Version` và chỉ mục phức hợp Composite Indexing. | [05_THIET_KE_CO_SO_DU_LIEU.md](./05_THIET_KE_CO_SO_DU_LIEU.md) |
| **06** | **Quy Trình Nghiệp Vụ Hệ Thống (Workflows)** | Sơ đồ workflow tổng thể và các quy trình chi tiết: Đặt vé online, Quầy vé POS soát vé in nhiệt K80, Tự động hóa tiến trình nền. | [06_QUY_TRINH_NGHIEP_VU_WORKFLOW.md](./06_QUY_TRINH_NGHIEP_VU_WORKFLOW.md) |

---

## 🎯 CÁC TÍNH NĂNG & ĐỘT PHÁ CÔNG NGHỆ CHÍNH

1. **Real-time SignalR Seat Map (< 50ms)**: Đồng bộ tức thì trạng thái giữ ghế trên ma trận 2D CSS Grid giữa hàng trăm người dùng cùng suất chiếu qua WebSockets.
2. **Transactional Outbox Pattern (Zero Email Loss)**: Đảm bảo 100% email vé điện tử E-Ticket kèm mã QR cá nhân hóa được gửi tin cậy ngay cả khi mất điện đột ngột hoặc máy chủ thư điện tử gián đoạn.
3. **Staff Box Office POS "Một Cửa"**: Quét mã QR vé từ camera thiết bị, tự động kiểm tra cờ chống in vé lậu lần 2 (`IsPrinted Guard`), gợi ý bán kèm bắp nước (Upsell F&B) và in cuộn vé nhiệt K80 khổ 80mm rời từng ghế.
4. **Visual Seat Layout Builder Studio**: Trình thiết kế sơ đồ phòng chiếu trực quan cho phép Admin tự do điều chỉnh ma trận hàng/cột, bật tắt hành lang ngang/dọc, cọ vẽ hạng ghế với thuật toán chống tràn Sweetbox sang lối đi và tự động đánh lại nhãn ghế chuẩn xác.
