# AURA — Sơ đồ Tuần tự Tương tác (Sequence Diagrams): Identity & Profile Services

- **Mã đề tài**: SP26SE025 — Hệ thống sàng lọc sức khỏe mạch máu võng mạc (AURA)
- **Sprint**: Sprint 2
- **Task Jira**: SCRUM-316 (W2-M6-03: Kiểm thử và tài liệu Identity/Profile)
- **Module phụ trách**: M6 (Identity & Profile Services)
- **Thời điểm hoàn thành**: 2026-10-17

---

## 1. Sơ đồ 1: Luồng Đăng nhập, Xác thực và Khởi tạo Tài khoản (Login & Bootstrap Flow)

Mô tả quá trình người dùng đăng nhập qua Firebase Authentication trên giao diện Web, sau đó gọi qua API Gateway đến Identity Service để tạo hoặc đồng bộ tài khoản nội bộ (`accountId`), và tự động kích hoạt Profile Service để khởi tạo Personal Tenant và hồ sơ cá nhân.

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng (Client)
    participant Web as Frontend (aura-web)
    participant FB as Firebase Authentication
    participant GW as API Gateway (YARP / Port 5000)
    participant ID as Identity API (Port 5004)
    participant PR as Profile API (Port 5005)
    participant DB as PostgreSQL 17 (aura_dev)

    User->>Web: Nhập Email/Password hoặc Đăng nhập Google
    Web->>FB: Gửi thông tin xác thực (Firebase Web SDK)
    FB-->>Web: Trả về Firebase User & ID Token (JWT)

    Note over Web,GW: Client tự động gửi kèm X-Correlation-Id
    Web->>GW: POST /api/v1/identity/bootstrap<br/>Authorization: Bearer <Firebase ID Token>
    
    activate GW
    Note over GW: GatewayFirebaseAuthMiddleware
    GW->>GW: Loại bỏ header giả mạo (X-Account-Id, X-Role, X-Tenant-Id)
    GW->>FB: Xác minh chữ ký & thời hạn ID Token (VerifyIdTokenAsync)
    FB-->>GW: Token hợp lệ (UID: "hoang_dev_1")
    GW->>ID: Chuyển tiếp request (bảo lưu Authorization & Correlation-Id)
    deactivate GW

    activate ID
    ID->>DB: SELECT * FROM identity.accounts WHERE firebase_uid = 'hoang_dev_1'
    alt Tài khoản chưa tồn tại
        ID->>DB: INSERT INTO identity.accounts (firebase_uid, status: ACTIVE)<br/>INSERT INTO identity.account_roles (role: PATIENT)
        DB-->>ID: Sinh account_id UUID mới
    else Tài khoản đã tồn tại
        DB-->>ID: Trả về account_id hiện hành (Idempotent)
    end

    opt Hồ sơ chưa hoàn tất (profileProvisioningStatus != 'READY')
        ID->>PR: POST /internal/v1/profile/provision<br/>{ accountId, firebaseUid, displayName, email }
        activate PR
        PR->>DB: INSERT INTO profile.tenants (tenant_kind: 'PERSONAL', owner: accountId)<br/>INSERT INTO profile.profiles (account_id, version: 1)<br/>INSERT INTO profile.patients (owner: accountId)
        DB-->>PR: Hoàn tất khởi tạo (accountId != patientId != personalTenantId)
        PR-->>ID: HTTP 200 OK (ProfileDto)
        deactivate PR
        ID->>DB: UPDATE identity.accounts SET profile_provisioning_status = 'READY'
    end

    ID-->>GW: HTTP 200 OK (AccountDto: accountId, roles: ["PATIENT"])
    deactivate ID

    GW-->>Web: HTTP 200 OK (AccountDto)
    Web-->>User: Điều hướng vào Dashboard tương ứng với vai trò
