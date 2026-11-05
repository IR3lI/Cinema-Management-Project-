# Cloudflare Origin CA Certificates

Đặt các tệp chứng chỉ Cloudflare Origin CA vào thư mục này:
- `origin.crt`: Cloudflare Origin Certificate (x509 PEM format)
- `origin.key`: Cloudflare Origin Private Key (RSA/ECC format)

Hướng dẫn lấy chứng chỉ miễn phí 15 năm từ Cloudflare Dashboard:
1. Đăng nhập vào Cloudflare Dashboard -> Chọn tên miền của bạn.
2. Vào mục **SSL/TLS** -> **Origin Server** -> Bấm **Create Certificate**.
3. Chọn loại khóa RSA (2048) hoặc ECDSA, thời hạn 15 năm.
4. Sao chép nội dung Origin Certificate lưu vào `origin.crt`.
5. Sao chép Private Key lưu vào `origin.key`.
6. Tại mục **SSL/TLS** -> Chọn chế độ mã hóa **Full (strict)**.
