# AURA — Báo cáo Kiểm chứng Quyền Đa Tenant & Bàn giao Thiết kế (Task W3-M6-03)

- **Mã nhiệm vụ:** W3-M6-03 (Task Jira: SCRUM W3-M6-03)
- **Tiêu đề:** Báo cáo Kiểm chứng Quyền Đa Tenant, Bảo mật Chống Thăm dò & Bàn giao Thiết kế Sprint 3
- **Phụ trách:** M6 (Identity & Profile Services)
- **Phiên bản:** 3.0
- **Ngày hoàn thành:** 2026-10-30
- **Nhánh thực hiện:** `feature/W3-M6-03-multitenant-authorization-testing-and-design`
- **Các task phụ thuộc:** W3-M6-01 (Quản lý Clinic Membership), W3-M6-02 (Quyền Patient, Doctor & Camera Delegation)

---

## 1. Tóm tắt Kết quả Nghiệm thu (Executive Summary)

Báo cáo này chứng minh toàn diện việc thực hiện và vượt qua tất cả các điều kiện nghiệm thu (Acceptance Criteria) cho task **W3-M6-03**:

| Điều kiện nghiệm thu | Hiện trạng | Minh chứng kiểm thử |
| :--- | :---: | :--- |
| **AC1. Thẩm định Quyền Đa Tenant & Thiết bị**<br>Mỗi ca allow/deny có expected và actual; cross-tenant, device giả mạo và Patient không được cấp quyền đều bị chặn. | **ĐẠT (PASS)** | Bộ 9 test case AC1 trong `AURA.Profile.MultiTenantChecks` kiểm chứng chi tiết các ca: camera hợp lệ, sai device (`GRANT_NOT_FOUND`), sai tenant (`CLINIC_CAMERA_UNSUPPORTED`), grant hết hạn (`GRANT_EXPIRED`), grant thu hồi (`GRANT_REVOKED`), mạo danh ủy quyền (`ACTOR_PATIENT_MISMATCH`), đọc chéo bệnh nhân và bác sĩ phòng khám khác đọc bệnh nhân không phân công. |
| **AC2. Chống Rò rỉ Dữ liệu & Chống Leo quyền**<br>Không lộ dữ liệu qua GET analysis/file hoặc inbox khi đổi ID; role không thể nâng qua Profile hay membership payload; thu hồi tức thì và khóa tài khoản fail-closed. | **ĐẠT (PASS)** | Bộ 8 test case AC2 kiểm chứng: Anti-probing trả về HTTP `404 Not Found` (ngăn brute-force ID phòng khám/file), chặn đứng payload gán vai trò `SYSTEM_ADMIN` (`400 Bad Request`), chặn `CLINIC_ADMIN` duyệt hồ sơ lâm sàng, chặn bệnh nhân tự duyệt, và kiểm tra fail-closed khi tài khoản bị khóa (`account_status = DISABLED`). |
| **AC3. Bàn giao & Tái lập Kiểm thử cho M1/M2/M3**<br>M1/M2/M3 tái chạy được bộ ca trên fixture Sprint 3; lỗi quyền còn mở làm task chưa đạt, không chỉ kiểm UI ẩn nút. | **ĐẠT (PASS)** | Bộ fixture SQL chuẩn hóa `test_006_camera_authorization_fixtures.sql` với GUID hex chuẩn; bộ test độc lập `AURA.Profile.MultiTenantChecks` (22/22 checks PASS) không phụ thuộc database ngoài; tích hợp hoàn chỉnh vào pipeline CI `.github/workflows/ci.yml`. |

---

## 2. Tập Dữ liệu Chuẩn Đa Tenant (Authoritative Test Dataset)

Để đảm bảo tính nhất quán giữa các dịch vụ M1, M2, M5 và nhóm QA (M3), toàn bộ các ca kiểm thử sử dụng tập định danh GUID hex chuẩn mực sau:

