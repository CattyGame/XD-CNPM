# M2 Sprint 2 — trạng thái tích hợp upload/file access

## Kết quả runtime đã xác nhận tới 5M (2026-10-24)

Nguồn: log người dùng gửi, chưa đọc độc lập các tệp evidence trên máy Windows.
Không có thay đổi Jira/GitHub hoặc tuyên bố SCRUM-303/304 đã Done.

| Bước | Kết quả thực chạy | Evidence trong ~/.aura/checks |
| --- | --- | --- |
| 5K | Windows xUnit 288/288, PostgreSQL batch-status 8/8; database cô lập đã xóa | m2-analysis-batches-3bac11e36c60440aa06dfe925fc645ce.json |
| 5L | Duplicate/concurrency/replay/access 7/7; canonical reference đúng, không thêm reserve/job/outbox | m2-dedupe-a6153dfc936c4ae1a13bc952a25e373e.json |
| 5M | Validation/access/zero-credit abort 10/10; HTTP 400/401/404/413/415/402 đúng, dữ liệu trước đó giữ nguyên | m2-validation-abort-ce7441b87f0542009d12c75f2521cb13.json |

Baseline hiện tại: analysis_requests=2, images=2 (QUEUED và DUPLICATE), jobs=1
QUEUED, outbox_events=2 pending. Recovery operations=3, attempts=3: hai ACCEPTED
và một ABORTED/ANALYSIS_ABORT. Bốn bảng Analysis khác vẫn rỗng. TEST wallet vẫn
balance=0, reservations=1, CREDIT=1, RESERVE=1. Original Blob và duplicate private
copy giữ nguyên; 5M không ghi/xóa Blob. Frozen plan của attempt ABORTED có job/
outbox dự kiến nhưng không có business commit hoặc accepted request tương ứng.

Primary dùng image source5K, HTTP nghiệp vụ vẫn disabled; candidate kiểm thử đã
được dọn. Không refill, capture/release, chạy Worker/publisher/AI, migration hay
thay credential/role/ACL trong 5L/5M. 5L có build/recreate riêng primary Analysis
với feature flag false; 5M giữ nguyên primary/dependencies.

5M chứng minh abort khi reserve đầu tiên bị từ chối chắc chắn, chưa chứng minh
partial-reserve compensation, storage failure/late write, ambiguous outcome hoặc
reconciler/GC. Concurrent duplicate không chứng minh paid concurrency. Còn quyền
hai tài khoản/Clinic, Gateway/Web upload và các khoảng trống task gốc cần rà lại.
Worker/publisher đầy đủ thuộc Sprint3. Không mở rộng smoke thành toàn Sprint2 Done.

Không chạy lại các runner pin baseline cũ, sửa guard hoặc xóa dữ liệu để ép PASS.
Patch tài liệu 5N thay hash của hai tài liệu; runner tiếp theo cần baseline source
sau5N và database sau5M. Không cần build/deploy/retest runtime cho patch tài liệu.

Tiếp theo: rà M3 SCRUM-305/306/307 theo code và acceptance criteria thực tế:
package/version/snapshot/cache, reserve/capture/release và generation/idempotency,
TEST/LIVE, refund tách release, test plan/RTM cùng Chat ownership/retention ở mức
thiết kế. Quyết định nghiệp vụ còn mở phải được nêu rõ, không tự chọn giá/refund/
retention hoặc datastore Chat. Chưa gửi mutation Billing khi chưa có fixture và
kế hoạch riêng; giữ nguyên hold của 5J.

## Lịch sử triển khai theo từng bước

Các mục dưới đây mô tả trạng thái tại thời điểm của từng gói; kết quả mới nhất ở trên.


Ngày cập nhật: 2026-10-24. Baseline Git `87af21da20489eda6d4899f7e1f3a428b24960db`, nhánh local `fix/sprint2-closeout-20261023` cùng các patch source chưa commit. Contract REST giữ nguyên `contracts/analysis-openapi.yaml` v0.6.0.

## Trạng thái hiện tại sau runtime 5J

- Người dùng đã chạy upload PNG tổng hợp với Firebase thật trong candidate cùng image/config primary: HTTP202, một item/job QUEUED, transaction durable, replay không thêm hiệu ứng, payload đổi cùng key trả409, batch/file-access và SAS download/anonymous deny đều PASS (10/10 nhóm).
- Fixture riêng ACCOUNT/TEST bắt đầu bằng CREDIT(+1); sau upload có balance0, một reservation RESERVED và một RESERVE(-1). Một original Blob private, hai outbox pending và journal ACCEPTED được giữ lại. Không capture/release/refill hoặc dọn business records.
- Candidate kiểm thử đã được dọn; primary Analysis HTTP nghiệp vụ vẫn disabled. Upload trực tiếp trong candidate chưa chứng minh Gateway/Web upload hoặc Worker/AI; publisher/Worker thuộc Sprint3.
- Trước sửa5K, Windows Analysis tests đã PASS278/278; 5K sửa `existingAnalysisId` cho DUPLICATE và thêm 10 regression cases. Máy chuẩn bị đã PASS288/288; cần log Windows và kiểm repository PostgreSQL cập nhật trước deploy.
- Evidence JSON nằm tại máy người dùng; ghi nhận dưới đây dựa trên log đã gửi, chưa đọc độc lập các tệp đó. Các phần "chưa chạy" phía dưới là lịch sử theo từng gói, không phủ định kết quả mới.
- Chưa đóng SCRUM-303/304: còn compensation/reconciliation cho storage/unknown outcome, coverage concurrency/failure trên dependency thật, quyền hai tài khoản/Clinic và đồng bộ bàn giao. Analysis detail/list/retry/review/report, NiFi ingest và Gateway/Web chưa được chứng minh bởi smoke này.

| Bước | Actual result người dùng cung cấp | Evidence dưới `~/.aura/checks` |
| --- | --- | --- |
| 5E4 | Candidate GET readiness ready, DB/storage/grants hợp lệ, primary disabled | `m2-runtime-readiness-415bca0571a94e86bb23ef64424c8a0a.json` |
| 5F | 278/278 xUnit sau mapping FILE_READ -> ANALYSIS_FILE_READ | Log test Windows trong bàn giao |
| 5G | 17/17 service-auth/native/image groups + GET readiness; data không đổi | `m2-service-integration-c95c06927c8c44049a4c756c83efeadd.json` |
| 5H | 14/14 Firebase/Analysis access groups + Gateway Identity/Profile; chưa có TEST wallet | `m2-firebase-access-678be5d90b4e497f8da0dca0e087505c.json` |
| 5I | Tạo mới đúng một TEST wallet/CREDIT(+1), balance1/reservations0; data cũ không đổi | `m2-test-wallet-5942b930ff524456a48e4e1ed846f395.json` |
| 5J | 10/10 upload/replay/access groups; balance0, reservations1, CREDIT1/RESERVE1 | `m2-upload-a85bec2edca34a988e05014efc3412bd.json` |

