# Contract: Visual Seat Layout & Aisle Guard API

**Endpoint**: `POST /ScreeningRoom/SaveSeatLayout`  
**Security**: `[Authorize(Roles = "Admin")]`, `[ValidateAntiForgeryToken]`  
**Transaction Scope**: Database Transaction with `IsolationLevel.Serializable`

---

## 1. Request DTO Schema (`SaveSeatLayoutRequest`)

```json
{
  "roomId": 1,
  "totalRows": 10,
  "totalCols": 16,
  "aisleRows": "4,8",
  "aisleCols": "5,12",
  "seats": [
    {
      "gridRow": 1,
      "gridCol": 1,
      "colSpan": 1,
      "seatTypeId": 1,
      "rowLabel": "A",
      "seatNumber": 1,
      "isActive": true
    },
    {
      "gridRow": 10,
      "gridCol": 1,
      "colSpan": 2,
      "seatTypeId": 3,
      "rowLabel": "H",
      "seatNumber": 1,
      "isActive": true
    }
  ]
}
```

### Chi tiết trường dữ liệu:
| Trường | Kiểu | Bắt buộc | Ràng buộc |
| :--- | :--- | :--- | :--- |
| `roomId` | `int` | Có | ID phòng chiếu hợp lệ tồn tại trong DB |
| `totalRows` | `int` | Có | $3 \le \text{totalRows} \le 30$ |
| `totalCols` | `int` | Có | $4 \le \text{totalCols} \le 40$ |
| `aisleRows` | `string?`| Không | Chuỗi phân cách dấu phẩy chỉ các hàng lối đi ngang (ví dụ: `"4,8"`) |
| `aisleCols` | `string?`| Không | Chuỗi phân cách dấu phẩy chỉ các cột lối đi dọc (ví dụ: `"5,12"`) |
| `seats` | `array` | Có | Danh sách các ghế hợp lệ cấu hình trong phòng |

---

## 2. Aisle Guard Validation Rules & Response Codes

### A. Phản hồi thành công (`HTTP 200 OK`)
```json
{
  "success": true,
  "message": "Lưu sơ đồ phòng chiếu thành công!",
  "data": {
    "roomId": 1,
    "totalSeats": 128,
    "activeSeats": 126,
    "standardSeats": 80,
    "vipSeats": 36,
    "coupleSeats": 10
  }
}
```

### B. Lỗi vi phạm Aisle Guard - Ghế đôi đè hành lang dọc (`HTTP 400 Bad Request`)
*Khi ghế đôi `ColSpan = 2` tại cột $c$ mà cột $c$ hoặc $c+1$ nằm trong `AisleCols`:*
```json
{
  "success": false,
  "errorCode": "AISLE_GUARD_VIOLATION",
  "message": "Vi phạm quy tắc an toàn hành lang: Ghế đôi tại Hàng 10 (cột 4-5) đè lên hành lang dọc tại cột 5!",
  "details": {
    "gridRow": 10,
    "gridCol": 4,
    "colSpan": 2,
    "conflictAisleCol": 5
  }
}
```

### C. Lỗi vượt biên phòng chiếu (`HTTP 400 Bad Request`)
```json
{
  "success": false,
  "errorCode": "BOUNDARY_OVERFLOW",
  "message": "Ghế đôi tại Hàng 10 Cột 16 vượt quá biên cột tối đa của phòng chiếu (TotalCols = 16)!",
  "details": {
    "gridRow": 10,
    "gridCol": 16,
    "colSpan": 2,
    "maxCols": 16
  }
}
```

### D. Lỗi sửa ghế đã phát sinh vé ở tương lai (`HTTP 409 Conflict`)
```json
{
  "success": false,
  "errorCode": "SEAT_HAS_FUTURE_TICKETS",
  "message": "Không thể xóa hoặc thay đổi vị trí ghế E5 vì đã phát sinh 2 vé trong các suất chiếu sắp diễn ra!",
  "details": {
    "seatId": 45,
    "seatCode": "E5",
    "futureTicketsCount": 2
  }
}
```
