# Chốt contract và cấu hình Sprint 3

**Baseline 1.0.0 — 27/10/2026.** Mục tiêu Sprint 3: hoàn thiện NiFi của M1, rồi nối một lượt upload mới qua Analysis → Worker → Frangi thật → kết quả GET/UI → settlement và inbox. Phạm vi demo là TEST; không dùng dữ liệu bệnh nhân thật, không phát sinh thanh toán LIVE.

## 1. Quyền sở hữu và cách làm chung

| Thành viên | Phải thực hiện | Đầu ra phải bàn giao |
|---|---|---|
| M1 Nguyễn Trương Hậu | Namespace, broker, cấu hình chung, NiFi safe writer, exhaustion/HOLD, promotion vào Delivery, CI và demo tích hợp | Hạ tầng mới có image lock; NiFi đúng timeout và route; test recovery/exhaustion/HOLD; export flow đã loại secret; runbook và evidence |
| M2 Võ Hữu Duy | Outbox publisher, Worker, lease/retry, GET; hai resolver nội bộ | Transaction/outbox/inbox bền vững; mapping AI đúng; GET đúng quyền; test duplicate, crash, stale generation |
| M3 Thanh Dat | Billing reservation/settlement, Notification inbox, QA consistency | Một reserve/capture hoặc release; consumer idempotent; inbox đúng người; ma trận fault có bằng chứng |
| M4 Đào Duy Quân | Frangi thật, chất lượng ảnh, AI API/JWT, dataset và benchmark | API 1.2 đúng schema; PNG thật đúng geometry; phiên bản thuật toán và test; không trả risk giả |
| M5 Lâm Trần Nguyễn Hoàng | UI tiến độ, kết quả, file access, inbox | Upload một lần; polling; mask/overlay thật; hiển thị chưa có risk; phân biệt AI hoàn tất với credit/notification đang đồng bộ |
| M6 Thành viên mới | Identity/Profile, membership, Doctor assignment, camera delegation, quyền hiện hành | JWT và public key mới; service registry; quyết định truy cập từ backend; test thu hồi quyền và chéo tenant |

M1 sở hữu **toàn bộ công việc NiFi**. M2–M6 hỗ trợ qua contract, không sửa flow NiFi độc lập. Mỗi PR ghi task W3-Mx, contract version, migration/config thay đổi và phép kiểm. Không đánh Done bằng ảnh cấu hình hoặc trạng thái Running đơn thuần.

Thứ tự tích hợp: M1 namespace + M6 identity → M2 publisher/Worker + M4 AI → M3 settlement/inbox → M5 UI → M1/M3 demo/fault. M1 ưu tiên exhaustion/HOLD và safe-writer promotion trước tuyên bố NiFi hoàn tất. Các phần độc lập được làm song song; ngày thực hiện tính từ kickoff mới, không tự sửa lịch Sprint cũ trên Jira.

## 2. Namespace và định danh

| Mục | Giá trị duy nhất |
|---|---|
| Compose project / prefix tài nguyên | `aura-s3` |
| Private workspace mới | `~/.aura/sprint3/aura-s3-v1` |
| Network backend | `aura-s3-backend`, internal |
| Network edge | `aura-s3-edge` |
| RabbitMQ vhost | `/aura-s3` |
| Redis key prefix | `aura:s3:` |
| Gateway / frontend | `http://127.0.0.1:5300` / `http://127.0.0.1:5380` |
| NiFi UI / camera TLS mới | `https://127.0.0.1:8543/nifi/` / `https://127.0.0.1:8545` |
| Rabbit management | `http://127.0.0.1:15372` |
| Blob endpoint cho trình duyệt | `http://127.0.0.1:11000/devstoreaccount1` |

Compose dùng tên tự sinh `aura-s3-<service>-1`, không đặt `container_name`. PostgreSQL và AMQP không publish host port. Dịch vụ dùng DNS trong `config/common.yaml`; trình duyệt dùng host origin ở bảng trên. Không gửi URL `http://azurite:10000/...` cho trình duyệt.

Giữ tên database `aura_dev`, `aura_reporting` và Blob container `aura-originals`, `aura-annotations`, `aura-reports` **bên trong instance mới** để tương thích source. Tính tách biệt nằm ở server, volume, network, key và credential mới. Không đổi tên originals vì source Analysis đang kiểm tên `aura-originals`.

