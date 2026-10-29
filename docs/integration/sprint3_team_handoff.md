# Bàn giao chung Sprint 3 — mã nguồn, contract, cấu hình

Ngày30/10/2026; base `develop`:
`aa1ef55a82758259311cd3a4f5a66b33884f7c0b`.
Nhánh review: `fix/sprint3-team-handoff-20261030`.
Mọi người ghi full SHA của nhánh lúc bắt đầu; chỉ merge về `develop` sau review và
test tương ứng. Đây là bàn giao để triển khai song song, chưa nghiệm thu tích hợp toàn luồng.

## 1. Nhận đúng revision trong checkout mới

Không pull/checkout vào `aura-s3-source67` hoặc checkout đang giữ evidence Axx của M1.
Thành viên chưa có checkout riêng có thể dùng:

```powershell
git clone --branch fix/sprint3-team-handoff-20261030 --single-branch `
    https://github.com/CattyGame/XD-PMHDT.git aura-sprint3-team
Set-Location aura-sprint3-team
git rev-parse HEAD
```

Với checkout cá nhân sạch đã có, fetch nhánh rồi tạo nhánh task từ revision review.
Không reset/clean thay cho xử lý thay đổi local. Đính kèm commit SHA vào mỗi báo cáo test.
Sau khi PR merge, nhóm chốt SHA trên `develop`; không dùng tên `develop` làm version cố định.

## 2. Ai bắt đầu phần nào

| Owner / Jira | Làm ngay từ bàn giao | Đầu vào còn chờ cho E2E |
| --- | --- | --- |
| M2 / SCRUM-323,324,325 | Review sửa scope/fencing/schema, native tests; Worker state machine, GET examples | PostgreSQL concurrency; broker confirm/crash; Billing M3; authorization M6 |
| M3 / SCRUM-326,327,328 | Billing idempotency/settlement; inbox/dedup với event mẫu; matrix đối soát | Consumer/broker + Worker thật và fixture mới |
| M4 / SCRUM-329,330,331 | AI Frangi/quality/dataset/benchmark, contract1.2, ít nhất3Fundus theo AC | Worker gọi AI và lưu output thật |
| M5 / SCRUM-332,333,334 | UI tiến trình/result/inbox, mock có nhãn, polling/refresh/401/403/404 | GET M2, inbox M3, quyền M6 và camera qua NiFi |
| M6 / SCRUM-335,336,337 | Membership/assignment/camera authority, contract và negative cases | Cross-service permission tests với fixture mới |
| M1 / SCRUM-317,318,321,322 | Namespace/broker/config/runtime, review fix và ngân sách; tiếp tục từ A87 | Các đầu vào trên cho demo và nghiệm thu |

Dependencies trên Jira là điều kiện tích hợp/nghiệm thu; code/contract/unit tests có thể
chuẩn bị song song. Khi handoff API examples, owner ghi rõ target/implemented/runtime-verified.
Không đổi task sang Done chỉ vì có PR hoặc health200.

## 3. Contract chốt để tránh lệch phiên bản

Catalogue + hash: [`infra/sprint3/handoff.json`](../../infra/sprint3/handoff.json).
Quy tắc: [CONTRACT_SPRINT3.md](../../infra/sprint3/CONTRACT_SPRINT3.md).

| Thành phần | Nguồn Sprint3 | Version / giới hạn |
| --- | --- | --- |
| Domain events | `contracts/analysis-events-0.7.schema.json` | 0.7; requested/completed/terminal failed; Publisher không phát reviewed |
| Job command | `contracts/analysis-job-0.6.schema.json` | 0.6; messageId/messageType; không đổi sang event envelope |
| Analysis REST target | `infra/sprint3/contracts/analysis-openapi-0.7.yaml` | 0.7.0; nullable risk cho Frangi; GET endpoints vẫn cần M2 nghiệm thu |
| AI | `contracts/ai-openapi-1.2.yaml` + request/response1.2; copy target trong infra | request/response snake_case, JWT service; response/geometry phải kiểm trước commit |
| Billing/internal/Notification target | `infra/sprint3/contracts/` | Endpoint target không được suy là đã triển khai |

Các file không có version `contracts/analysis-events.schema.json`, `analysis-job.schema.json`
và baseline W1/W2 được giữ nguyên cho bằng chứng cũ; không dùng nhầm cho stream Sprint3 mới.
Validator C# là profile Publisher cố định, thêm UUID lowercase/nonzero và Int32 dương;
không phải JSON Schema engine tổng quát. Consumer phải làm contract tests cho nhánh mình dùng.

Events/jobs không chứa ảnh/base64, object key, SAS URL, token hoặc secret. AI Frangi thật
hiện trả `risk_available=false`, `risk_assessment=null`, WARNING/RISK_UNAVAILABLE;
UI không dựng risk score hoặc coi WARNING là thất bại. Receipt/HTTP202 chỉ là tiếp nhận;
COMPLETED cần result đã lưu bởi Analysis. Billing thực hiện settlement từ authoritative
binding; Worker không tự capture hoặc sửa trực tiếp DB service khác.

## 4. Bản sửa Publisher A84/84V

- Sweep/Backlog dùng cùng cutoff/excluded-tenant scope với Claim.
- `claim_generation` tăng mỗi claim, không refund/reset khi release attempt.
- Renew/Published/Retry/Attention/Release kiểm generation, lease và server clock;
  affected rows quyết định counters. Mất ownership thì không publish hoặc mark thành công giả.
- Payload required/exact fields, duplicate properties, enum/risk/terminal branches được kiểm.
- Mặc định Publisher tắt; template cutoff null phải được M1 cấp trước khi bật.

Migration `003_outbox_claim_generation.sql` là ứng viên: chưa được áp vào runtime cũ.
Nó cần001 core và002 publisher, phải cấp UPDATE claim_generation cho role Publisher.
Không chạy old/new Publisher đồng thời trên cùng outbox. Không chạy
`infra/postgres/migrations/analysis/tests/outbox_sql_test.py` trên DB đang giữ dữ liệu:
script cũ có thao tác DROP DATABASE. Harness mới cần hai backend PID/handshake và journal riêng.

Đã có evidence native84 được84V đọc lại:32/32pass =18OutboxPublisher +12OutboxRepair84
+2Upload, không skip. TRX SHA256:
`32c112323b9eb06d512ac9fee05d8b478bac9f943cee0865971608a08d2d939e`.
Journal84 vẫn RESTORED84 do gate đếm nhầm; không sửa journal để ghi COMPLETED.
Source C# sửa trong nhánh này có hash trùng overlay đã compile; kết quả đó không chứng nhận
toàn bộ test suite/CI trên revision mới, SQL concurrency hoặc RabbitMQ runtime.

## 5. Các kiểm tra mọi thành viên chạy được ngay

Bản bàn giao đã được chạy lại trên checkout Linux riêng với SDK10.0.401:
**toàn bộ Analysis test project318/318pass,0failed,0skipped**, gồm30Outbox tests.
Contract check mới đạt8schema/5OpenAPI/9examples/12ca âm;30file legacy giữ nguyên hash.
Các warning xUnit style còn tồn tại; không có compile error. Đây là kiểm thử source/native,
không thay thế runtime PostgreSQL/broker hoặc kết quả A87 trên máy M1.

Người dùng cũng đã chạy toàn bộ project trên checkout Windows bàn giao mới:
318/318pass,0failed,0skipped; build21.2s, test10.3s,24warning xUnit2009/xUnit2013.
Kết quả này lấy từ output thực chạy người dùng cung cấp; chưa nhận hash TRX Windows.

SDK host theo `global.json`10.0.401/latestPatch; Python3.12. Không cần credentials hoặc Docker:

```powershell
py -3.12 -m venv .venv-handoff
.\.venv-handoff\Scripts\python.exe -m pip install -r requirements-contract.txt
.\.venv-handoff\Scripts\python.exe scripts/sprint3/validate_handoff.py