Jira đã đọc lại ngày2026-10-24: [SCRUM-303](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-303) yêu cầu dedupe/compensation/file orphan; [SCRUM-304](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-304) yêu cầu payload/ownership/file-access và đồng bộ tài liệu. Cả hai đang In Review khi đọc; bản sửa này không cập nhật Jira/GitHub.

## Bước 5K — trả canonical ID cho DUPLICATE

Mô tả POST contract đã yêu cầu `existingAnalysisId`, nhưng AcceptedItem bỏ mất trường này; BatchItem schema và batch projection cũng chưa trả nó. Planner nay đưa ID canonical từ cùng kết quả dedupe đã kiểm scope vào accepted item; repository đọc `images.existing_analysis_id` và projection trả cho DUPLICATE sau authorization. Projection từ chối reference thiếu/rỗng/tự trỏ hoặc xuất hiện ở trạng thái khác.

BatchItem thêm property UUID tùy chọn và ví dụ accepted DUPLICATE. Response QUEUED không thêm trường null; response/event/job version không đổi. Replay response cũ thiếu trường vẫn giữ nguyên các field đã lưu, không rewrite accepted_response/journal hoặc suy diễn ID khi replay. Read batch có thể lấy canonical ID từ metadata DB đã có.

288/288 xUnit đạt tại máy chuẩn bị, gồm HTTP serialization sau resource authorization, duplicate mới/within-batch/replay, legacy stored JSON và malformed references. Còn17 analyzer warnings cũ. Console BatchChecks thêm assertion đọc reference thật trên DB cô lập; build đạt, chưa tuyên bố chạy PostgreSQL ở máy chuẩn bị. Không đổi migration, schema, permission policy, reserve/storage/compensation behavior hoặc runtime flags. Blob vẫn được lưu cho item duplicate theo behavior hiện tại; patch này không triển khai GC/reconciler.

Sau5J Analysis không còn trống. Các runner runtime5G/5H/5I/5J pin baseline cũ sẽ dừng là đúng; không sửa hash/guard hoặc xóa dữ liệu để ép chạy. Gói5K chỉ sửa local source/tài liệu, không gọi Docker/DB/Billing/Blob hoặc deploy. Bước runtime tiếp theo phải dùng source state sau5K và baseline dữ liệu đã tạo ở5J.

## SCRUM-302 — Analysis DB

### Đã bổ sung trong patch
- SQL test có `ON_ERROR_STOP=1`; lỗi assertion làm script kết thúc non-zero.
- Kiểm tra PostgreSQL 17+, đủ 8 bảng, constraint quan trọng, partial index `ix_outbox_unpublished`, runtime role và quyền DML.
- Test hành vi idempotency scope (cùng tenant/requester/key phải bị từ chối; key cùng giá trị ở tenant khác được phép), fingerprint, modality FUNDUS và FK không cho tham chiếu request khác tenant.
- Test fixture nằm trong transaction và được `ROLLBACK`.

### Cần chạy để nghiệm thu
- Chạy `psql` trên database kiểm thử cô lập sau migration; lưu log, version, exit code và commit SHA.
- Xác nhận role `aura_analysis_app` được provision bởi migration/infra. Script cố ý fail nếu role thiếu, không tự tạo role.
- Đối chiếu quyền với runtime role thật, không chạy API bằng bootstrap/migration role.

## SCRUM-303 — upload/idempotency domain

### Đã bổ sung trong patch
- PNG: CRC32 được xác minh cho từng chunk, bao gồm IHDR, IDAT và IEND.
- PNG: yêu cầu chunk IHDR đầu tiên, IEND rỗng và không có bytes thừa sau IEND.
- Fixtures tạo CRC hợp lệ; tests mới bao gồm CRC IDAT sai và bytes thừa sau IEND.

### Chưa hoàn thành / phụ thuộc tích hợp
- Inspector vẫn không giải mã toàn bộ JPEG hoặc giải nén dữ liệu IDAT PNG. CRC hợp lệ không chứng minh ảnh giải mã thành công. Để kiểm tra hỏng dữ liệu nén thật, cần thư viện decoder được nhóm phê duyệt và tests với ảnh JPEG/PNG thật/corrupt.
- `Program.cs` trong baseline chỉ map `/health` và `/api/v1/platform/ping`; chưa map endpoint nghiệp vụ `/api/v1/analyses`, batch status hoặc file access theo OpenAPI.
- Chưa có concrete `IUploadRepository`/`IFileAccessRepository` PostgreSQL, Billing reservations adapter hoặc Azure Blob/Azurite adapter trong thư mục Analysis API được rà soát.
- Cần map requester từ authenticated principal; không lấy requester/owner từ multipart form. Profile permission phải được kiểm tra trước replay; không trả 202 khi chưa durable commit.
- Cần kiểm thử crash/ambiguous commit và reconciliation. Không release reservation hoặc xóa object nếu chưa xác nhận transaction abort.

## SCRUM-304 — tests và bàn giao

### Bằng chứng có sẵn từ source snapshot
`docs/testing/scrum304_upload_test_results.txt` ghi 59 unit/domain tests PASS, 0 FAIL tại 2026-10-17; đây là kết quả đã được ghi lại trước patch, không phải kết quả chạy patch mới.

### Việc còn mở
- Chạy test .NET sau patch.
- Thêm HTTP integration tests cho status/headers/schema và xác thực.
- Chạy test với PostgreSQL/Azurite thật; kiểm tra cùng key replay, payload khác trả 409, concurrency chỉ tạo một batch/reservation, file access trái quyền trả 404 và URL ký không lộ object key.
- Bổ sung evidence từ endpoint/runtime sau khi các adapter có mặt. Không tuyên bố end-to-end hoàn tất chỉ dựa vào fake/stub tests.

## Điều kiện để đóng cả ba task

