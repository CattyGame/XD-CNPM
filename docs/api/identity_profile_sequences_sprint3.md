# AURA — Sơ đồ Tuần tự Phân quyền Đa Tenant (Sequence Diagrams Sprint 3)

- **Mã nhiệm vụ:** W3-M6-03 (Task Jira: SCRUM W3-M6-03)
- **Tiêu đề:** Sơ đồ Tuần tự Thẩm định Quyền Đa Tenant, Camera Delegation & Vòng đời Phân công
- **Phụ trách:** M6 (Identity & Profile Services)
- **Phiên bản:** 3.0
- **Ngày hoàn thành:** 2026-10-30

---

## 1. Sơ đồ 1: Luồng Thẩm định Ủy quyền Máy chụp (Camera Delegation Verification Flow)

Mô tả quá trình Apache NiFi (M1) nhận tệp hình ảnh từ thiết bị chụp ảnh y tế (Camera Device), gửi yêu cầu thẩm định đến Profile API (M6) qua Internal API được bảo vệ bởi Service JWT RS256 để xác thực tính hợp lệ của thiết bị, bệnh nhân và tenant trước khi lưu trữ hoặc chuyển tiếp cho phân tích.

```mermaid
sequenceDiagram
    autonumber
    participant Cam as Thiết bị Camera (Edge Device)
    participant NiFi as Apache NiFi Ingestion (M1)
    participant ID as Identity API (Port 5004)
    participant PR as Profile API (Port 5005)
    participant DB as PostgreSQL 17 (aura_dev)

    Cam->>NiFi: Gửi tệp ảnh (Kèm FlowFile metadata: deviceId, patientId, actorAccountId)
    activate NiFi
    Note over NiFi: Chuẩn bị kiểm tra ủy quyền camera

    opt NiFi chưa có Service JWT còn hạn
        NiFi->>ID: POST /internal/v1/identity/service-tokens<br/>{ clientId: "nifi-ingestion", clientSecret, audience: "aura-profile", scope: "profile:resolve" }
        activate ID
        ID->>ID: Xác thực PBKDF2 hash & Whitelist Scope
        ID-->>NiFi: Trả về Service JWT RS256 (exp: 300s)
        deactivate ID
    end

    NiFi->>PR: POST /internal/v1/profile/camera-delegation/resolve<br/>Authorization: Bearer <Service JWT><br/>{ deviceId, patientId, actorAccountId, expectedTenantId, action: "ANALYSIS_CREATE" }
    activate PR

    Note over PR: 1. ServiceTokenValidator kiểm tra RS256, audience, scope
    PR->>DB: SELECT account_status FROM identity.accounts WHERE account_id = actorAccountId
    alt Tài khoản ủy quyền bị khóa (account_status != 'ACTIVE')
        PR-->>NiFi: HTTP 200 OK { allowed: false, reason: "ACCOUNT_DISABLED" }
    else Tài khoản hợp lệ
        PR->>DB: SELECT tenant_kind FROM profile.tenants WHERE tenant_id = expectedTenantId
        alt Tenant là CLINIC (Chưa hỗ trợ camera delegation ở Sprint 3)
            PR-->>NiFi: HTTP 200 OK { allowed: false, reason: "CLINIC_CAMERA_UNSUPPORTED" }
        else Tenant là PERSONAL
            PR->>DB: SELECT * FROM profile.camera_grants<br/>WHERE device_id = deviceId AND patient_id = patientId AND tenant_id = expectedTenantId
            alt Không tìm thấy grant hoặc sai deviceId
                PR-->>NiFi: HTTP 200 OK { allowed: false, reason: "GRANT_NOT_FOUND" }
            else Grant bị thu hồi (status == 'REVOKED')
                PR-->>NiFi: HTTP 200 OK { allowed: false, reason: "GRANT_REVOKED" }
            else Grant hết hạn (expires_at <= NOW())
                PR-->>NiFi: HTTP 200 OK { allowed: false, reason: "GRANT_EXPIRED" }
            else Grant ACTIVE và hợp lệ
                PR-->>NiFi: HTTP 200 OK { allowed: true, patientId, tenantId, reason: "OK" }
            end
        end
    end
    deactivate PR

    alt allowed == true
        NiFi->>NiFi: Định tuyến ảnh vào pipeline phân tích hợp lệ
    else allowed == false
        NiFi->>NiFi: Định tuyến FlowFile vào hàng đợi lỗi (Quarantine Queue)
    end
    deactivate NiFi
```

---

## 2. Sơ đồ 2: Luồng Duyệt Kết quả Lâm sàng & Thu hồi Tức thì (Clinical Review & Instant Revocation)

Mô tả luồng Bác sĩ gửi đánh giá chuyên môn y tế (`DOCTOR_REVIEW`) trên hệ thống. Dịch vụ Analysis API (M2) gửi yêu cầu xác minh quyền đến Profile API (M6). Mọi trường hợp phân công bị thu hồi (`REVOKED`) hoặc tài khoản bị khóa (`DISABLED`) đều bị từ chối fail-closed lập tức.