dotnet test src/services/AURA.Analysis.Api.Tests/AURA.Analysis.Api.Tests.csproj `
    --configuration Release `
    --filter 'FullyQualifiedName~OutboxPublisherTests|FullyQualifiedName~OutboxRepair84Tests'
if ($LASTEXITCODE -ne 0) { throw 'Publisher native tests failed.' }
```

Filter trên chọn30Outbox tests; filter cũ `FullyQualifiedName~Outbox` từng chọn thêm2Upload.
Kiểm cả project bằng cách bỏ filter. Restore/test trên checkout mới không phải chạy lại
runner84/84V của M1. Frontend/AI dùng lệnh tests theo project tương ứng và cùng commit.

`scripts/validate_analysis_contract.py` là kiểm baseline W1/W2 cũ. Tại base aa1ef55,
chạy script đó báo hash mismatch `contracts/analysis-openapi.yaml`; giới hạn có sẵn này
được ghi nhận, không sửa baseline hash chỉ để làm xanh. Validator Sprint3 mới kiểm target
versioned contracts/examples độc lập và giữ checksum tài liệu legacy nguyên trạng.

## 6. Cấu hình và secret

Theo [README cấu hình](../../infra/sprint3/README.md), bắt đầu bằng validation-only.
`compose.auth-ai.example.yaml` là candidate mới chỉ cho Identity issuance + AI, không
Worker/Publisher/business DB/web. Các Dockerfile pin base đã review và non-root; build lại
trên máy khác phải ghi image ID mới, không giả định cùng image local75/80R.

| Material | Cấp cho |
| --- | --- |
| Issuer private key + registry runtime | Chỉ Identity |
| Public signing key | AI và các verifier cần xác minh JWT |
| Credential analysis-worker | Chỉ Worker; client identity không thay thế quyền Patient/tenant |
| DB/Rabbit publisher credentials | Chỉ Publisher; role/vhost/scope đã cấp |
| Firebase JSON, encryption key | Owner service liên quan qua kênh riêng |

Git chỉ chứa template/allowlist. Không gửi nguyên thư mục auth/Downloads, raw inspect/env,
JWT, password, PEM private, SAS hoặc TRX/log chưa lọc. Endpoint localhost chỉ thuộc máy
đang chạy; Docker DNS chỉ thuộc network đó. Hiện chưa có endpoint chung được nghiệm thu.
Mỗi môi trường mới có namespace và fixture mới; không refill ví/consume outbox của fixture08.

## 7. Trạng thái thật tại thời điểm bàn giao

| Evidence | Đã chứng minh | Chưa chứng minh |
| --- | --- | --- |
| A77R/A76 | AI non-root, health/ready, fail-closed auth/config | Worker pipeline |
| A80R/A81 | Identity image Production; một JWT thật được AI verify, tamper401, empty body422 | Business DB/Profile readiness toàn bộ |
| A82 | Một phantom synthetic192×160 inferenceHTTP200, non-mock, mask/overlay/density nhất quán | Ba ảnh Fundus, clinical accuracy, Worker/storage/credit/UI |
| A84V |32 native tests đúng danh tính, API/adapters compiled | PG locking/concurrency, broker/crash thật |
| A85/A86 | CachePG17.11 digest/platform/config/rootfs và capacity snapshot | PostgreSQL sandbox runtime |
| A87 | Gói đã bàn giao để chạy | Chưa nhận kết quả; không đánh PASS |

Sau A87: kiểm migration/fencing hai session PostgreSQL, broker integration và Npgsql I/O;
sau đó mới nối Worker, private storage, Billing, permissions, Notification và UI/camera.
M1 cập nhật evidence theo revision và môi trường thực chạy; không thay journal/evidence cũ.
