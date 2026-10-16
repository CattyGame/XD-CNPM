# AURA — Báo cáo Kiểm chứng Task W2-M6-02 (SCRUM-315)

## 1. Thông tin kiểm chứng
- **Dự án**: AURA — System for Retinal Vascular Health Screening
- **Sprint**: Sprint 2
- **Mã Task Jira**: SCRUM-315 (W2-M6-02: Triển khai Profile và Nền tảng Tổ chức)
- **Người thực hiện**: Thành viên M6
- **Thời điểm hoàn thành**: 2026-10-16
- **Môi trường**:
  - Hệ điều hành: Windows
  - Database: PostgreSQL 17 (Schema `profile`, Role runtime `aura_profile_app`)
  - Framework: .NET 10 (ASP.NET Core Minimal API)
  - Endpoint dịch vụ: `http://127.0.0.1:5005` (AURA.Profile.Api)

---

## 2. Kết quả kiểm chứng chức năng (Test Matrix)

| Mã Test | Kịch bản kiểm thử | Yêu cầu / Input | Kết quả thực tế | Trạng thái |
| :--- | :--- | :--- | :--- | :--- |
| **TC-01** | Healthcheck dịch vụ Profile | `GET /health` | HTTP 200 `Healthy` | **PASS** |
| **TC-02** | Khởi tạo hồ sơ nội bộ (Internal Provisioning) | `POST /internal/v1/profile/provision`<br>Payload: `accountId`, `firebaseUid`, `displayName` | HTTP 200, tạo 1 Personal Tenant (`tenant_kind = 'PERSONAL'`), 1 `profile.profiles` (`profileVersion: 1`), 1 `profile.patients`. Tách biệt rạch ròi `accountId` $\neq$ `patientId` $\neq$ `personalTenantId`. | **PASS** |
| **TC-03** | Xem hồ sơ cá nhân và nhận ETag | `GET /api/v1/profiles/me`<br>Bearer token: `mock-token-hoang_dev_1` | HTTP 200, trả về đầy đủ hồ sơ và gán header `ETag: "1"`. Ca tài khoản chưa bootstrap trả `409 ERR_PROFILE_NOT_READY` (`retryable: true`). | **PASS** |
| **TC-04** | Cập nhật hồ sơ & Chống sửa Role qua Profile | `PATCH /api/v1/profiles/me`<br>Header `If-Match: "1"`, cố tình gửi kèm `role: "SYSTEM_ADMIN"`, `accountId` mới | HTTP 200, chỉ cập nhật `displayName` và `phone`, `profileVersion` tăng lên `2`, Header `ETag: "2"`. Các trường `role`, `roles`, `accountId` hoàn toàn bị loại bỏ bởi DTO bảo mật. | **PASS** |
| **TC-05** | Kiểm soát xung đột phiên bản ETag / If-Match | `PATCH /api/v1/profiles/me`<br>Thử nghiệm 1: Thiếu `If-Match`<br>Thử nghiệm 2: Gửi `If-Match: "1"` khi phiên bản đã là `"2"` | Thử nghiệm 1: HTTP 400 `ERR_VALIDATION`.<br>Thử nghiệm 2: HTTP 409 `ERR_PROFILE_VERSION_CONFLICT`. Đảm bảo tính toàn vẹn dữ liệu (Optimistic Concurrency). | **PASS** |
| **TC-06** | Phân giải quyền truy cập (Access Resolve) | `POST /internal/v1/profile/access/resolve`<br>Thử nghiệm A: Cá nhân thao tác ảnh chính mình.<br>Thử nghiệm B: Người lạ truy cập trái phép.<br>Thử nghiệm C: Bác sĩ được phân công (`doctor_assignments`) | Thử nghiệm A: `allowed: true`, `tenantKind: "PERSONAL"`, `billingOwner: ACCOUNT`.<br>Thử nghiệm B: `allowed: false`.<br>Thử nghiệm C: `allowed: true`, `tenantKind: "CLINIC"`, `billingOwner: CLINIC`. | **PASS** |

---

## 3. Bằng chứng kiểm chứng dữ liệu & Kiến trúc

### 3.1. Phản hồi API Provisioning (`POST /internal/v1/profile/provision`):
```json
{
  "accountId": "668c4a32-5342-4d8b-b505-07d548732120",
  "displayName": "Nguyen Van Hoang",
  "phone": null,
  "patientId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "personalTenantId": "c4b81b5a-172b-426b-a25e-04f796123456",
  "profileVersion": 1
}
```
*(Xác nhận: `accountId`, `patientId` và `personalTenantId` là 3 định danh UUID hoàn toàn khác nhau).*

### 3.2. Phản hồi API Xem hồ sơ (`GET /api/v1/profiles/me`):
* Header: `ETag: "1"`, `X-Correlation-Id: "a1b2c3d4-..."`
* Body:
```json
{
  "accountId": "668c4a32-5342-4d8b-b505-07d548732120",
  "displayName": "Nguyen Van Hoang",
  "phone": null,
  "patientId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "personalTenantId": "c4b81b5a-172b-426b-a25e-04f796123456",
  "profileVersion": 1
}
```

### 3.3. Phản hồi API Cập nhật hồ sơ (`PATCH /api/v1/profiles/me`):
* Header: `ETag: "2"`
* Body:
```json
{
  "accountId": "668c4a32-5342-4d8b-b505-07d548732120",
  "displayName": "Nguyen Van Hoang (Da Cap Nhat)",
  "phone": "0912345678",
  "patientId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "personalTenantId": "c4b81b5a-172b-426b-a25e-04f796123456",
  "profileVersion": 2
}
```

---

## 4. Xác nhận tuân thủ quy tắc Sprint 2 của M6

1. **Tách biệt rạch ròi các định danh:**
   $$\text{accountId} \neq \text{patientId} \neq \text{clinicId} \neq \text{tenantId}$$
2. **Chống sửa Role qua Profile API:**
   - DTO `UpdateProfileRequest` chỉ cho phép truyền `displayName` và `phone`.
   - Bất kỳ trường nào như `role`, `roles`, `accountId`, `tenantId` đều bị loại bỏ hoàn toàn, không thể tiêm quyền hay leo thang đặc quyền (Privilege Escalation).
3. **Optimistic Locking chuẩn REST:**
   - Quản lý phiên bản bằng `profile_version` và Header `If-Match` / `ETag`.
   - Ngăn chặn lỗi Race Condition khi nhiều client cùng cập nhật hồ sơ.
4. **Schema phân công Bác sĩ (`profile.doctor_assignments`):**
   - Đã tạo migration `003_doctor_assignments.sql` liên kết `(clinic_id, doctor_account_id, patient_id)`.
   - Hỗ trợ đầy đủ cho endpoint tra cứu quyền `access/resolve` của Analysis Service.
5. **Least-Privilege Database Role:**
   - Chỉ sử dụng role `aura_profile_app`, chỉ thao tác DML trên schema `profile`.
