# Nhật ký Lỗi và Tái kiểm thử — Sprint 3 (SCRUM-328 Defect Log)

> **Dự án**: AURA (SP26SE025) · Retinal Vascular Health Screening  
> **Mã công việc Jira**: **SCRUM-328** (`[W3]M3] Kiểm thử lỗi toàn luồng và đối soát dữ liệu`)  
> **Người tổng hợp**: Nguyễn Trương Hậu (Lead Architect)  
> **Ngày cập nhật**: 31/10/2026  
> **Cam kết**: Mỗi defect có đầy đủ Owner, mức độ nghiêm trọng, các bước tái hiện, phương án khắc phục và kết quả tái kiểm định (Retest).

---

## 1. Bảng Tổng hợp Danh mục Lỗi Sprint 3

| Mã Lỗi | Tên Lỗi & Mô tả ngắn gọn | Mức độ | Owner | Trạng thái | Kết quả Tái kiểm |
|---|---|---|---|---|---|
| **DEFECT-S3-01** | Thiếu endpoint `GET /api/v1/analyses/{id}` trong Analysis API | High | **M2** (Võ Hữu Duy) | Open (Documented) | Có tài liệu bàn giao & workaround |
| **DEFECT-S3-02** | Lỗi Npgsql Typeless Null khi truy vấn cursor inbox không có tham số | Medium | **M3** (Nguyễn Trương Hậu) | **Fixed** | **PASS (19/19 InboxChecks)** |
| **DEFECT-S3-03** | Lệch khóa ngoại chủ thể ví trong kịch bản kiểm định Billing | Medium | **M3** (Nguyễn Trương Hậu) | **Fixed** | **PASS (32/32 BillingChecks)** |
| **DEFECT-S3-04** | Cảnh báo hạ cấp gói NuGet `Microsoft.Extensions.Logging.Abstractions` | Low | **M3** (Nguyễn Trương Hậu) | **Fixed** | **PASS (Build 0 warnings)** |
| **DEFECT-S3-05** | Mất dữ liệu khi restart container PostgreSQL vô danh (ephemeral) | High | **M1** (Nguyễn Trương Hậu) | **Fixed** | **PASS (Persistent volume)** |

---

## 2. Chi tiết Từng Lỗi và Bằng chứng Khắc phục

### DEFECT-S3-01: Thiếu Endpoint `GET /api/v1/analyses/{id}` trong Analysis API
- **Owner**: M2 (Võ Hữu Duy)
- **Phân loại**: API Contract / Backend Implementation
- **Mức độ**: High
- **Mô tả & Cách tái hiện**:
  1. Gửi request `GET /api/v1/analyses/{id}` với JWT hợp lệ của bệnh nhân/bác sĩ.
  2. Analysis API trả về `HTTP 404 Not Found` do chỉ mới đăng ký route `/api/v1/batches/{id}`.