1. SCRUM-302: SQL test chạy PASS trên PostgreSQL 17+; lưu output và exit code.
2. SCRUM-303: domain tests mới PASS; API/DB/Billing/storage adapters được nối đúng contract và có HTTP integration tests.
3. SCRUM-304: có test report thực chạy và tài liệu/sequence khớp triển khai; bằng chứng quyền sở hữu, replay, concurrency và cleanup trên dependency thật.
4. M1/M3/M6 xác nhận cấu hình storage/DB, DTO Billing và Profile access/auth; không thay đổi OpenAPI 0.6.0 nếu chưa có đồng thuận chính thức.

## Cập nhật ngày 2026-10-23 — kiểm schema và chuẩn bị adapter HTTP

### SCRUM-302: bằng chứng người dùng đã chạy

Trình kiểm tra `Test-AuraAnalysisSchema.py` đã được người dùng chạy trên
PostgreSQL 17 thật, với database thử riêng. Output báo: hai lượt migration
đạt; 8 bảng/constraints/idempotency/tenant/index/grants đạt; thử DML dưới
`aura_analysis_app` đạt và các quyền DELETE/append-only UPDATE/DDL bị từ
chối. Fixtures đã rollback và database thử đã được xóa. Không thay đổi
database ứng dụng hoặc định nghĩa global role.

Evidence tại máy bàn giao:
`%USERPROFILE%\.aura\checks\m2-analysis-schema-16f21c521ff3403d96cb3c1f7696f294.json`.
Đây là kết quả người dùng cung cấp; tệp JSON chưa được đọc độc lập trong
phiên chuẩn bị patch. Kết quả này không chứng minh API upload đã hoạt động.

### Bước 3A: thay đổi mã nguồn trong gói

- ServiceTokenProvider dùng credentials riêng `analysis-api`, xin đúng
  audience/scope; không dùng secret mặc định, không xin `billing:capture`.
- ProfileAccessClient tự gắn service JWT, kiểm tenant/billing owner và xử lý
  phản hồi sai định dạng thành lỗi dependency.
- BillingReservationsClient gửi object billingOwner cùng actorAccountId,
  patientId, units=1 và generation; giữ Idempotency-Key riêng cho thao tác.
- Không coi reservation CAPTURED/RELEASED là reserve thành công; không tự
  retry khi mất phản hồi. Xung đột được ánh xạ theo public Analysis contract.
- UploadService chuyển context có thẩm quyền sang adapter và dùng
  STORAGE_ABORT khi lỗi storage. Compensation dùng cancellation riêng có
  thời hạn thay vì token đã bị hủy của người gọi.
- Có hàm đăng ký HttpClient với redirect bị tắt và header nhạy cảm được
  redaction. Bước nối HTTP composition root sẽ gọi hàm này.

Tests mới dùng HTTP giả lập. Máy chuẩn bị patch không có .NET SDK; phải chạy
build và toàn bộ Analysis xUnit tại máy triển khai trước khi ghi PASS. Chưa
có kết quả runtime mới cho các adapter trong bước này.

### Phần còn tiếp tục ở bước 3B/3C

- PostgreSQL upload/file/batch repositories, Azurite adapter và decoder ảnh.
- Durable orchestration cho reservation/storage/commit chưa rõ kết quả,
  retry sau abort và generation; không chỉ dựa vào log hoặc fake Billing.
- Identity context của Analysis, cấu hình credentials/DI và các HTTP endpoint.
- HTTP integration tests và kiểm tra dependencies thật trước nghiệm thu
  SCRUM-303/304. Program.cs ở bước 3A vẫn chưa mở API upload nghiệp vụ.

## Bước 3A đã kiểm tra và bước 3B1 đang triển khai

Người dùng đã chạy gói 3A: Analysis Release build đạt; toàn bộ 113/113
xUnit test đạt, không skip; diff check đạt. Có 17 cảnh báo analyzer xUnit
về cách viết assertion, không phải test thất bại. Bằng chứng là log người
dùng cung cấp ngày 2026-10-23; các HTTP handler trong bộ này vẫn là giả lập.

Bước 3B1 bổ sung `PostgresUploadRepository`, `PostgresFileAccessRepository`
và Npgsql 10.0.3 (cùng phiên bản Billing đang dùng). Không thay migration
Analysis hoặc contract 0.6. Repository dùng transaction có advisory lock
cho idempotency và dedupe; commit batch/image/job/outbox/accepted_response
trong một transaction, giữ phản hồi gốc khi replay. File repository chỉ
trả metadata; `FileAccessService` vẫn phải kiểm quyền Profile trước cấp URL.

`tests/AURA.Analysis.PersistenceChecks` có 13 nhóm kiểm tra trên PostgreSQL
thật, gồm atomic commit, replay/conflict, contention, phạm vi dedupe, rollback
khi outbox lỗi, scope binding, original/annotation metadata và mở lại data
source. Chạy qua `scripts/dev/Test-AuraAnalysisPersistence.py`: mặc định chỉ
preflight; `--run` build project, tạo database có UUID riêng, dùng password
runtime nhập ẩn và chỉ dọn database đó khi mọi kiểm tra đạt.

Đường kết nối kiểm thử được truyền qua environment riêng của tiến trình
con; không ghi password vào arguments, JSON evidence hoặc Git. Database
thử phải có đúng marker và schema ban đầu rỗng; console check từ chối tên
database ứng dụng. Lỗi/timeout giữ database thử và evidence để điều tra.

Máy chuẩn bị gói không có .NET/PostgreSQL runtime để chạy các repository;
chưa ghi PASS cho 3B1. Kiểm tra phải chạy tại máy triển khai.

Còn tiếp: batch/status repository theo DTO public, Azurite adapter, decoder,
durable orchestration/reconciliation cho side effect chưa rõ kết quả, cấu
hình/DI/Identity context và HTTP endpoints. Bước này chưa trả 202 qua API.

Nguồn kỹ thuật:
- https://www.npgsql.org/doc/basic-usage.html
- https://www.postgresql.org/docs/17/explicit-locking.html#ADVISORY-LOCKS

## Bước 3B1 đã kiểm tra; bước 3B2 bổ sung adapter Blob

Log người dùng ngày 2026-10-23 xác nhận Analysis build đạt, 113/113 xUnit
đạt, 0 skip và 13/13 nhóm repository trên PostgreSQL 17 thật đạt bằng
`aura_analysis_app`; database UUID thử đã được xóa. Evidence local:
`~/.aura/checks/m2-analysis-persistence-bc8bfee567794c80bec584d653c76462.json`.
Đây là kết quả sau phần ghi chú "chưa ghi PASS cho 3B1" ở trên.

