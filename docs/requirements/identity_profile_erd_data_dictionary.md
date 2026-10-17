# AURA — Đặc tả ERD và Data Dictionary: Identity & Profile Services

- **Mã đề tài**: SP26SE025 — Hệ thống sàng lọc sức khỏe mạch máu võng mạc (AURA)
- **Sprint**: Sprint 2
- **Task Jira**: SCRUM-316 (W2-M6-03: Kiểm thử và tài liệu Identity/Profile)
- **Module phụ trách**: M6 (Identity & Profile Services)
- **Thời điểm hoàn thành**: 2026-10-17
- **Phiên bản tài liệu**: 1.0

---

## 1. Tổng quan Kiến trúc Dữ liệu M6

Theo quy chuẩn kiến trúc Microservices và nguyên tắc tách biệt ngữ cảnh (Bounded Context) được quy định tại [Contract Sprint 2 v1.0](file:///c:/Users/Hoang/OneDrive/Desktop/Phần Mền Hướng Đối Tượng/XD-PMHDT/docs/development/sprint2_local_setup.md):
1. **Phân chia Schema độc lập**:
   - `identity`: Quản lý xác thực, ánh xạ tài khoản ngoại vi (Firebase UID), cấp phát token dịch vụ và phân quyền hệ thống.
   - `profile`: Quản lý phạm vi dữ liệu (Multi-tenancy), hồ sơ người dùng, hồ sơ bệnh nhân, cơ cấu tổ chức phòng khám và phân công bác sĩ.
2. **Không tạo Khóa ngoại xuyên Schema (No Cross-Schema Foreign Keys)**:
   - Để đảm bảo tính độc lập và sẵn sàng tách thành các database riêng biệt trong tương lai, giữa `identity` và `profile` không sử dụng ràng buộc `FOREIGN KEY` của PostgreSQL mà liên kết qua định danh logic (`account_id` UUID).
3. **Nguyên tắc phân định định danh bắt buộc**:
   $$\text{accountId} \neq \text{patientId} \neq \text{clinicId} \neq \text{tenantId}$$
4. **Least-Privilege Database Roles**:
   - `aura_identity_app`: Chỉ có quyền DML trên schema `identity`.
   - `aura_profile_app`: Chỉ có quyền DML trên schema `profile`.

---

## 2. Sơ đồ Thực thể Liên kết (Entity Relationship Diagram - ERD)

```mermaid
erDiagram
    %% ==========================================
    %% SCHEMA IDENTITY
    %% ==========================================
    identity_roles {
        varchar(32) role_name PK "Tên vai trò (PATIENT, DOCTOR, CLINIC_ADMIN, SYSTEM_ADMIN)"
        varchar(255) description "Mô tả vai trò"
        timestamptz created_at "Thời điểm tạo"
    }

    identity_accounts {
        uuid account_id PK "Định danh tài khoản nội bộ duy nhất"
        varchar(128) firebase_uid UK "Định danh người dùng Firebase UID"
        varchar(32) account_status "Trạng thái: ACTIVE, DISABLED"
        varchar(32) profile_provisioning_status "Trạng thái khởi tạo: PENDING, READY"
        timestamptz created_at "Thời điểm tạo tài khoản"
        timestamptz updated_at "Thời điểm cập nhật gần nhất"
    }

    identity_account_roles {
        uuid account_id PK, FK "Tài khoản được gán vai trò"
        varchar(32) role_name PK, FK "Vai trò được gán"
        timestamptz granted_at "Thời điểm cấp quyền"
    }

    identity_service_clients {
        varchar(64) client_id PK "Mã định danh client dịch vụ"
        varchar(256) client_secret_hash "Hash mã bí mật client"
        text[] allowed_scopes "Danh sách quyền hạn cho phép"
        text[] allowed_audiences "Danh sách đối tượng thụ hưởng"
        boolean is_active "Trạng thái hoạt động"
        timestamptz created_at "Thời điểm đăng ký client"
    }

    identity_accounts ||--o{ identity_account_roles : "sở hữu"
    identity_roles ||--o{ identity_account_roles : "được gán cho"

    %% ==========================================
    %% SCHEMA PROFILE
    %% ==========================================
    profile_tenants {
        uuid tenant_id PK "Định danh phạm vi cách ly dữ liệu"
        varchar(32) tenant_kind "Loại phạm vi: PERSONAL, CLINIC"
        uuid owner_principal_id "Chủ sở hữu (accountId hoặc clinicId)"
        timestamptz created_at "Thời điểm khởi tạo"
    }

    profile_clinics {
        uuid clinic_id PK "Định danh phòng khám / tổ chức y tế"
        uuid tenant_id FK "Tenant quản lý phòng khám"
        varchar(200) name "Tên phòng khám"
        varchar(32) status "Trạng thái hoạt động: ACTIVE, INACTIVE"
        timestamptz created_at "Thời điểm tạo"
        timestamptz updated_at "Thời điểm cập nhật"
    }

    profile_profiles {
        uuid account_id PK "Liên kết logic 1:1 với identity.accounts"
        varchar(100) display_name "Tên hiển thị người dùng"
        varchar(30) phone "Số điện thoại liên hệ"
        uuid personal_tenant_id FK "Tenant cá nhân mặc định của người dùng"
        integer profile_version "Phiên bản hồ sơ phục vụ Optimistic Locking (ETag)"
        timestamptz created_at "Thời điểm tạo"
        timestamptz updated_at "Thời điểm cập nhật"
    }

    profile_patients {
        uuid patient_id PK "Định danh hồ sơ bệnh nhân duy nhất"
        uuid tenant_id FK "Tenant sở hữu hồ sơ bệnh nhân"
        uuid linked_account_id "Tài khoản liên kết nếu có (nullable)"
        uuid owner_principal_id "Chủ thể sở hữu dữ liệu (accountId hoặc clinicId)"
        varchar(32) status "Trạng thái: ACTIVE, ARCHIVED"
        timestamptz created_at "Thời điểm tạo"
        timestamptz updated_at "Thời điểm cập nhật"
    }

    profile_memberships {
        uuid membership_id PK "Định danh thành viên phòng khám"
        uuid clinic_id FK "Phòng khám trực thuộc"
        uuid account_id "Tài khoản nhân sự"
        varchar(32) role "Vai trò tại phòng khám: CLINIC_ADMIN, DOCTOR, STAFF"
        varchar(32) status "Trạng thái: ACTIVE, SUSPENDED"
        timestamptz created_at "Thời điểm tham gia"
        timestamptz updated_at "Thời điểm cập nhật"
    }

    profile_doctor_assignments {
        uuid assignment_id PK "Định danh phân công bác sĩ"
        uuid clinic_id FK "Phòng khám chỉ định"
        uuid doctor_account_id "Tài khoản bác sĩ phụ trách"
        uuid patient_id FK "Bệnh nhân được phân công"
        uuid assigned_by "Người thực hiện phân công"
        varchar(32) status "Trạng thái: ACTIVE, REVOKED"
        timestamptz created_at "Thời điểm phân công"
        timestamptz updated_at "Thời điểm cập nhật"
    }

    profile_tenants ||--o{ profile_clinics : "cung cấp phạm vi cho"
    profile_tenants ||--o{ profile_patients : "phân vùng"
    profile_tenants ||--o{ profile_profiles : "gán tenant cá nhân"
    profile_clinics ||--o{ profile_memberships : "quản lý nhân sự"
    profile_clinics ||--o{ profile_doctor_assignments : "phân công trong phạm vi"
    profile_patients ||--o{ profile_doctor_assignments : "được chỉ định cho"

    %% ==========================================
    %% LOGICAL CROSS-SCHEMA RELATIONSHIP
    %% ==========================================
    identity_accounts -.-> profile_profiles : "Liên kết logic 1:1 qua account_id"
    identity_accounts -.-> profile_memberships : "Liên kết nhân sự qua account_id"
    identity_accounts -.-> profile_doctor_assignments : "Bác sĩ thực hiện qua doctor_account_id"
```

---

## 3. Từ điển Dữ liệu Chi tiết (Data Dictionary)

### 3.1. Schema `identity`

#### Bảng: `identity.roles`
Lưu trữ danh mục vai trò chuẩn của toàn bộ hệ thống AURA.
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `role_name` | `VARCHAR(32)` | **PK**, NOT NULL | | Tên mã vai trò hệ thống (`PATIENT`, `DOCTOR`, `CLINIC_ADMIN`, `SYSTEM_ADMIN`). |
| `description` | `VARCHAR(255)` | NULL | | Diễn giải trách nhiệm và quyền hạn của vai trò. |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm tạo bản ghi. |

#### Bảng: `identity.accounts`
Định danh người dùng nội bộ, quản lý trạng thái tài khoản và ánh xạ 1:1 với Firebase Authentication.
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `account_id` | `UUID` | **PK**, NOT NULL | `gen_random_uuid()` | Định danh người dùng nội bộ. Sử dụng UUIDv4 ngẫu nhiên, độc lập hoàn toàn với Firebase UID. |
| `firebase_uid` | `VARCHAR(128)` | **UNIQUE**, NOT NULL | | UID được cấp phát bởi Firebase Authentication sau khi đăng nhập thành công. |
| `account_status` | `VARCHAR(32)` | NOT NULL | `'ACTIVE'` | Trạng thái tài khoản: `ACTIVE` (hoạt động), `DISABLED` (bị khóa bởi quản trị viên). |
| `profile_provisioning_status` | `VARCHAR(32)` | NOT NULL | `'PENDING'` | Trạng thái khởi tạo hồ sơ bên Profile Service: `PENDING` (chờ xử lý), `READY` (đã khởi tạo hoàn tất). |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm đăng ký / bootstrap tài khoản lần đầu. |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm cập nhật trạng thái gần nhất. |

- **Chỉ mục (Indexes)**:
  - `idx_accounts_firebase_uid` trên cột `firebase_uid` (B-Tree).

#### Bảng: `identity.account_roles`
Quản lý việc gán vai trò cho tài khoản.
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `account_id` | `UUID` | **PK, FK**, NOT NULL | | Tham chiếu đến `identity.accounts(account_id)` với `ON DELETE CASCADE`. |
| `role_name` | `VARCHAR(32)` | **PK, FK**, NOT NULL | | Tham chiếu đến `identity.roles(role_name)` với `ON DELETE RESTRICT`. |
| `granted_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm cấp phát vai trò. |

#### Bảng: `identity.service_clients`
Lưu trữ thông tin xác thực cho các dịch vụ nội bộ (Machine-to-Machine / Client Credentials).
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `client_id` | `VARCHAR(64)` | **PK**, NOT NULL | | Mã định danh dịch vụ gọi (ví dụ: `analysis-api`, `billing-api`). |
| `client_secret_hash` | `VARCHAR(256)` | NOT NULL | | Hash chuỗi bí mật của client (không lưu plaintext). |
| `allowed_scopes` | `TEXT[]` | NOT NULL | `'{}'` | Danh sách các phạm vi quyền được phép cấp (ví dụ: `identity:context`, `profile:resolve`). |
| `allowed_audiences` | `TEXT[]` | NOT NULL | `'{}'` | Danh sách audience được phép nhận token (ví dụ: `aura-internal-services`). |
| `is_active` | `BOOLEAN` | NOT NULL | `TRUE` | Trạng thái hoạt động của client. |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm tạo bản ghi. |

---

### 3.2. Schema `profile`

#### Bảng: `profile.tenants`
Quản lý phạm vi cách ly dữ liệu y tế (Data Isolation Scope).
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `tenant_id` | `UUID` | **PK**, NOT NULL | `gen_random_uuid()` | Định danh phạm vi cách ly dữ liệu duy nhất. |
| `tenant_kind` | `VARCHAR(32)` | NOT NULL | | Phân loại tenant: `PERSONAL` (người dùng tự do), `CLINIC` (phòng khám). |
| `owner_principal_id` | `UUID` | NOT NULL | | Định danh chủ sở hữu: lưu `account_id` nếu là `PERSONAL`, hoặc `clinic_id` nếu là `CLINIC`. |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm khởi tạo phạm vi. |

- **Chỉ mục (Indexes)**:
  - `idx_tenants_owner` trên cột `owner_principal_id`.

#### Bảng: `profile.clinics`
Lưu trữ thông tin phòng khám hoặc tổ chức y tế tham gia hệ thống AURA.
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `clinic_id` | `UUID` | **PK**, NOT NULL | `gen_random_uuid()` | Định danh phòng khám duy nhất. |
| `tenant_id` | `UUID` | **FK**, NOT NULL | | Tham chiếu đến `profile.tenants(tenant_id)` với `ON DELETE RESTRICT`. |
| `name` | `VARCHAR(200)` | NOT NULL | | Tên phòng khám / trung tâm y tế. |
| `status` | `VARCHAR(32)` | NOT NULL | `'ACTIVE'` | Trạng thái: `ACTIVE`, `SUSPENDED`, `INACTIVE`. |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm thành lập trên hệ thống. |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm cập nhật thông tin gần nhất. |

#### Bảng: `profile.profiles`
Lưu trữ thông tin hồ sơ cá nhân của người dùng, hỗ trợ Optimistic Concurrency Control.
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `account_id` | `UUID` | **PK**, NOT NULL | | Khóa chính, khớp với `identity.accounts(account_id)`. |
| `display_name` | `VARCHAR(100)` | NOT NULL | | Tên hiển thị người dùng (từ 1 đến 100 ký tự). |
| `phone` | `VARCHAR(30)` | NULL | | Số điện thoại liên hệ (tối đa 30 ký tự). |
| `personal_tenant_id` | `UUID` | **FK**, NULL | | Tham chiếu đến `profile.tenants(tenant_id)` với `ON DELETE SET NULL`. |
| `profile_version` | `INT` | NOT NULL | `1` | Số phiên bản tăng dần dùng để sinh header `ETag` và kiểm tra `If-Match`. |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm tạo hồ sơ. |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm cập nhật hồ sơ gần nhất. |

#### Bảng: `profile.patients`
Lưu trữ thực thể Bệnh nhân phục vụ nghiệp vụ chụp đáy mắt và phân tích sức khỏe võng mạc.
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `patient_id` | `UUID` | **PK**, NOT NULL | `gen_random_uuid()` | Định danh bệnh nhân duy nhất (khác biệt hoàn toàn với `account_id`). |
| `tenant_id` | `UUID` | **FK**, NOT NULL | | Tenant sở hữu bệnh nhân (`profile.tenants`). |
| `linked_account_id` | `UUID` | NULL | | Tài khoản người dùng liên kết nếu bệnh nhân tự tạo (`account_id`), có thể `NULL` nếu là bệnh nhân vãng lai do phòng khám lập. |
| `owner_principal_id` | `UUID` | NOT NULL | | Chủ sở hữu: `account_id` (với cá nhân) hoặc `clinic_id` (với phòng khám). |
| `status` | `VARCHAR(32)` | NOT NULL | `'ACTIVE'` | Trạng thái hồ sơ: `ACTIVE`, `ARCHIVED`. |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm khởi tạo bệnh nhân. |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm cập nhật gần nhất. |

- **Chỉ mục (Indexes)**:
  - `idx_patients_tenant` trên cột `tenant_id`.
  - `idx_patients_linked_account` trên cột `linked_account_id`.
  - `idx_patients_owner` trên cột `owner_principal_id`.

#### Bảng: `profile.memberships`
Quản lý vai trò và tư cách thành viên của nhân sự y tế trong một phòng khám.
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `membership_id` | `UUID` | **PK**, NOT NULL | `gen_random_uuid()` | Định danh tư cách thành viên duy nhất. |
| `clinic_id` | `UUID` | **FK**, NOT NULL | | Tham chiếu `profile.clinics(clinic_id)` với `ON DELETE CASCADE`. |
| `account_id` | `UUID` | NOT NULL | | Tài khoản nhân sự y tế được gán (`account_id`). |
| `role` | `VARCHAR(32)` | NOT NULL | | Vai trò tại phòng khám: `CLINIC_ADMIN`, `DOCTOR`, `STAFF`. |
| `status` | `VARCHAR(32)` | NOT NULL | `'ACTIVE'` | Trạng thái: `ACTIVE`, `REVOKED`. |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm gia nhập phòng khám. |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm cập nhật trạng thái gần nhất. |

- **Ràng buộc duy nhất**: `UNIQUE (clinic_id, account_id)`.
- **Chỉ mục (Indexes)**: `idx_memberships_account` trên cột `account_id`.

#### Bảng: `profile.doctor_assignments`
Lưu trữ lịch sử và trạng thái phân công Bác sĩ phụ trách đánh giá chuyên môn cho Bệnh nhân trong phạm vi Phòng khám.
| Tên cột | Kiểu dữ liệu | Ràng buộc | Mặc định | Ý nghĩa & Quy chuẩn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| `assignment_id` | `UUID` | **PK**, NOT NULL | `gen_random_uuid()` | Định danh phân công duy nhất. |
| `clinic_id` | `UUID` | **FK**, NOT NULL | | Tham chiếu `profile.clinics(clinic_id)` với `ON DELETE CASCADE`. |
| `doctor_account_id` | `UUID` | NOT NULL | | Tài khoản của Bác sĩ được phân công (`account_id`). |
| `patient_id` | `UUID` | **FK**, NOT NULL | | Tham chiếu `profile.patients(patient_id)` với `ON DELETE CASCADE`. |
| `assigned_by` | `UUID` | NOT NULL | | Tài khoản người ra quyết định phân công (`CLINIC_ADMIN`). |
| `status` | `VARCHAR(32)` | NOT NULL | `'ACTIVE'` | Trạng thái: `ACTIVE`, `REVOKED`. |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm phân công. |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL | `NOW()` | Thời điểm cập nhật trạng thái gần nhất. |

- **Ràng buộc duy nhất**: `UNIQUE (clinic_id, doctor_account_id, patient_id)`.
- **Chỉ mục (Indexes)**:
  - `idx_doctor_assignments_lookup` trên `(clinic_id, doctor_account_id, patient_id, status)` phục vụ tra cứu phân giải quyền siêu tốc cho Analysis Service.
