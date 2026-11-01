# Tài liệu Tích hợp Phân hệ Chiến dịch Tầm soát Sức khỏe Mắt (Screening Campaign Integration Spec)
**Module Cung cấp:** M6 / Analysis API
**Module Tiêu thụ / Phối hợp:** M2 (Batch Processing & Doctor Review), M5 (Frontend UI)
**Mục tiêu:** Cung cấp đặc tả luồng nghiệp vụ, danh mục API, cấu trúc dữ liệu và các ràng buộc khi tích hợp Chiến dịch Tầm soát với Đợt xử lý hàng loạt (Batch Processing).

---

## 1. Tổng quan Luồng Phối hợp (Workflow Overview)

```
   ┌────────────────────────────────────────────────────────┐
   │ 1. Điều phối viên / Clinic Admin tạo Chiến dịch         │
   │    POST /api/v1/campaigns (Status: PLANNED)            │
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 2. Kích hoạt đợt khám thực địa                         │
   │    PATCH /api/v1/campaigns/{id}/status (IN_PROGRESS)   │
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 3. Kỹ thuật viên chụp ảnh & Upload Batch (M2)          │
   │    POST /api/v1/batches (Nhận về batchId)              │
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 4. Gắn Batch vào Chiến dịch                            │
   │    POST /api/v1/campaigns/{id}/batches                 │
   │    (Kiểm tra: Nếu Campaign CLOSED -> Chặn lỗi 409)      │
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 5. AI Worker xử lý & Bác sĩ đánh giá (M2, M4)          │
   │    Cập nhật processing_status & risk_level              │
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 6. Thống kê Y tế & Xuất Báo cáo Tổng kết               │
   │    GET /api/v1/campaigns/{id}/summary                  │
   │    GET /api/v1/campaigns/{id}/export (File CSV an toàn)│
   └──────────────────────────┬─────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 7. Đóng chiến dịch khi kết thúc đợt tầm soát (AC3)     │
   │    PATCH /api/v1/campaigns/{id}/status (CLOSED)        │
   │    -> Khóa hoàn toàn, cấm nạp thêm batch ảnh           │
   └────────────────────────────────────────────────────────┘
```

---

## 2. Ràng buộc Nghiệp vụ & Điều kiện Nghiệm thu (DoD Rules)

1. **Ràng buộc AC1 (Tạo Campaign & Liên kết Batch)**:
   * Endpoint `POST /api/v1/campaigns`: Bắt buộc truyền `name`, `location`, `startDate`, `endDate`.
   * Endpoint `POST /api/v1/campaigns/{campaignId}/batches`: Batch phải thuộc cùng `clinicContextId` (tenant_id) với Campaign.
   * Hỗ trợ liên kết các batch lớn ($\ge 50$ ảnh).

2. **Ràng buộc AC2 (Thống kê Tiến độ & Lâm sàng)**:
   * `completionRatePercent`: $(\text{completedImages} / \text{totalImages}) \times 100\%$.
   * `normalCases`: Số ca có `risk_level = 'LOW'`.
   * `suspectedCases`: Số ca có `risk_level IN ('MODERATE', 'HIGH')` (dấu hiệu tổn thương mạch máu võng mạc).
   * `highRiskCases`: Số ca có `risk_level = 'HIGH'` cần chuyển tuyến điều trị.
   * `export`: Kết xuất file CSV chuẩn RFC 4180; các ô dữ liệu có ký tự đầu là `=`, `+`, `-`, `@` được tự động escape bằng dấu `'` để chống tấn công CSV Formula Injection.

3. **Ràng buộc AC3 (Khóa Chiến dịch khi CLOSED)**:
   * Khi chiến dịch có trạng thái `status = 'CLOSED'`, bất kỳ yêu cầu gắn batch hoặc nạp thêm ảnh vào chiến dịch này đều bị từ chối ngay lập tức với HTTP `409 Conflict`:
     ```json
     {
       "error": {
         "code": "ERR_CAMPAIGN_CLOSED",
         "message": "Chiến dịch đã kết thúc (CLOSED), không cho phép nạp thêm ảnh chụp mới.",
         "correlationId": "...",
         "retryable": false
       }
     }
     ```

4. **Bảo mật Đa Tenant**:
   * Mọi truy vấn bắt buộc có ngữ cảnh phòng khám (`X-Clinic-Context-Id` hoặc `clinicContextId`).
   * Truy cập chéo tenant giữa các phòng khám bị từ chối với HTTP `404 Not Found` (để tránh rò rỉ sự tồn tại của tài nguyên) hoặc `403 Forbidden`.