Bước 3B2 thêm Azure.Storage.Blobs 12.30.1 và adapter Shared Key, dùng các
khóa Storage__* hiện có. Chỉ cho phép HTTP ở Development với account
emulator, endpoint Docker `azurite:10000` hoặc Windows `127.0.0.1:10000`;
URL trình duyệt dùng loopback. Azure cloud yêu cầu HTTPS và endpoint chuẩn
cùng account. Chưa hỗ trợ endpoint tùy chỉnh/CDN hoặc managed identity.
Không có credential mặc định trong adapter; fixture local đọc khóa emulator
công khai từ env example đã kiểm hash.

Adapter không tạo container, không sửa ACL. Nó kiểm ACL private trước khi
thao tác; kiểm tra này không thay thế chính sách quyền Azure account và
không ngăn quản trị viên đổi ACL sau đó. Object key hiện tại gồm 3–6 UUID
khác rỗng, không nhận tên file hoặc đường dẫn tùy ý. Upload dùng điều kiện
If-None-Match; replay cùng SHA-256, size và MIME giữ blob/ETag; dữ liệu khác
bị từ chối ghi đè. Caller vẫn phải kiểm nội dung/decoder và quyền nghiệp vụ.

SAS chỉ cấp quyền đọc cho một blob, TTL 1–120 giây nguyên, Cache-Control
no-store; dùng clock System ở DI. Có kiểm blob tồn tại trước cấp URL.
URL SAS chứa đường dẫn blob nên không được ghi log. SDK diagnostics và
retry tự động được tắt; lỗi phụ thuộc do tầng dịch vụ xử lý.

`Test-AuraAnalysisStorage.py` mặc định preflight không ghi blob. `--run`
lưu kế hoạch bốn UUID blob dưới ~/.aura/checks trước build/request, xác
nhận đúng Compose service/image/loopback/env example, rồi chạy 12 nhóm
thử adapter trên Azurite thật: bytes, replay/ETag, chống overwrite,
anonymous denied, SAS read/TTL/no-store, write denied, sibling denied,
expired denied, fresh adapter, cancellation/TTL và cleanup. Chỉ xóa các
khóa đã xác nhận không tồn tại trước upload; không list hoặc xóa container.
Sau timeout/lỗi phải kiểm evidence và các khóa fixture, không xóa volume.

Máy chuẩn bị gói không có .NET/Azurite; chưa ghi kết quả build/xUnit/12
nhóm storage runtime. Cần log máy triển khai trước khi chốt bước 3B2.
Bước này thêm DI extension nhưng chưa gọi trong Program; chưa deploy API.
Decoder đầy đủ, durable orchestration/reconciliation, auth context và HTTP
upload/status/file-access vẫn cần hoàn thành trước khi đóng SCRUM-303/304.

Nguồn kỹ thuật:
- https://www.nuget.org/packages/Azure.Storage.Blobs/12.30.1
- https://learn.microsoft.com/en-us/dotnet/api/azure.storage.sas.blobsasbuilder
- https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite

## Bước 3B2 đã kiểm tra; bước 3B3 bổ sung full image decode

Log người dùng ngày 2026-10-23 xác nhận 134/134 xUnit đạt, 0 fail/skip,
và 12/12 nhóm adapter Azurite thật đạt; bốn blob thử đã được dọn, ba
container vẫn private. Build StorageChecks không có warning/error.
Evidence: ~/.aura/checks/m2-analysis-storage-c9eeee5d31a347cab1c480a24d7bdaab.json.
Các ghi chú chưa có kết quả runtime 3B2 ở trên đã được log này bổ sung.

Bước 3B3 dùng SkiaSharp và NativeAssets.Linux.NoDependencies 4.153.1
(bản stable), thay kiểm tra chỉ header bằng GetPixels cho toàn bộ ảnh.
Chỉ nhận SKCodecResult.Success; IncompleteInput và các kết quả lỗi bị
400 ERR_VALIDATION/corrupt, không nhận bitmap giải mã một phần. Giữ CRC
và framing PNG hiện có, so khớp MIME/kích thước với decoder, từ chối ảnh
nhiều frame. Không thay byte gốc, metadata EXIF, kích thước hay SHA-256.
Việc chuẩn hóa/resize/orientation vẫn thuộc preprocessing theo contract.

Giới hạn cấu hình có thể hạ nhưng không được nâng quá 10 MiB/16M pixel.
Header/pixel được kiểm trước bitmap RGBA (tối đa 64M byte output), tối đa
hai lần inspect đồng thời trong process. Đây không phải giới hạn tổng
memory process: còn input snapshot, codec/native buffers và multipart.
Cancellation được kiểm trước/sau native decode và trong đọc stream/chờ
slot; không hứa dừng tức thời native call hoặc hard timeout CPU. Native
runtime thiếu/sai phiên bản được báo 503 ERR_NETWORK, không gán thành
ảnh hỏng. Không triển khai sandbox process cho từng ảnh ở bước này.
Success là kết quả của codec, không phải chứng minh toàn bộ cú pháp JPEG
hoàn hảo: thư viện có thể phục hồi một số lỗi dữ liệu. Kiểm này không đo
chất lượng ảnh y tế hoặc thay cho quality gate của M4.

TestImages trước đây chỉ là PNG có IDAT rỗng không nén/JPEG giả header.
Fixture mới chứa RGB PNG nén zlib đúng và JPEG encode thật. Tham số seed
ở JPEG dùng COM hợp lệ để tạo hash byte khác nhau. Ngưỡng byte trong
một acceptance test dựa trên kích thước JPEG hợp lệ thay vì hằng số giả.
Regression mới kiểm PNG CRC hợp lệ nhưng IDAT hỏng/thiếu scanline,
JPEG chỉ SOF/EOI hoặc SOS trỏ bảng Huffman không hợp lệ. Các ca này phải dừng
trước Profile, Billing, storage hoặc DB commit. Có thêm PNG/JPEG baseline/
progressive do Pillow sinh độc lập (24x18 pixel tổng hợp) để kiểm decoder,
checksum, giữ byte và rewind stream; kiểm cancellation/config/pixel guard.