```mermaid
sequenceDiagram
    autonumber
    actor Doc as Bác sĩ (Doctor Client)
    participant GW as API Gateway (Port 5000)
    participant AN as Analysis API (Port 5001)
    participant ID as Identity API (Port 5004)
    participant PR as Profile API (Port 5005)
    participant DB as PostgreSQL 17 (aura_dev)

    Doc->>GW: POST /api/v1/analyses/{analysisId}/review<br/>Authorization: Bearer <Doctor User Token><br/>{ diagnosis, notes, approved: true }
    activate GW
    GW->>GW: Xác thực User Token & loại bỏ spoofed headers
    GW->>AN: Forward request kèm User Claims (accountId, email)
    deactivate GW

    activate AN
    Note over AN: Cần thẩm định quyền duyệt kết quả y tế

    opt Analysis chưa có Service JWT cho Profile
        AN->>ID: POST /internal/v1/identity/service-tokens<br/>{ clientId: "analysis-api", audience: "aura-profile", scope: "profile:resolve" }
        activate ID
        ID-->>AN: Service JWT RS256
        deactivate ID
    end

    AN->>PR: POST /internal/v1/profile/access/resolve<br/>Authorization: Bearer <Service JWT><br/>{ actorAccountId, patientId, clinicContextId, action: "DOCTOR_REVIEW" }
    activate PR

    PR->>DB: SELECT account_status FROM identity.accounts WHERE account_id = actorAccountId
    alt Tài khoản bị khóa (status == 'DISABLED')
        PR-->>AN: HTTP 200 OK { allowed: false, reason: "ACCOUNT_DISABLED" }
    else Tài khoản ACTIVE
        PR->>DB: SELECT role, status FROM profile.clinic_memberships<br/>WHERE clinic_id = clinicContextId AND account_id = actorAccountId
        alt Không phải thành viên hoặc status != 'ACTIVE'
            PR-->>AN: HTTP 200 OK { allowed: false, reason: "MEMBERSHIP_NOT_ACTIVE" }
        else Là CLINIC_ADMIN thuần túy (role == 'CLINIC_ADMIN')
            Note over PR: Admin phòng khám không có quyền duyệt chuyên môn
            PR-->>AN: HTTP 200 OK { allowed: false, reason: "CLINIC_ADMIN_CANNOT_REVIEW" }
        else Là DOCTOR
            PR->>DB: SELECT status FROM profile.doctor_assignments<br/>WHERE clinic_id = clinicContextId AND doctor_id = actorAccountId AND patient_id = patientId
            alt Không có phân công hoặc status == 'REVOKED'
                Note over PR: Instant Revocation có hiệu lực ngay lập tức
                PR-->>AN: HTTP 200 OK { allowed: false, reason: "DOCTOR_ASSIGNMENT_REVOKED" }
            else Phân công ACTIVE
                PR-->>AN: HTTP 200 OK { allowed: true, reason: "OK" }
            end
        end
    end
    deactivate PR

    alt allowed == true
        AN->>DB: Lưu trữ kết quả duyệt chuyên môn vào bảng phân tích
        AN-->>Doc: HTTP 200 OK { status: "REVIEWED", reviewerId: actorAccountId }
    else allowed == false
        AN-->>Doc: HTTP 403 Forbidden { code: "ERR_FORBIDDEN", message: "Bác sĩ không có quyền duyệt hồ sơ bệnh nhân này." }
    end
    deactivate AN
```

---

## 3. Sơ đồ 3: Cơ chế Chống Thăm dò ID & Chống Leo quyền Đa Phòng khám (Anti-Probing & Anti-Escalation)

Mô tả cơ chế bảo vệ danh tính phòng khám khi người dùng gọi API thao tác hoặc tra cứu chéo phòng khám, hệ thống trả về mã lỗi HTTP `404 Not Found` để che giấu sự tồn tại của Clinic ID mục tiêu, đồng thời ngăn chặn nâng quyền thông qua payload.

```mermaid
sequenceDiagram
    autonumber
    actor Attacker as Kẻ tấn công / User trái phép
    participant GW as API Gateway (Port 5000)
    participant PR as Profile API (Port 5005)
    participant DB as PostgreSQL 17 (aura_dev)

    Note over Attacker,GW: Tình huống 1: Dò quét ID phòng khám khác (Cross-Clinic ID Probing)
    Attacker->>GW: GET /api/v1/clinics/{foreignClinicId}/assignments<br/>Authorization: Bearer <User Token của Clinic Alpha>
    activate GW
    GW->>PR: Forward request
    deactivate GW

    activate PR
    PR->>DB: SELECT role, status FROM profile.clinic_memberships<br/>WHERE clinic_id = foreignClinicId AND account_id = callerAccountId
    DB-->>PR: Không tìm thấy bản ghi (Caller không thuộc foreignClinicId)
    Note over PR: Trả 404 Not Found thay vì 403 Forbidden<br/>để ngăn kẻ tấn công xác định foreignClinicId có tồn tại hay không
    PR-->>Attacker: HTTP 404 Not Found { code: "ERR_CLINIC_NOT_FOUND", message: "Không tìm thấy phòng khám." }
    deactivate PR

    Note over Attacker,GW: Tình huống 2: Cố tình nâng quyền qua Membership Payload (Privilege Escalation)
    Attacker->>GW: POST /api/v1/clinics/{clinicId}/members<br/>Authorization: Bearer <Admin Token><br/>{ accountId: targetUserId, role: "SYSTEM_ADMIN" }
    activate GW
    GW->>PR: Forward request
    deactivate GW

    activate PR
    Note over PR: Kiểm tra danh sách vai trò cho phép: [CLINIC_ADMIN, DOCTOR, STAFF]
    PR->>PR: Role "SYSTEM_ADMIN" không nằm trong AllowedRoles
    PR-->>Attacker: HTTP 400 Bad Request { code: "ERR_INVALID_ROLE", message: "Vai trò yêu cầu không hợp lệ." }
    deactivate PR
```