`batchId` là AnalysisRequest.id; `analysisId` là Image.id; `jobId` là Job của một attempt. Account, Patient, tenant và service principal là các định danh khác nhau. Mỗi lượt transport retry giữ ID; business attempt mới có jobId/messageId mới, giữ analysisId. Không dùng ID hoặc ảnh của fixture cũ trong demo mới.

## 3. Phiên bản và quy tắc event

| Contract | Chốt Sprint 3 | Tương thích |
|---|---|---|
| Domain event | `schemaVersion: "0.7"` | Mới, có riskAvailable và terminalStatus; không phát vào consumer 0.6 |
| Job command | `schemaVersion: "0.6"` | Giữ wire shape cũ; áp dụng giới hạn tối đa 3 attempt ở runtime |
| Public Analysis REST | `0.7.0` | Bổ sung risk nullable/riskAvailable |
| AI API | `1.2.0` | Bổ sung risk_available và risk_assessment nullable |
| Billing REST | `0.1.0` | Giữ request/response đang có |
| Reporting receipt | `receipt-v1` / writer `receipt-jdbc-v1` | Giữ stored procedure và idempotency đã kiểm chứng |

Domain event bắt buộc có `eventId,eventType,schemaVersion,occurredAt,tenantId,correlationId,analysisId,data`. Job command dùng `messageId,messageType`, **không đổi thành eventId/eventType**. UUID dùng chuỗi canonical; time RFC3339 UTC kết thúc `Z`; correlationId 1–64 ký tự `[A-Za-z0-9._-]`. Không nhúng Base64, object key, SAS, password, token hoặc thông tin Patient không cần thiết vào message/log.

| Routing key/eventType | Nội dung data | Ai dùng |
|---|---|---|
| `analysis.requested` | patientId, batchId | Audit; không kích hoạt xử lý ảnh lần hai |
| `analysis.job.run` | Command chứa jobId, analysisId, batchId, imageFileId, attempt | Worker |
| `analysis.completed` | patientId, riskAvailable, riskLevel nullable, modelVersion, isMock, configVersion, thresholdVersion, resultVersion | Billing, Notification |
| `analysis.failed` | patientId, errorCode, retryable=false, terminalStatus | Billing, Notification; chỉ phát khi kết thúc |
| `analysis.reviewed` | Shape giữ trong schema để tương thích thiết kế | Ngoài demo Sprint 3; không phát nếu review chưa được triển khai |

Analysis ghi dữ liệu nghiệp vụ và outbox trong **cùng transaction**. Event completed chỉ xuất hiện sau khi result hợp lệ và FileRef đã được ghi bền vững. Lỗi tạm thời chưa hết retry không phát terminal failed. Không dùng `analysis.requested` thay cho job command.

Outbox có unique event/message ID, available_at, attempt_count, published_at, lỗi đã loại dữ liệu nhạy cảm. Publisher claim bằng lease, gửi persistent/mandatory, chờ confirm tối đa 5s; chỉ đánh published khi **được confirm và không bị basic.return**. Timeout chưa rõ kết quả cho phép gửi lại cùng ID. Thử publish tối đa 10 lần, sau đó giữ `NEEDS_ATTENTION`, báo M1/M2; không xóa record.

## 4. RabbitMQ và Worker

`infra/rabbitmq.topology.template.json` là topology chính xác. Exchange chính `aura.s3.jobs` direct và `aura.s3.events` topic; queue `aura.s3.analysis.jobs`, `aura.s3.billing.events`, `aura.s3.notification.events`, `aura.s3.analysis.audit`. Mỗi consumer có retry exchange/queue và dead exchange/queue riêng; không đẩy retry của Billing trở lại events chung làm Notification chạy lại.

Queue durable, quorum, initial group size 1; **đây là cấu hình demo một node, chưa có HA**. Retry queues TTL 5000ms, dead-letter tới `aura.s3.retry.return`; policy dùng at-least-once dead-lettering và reject-publish. M1 kiểm feature flag `stream_queue` và policy thực tế trước acceptance. Queue giữ tối đa 10.000 message hoặc 64MiB rồi từ chối publish; không drop-head. Dead queue không TTL, không tự replay. Bật consumer audit để queue requested được quản lý.