SkiaSharp được phân phối theo MIT; các package native kèm license/notices
của dependencies. Giữ license/notices khi phân phối. Nguồn:
- https://www.nuget.org/packages/SkiaSharp/4.153.1
- https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/4.153.1
- https://github.com/mono/SkiaSharp/tree/v4.153.1
- https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skcodecresult

`scripts/dev/Test-AuraAnalysisDecoder.Linux.sh` chạy trong container SDK
10.0 riêng, repo bind read-only tại /input; chép hai source project vào
thư mục tạm trong container, bỏ bin/obj Windows rồi chạy toàn bộ xUnit.
Không nạp env/credentials của các API, không vào Compose network, không
mount Docker socket, không đổi service/volume/db/blob. Container --rm và
thư mục tạm được dọn sau khi test kết thúc. SDK/NuGet có thể cần download.
Kết quả Linux SDK này xác nhận bộ native Linux chạy được với unit tests;
chưa thay thế kiểm tra image API sau publish/deploy ở bước tích hợp.

Máy chuẩn bị gói không có .NET/Docker; chưa ghi PASS build/decoder runtime
3B3. Cần log Windows và Linux trước khi chốt. Chưa wire Program hoặc HTTP;
durable orchestration/reconciliation và endpoint/auth/status vẫn còn.

## Bước 3B3 đã kiểm tra; bước 3B4 bổ sung batch status read

Log người dùng ngày 2026-10-23 xác nhận 152/152 xUnit đạt trên Windows
và 152/152 đạt trong Linux SDK container, không fail/skip; native decoder
JPEG/PNG chạy được trên Linux. Container mount repo read-only, build trong
/tmp. Những ghi chú chưa có kết quả runtime 3B3 ở trên đã được bổ sung.
Còn 17 warning analyzer xUnit cũ, không phải test thất bại; diff check đạt.

Bước 3B4 thêm IBatchStatusRepository/PostgresBatchStatusRepository và
BatchStatusService, chưa map endpoint HTTP. Không đổi migration hoặc
OpenAPI 0.6. Service chỉ đọc binding batch/tenant/patient/owner trước,
kiểm Profile action ANALYSIS_READ ở mỗi lần poll, rồi mới tải items/counts.
Không có quyền, sai tenant/owner, batch không tồn tại hoặc binding đổi giữa
hai bước đều trả cùng 404. Dependency lỗi trả 503, caller cancellation
truyền ra. Kiểm tra role/function bằng Identity context tại HTTP vẫn cần
thực hiện ở bước nối endpoint; service không tự suy ra role từ UUID.

Query items ràng buộc lại bốn trường binding và dùng một SELECT snapshot
để đọc image + latest job/result. Latest job theo attempt_no, latest result
theo version, không chọn theo created_at. retryCount=attemptNo-1. Kết quả
riskLevel chỉ lấy từ trường public camelCase đã map trong risk_assessment,
chỉ có ở item COMPLETED; full AnalysisResult/analysis detail chưa thuộc gói
này. Worker tương lai phải ghi đúng mapping, version và trạng thái.

Tên file/thứ tự ảnh được khôi phục từ accepted_response gốc; trạng thái
hiện tại đọc từ images/jobs, không lấy trạng thái QUEUED cũ trong replay
response. Đối chiếu đủ tập analysisId; metadata thiếu/sai báo dependency
error thay vì trả một phần dữ liệu. Không SELECT object key, error_details,
annotation path, full result JSON hoặc outbox payload. Public DTO không
chứa tenant/owner/patient binding hay raw diagnostic; ErrorInfo dùng mã
allowlist và thông báo cố định, không phản chiếu exception của dependency.

BatchSummary luôn có đủ chín key trạng thái viết hoa, kiểm invariant
`total = active+succeeded+failed+duplicate = sum(byStatus)`. FAILED còn
retry (cờ retryable của latest FAILED/RETRY_READY job) tính active;
DEAD_LETTER là terminal dù cờ cũ vẫn true. Không áp đặt số lần retry mới
ở read side: Worker phải cập nhật cờ terminal theo policy thực thi.
Batch còn active là IN_PROGRESS; hết active: có cả lỗi và success/duplicate
là PARTIAL_FAILED, mọi item lỗi là FAILED, không lỗi là COMPLETED.
DUPLICATE đếm riêng, không tự tính thành kết quả AI thành công mới.

Unit tests thêm ma trận trạng thái, retry/dead-letter, tổng count, schema
field/case, quyền đọc, thu hồi quyền giữa các poll, binding đổi, dependency
và cancellation. Tests/AURA.Analysis.BatchChecks có tám nhóm PostgreSQL
thật, dùng role runtime: queued metadata, scope binding, latest attempt,
dead-letter, chín trạng thái/latest result/order, public JSON/không lộ
metadata nội bộ, visibility trước/sau transaction commit, fresh data source
và canceled reads không đổi dữ liệu. Chỉ tạo trạng thái tổng hợp trong DB
fixture; không thực thi AI Worker hoặc tạo kết quả y tế thật.

`scripts/dev/Test-AuraAnalysisBatches.py` tái sử dụng cơ chế test DB cô lập
đã kiểm ở 3B1: mặc định preflight, --run lưu plan trước build/create DB,
DB tên aura_m2_batch_<UUID> có marker riêng, migration hai lần và schema
assertions, nhập ẩn password aura_analysis_app, chỉ xóa đúng DB fixture
khi đủ 8/8 nhóm đạt. Lỗi/unknown giữ DB và evidence để xem lại. Không
chạy lại toàn bộ 13 nhóm persistence cũ vì không sửa upload repository.

Máy chuẩn bị gói không có .NET/PostgreSQL; chưa ghi PASS build/unit/8 nhóm
runtime 3B4. Cần log máy triển khai. Chưa đóng SCRUM-303/304: durable
orchestration/reconciliation cho side effect chưa rõ kết quả, Identity
context/DI/config và các endpoint HTTP vẫn cần hoàn thành.

Nguồn: contracts/analysis-openapi.yaml 0.6.0;
https://www.postgresql.org/docs/17/transaction-iso.html#XACT-READ-COMMITTED.

## Bước 3B4 đã kiểm tra; bước 3B5 bổ sung Analysis user identity

Log người dùng ngày 2026-10-23 xác nhận 176/176 xUnit đạt, 0 fail/skip,
và 8/8 nhóm PostgreSQL batch-status thật đạt bằng aura_analysis_app.
Database thử được xóa sau khi đạt. Evidence:
~/.aura/checks/m2-analysis-batches-9ed23443de97416d8bcaaf2b7a0ecf6f.json.
Những ghi chú chưa có kết quả runtime 3B4 ở trên đã được log này bổ sung.

