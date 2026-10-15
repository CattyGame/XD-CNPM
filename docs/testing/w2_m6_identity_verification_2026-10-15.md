# AURA — Báo cáo Kiểm chứng Task W2-M6-01 (SCRUM-314)

## 1. Thông tin kiểm chứng
- **Dự án**: AURA — System for Retinal Vascular Health Screening
- **Sprint**: Sprint 2
- **Mã Task Jira**: SCRUM-314 (W2-M6-01: Triển khai Identity API và Account Linking)
- **Người thực hiện**: M6
- **Thời điểm kiểm chứng**: 2026-10-15
- **Môi trường**:
  - Hệ điều hành: Windows
  - Database: PostgreSQL 17 (Docker Compose `aura-postgres-1`, port 5432, database `aura_dev`)
  - Framework: .NET 10 (ASP.NET Core Minimal API)
  - Endpoint dịch vụ: `http://127.0.0.1:5004` (AURA.Identity.Api)

---

## 2. Kết quả kiểm chứng chức năng (Test Matrix)

| Mã Test | Kịch bản kiểm thử | Yêu cầu / Input | Kết quả thực tế | Trạng thái |
| :--- | :--- | :--- | :--- | :--- |
| **TC-01** | Healthcheck | `GET /health` | HTTP 200 `Healthy` | **PASS** |
| **TC-02** | Bootstrap lần đầu (New Account) | `POST /api/v1/identity/bootstrap`<br>Bearer token: `mock-token-hoang_dev_1` | HTTP 200, tạo mới `accountId` UUID, gán `accountStatus: "ACTIVE"`, role `PATIENT`, `profileProvisioningStatus: "PENDING"`. | **PASS** |
| **TC-03** | Tính Idempotency (Bootstrap lặp lại) | `POST /api/v1/identity/bootstrap`<br>Cùng Bearer token `mock-token-hoang_dev_1` | HTTP 200, trả về đúng `accountId` cũ (`668c4a32-5342-4d8b-b505-07d548732120`), không sinh duplicate row. | **PASS** |
| **TC-04** | Lấy thông tin tài khoản hiện tại | `GET /api/v1/identity/me`<br>Bearer token: `mock-token-hoang_dev_1` | HTTP 200, trả về thông tin account và roles: `["PATIENT"]`. | **PASS** |
| **TC-05** | Truy vấn tài khoản chưa bootstrap | `GET /api/v1/identity/me`<br>Bearer token: `mock-token-unknown_user_123` | HTTP 409 Conflict: `ERR_ACCOUNT_NOT_INITIALIZED`. | **PASS** |
| **TC-06** | Từ chối request thiếu Authorization | `POST /api/v1/identity/bootstrap` (không kèm token) | HTTP 401 Unauthorized: `ERR_UNAUTHORIZED`. | **PASS** |

---

## 3. Bằng chứng kiểm chứng dữ liệu

### 3.1. Phản hồi API Bootstrap (HTTP 200 OK):
```json
{
  "accountId": "668c4a32-5342-4d8b-b505-07d548732120",
  "firebaseUid": "hoang_dev_1",
  "accountStatus": "ACTIVE",
  "roles": [
    "PATIENT"
  ],
  "profileProvisioningStatus": "PENDING"
}
```

### 3.2. Truy vấn đối chiếu Database PostgreSQL:
```sql
SELECT account_id, firebase_uid, account_status, profile_provisioning_status
FROM identity.accounts WHERE firebase_uid = 'hoang_dev_1';

              account_id              | firebase_uid | account_status | profile_provisioning_status
--------------------------------------+--------------+----------------+-----------------------------
 668c4a32-5342-4d8b-b505-07d548732120 | hoang_dev_1  | ACTIVE         | PENDING


SELECT * FROM identity.account_roles
WHERE account_id = '668c4a32-5342-4d8b-b505-07d548732120';

              account_id              | role_name |          granted_at
--------------------------------------+-----------+------------------------------
 668c4a32-5342-4d8b-b505-07d548732120 | PATIENT   | 2026-10-15 15:07:43.04979+00
```

---

## 4. Xác nhận tuân thủ quy tắc Sprint 2

- **Ánh xạ duy nhất 1:1:** Map `firebase_uid` sang `account_id` UUID nội bộ.
- **Không tự liên kết bằng email:** Tuyệt đối không query/gộp tài khoản theo email (phòng chống tấn công Account Takeover).
- **Phân quyền tối thiểu:** Người dùng mới chỉ được cấp vai trò `PATIENT`.
- **Trạng thái tài khoản:** Tài khoản bị khóa (`DISABLED`) bị từ chối truy cập ngay lập tức, không cho phép bootstrap để mở khóa.
- **Tính Idempotent:** Gọi bootstrap nhiều lần không sinh dữ liệu rác.