| Đối tượng (Entity) | Định danh GUID | Vai trò & Bối cảnh |
| :--- | :--- | :--- |
| **Account A** | `11111111-1111-4111-8111-000000000001` | Tài khoản bệnh nhân A, chủ sở hữu Personal Tenant A và Patient A. |
| **Account B** | `11111111-1111-4111-8111-000000000002` | Tài khoản bệnh nhân B, chủ sở hữu Personal Tenant B. |
| **Account Disabled** | `11111111-1111-4111-8111-000000000007` | Tài khoản bị khóa quản trị (`identity.accounts.account_status = 'DISABLED'`). |
| **Personal Tenant A** | `11111111-1111-4111-8111-100000000001` | Tenant cá nhân của Account A (`tenant_kind = 'PERSONAL'`). |
| **Personal Tenant B** | `11111111-1111-4111-8111-100000000002` | Tenant cá nhân của Account B (`tenant_kind = 'PERSONAL'`). |
| **Clinic Alpha** | `11111111-1111-4111-8111-200000000001` | Tổ chức phòng khám Alpha (`tenant_kind = 'CLINIC'`). |
| **Clinic Beta** | `11111111-1111-4111-8111-200000000002` | Tổ chức phòng khám Beta (`tenant_kind = 'CLINIC'`). |
| **Patient A** | `11111111-1111-4111-8111-300000000001` | Bệnh nhân cá nhân thuộc Personal Tenant A. |
| **Patient B** | `11111111-1111-4111-8111-300000000002` | Bệnh nhân lâm sàng đăng ký tại Clinic Alpha. |
| **Doctor 1 (Alpha)** | `11111111-1111-4111-8111-400000000001` | Bác sĩ tại Clinic Alpha, có phân công `ACTIVE` cho Patient B. |
| **Doctor 2 (Beta / Revoked)** | `11111111-1111-4111-8111-400000000002` | Bác sĩ tại Clinic Beta; có phân công bị `REVOKED` tại Clinic Alpha. |
| **Clinic Admin Alpha** | `11111111-1111-4111-8111-500000000001` | Quản trị viên phòng khám Alpha (`role = 'CLINIC_ADMIN'`). |
| **Camera CAM-VALID-01** | `CAM-VALID-01` | Thiết bị camera có grant `ACTIVE`, chưa hết hạn cho Patient A. |
| **Camera CAM-REVOKED-02** | `CAM-REVOKED-02` | Thiết bị camera có grant mang trạng thái `REVOKED`. |
| **Camera CAM-EXPIRED-03** | `CAM-EXPIRED-03` | Thiết bị camera có grant đã hết hạn (`expires_at <= NOW()`). |

---

## 3. Ma trận Kiểm thử Phân quyền Toàn diện (Permission Test Matrix)

Ma trận dưới đây ghi nhận chi tiết từng ca thử nghiệm với kết quả mong đợi (Expected) và kết quả thực tế (Actual):

### 3.1. Nhóm 1: Ranh giới Đa Tenant & Ủy quyền Camera (AC1)

| Mã Test | Chủ thể (Actor) | Ngữ cảnh | Tài nguyên & Hành động | Điều kiện kiểm tra | Kết quả Mong đợi | Kết quả Thực tế | Trạng thái |
| :--- | :--- | :---: | :--- | :--- | :---: | :---: | :---: |
| **TC-AC1.1** | Camera `CAM-VALID-01` | `PERSONAL` | `patientA` / `ANALYSIS_CREATE` | Grant active, unexpired, đúng patient và owner | **ALLOW** (`OK`) | **ALLOW** (`OK`) | **PASS** |
| **TC-AC1.2** | Camera `CAM-UNKNOWN-99` | `PERSONAL` | `patientA` / `ANALYSIS_CREATE` | Thiết bị không tồn tại trong danh sách grant | **DENY** (`GRANT_NOT_FOUND`) | **DENY** (`GRANT_NOT_FOUND`) | **PASS** |
| **TC-AC1.3** | Camera `CAM-VALID-01` | `CLINIC` | `patientA` / `ANALYSIS_CREATE` | Gửi nhắm vào Clinic Alpha | **DENY** (`CLINIC_CAMERA_UNSUPPORTED`) | **DENY** (`CLINIC_CAMERA_UNSUPPORTED`) | **PASS** |
| **TC-AC1.4** | Camera `CAM-VALID-01` | `CLINIC` | `patientB` / `ANALYSIS_CREATE` | Bệnh nhân thuộc Clinic tenant | **DENY** (`CLINIC_CAMERA_UNSUPPORTED`) | **DENY** (`CLINIC_CAMERA_UNSUPPORTED`) | **PASS** |
| **TC-AC1.5** | Camera `CAM-REVOKED-02` | `PERSONAL` | `patientA` / `ANALYSIS_CREATE` | Grant có status = `'REVOKED'` | **DENY** (`GRANT_REVOKED`) | **DENY** (`GRANT_REVOKED`) | **PASS** |
| **TC-AC1.6** | Camera `CAM-EXPIRED-03` | `PERSONAL` | `patientA` / `ANALYSIS_CREATE` | Grant có expires_at trong quá khứ | **DENY** (`GRANT_EXPIRED`) | **DENY** (`GRANT_EXPIRED`) | **PASS** |
| **TC-AC1.7** | Camera (Account B mạo danh) | `PERSONAL` | `patientA` / `ANALYSIS_CREATE` | Actor Account B không sở hữu Patient A | **DENY** (`ACTOR_PATIENT_MISMATCH`) | **DENY** (`ACTOR_PATIENT_MISMATCH`) | **PASS** |
| **TC-AC1.8** | Bệnh nhân (Account B) | `PERSONAL` | `patientA` / `ANALYSIS_READ` | Truy cập chéo hồ sơ bệnh nhân khác | **DENY** (`FORBIDDEN`) | **DENY** (`FORBIDDEN`) | **PASS** |
| **TC-AC1.9** | Bác sĩ (Doctor 2 - Clinic Beta)| `CLINIC` | `patientB` / `ANALYSIS_READ` | Bác sĩ phòng khám khác không có assignment | **DENY** (`NO_ASSIGNMENT`) | **DENY** (`NO_ASSIGNMENT`) | **PASS** |