AMQP message: content_type application/json, delivery_mode=2, message_id=eventId/messageId, type=eventType/messageType. Body ≤65536 byte; prefetch1, ACK tay. Producer/consumer không tự declare hoặc purge topology bằng quyền admin. Tài khoản và permission tạo riêng, app không có configure quyền; M1 kiểm cả negative permission test.

**Hai retry khác nhau:**

1. Retry nghiệp vụ gọi AI: tối đa 3 logical attempt, chờ 5s rồi 15s. Mỗi attempt một Job và messageId mới; giữ analysisId và reservation generation. Delay bằng outbox available_at, không sleep giữ unacked message dài hạn. Worker phải ghi durable AI-call intent trước HTTP; attempt đã bắt đầu nhưng mất kết quả vẫn tiêu hao budget. Không cam kết AI compute exactly-once.
2. Retry vận chuyển/consumer dependency: cùng messageId/eventId, header `x-aura-transport-retry-count` 0→1→2; delay 5s, tối đa 3 lần delivery theo đường ứng dụng. Republish vào retry riêng, chờ confirm rồi ACK bản cũ; sau budget đưa dead riêng có confirm. Broker poison delivery-limit3 là hàng rào bổ sung cho crash/requeue, không phải bộ đếm nghiệp vụ. Không dùng vô hạn nack(requeue=true).

Worker kiểm liên kết tenant/job/image/attempt trong DB, chỉ xử lý generation và lease hiện hành; recheck Profile trước lấy ảnh và trước commit. Lease90s, heartbeat15s; concurrency1. Kết quả tới sau khi mất lease không được commit. Duplicate job đã terminal: kiểm durable state rồi ACK, không gọi AI/capture lại. ACK sau transaction result/terminal/retry schedule đã commit. Nếu publish retry/dead chưa confirm, không ACK rồi làm mất message.

Lỗi AI timeout, 503 hoặc network được retry trong budget. Lỗi credential/scope, ảnh thiếu, input sai, output schema/geometry sai đi terminal hoặc HOLD để điều tra theo bản chất; không che lỗi quyền bằng retry vô hạn. LOW_QUALITY là terminal không có result. Sau attempt3 không có kết quả: FAILED + terminal event; không tự tạo attempt4. Dead-letter chưa chắc là terminal nghiệp vụ: reconciliation phải kiểm trạng thái và lease trước quyết định credit.

## 5. AI Frangi thật

M4 triển khai `POST /api/v1/analyze`, nhận **snake_case** đúng schema 1.2. request_id=analysisId; patient_id=null; mode=retina_vessels; modality=FUNDUS; eye_side left/right/unknown; image_base64 là Base64 trần, không data URL. Worker gọi JWT audience aura-ai, scope ai:analyze. AI xác minh signature/issuer/audience/expiry/scope; network nội bộ không thay thế xác thực.

Giới hạn ảnh 10MiB, 16 triệu pixel; public upload PNG/JPEG theo schema Analysis, không tự mở rộng WEBP chỉ vì AI có thể decode. Timeout Worker30s; response AI tối đa64MiB. Decode giới hạn trước khi cấp phát ảnh lớn. Bắt buộc mask+overlay trong demo mới; không lưu Base64 vào event/result public.

Thuật toán chốt theo baseline đã commit `frangi_baseline_v0.2`: green/255; Gaussian sigma1; estimated region max(R,G,B)>20, fill holes, erosion 3×3 lặp12; Frangi sigma[1,2,3,5,8], alpha=.5,beta=.5,gamma=None,black_ridges=true,mode=reflect; ngoài region=0; normalize theo max response từng ảnh; threshold .02. Response toàn0 cho mask toàn0. Không crop/resize 512 rồi gọi đó là cùng baseline; geometry giữ ảnh gốc.

Mask PNG L nhị phân0/255; overlay PNG RGB hoặc RGBA; cả hai đúng width/height gốc. `vessel_density` = số pixel mask foreground / (width×height ảnh gốc), không phải chỉ số lâm sàng. Các metric chưa có thuật toán/kiểm chứng trả null. Quality dùng luminance trung bình vùng ước lượng, contrast là độ lệch chuẩn grayscale, sharpness là variance của Laplacian grayscale trong vùng; region ratio là diện tích vùng ước lượng chia toàn ảnh. Ngưỡng trong ai-frangi.yaml là heuristic kỹ thuật, phải có fixture biên; không tuyên bố độ chính xác lâm sàng.

