# AURA — Môi trường phát triển Sprint 2

Owner: M1. Tasks: SCRUM-299/300/301. Cập nhật: 2026-10-23.

## Phạm vi hiện tại

`compose.yaml` có Gateway, Analysis, Billing, Identity, Profile, AI Core,
PostgreSQL, Redis, RabbitMQ và Azurite. `compose.dev.yaml` thêm Notification
và mount Firebase credentials cho Gateway, Identity, Notification.

Identity gọi Profile bằng `http://profile-api:8080`. Gateway dùng DNS Docker
cho các dịch vụ trong Compose. Việc có container/route không xác nhận mọi
API nghiệp vụ hoặc cơ chế phân quyền của M2/M3/M6 đã hoàn chỉnh.

## Cổng và công cụ

| Thành phần | Cổng host local | Địa chỉ Docker |
| --- | --- | --- |
| Web/Vite | 5173 | Chạy trên Windows |
| Gateway | 5000 | api-gateway:8080 |
| Analysis | 5001 | analysis-api:8080 |
| Billing | 5002 | billing-api:8080 |
| Notification | 5003 | notification-api:8080 |
| Identity | 5004 | identity-api:8080 |
| Profile | 5005 | profile-api:8080 |
| AI Core | 8000 | ai-core:8000 |
| PostgreSQL | 5432 | postgres:5432 |
| Redis | 6379 | redis:6379 |
| RabbitMQ | 5672; UI 15672 | rabbitmq:5672 |
| Azurite Blob | 10000 | azurite:10000 |

Cài Docker Desktop Linux containers, .NET SDK theo `global.json`, Node.js
theo `src/web/aura-web/.nvmrc`. Các script chạy từ thư mục gốc repository;
PowerShell 5.1 hoặc PowerShell 7. Cổng host chỉ bind loopback.

## 1. Chuẩn bị cấu hình và credentials

Không ghi đè `.env` đã có:

```powershell
if (-not (Test-Path -LiteralPath ".env")) {
    Copy-Item ".env.example" ".env"
}
```

Điền mật khẩu PostgreSQL bootstrap và RabbitMQ trong `.env` local.
Trình tự migration hiện dành cho `POSTGRES_DB=aura_dev` và
`POSTGRES_USER=aura_dev`; không đổi tên khi dùng bộ bootstrap này.

Chuẩn bị ngoài repo:

- `%USERPROFILE%\.aura\credentials\aura-dev-service-account.json`: service
  account của Firebase project `aura-dev-97716`.
- `%USERPROFILE%\.aura\credentials\notification-token-key-v1.txt`: Base64
  của khóa AES 32 byte. Database chứa token cũ phải giữ đúng khóa và key ID.
  Chỉ tạo khóa riêng khi database hoàn toàn mới.

Với database mới và chưa có file AES, có thể tạo một lần:

```powershell
$keyPath = Join-Path $env:USERPROFILE ".aura\credentials\notification-token-key-v1.txt"
if (-not (Test-Path -LiteralPath $keyPath)) {
    New-Item -ItemType Directory -Path (Split-Path $keyPath) -Force | Out-Null
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
        [IO.File]::WriteAllText($keyPath, [Convert]::ToBase64String($bytes))
    }
    finally {
        $rng.Dispose()
        [Array]::Clear($bytes, 0, $bytes.Length)
    }
}
```

Chuẩn bị bộ service credentials **trước** khi nạp môi trường. Máy mới dùng
công cụ trong [hướng dẫn service identity](../../infra/service-auth/README.md)
để tạo một bộ ngoài repository. Bộ hiện tại có bốn client: `analysis-api`,
`identity-api`, `billing-api`, `profile-api`. Máy đã có bộ cũ chỉ bổ sung
Profile theo hướng dẫn; không tạo lại key hoặc ghi đè credentials đang dùng.

`Set-AuraDevEnvironment.ps1` đọc secret riêng của Identity, Billing và Profile;
Analysis chưa được nối DI/adapter nghiệp vụ tại baseline này. Không dùng
secret mặc định trong script kiểm thử cũ thay cho secret của môi trường.

Nạp cấu hình vào cửa sổ PowerShell hiện tại:

```powershell
& ".\scripts\dev\Set-AuraDevEnvironment.ps1"
```

Script hỏi password của **cả năm** runtime role Identity, Profile, Analysis,
Billing, Notification. Đây là mật khẩu PostgreSQL đã đặt hoặc dự định đặt
ở bước 3, không phải mật khẩu Google. Script không tạo/đổi password và không
xác nhận kết nối database. Bốn biến chèn vào connection string Npgsql được
escape dấu nháy kép; Notification dùng trường password riêng nên giữ nguyên.

Máy cũ có network/key ID khác thì giữ cấu hình thực tế, ví dụ:

```powershell
& ".\scripts\dev\Set-AuraDevEnvironment.ps1" `
    -DockerNetwork "aura_aura_network" `
    -NotificationKeyId "local-v1"
```

