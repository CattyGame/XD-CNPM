# Tài liệu Hợp đồng Phân giải Quyền Tài nguyên (Resource Authorization Contract)
**Module Cung cấp:** M6 — Identity & Profile Service  
**Module Tiêu thụ:** M2 — Analysis Service / AI Worker / API Gateway  
**Mục tiêu:** Kiểm tra và cấp phép cho Actor (Bác sĩ, Kỹ thuật viên, Bệnh nhân) khi thực hiện các thao tác: truy xuất kết quả phân tích ảnh đáy mắt (`ANALYSIS_READ`), tải tệp ảnh gốc/mask (`ANALYSIS_FILE_READ`), hoặc nộp kết luận chuyên môn bác sĩ (`DOCTOR_REVIEW`).

---

## 1. Thông tin Kết nối & Xác thực (Service Authentication)

* **Phương thức:** `POST`
* **Đường dẫn:** `/internal/v1/profile/resolve`
* **Xác thực mạng nội bộ:** Bắt buộc truyền Service JWT qua header `Authorization`.
  * `Authorization: Bearer <service_token>`
  * **Audience (`aud`):** `aura-profile`
  * **Scope (`scope`):** `profile:resolve`
* **Mã lỗi xác thực:**
  * `401 Unauthorized` (`ERR_UNAUTHORIZED`): Thiếu token hoặc token không hợp lệ.
  * `403 Forbidden` (`ERR_FORBIDDEN`): Sai audience hoặc thiếu scope `profile:resolve`.

---

## 2. Đặc tả Dữ liệu Yêu cầu (Request Payload)

```json
{
  "actorAccountId": "a1111111-0000-0000-0000-000000000002",
  "patientId": "p1111111-0000-0000-0000-000000000001",
  "clinicContextId": "c1111111-0000-0000-0000-000000000001",
  "action": "DOCTOR_REVIEW",
  "analysisId": "e1111111-0000-0000-0000-000000000001"
}
```

### Chi tiết các trường:
| Tên trường | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `actorAccountId` | UUID | Có | Định danh tài khoản người thực hiện thao tác (trích xuất từ JWT người dùng). |
| `patientId` | UUID | Có | Định danh bệnh nhân sở hữu ca chụp. |
| `clinicContextId`| UUID | Không | Định danh phòng khám. Bắt buộc nếu thao tác diễn ra trong ngữ cảnh phòng khám (`CLINIC`); để `null` nếu là người dùng cá nhân (`PERSONAL`). |
| `action` | String | Có | Hành động cần phân giải: `ANALYSIS_READ`, `ANALYSIS_FILE_READ`, `DOCTOR_REVIEW`, `ANALYSIS_CREATE`. |
| `analysisId` | UUID | Không | Định danh ca chụp cụ thể (hỗ trợ phân công riêng lẻ từng ca bệnh hội chẩn). |

---

## 3. Đặc tả Dữ liệu Phản hồi (Response Payload)

### 3.1 Cấu trúc phản hồi thành công (HTTP 200 OK)

```json
{
  "allowed": true,
  "accountStatus": "ACTIVE",
  "patientId": "p1111111-0000-0000-0000-000000000001",
  "tenantId": "t1111111-0000-0000-0000-000000000001",
  "tenantKind": "CLINIC",
  "ownerPrincipalId": "c1111111-0000-0000-0000-000000000001",
  "billingOwner": {
    "type": "CLINIC",
    "id": "c1111111-0000-0000-0000-000000000001"
  },
  "permissionVersion": 1,
  "reason": null
}
```

### 3.2 Cấu trúc phản hồi từ chối quyền (HTTP 200 OK, `allowed: false`)

```json
{
  "allowed": false,
  "accountStatus": "ACTIVE",
  "patientId": "p1111111-0000-0000-0000-000000000001",
  "tenantId": null,
  "tenantKind": null,
  "ownerPrincipalId": null,
  "billingOwner": null,
  "permissionVersion": 1,
  "reason": "CLINIC_SUSPENDED"
}
```

---

## 4. Ma trận Quyết định Phân quyền (Authorization Rules)

| Ngữ cảnh | Vai trò Actor | Điều kiện | `ANALYSIS_READ` / `FILE_READ` | `DOCTOR_REVIEW` | `ANALYSIS_CREATE` |
| :--- | :--- | :--- | :---: | :---: | :---: |
| **Cá nhân** (`clinicContextId = null`) | Patient (`owner`) | Tài khoản `ACTIVE`, đúng chủ sở hữu | **ALLOW** | **DENY** (Bệnh nhân không tự review) | **ALLOW** |
| **Cá nhân** (`clinicContextId = null`) | Người lạ | Không phải chủ sở hữu | **DENY** | **DENY** | **DENY** |
| **Phòng khám** (`clinicContextId` hợp lệ) | `CLINIC_ADMIN` | Clinic `ACTIVE` | **ALLOW** | **DENY** (Chỉ bác sĩ lâm sàng review) | **ALLOW** |
| **Phòng khám** (`clinicContextId` hợp lệ) | `DOCTOR` | Có phân công `ACTIVE` (theo `patientId` hoặc `analysisId`) | **ALLOW** | **ALLOW** | **ALLOW** |
| **Phòng khám** (`clinicContextId` hợp lệ) | `DOCTOR` | Phân công bị `REVOKED` hoặc đã chuyển giao | **DENY** (Thu hồi tức thì) | **DENY** (Thu hồi tức thì) | **ALLOW** |
| **Phòng khám** (`clinicContextId` hợp lệ) | Bác sĩ Clinic khác | Không có membership tại Clinic này | **DENY** (Cách ly đa tenant) | **DENY** | **DENY** |
| **Bất kỳ** | Bất kỳ | Tài khoản bị khóa (`identity.accounts.status != ACTIVE`) | **DENY** (Fail-Closed) | **DENY** (Fail-Closed) | **DENY** |
| **Phòng khám** | Bất kỳ | Phòng khám bị đình chỉ (`status = SUSPENDED`) | **ALLOW** (Bảo toàn lịch sử cũ) | **DENY** | **DENY** (`reason: CLINIC_SUSPENDED`) |

---

## 5. Cam kết Hiệu năng (SLA & Latency)

* **Thời gian xử lý API:** $\le 5\text{ms}$ ở p95.
* **Cơ chế tối ưu:** Truy vấn dựa trên chỉ mục phức hợp duy nhất (`uq_clinic_doctor_patient_active`, `idx_doctor_assignments_analysis`, `idx_patients_clinic_search`), không dùng JOIN nặng, kết nối Connection Pool mở sẵn.
* **Xử lý sự cố (Failure Policy):**
  * Nguyên tắc **Fail-Closed**: Khi có lỗi đường truyền hoặc timeout, Analysis API mặc định từ chối cấp quyền thao tác trên tài nguyên nhạy cảm.