Với Frangi Sprint 3: `is_mock=false`, model_version=frangi-v0.2, config_version=s3-frangi-1.0.0, threshold_version=frangi-threshold-0.02-v1; **risk_available=false, risk_assessment=null**. Trả WARNING hợp lệ kèm RISK_UNAVAILABLE; Analysis vẫn COMPLETED. Public result phải có riskAvailable=false; riskLevel/riskScore/confidenceScore=null; indicators rỗng. Không tự đặt LOW hoặc0 để lấp trường thiếu. M5 hiển thị “Chưa cung cấp đánh giá rủi ro”. Mock riêng phải is_mock=true và không được dùng để nghiệm thu AI thật/settlement real.

HTTP422 LOW_QUALITY_IMAGE → LOW_QUALITY, không result; WARNING có mask hợp lệ không tự thành LOW_QUALITY. Lỗi AI có `{error_code,message,timestamp,details?}`, khác public error envelope. Worker xác thực request_id, version, Base64, MIME thực, kích thước, mask values và risk invariants trước commit; AI thành công HTTP200 chưa đủ.

Frangi không có weights để train. M4 bàn giao code tái lập, dataset manifest/license và split theo subject seed42; scaffold training cho model học sau này là đầu ra riêng, không thay thuật toán này âm thầm. Benchmark Frangi không được tính là chứng nhận NFR end-to-end hay kết quả chẩn đoán.

## 6. Quyền và ranh giới tenant

JWT service dùng RS256, issuer `aura-identity-s3-v1`, TTL300s, clock skew30s, public SPKI cho verifier; private key chỉ Identity. Dùng key/secret mới từ Setup đã patch trong checkout mới. Không nhúng Firebase token trong queue hoặc dùng một admin secret cho tất cả app. Client credentials không cấp quyền bệnh nhân.

`clients.sprint3.json` chốt audience/scope:

| Caller | Audience → scope được cấp |
|---|---|
| analysis-api | aura-identity → identity:context; aura-profile → profile:resolve; aura-billing → billing:reserve,billing:read,billing:release |
| analysis-worker | aura-ai → ai:analyze; aura-profile → profile:resolve |
| billing-api | aura-profile → profile:resolve; aura-analysis → analysis:reconcile |
| notification-api | aura-identity → identity:context; aura-analysis → analysis:notify |
| identity-api | aura-profile → profile:provision |
| profile-api | aura-identity → identity:context |
| nifi-camera | aura-analysis → analysis:ingest |

Scope có đúng một dấu `:` vì validator source yêu cầu hai phần. Analysis/Worker/NiFi không có billing:capture. Endpoint phải kiểm caller identity và scope; không chỉ kiểm JWT có chữ ký hợp lệ. Publisher có DB/Rabbit credential riêng, không cần service JWT nếu không gọi HTTP.

User API dùng bearer người dùng đã xác minh qua Identity, Profile trả authority bằng context. `X-User-Authorization` chỉ dùng server-to-server khi endpoint yêu cầu; client không gửi role/tenant tự chọn để cấp quyền. Người dùng tự đổi clinicContextId không tạo membership. Scope service không thay thế recheck authority của người được ủy quyền.

| Chủ thể | Tạo analysis | Đọc batch/result/file | Phạm vi và điều kiện |
|---|---|---|---|
| Patient/owner cá nhân | Patient thuộc account đang ACTIVE | Tài nguyên của Patient mình có authority hiện tại | tenant PERSONAL và billing ACCOUNT được Profile resolve |
| Nhân viên clinic có quyền tạo | Patient thuộc clinic có membership ACTIVE | Chỉ khi có quyền đọc tương ứng | Clinic context backend xác thực; billing CLINIC do Profile quyết định |
| Doctor | Theo permission cụ thể, không mặc định vì role Doctor | Chỉ Patient/analysis được assignment ACTIVE và quyền đọc | Thu hồi assignment chặn GET mới và xử lý mới |
| Clinic admin | Quản lý membership theo role | Không mặc định được xem ảnh y tế | Muốn đọc phải có permission riêng |
| System admin | Vận hành theo quyền quản trị | Không mặc định được xem ảnh y tế | Không tạo bypass chéo tenant |
| Camera/NiFi | Chỉ qua grant ACTIVE đúng client/device/actor/Patient/tenant | Không cấp public GET bằng ingest scope | Camera Sprint 3 chỉ PERSONAL; clinic camera bị từ chối tới contract riêng |

