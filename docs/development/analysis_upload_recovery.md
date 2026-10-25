# Durable upload recovery — foundation for Sprint 2 acceptance

## Current status, 2026-10-24 (after runtime 5J, source 5K)

The running acceptance path uses DurableUploadService behind the HTTP feature flag.
The user certified 5J against a private candidate: one Firebase PNG upload accepted,
replay/conflict and batch/private-file access passed, one TEST credit reserved,
one ACCEPTED recovery attempt and one private Blob retained. Two outbox rows and
the image/job remain QUEUED; no Worker/publisher/AI execution was certified. The
primary service remains disabled. Source5K has not been deployed by this patch.

Evidence: `~/.aura/checks/m2-upload-a85bec2edca34a988e05014efc3412bd.json`.
This is the user-reported runtime result; its local JSON was not independently
read on the author machine. Do not rerun guards that require empty Analysis tables
or rewrite their source hashes to fit the now-populated application database.

Source5K adds the canonical `existingAnalysisId` to newly created DUPLICATE accepted
items and authorized batch items. Queue responses omit the nullable field. Old
stored accepted responses without it remain replayable without rewriting fields;
batch reads obtain the reference from existing DB metadata. Frozen QUEUED journals
retain their prior serialized shape. No migration, financial/storage lifecycle or
permission-policy change is part of this patch. Duplicate items still store a Blob
under current behavior; this patch does not implement dedupe GC.

Known-credit abort and client-driven resume exist; background reconciliation/GC
for abandoned requests, unresolved reserve, canonical-dedupe changes, storage
unknown outcomes and late writes remains open. Passing5J does not certify those
failure paths or authorize primary activation. See the integration status document
for actual-result evidence and remaining SCRUM-303/304 requirements.

## Historical foundation (5A; later sections describe subsequent patches)

This is an internal persistence/state component, not a new public API or proof of
end-to-end upload recovery. Analysis REST/Event/Job schemas remain at 0.6. The
existing UploadService.CreateAsync is not switched to this component in this
patch. Program still exposes no business upload endpoint. Do not deploy Analysis
or migrate application databases until orchestration is connected and tested.

## Persisted state and binding

A new migration, analysis/002_upload_recovery.sql, adds:

- upload_recovery_operations: immutable tenant/requester/key, fingerprint version,
  batch, patient, owner and Billing owner. First intent freezes a key's payload
  even if no accepted request exists yet. No cross-service foreign keys.
- upload_recovery_attempts: frozen prepared BatchPlan, attempt UUID/number,
  originals container, phase, revision, per-image effect checkpoints and abort
  reason. Aborted attempt history is retained. One non-aborted attempt per
  operation is enforced by a partial unique index. Due scans use a partial index
  on updated_at/id for PENDING/ABORTING only.

Plans are bounded to 1 MiB and effect JSON to 128 KiB/100 images. The plan is stored
once per attempt; checkpoint writes update only lifecycle/effects columns. Runtime
cannot rewrite operation bindings/plans or delete either table's records. Final
service grants reapply these restrictions after their broader initial grants.
The original applied 001 migration is unchanged. Initialize-AuraDevDatabase now
lists nine migration invocations, including final ownership/grants; this patch's
isolated test runner never executes that global role-management migration.

## State transitions

PENDING -> ACCEPTED happens only inside the transaction inserting the accepted
request, images, jobs and outbox. All chargeable reservations and all blob writes
must have acknowledgements, and the exact frozen plan must match. A bad outbox
insert after the fence rolls everything back, including ACCEPTED. Ambiguous
COMMIT acknowledgement requires a fresh durable read; never compensate from the
exception alone.

PENDING -> ABORTING is a durable fence against future acceptance. It is not a
statement that remote requests cannot still be in flight. The coordinator must
resolve each previously recorded reserve/put intent through the idempotent remote
contract before acknowledging cleanup. ABORTING -> ABORTED requires reserve
intents settled as denied or released, and every blob intent confirmed deleted.
ACCEPTED cannot authorize abort/release/delete. Unknown reserve/put outcomes keep
the attempt nonterminal. Timeout alone is never proof of an abort.

Each reserve/blob intent must be committed BEFORE sending its external request;
acknowledgement follows a verified remote result. Checkpoint writes have their own
short transaction and revision check. A stale attempt/revision returns retryable
409 ERR_REQUEST_IN_PROGRESS, without an automatic retry. Replaying an already
recorded identical acknowledgement at the current revision is a no-op. Actor,
patient, tenant and Billing owner come from authenticated/authoritative context,
not request headers or client-supplied recovery state.