Bước 3B5 thêm IAnalysisUserIdentityResolver/AnalysisUserIdentityResolver,
đăng ký transient trong AddAnalysisServiceClients. Input chỉ lấy đúng một
Authorization: Bearer; chặn thiếu/sai scheme, token quá 16 KiB, whitespace,
header ghép và ký tự ngoài dạng token Firebase JWT/base64url hoặc fixture
Development hiện có. Không parse JWT claims, không hash UID thành GUID;
không dùng X-Account-Id/X-User-Id/X-Tenant-Id/X-Roles, X-User-Authorization
của caller hoặc request.User để nhận diện account/role.

Resolver dùng ServiceTokenProvider hiện có, với client analysis-api và
scope identity:context / audience aura-identity, rồi gọi endpoint context
bằng service JWT; chỉ bearer public thực sự được đặt vào X-User-Authorization.
Identity kiểm Firebase hoặc mock Development theo cấu hình của Identity;
Analysis không tự tạo Development account hoặc mặc định PATIENT.
Context trả về account GUID khác rỗng, đúng ACTIVE và roles có cấu trúc
hợp lệ mới tạo AnalysisUserContext; không cache account/roles giữa poll.
Roles được copy thành collection read-only và loại trùng theo Ordinal.

401 từ context trả 401 ERR_UNAUTHORIZED; chưa có mapping account (404)
trả 409 ERR_ACCOUNT_NOT_INITIALIZED để client bootstrap riêng; không tự
bootstrap tại read/upload auth. Account không ACTIVE trả 403
ERR_ACCOUNT_DISABLED. Issuance/config/body/network lỗi, context service
scope 403 hoặc status bất thường trả 503 ERR_NETWORK retryable; không
phản chiếu error body/exception của Identity.

Lưu ý giới hạn contract hiện có: context dùng cùng HTTP 401 cho service
JWT không hợp lệ và user token không hợp lệ. Resolver xin service JWT mới
mỗi lần và phân loại issuance failure là dependency; nhưng một HTTP 401
phát sinh tại context sau đó không thể phân biệt hai nguồn bằng status
hiện tại. Chưa thay error contract của Identity trong gói này. Không coi
kết quả unit client là đã audit xong issuer/JWT validation của M6.

Operation có deadline dùng timeout 1–30 giây của named client, bao gồm
issuance/context và đọc body. ResponseHeadersRead và buffer tối đa 32 KiB
ngăn body không có Content-Length bị đọc vô hạn vào memory. Caller cancel
được truyền ra; dependency timeout là 503. Không retry tự động. Correlation
chỉ nhận giá trị server đã xác lập trong HttpContext.Items và kiểm lại
ký tự/độ dài trước khi forward; không copy tùy ý header của caller.

AnalysisUserPolicy.RequireClinicalRole cho phép PATIENT/DOCTOR/CLINIC_ADMIN
ở những chức năng Analysis có đúng x-roles này; SYSTEM_ADMIN bị chặn kể
cả khi đi kèm PATIENT, theo mô tả contract không cho SYSTEM_ADMIN dùng
endpoint lâm sàng. Role gate không cấp quyền resource: Profile vẫn kiểm
OWN/ASSIGNED/TENANT cho từng create/read/file/batch. Policy này không thay
thế policy review của DOCTOR hoặc policy service ingest của NiFi.

Tests thêm HTTP handler giả lập cho public bearer, chống spoof, scope,
context status/body, role status, body không có Content-Length, roles quá
lớn, caller cancellation và deadline body read; integration DI và issuance
401 phân loại dependency. Role policy kiểm trường hợp không có role,
role không được phép và SYSTEM_ADMIN có thêm PATIENT. Test Development
chỉ xác nhận token được chuyển cho Identity, không giả GUID tại Analysis.

Máy chuẩn bị gói không có .NET/runtime Firebase; chưa ghi PASS build/unit
3B5 hoặc Firebase -> Analysis thật. Chưa gọi resolver từ Program, chưa
map HTTP endpoint và chưa đổi credentials/Compose/registry. Khi nối runtime
phải nạp secret analysis-api riêng, cấu hình clients, dùng resolver/policy
trên các route và kiểm 401/403/404 cùng Firebase qua Gateway. Durable
orchestration/reconciliation, service ingest, Identity issuer fail-closed
và HTTP integration vẫn cần xử lý trước nghiệm thu toàn luồng.

Nguồn nội bộ đối chiếu tại 87af21d:
- contracts/identity-openapi.yaml; src/services/AURA.Identity.Api/Models/IdentityModels.cs
- contracts/analysis-openapi.yaml 0.6.0; infra/service-auth/clients.example.json

## Bước 3B5 đã kiểm tra; ưu tiên đóng lỗi service-auth M6 trước runtime

Log ngày 2026-10-23 xác nhận 220/220 xUnit đạt, không fail/skip; Analysis
Release build và diff check đạt. 17 cảnh báo analyzer xUnit là các cảnh báo
đã có. Các test mới dùng fake HTTP, chưa có Firebase -> Analysis runtime.

Trước khi nối HTTP upload, gói bước 4 loại bỏ fallback khóa/registry/default
secret ở Identity và fallback private key/network discovery ở Profile.
Identity/Profile resolve component auth ngay lúc startup; Profile dùng public
key explicit từ mount read-only riêng. Bộ tests/AURA.ServiceAuth.Checks chạy
source auth thật với khóa RSA tổng hợp, kiểm signing/validation/config failure;
chế độ --check-local-material chỉ đọc credential runtime ngoài repo.

Gói không đổi Analysis resolver, upload orchestration, schema hay Billing.
Vấn đề context trả cùng 401 cho service JWT và user token vẫn là giới hạn
contract chưa xử lý. Cơ chế phục hồi upload sau ambiguous reserve/storage/DB
commit, generation sau abort, HTTP composition/config và test end-to-end vẫn
chưa hoàn tất. Không coi thành công component M6 là toàn bộ SCRUM-314/315/316
hoặc M2 đã Done. Kết quả Linux component ghi trong hướng dẫn gói; kiểm tra
Windows/material thật và rollout phải có log riêng trước nghiệm thu runtime.