Profile access resolve giữ request `{actorAccountId,patientId,action,clinicContextId?}` với action ANALYSIS_CREATE/ANALYSIS_READ/ANALYSIS_FILE_READ/DOCTOR_REVIEW. Camera resolver giữ fields đã có trong overlay: actorAccountId,patientId,servicePrincipalId,clientId,deviceId,expectedTenantId,action=ANALYSIS_CREATE. M6 bổ sung allowlist analysis-worker để recheck grant bằng binding đã persist; không cần token Patient trong queue, không tin các ID nhận từ queue thay DB.

Permission bị thu hồi sau upload: Worker phải recheck và dừng an toàn; không publish result vào vùng người dùng đã mất quyền. Nếu chưa có kết quả hợp lệ, terminal theo policy và giải phóng đúng reservation sau authoritative resolve. Result đã có không bị xóa bởi thao tác revoke; GET mới bị chặn. Signed URL đã phát có thể còn dùng đến TTL300s; không tuyên bố revoke tức thì URL đã ký. Không log token hoặc URL ký.

Không đăng nhập401; có danh tính nhưng sai chức năng403; resource khác tenant/ngoài authority404 để hạn chế lộ tồn tại. Cả file-access và batch phải recheck, không chỉ trang result.

## 7. Credit và settlement

Billing là nguồn sự thật của số dư/ledger. Analysis không sửa bảng credit; Worker không tự capture. TEST và LIVE không dùng chung wallet hay credential. Một ảnh được nhận mới hợp lệ reserve1 unit; duplicate không reserve mới. Không nạp thêm credit vào fixture cũ để demo.

Reserve giữ contract0.1: analysisId,tenantId,actorAccountId,patientId,billingOwner{type,id},units=1,generation. Idempotency-Key UUID; key và payload gắn đúng một operation. Replay giống trả reservation gốc; cùng key khác payload409. Billing owner do Profile resolve; body client không quyết định người bị trừ credit.

| Tình huống | Reservation/ledger phải đạt |
|---|---|
| Upload được chấp nhận, chưa có AI hợp lệ | RESERVED1; available giảm1; chưa CAPTURE |
| AI transient retry hoặc Reporting outage | Giữ cùng reserve; không thêm CREDIT/RESERVE/CAPTURE |
| COMPLETED, result thật hợp lệ của generation hiện hành | Billing consumer CAPTURE một lần |
| FAILED kết thúc / LOW_QUALITY / REJECTED, chưa có result hợp lệ | RELEASE một lần; đúng reason |
| Cùng event giao lại hoặc cùng receipt giao lại | Không thêm ledger/receipt; ACK sau xử lý idempotent |
| Event cũ từ generation trước, sai tenant hoặc không khớp authoritative state | Không settlement; giữ evidence/điều tra khi cần |
| Chưa xác định commit hoặc authority unavailable | HOLD/pending; không đoán RELEASE hay CAPTURE |

State machine RESERVED→CAPTURED hoặc RESERVED→RELEASED; không đi từ CAPTURED sang RELEASED. Unique `(analysisId,generation)` và unique ledger operation/reservation chống xử lý hai lần. CAPTURE và RELEASE tranh chấp phải serialize trong Billing transaction, một bên nhận conflict. CREDIT là thao tác cấp TEST ban đầu riêng, không nằm trong luồng retry.

Billing consumer có inbox unique `(consumer,eventId)` và transaction inbox+ledger. Chỉ đánh inbox completed sau durable settlement; crash trước commit cho phép event giao lại. Consumer gọi resolver Analysis `POST /internal/v1/analysis/settlements/resolve` với eventId/analysisId/tenantId; Analysis tra persisted event binding gồm reservationId/generation/resultVersion và state hiện hành. Không có binding thì không đoán bằng analysisId đơn thuần. `applicable=false` hoặc action NONE không được capture/release. State COMPLETED mà result/event không khớp đưa RECONCILE_REQUIRED.

Release reason dùng enum hiện có: VALIDATION_ABORT,STORAGE_ABORT,ANALYSIS_ABORT,FINAL_FAILURE,LOW_QUALITY. Terminal LOW_QUALITY dùng LOW_QUALITY; lỗi AI hết budget dùng FINAL_FAILURE; quyền thu hồi trước kết quả dùng ANALYSIS_ABORT. Trong namespace real-AI này event isMock=true phải HOLD để điều tra, không tính demo real thành công và không tự capture.