- **Tác động**: Giao diện M5 và các kịch bản E2E kiểm tra trạng thái từng ảnh đơn lẻ bị gián đoạn nếu gọi trực tiếp endpoint ảnh.
- **Phương án khắc phục**:
  - Đã xuất bản tài liệu chi tiết yêu cầu M2 sửa: [docs/qa/m2_sprint3_scrum325_review_and_action_items.md](file:///D:/XD-PMHDT/docs/qa/m2_sprint3_scrum325_review_and_action_items.md).
  - Workaround cho Sprint 3: Tra cứu qua `/api/v1/batches/{batchId}` hoặc truy vấn trực tiếp bảng `analysis.images`.
- **Trạng thái**: Đã ghi nhận rõ ràng vào backlog của M2, không chặn tiến độ M3.

---

### DEFECT-S3-02: Lỗi Npgsql Typeless Null khi Truy vấn Cursor Inbox Không Có Tham số
- **Owner**: M3 (Nguyễn Trương Hậu)
- **Phân loại**: Database / Driver Compatibility
- **Mức độ**: Medium
- **Mô tả & Cách tái hiện**:
  1. Gọi hàm `GetInboxAsync(accountId, limit, cursor: null)`.
  2. Npgsql ném lỗi: `PostgresException: 42P08: could not determine data type of parameter $3`.
  3. Nguyên nhân: Khi `cursorCreatedAt` là `DBNull.Value`, PostgreSQL không thể suy diễn kiểu dữ liệu nếu không khai báo kiểu rõ ràng.
- **Phương án khắc phục**:
  - Sửa đổi trong [NotificationInboxRepository.cs](file:///D:/XD-PMHDT/src/services/AURA.Notification.Api/NotificationInboxRepository.cs):
    ```csharp
    cmd.Parameters.Add("cursor_created_at", NpgsqlDbType.TimestampTz).Value =
        cursorCreatedAt.HasValue ? cursorCreatedAt.Value : DBNull.Value;
    ```
- **Kết quả Tái kiểm (Retest)**:
  - Chạy lại toàn bộ bộ test [tests/AURA.Notification.InboxChecks](file:///D:/XD-PMHDT/tests/AURA.Notification.InboxChecks/Program.cs).
  - Test `AC2.7 Cursor Pagination` và `AC2.7 Cursor Progression` đạt **PASS 100%**.

---

### DEFECT-S3-03: Lệch Khóa Ngoại Chủ Thể Ví trong Kịch bản Kiểm định Billing
- **Owner**: M3 (Nguyễn Trương Hậu)
- **Phân loại**: Database Fixture & Constraints
- **Mức độ**: Medium
- **Mô tả & Cách tái hiện**:
  1. Chạy script SQL xác minh `test_007_sprint3_billing_verification.sql`.
  2. Bị chặn bởi lỗi ràng buộc khóa ngoại: `violates foreign key constraint fk_reservation_wallet_owner_environment`.
  3. Nguyên nhân: Script dùng `actor1_id` (`e301...`) để tạo reservation cho ví đa credit `a302`, trong khi chủ sở hữu của ví `a302` là `actor2_id` (`e302...`).
- **Phương án khắc phục**:
  - Đồng bộ hóa chính xác `actor2_id` với ví `a302` trong file SQL và các unit test.
- **Kết quả Tái kiểm (Retest)**:
  - Script SQL chạy thành công với lệnh `COMMIT`. Bộ test `AURA.Billing.ReservationChecks` đạt **PASS 32/32 (100%)**.

---

### DEFECT-S3-04: Cảnh báo Hạ cấp Gói NuGet `Microsoft.Extensions.Logging.Abstractions`
- **Owner**: M3 (Nguyễn Trương Hậu)
- **Phân loại**: Dependency / Build System
- **Mức độ**: Low
- **Mô tả & Cách tái hiện**:
  1. Thêm package `Microsoft.Extensions.Logging.Abstractions` phiên bản 10.0.0 vào project test.
  2. .NET SDK 10 báo lỗi `NU1605: Warning As Error: Detected package downgrade: Microsoft.Extensions.Logging.Abstractions from 10.0.5 to 10.0.0`.
- **Phương án khắc phục**:
  - Cập nhật phiên bản lên `10.0.5` đồng bộ với runtime framework.
- **Kết quả Tái kiểm (Retest)**:
  - Lệnh `dotnet build` hoàn tất với 0 Errors và 0 Warnings.

---

### DEFECT-S3-05: Mất Dữ Liệu Khi Restart Container PostgreSQL Vô Danh (Ephemeral)
- **Owner**: M1 / DevOps (Nguyễn Trương Hậu)
- **Phân loại**: Infrastructure & Persistence
- **Mức độ**: High
- **Mô tả & Cách tái hiện**:
  1. Container tạm thời `aura-pg87s-c4fdb65507bd41d9` không được ánh xạ Docker volume, khi restart sẽ tự động kích hoạt `initdb` làm mất toàn bộ bảng và dữ liệu đã seed.
- **Phương án khắc phục**:
  - Chuyển hướng toàn bộ chuỗi kết nối của Billing, Notification, Analysis sang container authoritative `aura-postgres-1` (gắn volume chuẩn `aura_postgres_data`) hoạt động bền vững tại cổng `127.0.0.1:5432`.
- **Kết quả Tái kiểm (Retest)**:
  - Toàn bộ dữ liệu migration và test fixtures được lưu trữ vĩnh viễn, kiểm thử thành công qua nhiều lần restart.