```

---

## 2. Sơ đồ 2: Luồng Xem và Cập nhật Hồ sơ cá nhân với Optimistic Concurrency Control

Mô tả cơ chế kiểm soát đồng thời lạc quan (Optimistic Concurrency Control) thông qua Header `ETag` và `If-Match`, đồng thời chứng minh khả năng **chống sửa Role qua Profile API** để ngăn ngừa triệt để lỗ hổng leo thang đặc quyền (Privilege Escalation).

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    participant Web as Frontend (aura-web)
    participant GW as API Gateway (Port 5000)
    participant PR as Profile API (Port 5005)
    participant DB as PostgreSQL (profile schema)

    %% GIAI ĐOẠN 1: XEM HỒ SƠ
    rect rgb(240, 248, 255)
    Note over User,DB: Giai đoạn 1: Xem thông tin hồ sơ và lấy ETag phiên bản
    User->>Web: Mở trang "Cài đặt hồ sơ cá nhân"
    Web->>GW: GET /api/v1/profiles/me (Bearer Token)
    GW->>PR: Chuyển tiếp GET /api/v1/profiles/me
    activate PR
    PR->>DB: SELECT * FROM profile.profiles WHERE account_id = @accountId
    DB-->>PR: Trả về thông tin hồ sơ (profile_version = 1)
    PR-->>GW: HTTP 200 OK<br/>Header: ETag: "1"<br/>Body: { displayName: "Nguyen Van Hoang", phone: null, profileVersion: 1 }
    deactivate PR
    GW-->>Web: HTTP 200 OK (Header ETag: "1")
    Web-->>User: Hiển thị form thông tin (Lưu trữ ETag "1" vào memory state)
    end

    %% GIAI ĐOẠN 2: CẬP NHẬT HỒ SƠ
    rect rgb(255, 250, 240)
    Note over User,DB: Giai đoạn 2: Cập nhật hồ sơ & Chống hack Role
    User->>Web: Nhập DisplayName mới, Phone mới và bấm "Lưu thay đổi"
    
    Note over Web,GW: Client gửi kèm If-Match: "1" và DTO chỉ chứa displayName, phone
    Web->>GW: PATCH /api/v1/profiles/me<br/>Header: If-Match: "1"<br/>Body: { displayName: "Hoang Updated", phone: "0912345678", role: "SYSTEM_ADMIN" }
    GW->>PR: Chuyển tiếp PATCH /api/v1/profiles/me
    
    activate PR
    Note over PR: 1. Kiểm tra header If-Match bắt buộc<br/>2. DTO bảo mật loại bỏ hoàn toàn trường 'role'
    PR->>DB: UPDATE profile.profiles<br/>SET display_name = 'Hoang Updated', phone = '0912345678',<br/>    profile_version = profile_version + 1, updated_at = NOW()<br/>WHERE account_id = @accountId AND profile_version = 1
    
    alt Phiên bản khớp (Cập nhật thành công 1 dòng)
        DB-->>PR: 1 row updated (profile_version mới = 2)
        PR-->>GW: HTTP 200 OK<br/>Header: ETag: "2"<br/>Body: { displayName: "Hoang Updated", phone: "0912345678", profileVersion: 2 }
        GW-->>Web: HTTP 200 OK (Header ETag: "2")
        Web-->>User: Hiển thị thông báo "Cập nhật hồ sơ thành công!"
    else Xung đột phiên bản (Đã có phiên bản khác ghi đè trước đó)
        DB-->>PR: 0 rows updated
        PR-->>GW: HTTP 409 Conflict (ERR_PROFILE_VERSION_CONFLICT)
        GW-->>Web: HTTP 409 Conflict
        Web-->>User: Cảnh báo "Hồ sơ đã bị thay đổi bởi phiên khác, vui lòng tải lại!"
    end
    deactivate PR
    end
```

---

## 3. Sơ đồ 3: Luồng Phân giải Quyền truy cập giữa các Dịch vụ (Access Resolution Flow)

Mô tả cách Analysis Service xác minh quyền sở hữu hoặc quyền khám chữa bệnh của người gọi đối với bệnh nhân trước khi cho phép tạo yêu cầu phân tích ảnh hoặc xem kết quả.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Bác sĩ hoặc Bệnh nhân
    participant GW as API Gateway
    participant AN as Analysis API (Port 5001)
    participant ID as Identity API (Port 5004)
    participant PR as Profile API (Port 5005)
    participant DB as PostgreSQL (profile schema)

    Client->>GW: POST /api/v1/analyses (Upload ảnh đáy mắt)<br/>Form: patientId, modality: FUNDUS, images...
    GW->>AN: Chuyển tiếp request phân tích
    activate AN

    %% 1. Phân giải danh tính
    AN->>ID: POST /internal/v1/identity/context<br/>Header: X-User-Authorization: Bearer <Token>
    activate ID
    ID-->>AN: HTTP 200 OK { accountId: "uuid-actor", roles: ["DOCTOR"] }
    deactivate ID

    %% 2. Phân giải quyền truy cập tại Profile Service
    AN->>PR: POST /internal/v1/profile/access/resolve<br/>{ actorAccountId, patientId, clinicContextId, action: "ANALYSIS_CREATE" }
    activate PR

    PR->>DB: Kiểm tra quyền sở hữu hoặc phân công lâm sàng
    Note over PR,DB: 1. Chính chủ: patient.owner_principal_id == actorAccountId?<br/>2. Phân công: doctor_assignments.status == ACTIVE?<br/>3. Phòng khám: memberships.role in [CLINIC_ADMIN, DOCTOR]?
    
    alt Hợp lệ (Chính chủ hoặc Bác sĩ được chỉ định)
        DB-->>PR: Quyền hợp lệ (Quan hệ lâm sàng hợp pháp)
        PR-->>AN: HTTP 200 OK<br/>{ allowed: true, tenantId: "uuid-tenant", tenantKind: "CLINIC", billingOwner: CLINIC }
        Note over AN: Tiếp tục luồng Reserve Credit & Lưu ảnh Private
        AN-->>GW: HTTP 202 Accepted { batchId, status: "QUEUED" }
        GW-->>Client: HTTP 202 Accepted
    else Không có quyền / Người lạ truy cập trái phép
        DB-->>PR: Không tìm thấy quan hệ hợp lệ
        PR-->>AN: HTTP 200 OK { allowed: false }
        Note over AN: Che giấu thông tin tài nguyên ngoài phạm vi
        AN-->>GW: HTTP 404 Not Found (ERR_NOT_FOUND: "Không tìm thấy bệnh nhân")
        GW-->>Client: HTTP 404 Not Found
    end
    deactivate PR
    deactivate AN
```
