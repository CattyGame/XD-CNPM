# AURA — Hệ thống sàng lọc sức khỏe mạch máu võng mạc

Mã đề tài: SP26SE025. Repository: XD-PMHDT.

## Công nghệ

- ASP.NET Core/.NET 10, YARP Gateway.
- React/TypeScript/Vite, Firebase Authentication và FCM.
- PostgreSQL 17, Redis 7.4, RabbitMQ 4.
- Azure Blob Storage; Azurite 3.33.0 cho local.
- Python/FastAPI cho AI Core.

## Bắt đầu

**Sprint 3:** dùng [bàn giao chung cho M1–M6](docs/integration/sprint3_team_handoff.md)
và [cấu hình Sprint 3](infra/sprint3/README.md) để nhận đúng revision, contract và
phạm vi kiểm thử. Publisher/Worker vẫn tắt; toàn luồng chưa được nghiệm thu.

Thực hiện [hướng dẫn môi trường Sprint 2](docs/development/sprint2_local_setup.md)
theo thứ tự: chuẩn bị `.env` và credentials, nạp cấu hình, chạy hạ tầng,
migration, đặt runtime passwords và khởi tạo private storage. Hướng dẫn có
trường hợp máy mới và database đang có dữ liệu.

Sau khi hoàn tất các bước bootstrap, tại thư mục gốc repository và trong
cùng cửa sổ PowerShell đã nạp cấu hình:

```powershell
docker compose -p aura --env-file .env `
    -f compose.yaml -f compose.dev.yaml `
    up -d --build
if ($LASTEXITCODE -ne 0) { throw "Chưa khởi động được ứng dụng." }

& ".\scripts\dev\Test-AuraDevEnvironment.ps1"
```

Mở PowerShell mới cần chạy lại `scripts/dev/Set-AuraDevEnvironment.ps1`.
Script này hỏi password PostgreSQL của năm runtime role; không tạo hoặc
đổi password trong database. Mỗi máy có database riêng có thể dùng password
riêng. Giữ nguyên khóa AES/key ID nếu database đã có token mã hóa.

## Địa chỉ local

| Thành phần | Địa chỉ |
| --- | --- |
| Web/Vite | http://localhost:5173 |
| Gateway | http://127.0.0.1:5000 |
| Analysis | http://127.0.0.1:5001 |
| Billing | http://127.0.0.1:5002 |
| Notification | http://127.0.0.1:5003 |
| Identity | http://127.0.0.1:5004 |
| Profile | http://127.0.0.1:5005 |
| AI Core | http://127.0.0.1:8000 |
| RabbitMQ UI | http://127.0.0.1:15672 |

Identity có Dockerfile và kết nối Profile trong Compose. Dev override mount
Firebase JSON read-only cho Gateway, Identity và Notification. Credentials
đặt ngoài repository; frontend dùng Firebase Web app config riêng.

## Tài liệu

- [Môi trường, migration và kiểm tra sau khởi động](docs/development/sprint2_local_setup.md).
- [ADR-004: private storage và quyền service](docs/adr/ADR-004-private-storage.md).
- [Gateway Sprint 2](docs/api/gateway_sprint2.md).
- [Database và quyền runtime](infra/postgres/README.md).
- [Private storage local](infra/storage/README.md).
- [Service identity](infra/service-auth/README.md).
- [Analysis OpenAPI](contracts/analysis-openapi.yaml).
- [AI OpenAPI](contracts/ai_service_openapi.yaml).
- [Notification contract](docs/api/notification_service_contract_v0.1.md).

## Phạm vi hiện tại

Repository có nền tảng hạ tầng, Gateway, Billing catalog PoC, Notification,
AI Core và các phần triển khai Sprint 2. Health thành công không chứng minh
toàn bộ nghiệp vụ, Firebase hoặc phân quyền đã hoàn tất. Xem giới hạn và
kết quả kiểm chứng trong tài liệu từng thành phần.

Kiểm tra cấu hình và migration control bằng credentials giả lập:

```powershell
& ".\tests\AURA.DevEnvironment.Checks\Run-Checks.ps1"
```

Kiểm tra này không thay thế việc chạy migration trên PostgreSQL, kiểm tra
Docker và xác thực Firebase thật trên môi trường local.
