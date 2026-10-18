## Sprint 2 — Phân quyền PostgreSQL theo service

### Database và tài khoản

Database local: `aura_dev`.

| Service | Schema | Runtime role | Migration role |
| --- | --- | --- | --- |
| Identity | identity | aura_identity_app | aura_identity_migrator |
| Profile | profile | aura_profile_app | aura_profile_migrator |
| Analysis | analysis | aura_analysis_app | aura_analysis_migrator |
| Billing | billing, billing_poc | aura_billing_app | aura_billing_migrator |
| Notification | notification | aura_notification_app | aura_notification_migrator |

`billing_poc` được giữ lại để phục vụ Billing PoC hiện tại.

Schema `billing` dành cho triển khai nghiệp vụ tiếp theo.

### Quyền và nguyên tắc sử dụng

- Runtime role có CONNECT database, USAGE schema của service,

  SELECT/INSERT/UPDATE/DELETE trên bảng và USAGE/SELECT trên sequence.

- Runtime role không có quyền CREATE trong schema nghiệp vụ,

  không sở hữu bảng và không có quyền TRUNCATE.

- Runtime role không được truy cập schema của service khác.

- Migration role sở hữu schema và các bảng đã chuyển ownership.

- Migration role giữ NOLOGIN; người chạy migration được ủy quyền

  sử dụng SET LOCAL ROLE trong transaction.

- Không cấp SUPERUSER, CREATEDB, CREATEROLE hoặc BYPASSRLS

  cho runtime/migration role.

- Các API sử dụng runtime role, không dùng tài khoản quản trị aura_dev.

- Không thực hiện CREATE TABLE hoặc seed dữ liệu khi API khởi động.

### Migration của đủ năm service

Chạy từ gốc repository sau khi PostgreSQL healthy và đã nạp cấu hình:

```powershell
& ".\scripts\dev\Initialize-AuraDevDatabase.ps1" -PlanOnly
& ".\scripts\dev\Initialize-AuraDevDatabase.ps1"
```

Thứ tự: Notification → service roles → Identity/Profile → doctor assignments
→ Billing PoC → Analysis → service roles một lần nữa. Bước cuối chuyển
ownership và giữ quyền append-only. Script chạy bằng bootstrap admin aura_dev
trong database aura_dev, dừng khi một file lỗi; không xóa dữ liệu/volume.
Mỗi file có transaction riêng, các bước đã commit không tự rollback chung.

Máy mới cần đặt password và bật LOGIN cho năm role `_app`:

```powershell
& ".\scripts\dev\Initialize-AuraDevDatabase.ps1" -ConfigureRuntimeLogins
```

Tùy chọn tương tác dùng psql `\password` (nhập hai lần cho mỗi role), rồi
bật LOGIN. Bỏ tùy chọn để giữ password/LOGIN hiện có. Nhập cùng mật khẩu
khi chạy Set-AuraDevEnvironment.ps1; script môi trường chỉ nạp giá trị,
không thay đổi tài khoản trong PostgreSQL. Migrator luôn giữ NOLOGIN.

Các migration M6 có password mẫu nhưng trình tự này tạo role NOLOGIN trước,
nên không dựa vào password mẫu để bật tài khoản. Dữ liệu Billing đã tồn tại
được giữ bằng ON CONFLICT DO NOTHING. Backup trước khi đổi ownership/grants
trên database có dữ liệu; không coi IF NOT EXISTS là cơ chế nâng mọi schema cũ.

### Kết nối local

- Service chạy trên Windows: PostgreSQL tại `127.0.0.1:5432`

  theo cấu hình cổng local hiện tại.

- Service trong Compose: PostgreSQL tại `postgres:5432`.

- Mỗi service dùng runtime role và mật khẩu riêng.

- Không commit mật khẩu, connection string có mật khẩu hoặc file backup.

Identity/Profile/Analysis/Billing trong Compose dùng runtime role tương ứng.
Các biến IDENTITY_DB_PASSWORD, PROFILE_DB_PASSWORD, ANALYSIS_DB_PASSWORD,
BILLING_DB_PASSWORD được chèn trong connection string Npgsql có nháy kép.
Set-AuraDevEnvironment.ps1 escape nháy kép cho cả bốn giá trị.

Billing trong Compose dùng username `aura_billing_app`.

Biến `BILLING_DB_PASSWORD` cần được cung cấp khi Compose đọc

cấu hình hiện tại. Giá trị được chèn trong dấu nháy kép của

connection string Npgsql; dấu nháy kép bên trong mật khẩu

phải được nhân đôi trước khi truyền vào biến này.

Notification chạy trên Windows dùng:

- `NotificationDatabase__Database=aura_dev`

