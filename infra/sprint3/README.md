# Cấu hình bàn giao Sprint 3

Điểm bắt đầu: [hướng dẫn cho cả nhóm](../../docs/integration/sprint3_team_handoff.md).
Version và hash: [handoff.json](handoff.json). Trạng thái: code/contract review;
`integrationReady=false`, Publisher/Worker tắt. Chưa có kết quả A87.

| File | Ý nghĩa |
| --- | --- |
| `.env.example` | Catalogue public; copy `.env.local` chỉ trên môi trường mới |
| `appsettings.publisher.example.json` | Key .NET thật của Publisher; `Enabled=false`, cutoff chưa cấp |
| `compose.auth-ai.example.yaml` | Compose candidate chỉ Identity issuance + AI, profile `auth-ai-review`, mạng internal/không port host |
| `docker/` | Base Python/.NET ghim digest đã review; host SDK vẫn theo root `global.json` |
| `contracts/`, `examples/` | Target Sprint 3 từ gói contract1.0.0, có nullable risk; endpoint chưa triển khai phải để pending |
| `common.reference.yaml` | Catalogue thống nhất URL/timeout/retry/credit; không được tự động nạp làm appsettings |
| `rabbitmq.topology.reference.json` | Topology + regex quyền, không user/password, không script import |
| `CONTRACT_SPRINT3.md` | Quy tắc giao tiếp và nghiệp vụ đã chốt; không phải chứng nhận runtime |

Issuer `aura-identity-s3-v1`, RS256, TTL300s; AI audience `aura-ai`, scope `ai:analyze`,
client `analysis-worker`. Registry public tại
`../service-auth/clients.sprint3.example.json` chỉ chứa allowlist/principal, chưa có hash secret.
Không dùng file ví dụ làm runtime registry.

Setup tool cũ `tools/AURA.ServiceIdentity.Setup` hiện chỉ nhận issuer Sprint 2
`aura-identity-local`; không dùng tool đó trực tiếp cho registry Sprint 3 rồi tự sửa
issuer trong output. M1/M6 phải provision bộ mới vào thư mục ngoài repo bằng quy trình
được review, hoặc cấp đúng credential/public key cần dùng qua kênh riêng. Bộ đang dùng
ở M1 phải giữ nguyên. Profile/Billing scopes trong registry không tự chứng minh endpoint
đã triển khai. Worker không có `billing:capture`; settlement theo contract thuộc Billing.

## Validate, chưa start

Từ root repository, mọi thành viên chạy được kiểm tra contract không cần credentials:

```powershell
py -3.12 -m venv .venv-handoff
.\.venv-handoff\Scripts\python.exe -m pip install -r requirements-contract.txt
.\.venv-handoff\Scripts\python.exe scripts/sprint3/validate_handoff.py
```

Compose là candidate ghép mạng mới, chưa được runtime-smoke trên máy M1.
Chỉ sau khi M1/M6 cấp material cho một môi trường mới, kiểm quyền file cho UID1654
(issuer) và UID1001 (public key), đặt project name riêng và auth path ngoài repo:

```powershell
if (Test-Path infra/sprint3/.env.local) { throw 'Keep existing local configuration.' }
Copy-Item infra/sprint3/.env.example infra/sprint3/.env.local
# Điền AURA_S3_PROJECT và AURA_S3_AUTH_HOME trong .env.local bằng cấu hình đã cấp.
docker compose --env-file infra/sprint3/.env.local `
    -f infra/sprint3/compose.auth-ai.example.yaml --profile auth-ai-review config --quiet
```

`config --quiet` không pull/build/start/mount/issue JWT. Khi validate đạt, M1 xác nhận
ngân sách và namespace trên môi trường mới trước một plan startup riêng. Không chạy
`up`, `down`, migration, import definitions hoặc đổi network trên máy đang giữ Axx
chỉ từ tài liệu này. Shared Compose candidate chưa thay thế A77R/A81 đã kiểm chứng.
Identity `/health` trong profile này chỉ chứng minh startup/issuer, không chứng minh DB/Profile.

Các image ID AI75/Identity80R trong evidence là image build local, không phải registry
artifact đã push cho cả nhóm. Thành viên build từ source/nhận image đã phân phối cần ghi
commit + image ID mới và chạy smoke; không giả định build lại ra cùng ID.

Một broker riêng có thể dùng vhost `/aura-s3` và resource names trong topology reference.
Nếu dùng broker chung, M1 phải cấp vhost riêng và render/verify toàn bộ vhost/permissions
tương ứng. Không trỏ vào lab retained hay fixture08. Publisher không có quyền configure,
không tự declare/purge; cần `RabbitMq__User`, `RabbitMq__Password` và
`ConnectionStrings__OutboxPublisher` ngoài Git, cùng `Outbox__MinOccurredAtUtc`/tenant scope
đã cấp. Template giữ null cutoff để việc bật nhầm fail closed.

Tên biến `AURA_S3_*` là catalogue/template Compose; không tự biến thành key .NET.
M2 phải bind đúng `Outbox__*`, `RabbitMq__*`, `ConnectionStrings__OutboxPublisher`.
Chưa có executable Worker service trong Compose này; Worker limits/retry trong common là
contract cho M2 triển khai. URL Docker chỉ resolve trong đúng network, không phải endpoint
có thể truy cập từ máy thành viên khác.