Giữ cửa sổ này cho Compose. Mở cửa sổ mới phải nạp lại.
Không chia sẻ output `docker compose config` đầy đủ vì có secrets;
dùng `config -q` để chỉ kiểm tra hợp lệ.

## 2. Khởi động hạ tầng

```powershell
docker volume create aura_azurite_data
if ($LASTEXITCODE -ne 0) { throw "Chưa tạo/kiểm tra được volume storage." }

docker compose -p aura --env-file .env `
    -f compose.yaml -f compose.dev.yaml config -q
if ($LASTEXITCODE -ne 0) { throw "Cấu hình Compose chưa hợp lệ." }

docker compose -p aura --env-file .env `
    -f compose.yaml -f compose.dev.yaml `
    up -d --wait postgres redis rabbitmq azurite
if ($LASTEXITCODE -ne 0) { throw "Hạ tầng chưa khởi động thành công." }
```

Network mặc định máy mới là `aura_network`. Không xóa volume để xử lý lỗi,
không dùng `down -v` hoặc `--remove-orphans` khi chưa xác định dữ liệu/container.

## 3. Migration và runtime logins

Xem kế hoạch trước khi chạy:

```powershell
& ".\scripts\dev\Initialize-AuraDevDatabase.ps1" -PlanOnly
```

Script thực hiện đúng thứ tự:

1. `001_notification_devices.sql`.
2. `002_service_database_roles.sql`.
3. `002_identity_and_profile.sql`.
4. `003_doctor_assignments.sql`.
5. `003_billing_poc_packages.sql`.
6. `004_billing_business_schema.sql`.
7. `analysis/001_analysis_core.sql`.
8. `analysis/002_upload_recovery.sql` (durable upload journal).
9. Chạy lại `002_service_database_roles.sql` để chuyển ownership và giữ quyền
   append-only cho các bảng Analysis.

Trên máy mới, chạy trong terminal tương tác:

```powershell
& ".\scripts\dev\Initialize-AuraDevDatabase.ps1" -ConfigureRuntimeLogins
```

Sau migration, script gọi `psql \password` cho từng role (nhập hai lần), rồi
bật LOGIN. Nhập đúng password đã cung cấp cho script môi trường. Password
không nằm trong file SQL hay command line; migrator tiếp tục NOLOGIN.

Trên database đã có role/password đúng, bỏ `-ConfigureRuntimeLogins`:

```powershell
& ".\scripts\dev\Initialize-AuraDevDatabase.ps1"
```

Mặc định script không đổi password/LOGIN hiện có. Mỗi migration chạy với
`ON_ERROR_STOP=1`; lỗi thì dừng, các migration trước đã commit vẫn được giữ.
Không có transaction chung cho toàn bộ chín bước. Backup database đã có dữ
liệu trước khi áp dụng thay đổi ownership/grants; không coi IF NOT EXISTS
là bằng chứng tự nâng cấp được mọi schema cũ.

| Service | Runtime role | Migration role |
| --- | --- | --- |
| Identity | aura_identity_app | aura_identity_migrator |
| Profile | aura_profile_app | aura_profile_migrator |
| Analysis | aura_analysis_app | aura_analysis_migrator |
| Billing | aura_billing_app | aura_billing_migrator |
| Notification | aura_notification_app | aura_notification_migrator |

Mỗi máy có database riêng có thể dùng password riêng. Xem
[ma trận quyền database](../../infra/postgres/README.md).

## 4. Khởi tạo private storage

Dùng [hướng dẫn storage](../../infra/storage/README.md) và hai script
`initialize_local_storage.py`, `verify_local_storage.py`.
Nếu có Python trên host, chạy:

```powershell
$env:AURA_STORAGE_TEST_ENDPOINT = "http://127.0.0.1:10000"
python .\scripts\storage\initialize_local_storage.py
if ($LASTEXITCODE -ne 0) { throw "Khởi tạo storage chưa đạt." }
python .\scripts\storage\verify_local_storage.py
if ($LASTEXITCODE -ne 0) { throw "Kiểm thử storage chưa đạt." }
```

Endpoint của script không thêm `/devstoreaccount1`. Script giữ container cũ,
xác nhận anonymous access đã tắt bằng kiểm tra ACL và dọn riêng blob kiểm thử.
Khóa emulator trong file example chỉ dùng local.

## 5. Build và chạy ứng dụng

```powershell
docker compose -p aura --env-file .env `
    -f compose.yaml -f compose.dev.yaml `
    up -d --build
if ($LASTEXITCODE -ne 0) { throw "Chưa khởi động được ứng dụng." }

docker compose -p aura --env-file .env `
    -f compose.yaml -f compose.dev.yaml ps
```

Identity có Dockerfile và service Compose; dev override mount JSON read-only
và đặt Firebase project đúng với Gateway/Notification. Profile DB password
được lấy từ môi trường, không còn cố định trong Compose. Analysis nhận sẵn
connection string runtime nhưng repository/DI nghiệp vụ vẫn cần M2 sử dụng.

## 6. Kiểm tra sau khởi động

```powershell
& ".\scripts\dev\Test-AuraDevEnvironment.ps1"
```