- `NotificationDatabase__Username=aura_notification_app`

- `NotificationDatabase__Password`: cung cấp riêng khi chạy.

Khóa mã hóa token Notification được giữ nguyên.

Thay tài khoản database không yêu cầu tạo lại khóa mã hóa.

### Kết quả kiểm tra local ngày 2026-10-15

- Cả 5 runtime role kết nối PostgreSQL qua TCP thành công.

- Kiểm thử bảng mới dưới migrator: runtime CRUD và sequence đạt.

- Runtime bị từ chối CREATE, ALTER, TRUNCATE và truy cập chéo

  schema trong các trường hợp đã kiểm thử.

- Transaction kiểm thử đã rollback.

- Billing đọc danh sách gói từ PostgreSQL bằng aura_billing_app.

- Dữ liệu gói Basic Updated được giữ nguyên sau migration.

- Notification readiness trả True; PostgreSQL ghi nhận kết nối

  bằng aura_notification_app.

Identity, Profile và Analysis đã có tài khoản database được

kiểm thử; việc chuyển cấu hình và kiểm chứng runtime của

các service này chưa được xác nhận trong bước bàn giao này.

Các kiểm tra trên xác nhận quyền database local, không thay thế

kiểm thử phân quyền tài khoản, tenant hoặc tài nguyên tại API.
## Bổ sung SCRUM-300 — Bootstrap và quyền Analysis (2026-10-16)

### Bootstrap database mới

Migration `002_service_database_roles.sql` luôn tạo schema
`billing_poc` nếu chưa tồn tại, với owner `aura_billing_migrator`.
Sau đó chạy `003_billing_poc_packages.sql` để tạo bảng và seed.

Không cần chạy Billing API bằng tài khoản quản trị để tạo bảng.
Seed không ghi đè các gói đã tồn tại.

### Ngoại lệ quyền của Analysis

Quyền CRUD chung không áp dụng cho mọi bảng Analysis.

| Bảng/phạm vi | Quyền aura_analysis_app |
| --- | --- |
| analysis_results | SELECT, INSERT |
| doctor_reviews | SELECT, INSERT |
| audit_logs | SELECT, INSERT |
| Bảng Analysis mới qua default privileges | SELECT, INSERT |
| Bảng nghiệp vụ cần cập nhật, ví dụ images | Migration service cấp UPDATE rõ ràng |

Runtime không có UPDATE, DELETE hoặc TRUNCATE trên ba bảng
append-only nêu trên.

Migration role 002 thu hồi UPDATE/DELETE trên các bảng append-only
đã tồn tại và điều chỉnh default privileges của
`aura_analysis_migrator` và `aura_dev`.

Migration Analysis cũng thu hồi cả UPDATE và DELETE trên ba bảng
append-only. Chạy lại migration không được mở lại các quyền này.

### Cách chạy migration Analysis hiện tại

File `analysis/001_analysis_core.sql` hiện vẫn chứa CREATE SCHEMA.
Với phiên bản này, chạy bằng tài khoản quản trị bootstrap;
không chạy trực tiếp bằng runtime role hoặc SET ROLE migrator.

Sau khi chạy migration Analysis, chạy lại migration role 002
để chuyển ownership các bảng về `aura_analysis_migrator`
và áp dụng lại chính sách quyền.

### Kiểm thử đã thực hiện

Trên PostgreSQL 17 tạm, không dùng volume database hiện tại:

- Bootstrap database mới và seed ba gói Billing: PASS.
- Quyền append-only sau migration Analysis: PASS.
- Chạy lại migration role rồi kiểm tra quyền: PASS.
- Chạy lại migration Analysis/Billing rồi kiểm tra quyền: PASS.
- Dữ liệu gói đã chỉnh sửa được giữ nguyên: PASS.
- Bảng mới do aura_analysis_migrator tạo chỉ có
  default SELECT/INSERT cho runtime: PASS.

Trên database local hiện tại:

- Đã backup và đọc được danh mục backup trước khi áp dụng.
- Migration role 002 đã COMMIT.
- Schema analysis/billing_poc có đúng owner migrator.
- Billing/Notification runtime role vẫn có LOGIN.
- Ba bảng Analysis append-only chưa tồn tại tại thời điểm kiểm tra;
  không ghi nhận kiểm thử quyền trên các bảng này ở database local.

### Cập nhật cấu hình runtime ngày 2026-10-18

Compose đã cấu hình kết nối runtime cho Identity/Profile/Analysis/Billing;
Notification nhận password riêng trong dev override. Analysis còn cần M2
nối repository/DI để thực sự dùng connection string. Việc có cấu hình không
xác nhận API nghiệp vụ hoặc quyền tenant đã được kiểm chứng.