Mốc15 phút chỉ phát hiện reservation bất thường và chạy reconciliation có audit; **không tự release do quá thời gian**. Resolver unavailable tối đa3 lần probe với backoff5/15 rồi HOLD, vận hành kiểm tra. Sprint 3 chưa mở nút public manual retry; nếu sau này cho phép generation mới chỉ sau terminal release theo contract riêng. Transport/job retry không tăng generation.

## 8. Upload, GET và UI

Public upload `POST /api/v1/analyses` multipart field `images`, 1 PNG/JPEG FUNDUS ≤10MiB,≤16Mpx; patientId,eyeSide theo schema; Idempotency-Key UUID không zero. Camera thêm binding headers theo Analysis OpenAPI từ overlay, không bỏ kiểm delegated authority. 202 chứa batchId, items và statusUrl; một upload được nhận không tự POST lại vì browser polling lỗi.

GET đúng các endpoint sau, M2/M5 không tự đổi thành `/results`:

| Endpoint | Kết quả và hành vi |
|---|---|
| `/api/v1/batches/{batchId}` | Batch, items, batchStatus, summary; không chứa Base64; recheck Patient/clinic context |
| `/api/v1/analyses/{analysisId}` | processingStatus, reviewStatus, job, image FileRef, result hoặc error theo schema |
| `/api/v1/analyses/{analysisId}/files/{fileId}/access` | `{url,expiresAt}`, SAS read-only300s, Cache-Control:no-store; file phải thuộc đúng analysis |
| `/api/v1/notifications/inbox?limit=20&cursor=...&unreadOnly=false` | Chỉ inbox account hiện hành; nextCursor opaque; tối đa100 |
| `/api/v1/notifications/inbox/unread-count` | `{unreadCount}` |
| `POST /api/v1/notifications/inbox/{notificationId}/read` | 204 idempotent; không đọc/sửa inbox account khác |

Analysis processingStatus: PENDING,QUEUED,PROCESSING,RETRY_READY,COMPLETED,FAILED,LOW_QUALITY,REJECTED,DUPLICATE. Job status QUEUED,RUNNING,SUCCEEDED,FAILED,RETRY_READY,DEAD_LETTER; retryCount=attemptNo−1. Review COMPLETED mới PENDING; không tính “đã bác sĩ review” trong Sprint 3. DUPLICATE tham chiếu existingAnalysisId không cấp quyền đọc tài nguyên đó tự động.

Batch IN_PROGRESS khi còn active hoặc lỗi còn retry; COMPLETED nếu mọi item thành công/duplicate; PARTIAL_FAILED nếu có cả tốt và terminal lỗi; FAILED nếu tất cả terminal lỗi. GET thành công vẫn HTTP200 khi nghiệp vụ PROCESSING/FAILED; lỗi truy cập dùng401/403/404, lỗi dependency503. Không dùng HTTP500 để biểu diễn ảnh LOW_QUALITY đã lưu.

M5 poll2s→4s→8s→10s, giữ cap10s, dừng khi terminal. Network lỗi hiển thị chờ/cho thử GET lại, không upload lại. Refresh file URL khi còn30s tới expiry; URL hết hạn xin URL mới sau auth, không sửa timestamp ở client. Public result camelCase, AI snake_case; mapping do Worker thực hiện.

Result Frangi cho mask/overlay qua FileRef, riskUnavailable hiển thị rõ; không dùng heatmap placeholder. COMPLETED phản ánh AI result bền vững, không đảm bảo Billing/Notification đã consume. UI có thể ghi “Đang đồng bộ” cho phần phụ thuộc chưa sẵn sàng; không đổi kết quả thành FAILED để che eventual consistency. Không thêm `creditStatus` vào response Analysis nếu chưa version contract; UI lấy từ Billing view được cấp quyền hoặc inbox tương ứng.

Inbox consumer dùng eventId để dedupe, resolve recipient bằng `POST /internal/v1/analysis/notification-context/resolve`, không gửi theo tenantId hay PatientId tự suy ra từ event. Trong demo PERSONAL chỉ account được Profile xác nhận nhận; clinic notifications chưa fan-out tới cả clinic. Inbox không chứa riskScore hay ảnh, dùng tiêu đề trung tính và deep link có recheck quyền. Nếu quyền đã bị thu hồi, không tạo thông báo lộ dữ liệu; inbox cũ có thể giữ lịch sử nhưng link GET vẫn kiểm quyền hiện tại.

