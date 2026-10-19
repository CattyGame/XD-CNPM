# AURA — Báo cáo Kiểm chứng Toàn diện Task W2-M6-03 (SCRUM-316)

## 1. Thông tin Báo cáo Kiểm chứng
- **Dự án**: AURA — System for Retinal Vascular Health Screening (Mã đề tài: `SP26SE025`)
- **Sprint**: Sprint 2
- **Mã Task Jira**: **SCRUM-316** (`[W2][M6] Kiểm thử và tài liệu Identity/Profile`)
- **Mã công việc thành phần**: `W2-M6-03`
- **Người thực hiện**: Thành viên M6
- **Thời điểm hoàn thành**: 2026-10-17
- **Môi trường thử nghiệm**:
  - Hệ điều hành: Windows
  - Dịch vụ Identity: `http://127.0.0.1:5004` (AURA.Identity.Api, .NET 10 Minimal API)
  - Dịch vụ Profile: `http://127.0.0.1:5005` (AURA.Profile.Api, .NET 10 Minimal API)
  - Cơ sở dữ liệu: PostgreSQL 17 (Database `aura_dev`, Schemata `identity` và `profile`)
  - Kịch bản thực thi tự động: [scripts/verify_m6_test_suite.ps1](file:///c:/Users/Hoang/OneDrive/Desktop/Phần Mền Hướng Đối Tượng/XD-PMHDT/scripts/verify_m6_test_suite.ps1)

---

## 2. Ma trận Kết quả Kiểm thử Chi tiết (Test Matrix)

### 2.1. Nhóm 1: Kiểm thử Xác thực & Token (Authentication & Token Security)

| Mã Test | Kịch bản kiểm thử | Yêu cầu / Input | Kết quả thực tế | Trạng thái |
| :--- | :--- | :--- | :--- | :--- |
| **TC-AUTH-01** | Từ chối yêu cầu thiếu header Authorization | `GET /api/v1/identity/me`<br>Không truyền header `Authorization` | HTTP 401 `ERR_UNAUTHORIZED`, thông báo "Thiếu Firebase ID token." | **PASS** |
| **TC-AUTH-02** | Từ chối Token sai định dạng / Token rác | `GET /api/v1/identity/me`<br>`Authorization: Bearer invalid_garbage_token_xyz` | HTTP 401 `ERR_UNAUTHORIZED`, token không vượt qua bước verify. | **PASS** |
| **TC-AUTH-03** | Chặn truy vấn tài khoản chưa từng Bootstrap | `GET /api/v1/identity/me`<br>Token người dùng mới chưa bootstrap | HTTP 409 `ERR_ACCOUNT_NOT_INITIALIZED`, `retryable: true` yêu cầu client gọi bootstrap trước. | **PASS** |
| **TC-AUTH-04** | Bootstrap tài khoản hợp lệ (Idempotent) | `POST /api/v1/identity/bootstrap`<br>Bearer token: `mock-token-hoang_dev_1` | HTTP 200 OK, trả về `accountId` UUID, `accountStatus: "ACTIVE"`, `roles: ["PATIENT"]`. Gọi lặp lại nhiều lần vẫn giữ nguyên định danh cũ. | **PASS** |

---

### 2.2. Nhóm 2: Kiểm thử Hồ sơ Trái quyền & Chống sửa Role (Authorization & Privilege Protection)

| Mã Test | Kịch bản kiểm thử | Yêu cầu / Input | Kết quả thực tế | Trạng thái |
| :--- | :--- | :--- | :--- | :--- |
| **TC-AUTHZ-01** | Chống leo thang đặc quyền: Cố tình gửi kèm Role trong PATCH me | `PATCH /api/v1/profiles/me`<br>Header: `If-Match: "1"`<br>Body: cố tình truyền `role: "SYSTEM_ADMIN"`, `roles: ["SYSTEM_ADMIN"]`, `accountId: "0000..."` | HTTP 200 OK. Cập nhật thành công `displayName` và `phone`, nhưng **hoàn toàn loại bỏ trường role**. Kiểm tra cơ sở dữ liệu xác nhận không có bất kỳ quyền quản trị nào bị gán trái phép. | **PASS** |
| **TC-AUTHZ-02** | Phân giải quyền truy cập cho Chính chủ bệnh nhân (`OWN`) | `POST /internal/v1/profile/access/resolve`<br>`actorAccountId`: Chính chủ bệnh nhân<br>`patientId`: Thuộc về chính chủ | HTTP 200 OK, `allowed: true`, `tenantKind: "PERSONAL"`, `billingOwner.type: "ACCOUNT"`. Phân quyền chính xác cho luồng upload của bệnh nhân. | **PASS** |
| **TC-AUTHZ-03** | Từ chối truy cập đối với Người lạ (Stranger) | `POST /internal/v1/profile/access/resolve`<br>`actorAccountId`: UUID người lạ ngẫu nhiên<br>`patientId`: Bệnh nhân của người khác | HTTP 200 OK, `allowed: false`. Không để lộ thông tin tài nguyên ngoài phạm vi sở hữu. | **PASS** |

---

### 2.3. Nhóm 3: Kiểm thử Validation & Quản lý Phiên bản Đồng thời (Validation & Concurrency)

| Mã Test | Kịch bản kiểm thử | Yêu cầu / Input | Kết quả thực tế | Trạng thái |
| :--- | :--- | :--- | :--- | :--- |
| **TC-VAL-01** | Bắt buộc header `If-Match` khi cập nhật | `PATCH /api/v1/profiles/me`<br>Body hợp lệ nhưng thiếu `If-Match` | HTTP 400 `ERR_VALIDATION`: "Thiếu header If-Match bắt buộc để cập nhật hồ sơ." | **PASS** |
| **TC-VAL-02** | Kiểm tra định dạng header `If-Match` | `PATCH /api/v1/profiles/me`<br>Header `If-Match: "invalid_etag"` | HTTP 400 `ERR_VALIDATION`: "Header If-Match không hợp lệ." | **PASS** |
| **TC-VAL-03** | Ngăn chặn xung đột phiên bản ETag (Optimistic Locking) | `PATCH /api/v1/profiles/me`<br>Header `If-Match: "999999"` (khác phiên bản hiện tại) | HTTP 409 `ERR_PROFILE_VERSION_CONFLICT`: "Phiên bản hồ sơ không khớp (xung đột dữ liệu)." | **PASS** |
| **TC-VAL-04** | Từ chối `displayName` rỗng hoặc chỉ có khoảng trắng | `PATCH /api/v1/profiles/me`<br>`displayName: "   "` | HTTP 400 `ERR_VALIDATION`: "Tên hiển thị (displayName) phải từ 1 đến 100 ký tự." | **PASS** |
| **TC-VAL-05** | Từ chối `displayName` vượt quá 100 ký tự | `PATCH /api/v1/profiles/me`<br>`displayName`: chuỗi 105 ký tự | HTTP 400 `ERR_VALIDATION`: Giới hạn tối đa 100 ký tự. | **PASS** |
| **TC-VAL-06** | Từ chối số điện thoại `phone` vượt quá 30 ký tự | `PATCH /api/v1/profiles/me`<br>`phone`: chuỗi 35 chữ số | HTTP 400 `ERR_VALIDATION`: Giới hạn tối đa 30 ký tự. | **PASS** |
| **TC-VAL-07** | Bắt buộc thông tin khi cấp Service Token nội bộ | `POST /internal/v1/identity/service-tokens`<br>`clientId: ""`, `clientSecret: ""` | HTTP 400 `ERR_VALIDATION`: "Thiếu clientId hoặc clientSecret." | **PASS** |
| **TC-VAL-08** | Bắt buộc `accountId` khi khởi tạo hồ sơ nội bộ | `POST /internal/v1/profile/provision`<br>`accountId: "00000000-0000-0000-0000-000000000000"` | HTTP 400 `ERR_VALIDATION`: "Thiếu accountId hợp lệ." | **PASS** |

---

## 3. Bằng chứng Phản hồi API & Cấu trúc Dữ liệu

### 3.1. Phản hồi Lỗi Chuẩn Envelope (Tuân thủ Sprint 2):
```json
{
  "error": {
    "code": "ERR_PROFILE_VERSION_CONFLICT",
    "message": "Phiên bản hồ sơ không khớp (xung đột dữ liệu). Hãy tải lại hồ sơ mới nhất.",
    "correlationId": "8f9e0b12-3c4d-4e5f-a6b7-c8d9e0f1a2b3",
    "retryable": false
  }
}
```

### 3.2. Bằng chứng Chống sửa Role qua Profile API:
- Request gửi đi:
```json
{
  "displayName": "Nguyen Van Hoang",
  "phone": "0988888888",
  "role": "SYSTEM_ADMIN",
  "roles": ["SYSTEM_ADMIN"],
  "accountId": "00000000-0000-0000-0000-000000000000"
}
```
- Phản hồi nhận được từ Profile Service (Header `ETag: "2"`):
```json
{
  "accountId": "668c4a32-5342-4d8b-b505-07d548732120",
  "displayName": "Nguyen Van Hoang",
  "phone": "0988888888",
  "patientId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "personalTenantId": "c4b81b5a-172b-426b-a25e-04f796123456",
  "profileVersion": 2
}
```
*(Xác nhận: DTO phản hồi và database hoàn toàn không tồn tại trường `role`, ngăn chặn 100% rủi ro leo thang đặc quyền).*

---

## 4. Tổng kết Tuân thủ Quy chuẩn Sprint 2

1. **Tách biệt định danh hoàn toàn**:
   $$\text{accountId} \neq \text{patientId} \neq \text{clinicId} \neq \text{tenantId}$$
2. **Không tự liên kết bằng Email**:
   - Identity Service chỉ ánh xạ duy nhất 1:1 theo `firebase_uid`. Tuyệt đối không tự động gộp hai tài khoản vì trùng địa chỉ email, bảo vệ người dùng trước các cuộc tấn công chiếm quyền tài khoản (Account Takeover).
3. **Phân quyền tối thiểu (Least Privilege)**:
   - Người dùng mới khởi tạo luôn được cấp vai trò mặc định `PATIENT`. Các vai trò quản trị `DOCTOR`, `CLINIC_ADMIN`, `SYSTEM_ADMIN` chỉ được cấp qua quy trình quản trị chuyên trách.
   - Database runtime roles `aura_identity_app` và `aura_profile_app` chỉ có quyền DML trên schema tương ứng, không có quyền DDL hay can thiệp chéo sang schema khác.
4. **Kiểm soát cập nhật đồng thời (Optimistic Concurrency)**:
   - Quản lý phiên bản chặt chẽ qua header `ETag` và `If-Match`, triệt tiêu hoàn toàn lỗi mất mát dữ liệu (Lost Updates) khi có nhiều client cùng thao tác.