## M6 runtime đã đạt; bước 5A bổ sung durable upload journal

Người dùng đã xác nhận ngày 2026-10-23: 70/70 service-auth, build Identity và
Profile, 20/20 Profile identity, 220/220 Analysis và kiểm tra chỉ đọc bốn client
credential đều đạt. Hai container đã cập nhật; health, Identity -> Profile
provisioning ACTIVE/READY, Profile -> Identity context và ETag đều đạt bằng
Development fixture. Evidence:
~/.aura/checks/m6-service-auth-runtime-bc5eeb66030849eba90d05217ab948c3.json.
Không coi đây là lần kiểm thử lại Firebase thật hoặc toàn bộ task M6 đã Done.

Bước 5A thêm migration Analysis 002_upload_recovery.sql (hai bảng), state machine,
PostgresUploadRecoveryRepository và acceptance fence trong transaction của
PostgresUploadRepository. Intent reserve/blob, kết quả đã biết và kết quả chưa
biết được phân biệt rõ; abort không hoàn tất khi còn outcome chưa giải quyết.
Các attempt abort được giữ lại; chỉ generation của ảnh đã release mới tăng khi
chuẩn bị lại, và object key có UUID attempt để tách retry khỏi blob cũ.

Cùng scope/key khác fingerprint trả 409 cả khi chưa có accepted request. Checkpoint
có revision chống ghi đè; plan/binding bị đóng băng, runtime không có quyền UPDATE
các cột đó hoặc DELETE evidence. ACCEPTED được ghi cùng transaction batch/image/
job/outbox; abort fence chặn late commit và acceptance chặn cleanup. Scan 15 phút
chỉ đọc ứng viên; không tự release hoặc coi timeout là bằng chứng abort.

Migration runner có chín lượt migration và final grants bảo vệ hai bảng mới.
Bộ development configuration mock tăng từ 35 lên 36 checks. CI có state checks
riêng; legacy repository fixtures vẫn dùng schema 001 nếu chưa chạy migration mới.

Gói chưa sửa CreateAsync của UploadService, chưa có orchestration/reconciliation
worker gọi journal, chưa map endpoint và chưa deploy Analysis. Không chạy
Initialize-AuraDevDatabase.ps1 lên database ứng dụng ở bước này. Chỉ runner
Test-AuraAnalysisJournal.py --run được dùng để tạo database cô lập và chạy SQL.
Checkpoint/state component đã kiểm trên Linux theo hướng dẫn gói; PostgreSQL thật
và build Analysis phải có log tại máy người dùng trước khi chốt bước 5A.
Xem docs/development/analysis_upload_recovery.md để biết ranh giới và bước tiếp.


## Bước 5B — resumable coordinator (2026-10-24)

Log 5A xác nhận 41/41 state checks, 220/220 Analysis, 36 config mock checks và
12/12 PostgreSQL groups đạt; database thử được dọn. Evidence:
~/.aura/checks/m2-analysis-journal-aa1a714f793f4416a654be45348306a9.json.

UploadPlanningService dùng chung validation, image inspection, Profile authorization,
idempotency session và dedupe planning; tách từ UploadService, không đổi các luật
đó. PreparedUpload sở hữu session và dispose cả khi preparation/coordinator lỗi.
UploadService.CreateAsync cũ giữ hành vi cho legacy/domain tests, không phải entry
point được duyệt cho runtime. Entry point mới là DurableUploadService.CreateAsync,
nhận IUploadRecoveryJournal, ICreditReservations và IPrivateFileStorage.

Coordinator ghi intent trước reserve/put, rồi ghi acknowledgement đã kiểm chứng.
Khi retry nó dùng plan/time/object key/reservation key/generation đã lưu. Reserve
ack hoặc blob ack bị mất được replay cùng key. DB commit lỗi/ack mất không gây
release/delete; accepted replay dùng response gốc. Quyền Profile được kiểm lại
mỗi request. Hai request cùng key vẫn bị upload session serialize. Nếu canonical
dedupe đổi trong lúc outage, request giữ chờ đối soát, không tự queue ảnh trùng.

Mỗi Prepare cho attempt PENDING/ABORTING đang có tăng revision để claim ownership
trong khi caller giữ upload session. Writer cũ sau khi mất connection/lock không
thể ghi acknowledgement bằng revision trước. Accepted/aborted attempt không bị
claim lại như pending. PostgreSQL fixture điều chỉnh để kiểm tra revision claim;
do thay behavior này phải chạy lại 12 nhóm PostgreSQL trên database riêng.

402 của reserve mới, có kết quả chắc chắn, tạo abort fence rồi release các hold
đã biết; release mất ack được replay cùng key trước khi hoàn tất abort. Retry sau
abort dùng generation tăng riêng cho ảnh đã release. Với reserve đã mất phản hồi,
402 của lần replay không chứng minh lần gọi trước không tạo hold muộn: giữ pending
và trả 503 retryable, không ghi CreditDenied hoặc giả định abort đã hoàn tất.

Chỉ đường abort do thiếu credit đã biết, chưa có blob intent, được coordinator
hoàn tất. Lỗi storage, cancel và unknown outcome giữ state để resume. Reconciler
nền cho request bị bỏ dở, unknown reserve không xác minh được, dedupe thay đổi và
blob orphan/late write vẫn còn phải triển khai. Không release do timeout đơn thuần.

241/241 Analysis xUnit (21 test coordinator mới) và build API đã đạt trên Linux
.NET 10.0.401, cùng 41 state checks. Billing/storage/journal trong 21 test mới là
fake phục vụ fault injection, không phải bằng chứng HTTP Billing/Azurite/Firebase
thật. JournalChecks đã build; 12 nhóm DB cập nhật cần log trên máy người dùng.

Không đổi migration/credential/Compose ở gói 5B, chưa đăng ký composition root hoặc
map route trong Program. Chưa deploy Analysis. Chỉ áp dụng source, chạy tests và
runner cô lập. Nối HTTP/DI/config và các kiểm tra runtime là bước tiếp theo;
không coi thành công component này là SCRUM-303/304 hoặc toàn Sprint 2 đã Done.


## Bước 5C — HTTP boundary và composition có cờ bật (2026-10-24)

Log 5B xác nhận 241/241 xUnit, 41/41 state checks, 12/12 PostgreSQL groups đạt,
database thử đã dọn; application DB/global roles không đổi. Evidence:
~/.aura/checks/m2-analysis-journal-fadb9cc0b9a947a49eebed01c6c3deff.json.