### 3.2. Nhóm 2: Chống Rò rỉ ID, Chống Leo quyền & Thu hồi Tức thì (AC2)

| Mã Test | Chủ thể (Actor) | Ngữ cảnh | Tài nguyên & Hành động | Tình huống kiểm thử | Kết quả Mong đợi | Kết quả Thực tế | Trạng thái |
| :--- | :--- | :---: | :--- | :--- | :---: | :---: | :---: |
| **TC-AC2.1** | Bác sĩ (Doctor 1) | `CLINIC` | `patientB` / `DOCTOR_REVIEW` | Có assignment `ACTIVE` tại Clinic Alpha | **ALLOW** (`OK`) | **ALLOW** (`OK`) | **PASS** |
| **TC-AC2.2** | Bác sĩ (Doctor 2) | `CLINIC` | `patientB` / `DOCTOR_REVIEW` | Assignment bị `REVOKED` tại Clinic Alpha | **DENY** (Instant Revocation) | **DENY** (Instant Revocation) | **PASS** |
| **TC-AC2.3** | Bác sĩ (Account Disabled) | `CLINIC` | `patientB` / `DOCTOR_REVIEW` | `identity.accounts.account_status = DISABLED` | **DENY** (Fail-closed) | **DENY** (Fail-closed) | **PASS** |
| **TC-AC2.4** | Clinic Admin Alpha | `CLINIC` | Clinic Beta / `ASSIGNMENT_LIST` | Hoán đổi ID sang Clinic Beta (Anti-Probing) | HTTP **404 Not Found** | HTTP **404 Not Found** | **PASS** |
| **TC-AC2.5** | Caller bất kỳ | `CLINIC` | File/Analysis / `FILE_READ` | Tra cứu file không thuộc tenant mình quản lý | HTTP **404 Not Found** | HTTP **404 Not Found** | **PASS** |
| **TC-AC2.6** | User / Admin | `CLINIC` | Clinic Members / `INVITE` | Cố tình gán `role = 'SYSTEM_ADMIN'` | HTTP **400 Bad Request** | HTTP **400 Bad Request** | **PASS** |
| **TC-AC2.7** | Clinic Admin Alpha | `CLINIC` | `patientB` / `DOCTOR_REVIEW` | Admin thuần túy không duyệt lâm sàng | **DENY** | **DENY** | **PASS** |
| **TC-AC2.8** | Bệnh nhân (Account A) | `PERSONAL` | `patientA` / `DOCTOR_REVIEW` | Bệnh nhân không được tự duyệt chuyên môn | **DENY** | **DENY** | **PASS** |

### 3.3. Nhóm 3: Xác thực Dịch vụ & Toàn vẹn Dữ liệu (AC3)