Kiểm tra health của các API, Notification readiness, Gateway ping/catalog,
correlation ID, CORS hai origin, từ chối request thiếu token và không mở
internal token endpoint qua Gateway.

Tùy chọn kiểm tra Identity → Profile → PostgreSQL bằng token Development:

```powershell
& ".\scripts\dev\Test-AuraDevEnvironment.ps1" -CheckDevelopmentBootstrap
```

Tùy chọn này tạo một account/profile tổng hợp có UID bắt đầu `m1-check-`;
không dùng trên production. Chỉ chứng minh wiring và provisioning local,
không chứng minh Firebase thật hay phân quyền Profile an toàn.

Kiểm tra token người dùng Firebase thật, nhập ID token mới ở prompt ẩn:

```powershell
& ".\scripts\dev\Test-AuraDevEnvironment.ps1" -CheckFirebase
```

Script không lấy ID token từ private key; cần đăng nhập tài khoản thử bằng
Firebase Web SDK để có ID token. Không in token hoặc nội dung hồ sơ.
Kiểm tra đi qua Gateway tới Notification và Identity; trạng thái Identity
200/403/409 được chấp nhận sau xác thực. Chưa bootstrap hoặc account nội bộ
bị khóa không phải lỗi wiring. Việc 403 do account khóa cần M6 kiểm riêng.

## 7. Chạy frontend

Tạo `src/web/aura-web/.env.local` theo `.env.example` của frontend, dùng Web
app config thuộc `aura-dev-97716`; không dùng service account JSON làm Web config.
Đặt `VITE_ENABLE_MOCK=false`, `VITE_API_BASE_URL=` khi dùng Vite proxy.

Trong cửa sổ khác:

```powershell
Set-Location "src/web/aura-web"
npm ci
if ($LASTEXITCODE -ne 0) { throw "Cài frontend chưa đạt." }
npm run dev -- --host localhost --port 5173 --strictPort
```

## 8. Service JWT và ranh giới bàn giao

M1 đã chuẩn bị registry và công cụ sinh key/credentials; xem
[hướng dẫn service identity](../../infra/service-auth/README.md).
Mỗi môi trường độc lập có thể tự sinh một bộ; issuer/caller/consumer trong
cùng môi trường phải sử dụng đúng cùng bộ. Không sinh lại khóa đang dùng.

Identity đã đọc registry, phát service JWT và cung cấp identity context.
Profile đã dùng client riêng để giải quyết danh tính người dùng qua Identity;
Billing đã có consumer service JWT và gọi Profile để kiểm quyền reserve.
Xem [luồng Profile identity](profile_identity_resolution.md).

Nếu chỉ bổ sung Profile vào bộ credentials cũ, Identity cần restart để đọc
registry; Profile cần nhận `PROFILE_SERVICE_CLIENT_SECRET` và được recreate.
Các bước này đã được kiểm tra trên máy bàn giao ngày 2026-10-23; không cần
đăng ký lại client hoặc đổi khóa chỉ vì cập nhật tài liệu.

Vẫn cần sửa các fallback khi cấu hình service auth bị thiếu/sai và kiểm thử
các nhánh đó trong M6. Không coi việc health hoặc login thành công là bằng
chứng toàn bộ quyền service/user đã an toàn.

Tại baseline `87af21d`, Analysis `Program.cs` mới map health/platform ping;
upload, batch status, file access và các adapter PostgreSQL/Billing/Azurite
chưa được nối vào runtime. Upload/DB/storage adapter thuộc M2, Billing thuộc
M3, bootstrap/Profile UI và retry thuộc M5. Worker/publisher đầy đủ thuộc
Sprint 3. Xem [kế hoạch sửa đủ 18 task](../integration/sprint2_closeout_plan.md).

## 9. Kiểm thử và bằng chứng

- `tests/AURA.DevEnvironment.Checks/Run-Checks.ps1`: kiểm hành vi nạp cấu hình,
  password escaping, validation, migration plan và dừng khi lệnh lỗi;
  dùng credentials giả lập và Docker mock, không chạm database thật.
- CI chạy thêm Analysis xUnit, script checks, config cả hai Compose, migration
  hai lượt trên PostgreSQL sạch và wiring Identity/Profile/Billing. Đây là
  định nghĩa pipeline; chỉ ghi PASS khi run CI thực tế đã thành công.
- Kiểm thử Firebase thật/SAS/cloud RBAC và toàn luồng nghiệp vụ cần môi trường
  phù hợp. Health/build/CI mock không thay thế các kết quả đó.

Các kết quả local ngày 15–16/10 trong tài liệu storage/database/Gateway là
bằng chứng lịch sử của phiên bản lúc đó, không tự xác nhận bản sửa mới.

## Tài liệu liên quan

- [Kế hoạch sửa Sprint 2](../integration/sprint2_closeout_plan.md)
- [Gateway](../api/gateway_sprint2.md)
- [ADR private storage](../adr/ADR-004-private-storage.md)
- [Database](../../infra/postgres/README.md)
- [Storage](../../infra/storage/README.md)
- [Service identity](../../infra/service-auth/README.md)