Gói 5C map POST analyses, GET batches/{id}, GET analyses/{id}/files/{id}/access.
Authentication dùng IAnalysisUserIdentityResolver hiện có, role gate trước đọc
body, requester từ context authoritative. ClinicContextId query là selector UUID
không đáng tin, Profile vẫn quyết định quyền patient/assignment/tenant; contract
thêm parameter tùy chọn này cho ba operation, không đổi response/event/job schema.
Không nhận requester/owner/tenant từ multipart; field lạ hoặc lặp bị 400.

Multipart được đọc từng section, kiểm giới hạn aggregate/file trong lúc đọc,
buffer RAM 64 KiB rồi spill vào temp theo ASP.NET, không dùng filename làm path.
Buffer được dispose khi thành công/lỗi/cancel. Chặn request quá lớn 413 trước
handler; thiếu/sai key 400; sai content type 415; ảnh vẫn được decoder của
coordinator kiểm tra. Upload concurrency bounded, không xếp hàng vô hạn.
202 chỉ sau coordinator trả kết quả, Location và Idempotent-Replayed theo result.
Batch giữ Profile authorization trước item read. File access kiểm thêm owner
binding và trả no-store. Error envelope/correlation được chuẩn hóa, không phản
chiếu exception, token hay dependency diagnostic. Response error cũng no-store.

AddAnalysisRuntime đăng ký concrete clients/PostgreSQL/storage/coordinator/read
services. Bắt buộc own client analysis-api và connection role aura_analysis_app,
origins explicit, upload limits/pool/concurrency hợp lệ. Không tạo DB/schema hay
gọi Billing lúc startup. Program chỉ bật khi Analysis:EnableHttpEndpoints=true;
mặc định false. Gói không đổi Compose, env, credential hay bật cờ này. Không
deploy Analysis và không chạy application migration ở bước kiểm tra source.

HTTP tests chạy TestServer trong memory với fake identity/upload/DB/storage.
Chúng kiểm tra routing/binding/roles/errors/limits/authorization; không phải
Firebase -> Gateway -> PostgreSQL/Billing/Azurite integration thật. Các test
coordinator và journal trước vẫn được chạy. Không claim 12 PostgreSQL groups
được chạy lại tại máy chuẩn bị. Không đổi SQL/journal behavior ở gói này.

Kết quả chuẩn bị: build API và 270/270 xUnit đạt trên Linux/.NET 10.0.401,
0 fail/skip; có 29 case HTTP/config mới, 17 analyzer warnings đã có, không thêm
compiler warning mới. Test host thật với flag false trả health 200/upload 404
và đã dừng sau kiểm tra. Sáu tình huống installer kiểm bảo toàn source/index/HEAD
đạt. Test multipart chunked không Content-Length vẫn chặn quá giới hạn, slot được
trả sau lỗi, và buffer spill giữ đúng bytes cho handler.

Chưa có analysis detail/list/retry/review/report HTTP, NiFi ingest, reconciler
nền/GC, hoặc activation/runtime end-to-end. Trước bật feature phải nạp own
Analysis credentials/config, apply migration bằng quy trình đã kiểm tra và
hoàn thiện kiểm tra side effect/recovery thật. M2/Sprint 2 chưa được coi Done.


## Bước 5D — cấu hình runtime và readiness, chưa activation

Log người dùng xác nhận 270/270 tests và build API đạt trên Windows sau 5C.
Gói 5D nạp ANALYSIS_SERVICE_CLIENT_SECRET từ credential analysis-api hiện có;
không tạo/đổi key, registry hay secret. Set-AuraDevEnvironment thêm Analysis vào
đợt nạp atomic sau validation, và loader riêng chỉ nạp Analysis để không cần
nhập lại năm password DB khi cửa sổ PowerShell hiện tại đã có cấu hình.
Dev Compose thêm explicit dependency origins và own client analysis-api, giữ
Analysis__EnableHttpEndpoints="false" cố định. Storage tiếp tục dùng env_file
Docker đã có (Azurite backend azurite:10000, public localhost:10000); không mount
issuer/private-key/client credential directory vào Analysis. CI nạp/mask Analysis
secret được sinh riêng trong job để validate dev Compose không bị thiếu biến.

/health vẫn là liveness. /health/ready trả 503 disabled khi flag tắt, không gọi
DB/storage. Khi bật, readiness có deadline tổng 5 giây: kiểm role aura_analysis_app,
đủ 10 bảng, các grant tối thiểu cho acceptance và journal; phát hiện thiếu migration
hoặc table-level UPDATE/DELETE thừa ở journal. Storage kiểm cả ba container đã
tồn tại và private qua GetAccessPolicy. Chỉ SELECT/metadata/ACL read, không tạo
schema/container/blob, không đổi quyền hoặc đặt Billing reservation. Error chỉ có
status not_ready, không phản chiếu exception hoặc connection string.

Readiness KHÔNG xác minh Firebase, quyền gọi Identity/Profile/Billing, account,
wallet, hoặc toàn bộ dữ liệu business. Schema/grant check không phải migration
runner hay full schema audit. Real integration/reconciliation vẫn cần bước sau.
Không deploy/restart, bật flag hoặc migrate database ứng dụng ở bước source này.

Analysis xUnit thêm sáu case readiness/fail-closed. Development configuration mock
thêm một check Analysis secret, tổng 37. Journal console thêm nhóm thứ 13 để kiểm
SQL readiness bằng role thật trong database cô lập; cần chạy lại runner --run.
Không thay SQL migrations. Máy chuẩn bị không có PostgreSQL/Azurite/Docker thật
để chứng nhận readiness runtime; mọi kết quả real DB/ACL cần log riêng.

Kết quả chuẩn bị: 276/276 xUnit và build API đạt trên Linux/.NET 10.0.401,
chỉ còn 17 analyzer warnings đã có; JournalChecks 13-group source build đạt.
Sáu tình huống installer đạt. PowerShell config suite 37 checks và Compose config
chưa chạy tại máy chuẩn bị, cần log Windows; không coi YAML/source inspection là
Docker/runtime PASS. Readiness dùng truy vấn metadata/quyền tách riêng từng
privilege, không dựa vào phép kiểm tra comma-list có ý nghĩa any-of.

Nguồn: https://www.postgresql.org/docs/17/functions-info.html (privilege inquiry và to_regclass).