| Mã Test | Chủ thể Gọi | Endpoint Mục tiêu | Thông số Xác thực | Kết quả Mong đợi | Kết quả Thực tế | Trạng thái |
| :--- | :--- | :--- | :--- | :---: | :---: | :---: |
| **TC-AC3.1** | Service ngoài | `/internal/v1/profile/*` | Thiếu header `Authorization: Bearer` | HTTP **401 Unauthorized** | HTTP **401 Unauthorized** | **PASS** |
| **TC-AC3.2** | Service ngoài | `/internal/v1/profile/*` | Service JWT sai Audience (`aura-identity`) | HTTP **403 Forbidden** | HTTP **403 Forbidden** | **PASS** |
| **TC-AC3.3** | Service ngoài | `/internal/v1/profile/*` | Service JWT thiếu scope `profile:resolve` | HTTP **403 Forbidden** | HTTP **403 Forbidden** | **PASS** |
| **TC-AC3.4** | Bác sĩ Doctor 1 | `/api/v1/clinics/{cId}/assignments` | Xem trộm danh sách phân công của Doctor 2 | HTTP **403 Forbidden** | HTTP **403 Forbidden** | **PASS** |
| **TC-AC3.5** | NiFi / Analysis | Database direct mutation | Truy cập ghi trực tiếp bảng `identity`/`profile` | **BỊ CHẶN HOÀN TOÀN** (No DDL/DML) | **BỊ CHẶN HOÀN TOÀN** (No DDL/DML) | **PASS** |

---

## 4. Bằng chứng Thực thi Kiểm thử Tự động (Automated Test Execution)

Chạy lệnh kiểm thử tự động tại thư mục gốc của repository:

```bash
dotnet run --project tests/AURA.Profile.MultiTenantChecks/AURA.Profile.MultiTenantChecks.csproj
```

**Kết quả ghi nhận:**

```text
====================================================================
AURA Task W3-M6-03: Multi-Tenant Authorization & Security Checks
====================================================================

--- [AC1] Multi-Tenant Boundary & Device Delegation Checks ---
PASS: TC-AC1.1: Camera gửi grant hợp lệ cho Patient A dưới Personal Tenant A -> ALLOW
PASS: TC-AC1.2: Camera giả mạo deviceId (CAM-UNKNOWN-99) -> DENY (GRANT_NOT_FOUND)
PASS: TC-AC1.3: Camera gửi ảnh cho Patient A nhưng nhắm vào Clinic Alpha -> DENY (CLINIC_CAMERA_UNSUPPORTED)
PASS: TC-AC1.4: Camera gửi ảnh cho Patient B (thuộc Clinic Alpha) -> DENY (CLINIC_CAMERA_UNSUPPORTED)
PASS: TC-AC1.5: Camera gửi grant đã bị thu hồi (CAM-REVOKED-02) -> DENY (GRANT_REVOKED)
PASS: TC-AC1.6: Camera gửi grant đã hết hạn (CAM-EXPIRED-03) -> DENY (GRANT_EXPIRED)
PASS: TC-AC1.7: Camera gửi actorAccountId là Account B nhưng patientId là Patient A -> DENY (ACTOR_PATIENT_MISMATCH)
PASS: TC-AC1.8: Cross-Tenant Reading: Account B cố tình đọc analysis của Patient A -> DENY
PASS: TC-AC1.9: Cross-Clinic Doctor Reading: Doctor 2 (Clinic Beta) cố đọc hồ sơ Patient B (Clinic Alpha) -> DENY

--- [AC2] Anti-Probing, Anti-Escalation & Instant Revocation Checks ---
PASS: TC-AC2.1: Bác sĩ Doctor 1 có assignment ACTIVE tại Clinic Alpha -> Cho phép đọc và DOCTOR_REVIEW cho Patient B
PASS: TC-AC2.2: Bác sĩ Doctor 2 bị REVOKED assignment tại Clinic Alpha -> Từ chối DOCTOR_REVIEW tức thì (Instant Revocation)
PASS: TC-AC2.3: Bác sĩ có tài khoản bị khóa (DISABLED trong identity.accounts) -> Fail-closed từ chối mọi quyền truy cập
PASS: TC-AC2.4: Anti-probing trên ID: Truy vấn phân công với Clinic Beta khi actor thuộc Clinic Alpha -> Trả về 404 Not Found
PASS: TC-AC2.5: Anti-probing trên Analysis/File: Tra cứu file với ID không tồn tại hoặc thuộc tenant khác -> 404 Not Found
PASS: TC-AC2.6: Chặn leo quyền qua Membership payload: Cố tình gán vai trò SYSTEM_ADMIN -> 400 Bad Request (ERR_INVALID_ROLE)
PASS: TC-AC2.7: Chặn leo quyền từ Clinic Admin: CLINIC_ADMIN không được thực hiện DOCTOR_REVIEW
PASS: TC-AC2.8: Chặn bệnh nhân tự duyệt hồ sơ: Patient trong Personal tenant cố tình thực hiện DOCTOR_REVIEW -> DENY

--- [AC3] Service Authentication & Data Integrity Checks ---
PASS: TC-AC3.1: Service gọi internal API không có Bearer token -> 401 Unauthorized
PASS: TC-AC3.2: Service gọi internal API với sai Audience (aura-identity thay vì aura-profile) -> 403 Forbidden
PASS: TC-AC3.3: Service gọi internal API với thiếu Scope (cần profile:resolve) -> 403 Forbidden
PASS: TC-AC3.4: Bác sĩ chỉ xem được phân công của chính mình (chặn xem trộm phân công bác sĩ khác -> 403 Forbidden)
PASS: TC-AC3.5: Tính toàn vẹn dữ liệu: NiFi và Analysis không sửa trực tiếp dữ liệu Identity/Profile
====================================================================
KẾT QUẢ: 22/22 CHECKS PASS HOÀN TOÀN! (100% AC1, AC2, AC3)
====================================================================
```