Fifteen-minute scanning returns up to 100 pending/aborting candidates. It does not
change state, release credit or delete files. Unavailable dependencies leave work
pending reconciliation. A legacy accepted request also prevents abort. A legacy
writer cannot bypass a journal that already owns its scope; unjournaled legacy
commits and initial journal preparation serialize on the same recovery lock.

## Retry identity

Preparation of a new attempt is allowed only after a confirmed ABORTED attempt.
The same tenant/requester/key remains bound to the original fingerprint, patient,
owner and stable analysis/file IDs. Per-image generation advances only if the
previous reservation was confirmed released; a confirmed 402 with no reservation
keeps its generation. Reserve keys include generation; release keys are stable
within the attempt. Billing remains responsible for validating server-side
reservation generation. This is a pre-acceptance transport/abort retry; it is not
manual Worker retry of an already accepted analysis.

Object keys become tenant/batch/analysis/attempt (four UUID segments), within the
existing private-storage adapter's permitted format. A delayed write belonging
to an old attempt cannot replace the next attempt's blob. Old plans are retained
for future orphan reconciliation. No bytes, JWT, caller secret or SAS URL are
stored in the recovery journal or written to the test evidence. Plans do contain
existing internal metadata such as synthetic filenames/IDs in tests; production
metadata requires the same protected database handling as accepted Analysis data.

## Tests and boundaries

JournalStateChecks links production state/port/fingerprint/failure sources, needs
only the .NET SDK, and has 41 state checks. They cover write-ahead intents, unknown
outcomes, replay, abort/accept exclusion, generation/key changes, corruption and
serialization. These checks were run on Linux/.NET 10.0.401. No remote side effect
is simulated as a real Billing/storage integration pass.

JournalChecks references the actual Analysis API and has 12 groups for PostgreSQL
17 with an actual restricted aura_analysis_app login: durability after reopening,
scope/fingerprint, concurrent revisions, unknown outcome/abort, generation,
frozen-plan and legacy-bypass rejection, atomic accept/replay, rollback after
fence, abort-before-commit, due scan, runtime permissions and corrupt state/cancel.

Use Test-AuraAnalysisJournal.py, default check-only, then --run. It records a plan
before build/database creation, creates only aura_m2_journal_<random UUID> with a
matching marker, applies the unchanged core migration/assertions followed by the
new migration twice, runs the console with the runtime password in child env,
and drops only that database after all 12 groups pass. Failure or unknown outcome
retains it. No application database, global role, wallet, reservation, blob or
credential file is changed. No real Billing/storage HTTP requests are sent.

The author restored dependencies and built the full Analysis API on Linux with
.NET SDK 10.0.401, then ran 220/220 Analysis xUnit tests successfully (0 failed,
0 skipped; 17 existing analyzer warnings). The production recovery repository,
fence and PostgreSQL console source also compiled against Npgsql 10.0.3. Eight
runner control-flow tests passed using fake commands, covering preflight,
ownership/cleanup gates, failure retention and password redaction. Six installer
scenarios verified preservation of previous edits, index and HEAD.

There is no PostgreSQL service in the preparing environment: the 12 real database
groups are NOT claimed passed here. They must pass on the developer's machine.
The configuration suite uses fake Docker; it does not apply SQL to any database.

## Next integration step

Connect the coordinator to this journal before enabling HTTP: validate/authorize,
acquire the upload session and dedupe locks, prepare/freeze the plan, persist each
intent, call the existing idempotent adapters, record verified responses, and
commit only the journal's prepared plan. Resume reads must reuse that plan rather
than recompute dedupe/time/keys. Bound active upload concurrency and pool capacity
because a held upload transaction and an independent checkpoint may need two
connections. Never start external work if an intent write failed or is unknown.

Recovery must resolve remote in-flight outcomes, authorize abort through the DB
fence, retry release/delete idempotently, retain unresolved work, and periodically
revisit old attempt blob keys (including delayed-write/orphan cases). It must not
release accepted QUEUED/PROCESSING work. This component alone does not implement
that coordinator, background job, orphan collector, HTTP wiring, service ingest,
Firebase -> Analysis integration, or Worker/publisher processing. Those remain
open; the unsafe legacy compensation path is not approved for deployment.

References:
- https://www.postgresql.org/docs/17/explicit-locking.html#ADVISORY-LOCKS
- https://www.npgsql.org/doc/basic-usage.html


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