Public error envelope `{error:{code,message,correlationId,retryable,details?}}`; thông báo đã loại secret, không đưa raw exception/SQL. Gateway chỉ route `/api/v1/...` và health alias đã duyệt; không public `/internal`, AI hoặc Billing internal.

## 9. NiFi là ưu tiên của M1

Giữ logic upload guard, không tạo request mới khi receipt lỗi. Main Delivery phải dùng safe writer đã review, chưa lấy PutSQL cũ làm bằng chứng retry end-to-end. Dynamic property **CTL.reporting**, không SQL.reporting; ReportingJdbcBase là URL base không query; DBCP URL có timeout theo config.

Timeout: connect5s, socket15s, statement10s, lock5s, maxWait5s, pool2/minIdle0/maxIdle2, SELECT1. Chỉ user EXECUTE stored procedure, không INSERT/UPDATE trực tiếp. Tất cả controller/password mới cho namespace mới. Bản script lấy từ source validated receipt-jdbc-v1, kiểm checksum và package GroovyX1.27 trước import; YAML không tự tạo/import processor.

Reporting retry attribute camera.reporting.retries, MaximumRetries10, penalty30s, penalize=true, fail nonnumeric=true, Fail on Reuse, ConcurrentTasks1. Nghĩa là tối đa10 retry sau lần ghi đầu, tối đa11 writer attempts; **khác Worker AI tối đa3 attempt**.

RECORDED+COMMITTED đi Recorded; RETRY+NOT_COMMITTED có lỗi transient rõ mới qua Retry; commit UNKNOWN, cleanup_failed=true hoặc lỗi contract đi HOLD/Reconcile, không đoán rollback. retries_exceeded giữ ở Exhausted; failure giữ Reconcile; không terminate/xóa queue để dashboard đẹp. Trường RouteOnAttribute.Route là dấu tuyến cũ, không thay kết luận commit/outcome mới.

M1 phải chứng minh bốn tình huống riêng: ghi bình thường; Reporting outage rồi recovery; exhaustion giữ mẫu; HOLD không tự retry. Seed10 test biên là bằng chứng route exhaustion, không được gọi là đã chạy đủ11 writer attempts nếu chưa thực hiện. Dùng synthetic receipt mới đúng new TEST contract, không gọi lại request/Blob/credit fixture cũ. Main flow promotion chỉ sau test; seed/group test mặc định Disabled, không Start cả group.

## 10. Cấu hình, secret và nghiệm thu chung

`config/*.yaml` là skeleton cấu hình có tên field chuẩn cho nhóm. Ngoại trừ file Gateway lấy đúng shape hiện có, các file mô tả binding chưa được service tự đọc. Mỗi owner phải thêm adapter hoặc map vào IConfiguration/env hiện hành và test; không đổi tên config rồi tưởng runtime đã nhận.

Infrastructure image phải pin RepoDigest đã kiểm; gói để trống image env để chặn chạy nhầm. Service images/build paths được bổ sung sau khi có code thật; không có Worker/NiFi app tự bật. Credentials chỉ file private ngoài repo; output Setup không ghi đè. Chọn một Firebase TEST project chung do M6 sở hữu; ID thực tế chưa có trong nguồn nên M6 phải điền trước bật user auth. Đây là đầu vào môi trường bắt buộc, không phải quyết định kiến trúc còn bỏ ngỏ.

Done Sprint 3 cần artifact của từng task và một demo E2E bằng ảnh mới: một upload/reserve → job/outbox → Frangi mask/overlay thật → GET/UI đúng quyền → capture1 → inbox1, sau đó fault test độc lập. Phải có duplicate/crash/revoke/stale event/HOLD/exhaustion và kiểm dữ liệu không tăng sai. Mọi báo cáo phân biệt assertion tự động với quan sát NiFi thủ công; không tính screenshots thành chứng nhận DB/Blob/ledger.

Thay đổi baseline phải có version mới và migration note được M1 cùng owner liên quan review. Không rollback bằng xóa DB/Blob/queue hoặc recreate môi trường cũ. Gói này không tự thay đổi Jira hay gửi thông báo cho thành viên.