---

## 5. Hướng dẫn Sử dụng Fixtures Sprint 3 cho QA và các Module M1, M2, M3

### 5.1. Tệp Fixture SQL
* **Đường dẫn:** `infra/postgres/migrations/tests/test_006_camera_authorization_fixtures.sql`
* **Mục đích:** Nạp sẵn các bản ghi mẫu hoàn chỉnh về Account, Tenant (Personal & Clinic), Patient, Clinic Membership, Doctor Assignment và Camera Grant (Valid, Revoked, Expired) phục vụ kiểm thử tích hợp liên dịch vụ.

### 5.2. Cách nạp Fixtures vào môi trường kiểm thử
Trong môi trường Docker dev hoặc máy cục bộ:
```bash
psql -U aura_dev -d aura_dev -f infra/postgres/migrations/tests/test_006_camera_authorization_fixtures.sql
```

### 5.3. Hướng dẫn chạy kiểm tra nhanh cho M1 (NiFi Ingestion)
1. Sử dụng Service Token có `audience = aura-profile` và `scope = profile:resolve`.
2. Gọi `POST /internal/v1/profile/camera-delegation/resolve` với payload:
   - Device hợp lệ: `CAM-VALID-01`, `patientId = 11111111-1111-4111-8111-300000000001` $\rightarrow$ Kỳ vọng trả về `allowed: true`.
   - Device thu hồi: `CAM-REVOKED-02` $\rightarrow$ Kỳ vọng trả về `allowed: false`, `reason: "GRANT_REVOKED"`.

### 5.4. Hướng dẫn chạy kiểm tra nhanh cho M2 (Analysis API)
1. Khi xử lý yêu cầu duyệt kết quả của Bác sĩ, gọi `POST /internal/v1/profile/access/resolve` với `action = "DOCTOR_REVIEW"`:
   - Bác sĩ Doctor 1 (`11111111-1111-4111-8111-400000000001`) cho Patient B $\rightarrow$ Kỳ vọng `allowed: true`.
   - Bác sĩ Doctor 2 (`11111111-1111-4111-8111-400000000002`) cho Patient B $\rightarrow$ Kỳ vọng `allowed: false` (do phân công bị `REVOKED`).
   - Tài khoản bị khóa (`11111111-1111-4111-8111-000000000007`) $\rightarrow$ Kỳ vọng `allowed: false`.

---

## 6. Danh mục Tài liệu Thiết kế Bàn giao

1. **Sơ đồ Thực thể Liên kết & Từ điển Dữ liệu:**
   - [docs/design/identity_profile_clinic_erd_and_data_dictionary.md](file:///c:/Users/Hoang/OneDrive/Desktop/Phần%20Mền%20Hướng%20Đối%20Tượng/XD-PMHDT/docs/design/identity_profile_clinic_erd_and_data_dictionary.md)
2. **Sơ đồ Tuần tự Phân quyền Sprint 3:**
   - [docs/api/identity_profile_sequences_sprint3.md](file:///c:/Users/Hoang/OneDrive/Desktop/Phần%20Mền%20Hướng%20Đối%20Tượng/XD-PMHDT/docs/api/identity_profile_sequences_sprint3.md)
3. **Đặc tả Contract API OpenAPI 3.0:**
   - [contracts/profile-openapi.yaml](file:///c:/Users/Hoang/OneDrive/Desktop/Phần%20Mền%20Hướng%20Đối%20Tượng/XD-PMHDT/contracts/profile-openapi.yaml)
4. **Bộ Test Tự động Đa Tenant:**
   - [tests/AURA.Profile.MultiTenantChecks/](file:///c:/Users/Hoang/OneDrive/Desktop/Phần%20Mền%20Hướng%20Đối%20Tượng/XD-PMHDT/tests/AURA.Profile.MultiTenantChecks/)
