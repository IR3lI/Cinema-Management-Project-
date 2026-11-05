# Quickstart Validation Guide: CineManagement (CLO1)

**Feature**: `001-cinema-chain-booking` | **Date**: 2026-11-04  
**Target**: Xác thực tích hợp 4 trụ cột kỹ thuật đáp ứng chuẩn CLO1.

---

## 1. Yêu cầu tiên quyết (Prerequisites)

- [.NET 10.0 SDK](https://dotnet.microsoft.com/) đã cài đặt trên máy.
- [Docker Engine & Docker Compose](https://docs.docker.com/compose/) đang chạy.
- MariaDB / MySQL client (hoặc dùng qua container docker).
- Trình duyệt web hỗ trợ HTML5 / WebSockets (Chrome, Firefox, Edge).

---

## 2. Kịch bản xác thực 1: Khởi động Cụm Container & Hạ tầng Cloud-Native

### Mục tiêu:
Xác thực `docker-compose.yml` khởi động đồng bộ 4 dịch vụ (`mariadb`, `minio`, `app`, `nginx`) với tính năng tự tạo bucket và phân giải SSL.

### Các bước thực hiện:
```bash
# 1. Khởi động toàn bộ cụm container ở chế độ nền
docker compose up -d

# 2. Kiểm tra trạng thái sức khỏe của các container
docker compose ps
```

### Kết quả mong đợi:
- `cinemanagement-mariadb` đạt trạng thái `healthy`.
- `cinemanagement-minio` chạy bình thường trên cổng `9000` (API) và `9001` (Console).
- `cinemanagement-minio-init` hoàn thành nhiệm vụ tạo bucket `cinemanagement-media` và thoát mã 0 (`Exited (0)`).
- `cinemanagement-app` kết nối thành công tới MariaDB và MinIO, lắng nghe cổng 8080 nội bộ.
- `cinemanagement-nginx` phân giải HTTPS cổng 443 và HTTP cổng 80.

---

## 3. Kịch bản xác thực 2: Kiểm thử Module Lưu trữ Cloudflare R2 / MinIO (`IStorageService`)

### Mục tiêu:
Chứng minh hệ thống lưu trữ hoàn toàn phi trạng thái (Stateless), ảnh poster và mã QR được upload trực tiếp lên S3 API mà không ghi đè vào đĩa cục bộ `wwwroot/uploads`.

### Các bước thực hiện:
1. Đăng nhập trang Quản trị (`/Admin/Movie/Create`).
2. Tải lên một bộ phim mới kèm tập tin poster ảnh `test-poster.webp`.
3. Kiểm tra bản ghi trong bảng `movies` và kiểm tra bucket trên MinIO:
   ```bash
   # Mở MinIO Console trên trình duyệt: http://localhost:9001 (minioadmin / minioadmin123)
   # Hoặc kiểm tra qua curl:
   curl -I http://localhost:9000/cinemanagement-media/movies/{generated_key}.webp
   ```

### Kết quả mong đợi:
- Phản hồi `HTTP 200 OK` từ MinIO S3 API.
- Trường `PosterUrl` trong bảng `movies` trỏ tới đường dẫn S3 Public URL (`http://localhost:9000/...` hoặc CDN Cloudflare R2).
- Thư mục `wwwroot/uploads` cục bộ trong container hoàn toàn không có tệp mới được sinh ra.

---

## 4. Kịch bản xác thực 3: Kiểm thử Thuật toán Aisle Guard & Visual Seat Layout Builder

### Mục tiêu:
Chứng minh thuật toán Aisle Guard chặn triệt để ghế đôi `ColSpan = 2` lấn vào cột hành lang hoặc vượt biên phòng.

### Các bước thực hiện:
1. Đăng nhập tài khoản `Admin` và truy cập Studio thiết kế sơ đồ: `/ScreeningRoom/LayoutBuilder/1`.
2. Đặt ma trận lưới: `Hàng = 10`, `Cột = 16`.
3. Click vào tiêu đề Cột 5 để đánh dấu Cột 5 là **Hành lang dọc** (`AisleCols = "5"`). Cột 5 đổi sang màu lối đi có vạch neon.
4. Chọn công cụ vẽ **Ghế đôi (Sweetbox - 2 ô)** và thử click đặt ghế đôi bắt đầu từ Cột 4 (chiếm Cột 4 và Cột 5):
   - **Quan sát giao diện**: Hệ thống chặn ngay lập tức, hiển thị pop-up cảnh báo: *"Ghế đôi đè lên hành lang dọc tại cột 5!"* và không cho lưu.
5. Đổi sang đặt ghế đôi tại Cột 1 và Cột 2 (cách xa hành lang), đặt toàn bộ Hàng 1 thành VIP bằng nút "Gán toàn bộ hàng thành VIP".
6. Bấm nút **💾 LƯU SƠ ĐỒ PHÒNG CHIẾU**:
   - Gửi request `POST /ScreeningRoom/SaveSeatLayout` có token Anti-CSRF.

### Kết quả mong đợi:
- Server phản hồi `HTTP 200 OK` với thông báo lưu thành công.
- Thuật toán tự động đánh số: Các ghế bên trái hành lang được đánh số liên tục (ví dụ: A1, A2, A3, A4), Cột 5 bỏ qua, các ghế bên phải hành lang tiếp tục từ A5, A6...
- Kiểm tra cơ sở dữ liệu bảng `seats`: không có bất kỳ ghế nào có tọa độ `GridCol = 5`.

---

## 5. Kịch bản xác thực 4: Kiểm thử Tái cấu trúc Modular Monolith & Build Integrity

### Mục tiêu:
Đảm bảo giải pháp biên dịch sạch sẽ (`Build succeeded`), không phát sinh lỗi cảnh báo hoặc phụ thuộc vòng giữa các module.

### Các bước thực hiện:
```bash
# Biên dịch toàn bộ dự án
dotnet build CineManagement.csproj -c Release
```

### Kết quả mong đợi:
- `Build succeeded: 0 Warning(s), 0 Error(s)`.
- Các namespace phân tách rành mạch theo cấu trúc:
  - `CineManagement.Modules.Cinemas`
  - `CineManagement.Modules.Catalog`
  - `CineManagement.Modules.Ticketing`
  - `CineManagement.Modules.Ordering`
  - `CineManagement.Modules.Payments`
  - `CineManagement.Modules.Shared`
