# M3 Sprint2 — Billing integration actual result tới6F

Ngày cập nhật2026-10-24. Baseline Git87af21da20489eda6d4899f7e1f3a428b24960db,
nhánh fix/sprint2-closeout-20261023 và local patches chưa commit. Nguồn là log
người dùng gửi; chưa đọc độc lập các evidence JSON trên Windows. Không có thay đổi
Jira/GitHub/Confluence, approval chính sách hoặc tuyên bố toàn bộ task Done.

## Kết quả đã chạy và phạm vi

| Bước | Actual result người dùng cung cấp | Evidence dưới ~/.aura/checks |
| --- | --- | --- |
| 6A | 6/6 PostgreSQL17 schema groups: migration2 lần, price/version/order snapshot, reservation/terminal guards, runtime DML/ledger restrictions; DB riêng đã xóa | m3-billing-schema-5802c154012e4a7ebddb535e92075969.json |
| 6B | FAIL ở same_key_concurrency sau2 nhóm; DB lỗi giữ lại, không tính PASS | m3-billing-repository-5bf34242665f4f1cbf1669ed5db7af73.json |
| 6C | Source thêm transaction scope lock; same-key/distinct concurrency PASS; fail release replay với42501 sau5 nhóm. DB lỗi giữ lại | m3-billing-repository-1cac8bd51d964c7da98c3f8ebadb7ad4.json |
| 6D | 10/10 actual CreditReservationRepository với login aura_billing_app: reserve/replay/conflict/concurrency/empty wallet/release/capture/generation/terminal race/ledger integrity; DB riêng đã xóa | m3-billing-repository-7a4f12a54fba4deda94dd04024ff086d.json |
| 6E | Full Billing Release build trên Windows; deploy image mới riêng Billing;5/5 GET/security/postconditions PASS | m3-billing-deploy-5e3808f094244419945853426365caf6.json |
| 6F | 13/13 HTTP replay/conflict/validation/scope groups trên image mới; application/container baseline giữ nguyên | m3-billing-http-9a199cb4853d4fa393df900e7352c174.json |

6A SQL fixtures không chứng minh application repository;6D gọi repository thực,
không giả lập DB nhưng không có HTTP/auth/Profile;6F bổ sung wire/auth/Profile
denial và exact replay, không phải positive terminal HTTP/event consumer.
Build và các kiểm tra tooling tại môi trường chuẩn bị không thay actual runtime.

## Hai lỗi repository đã sửa

1. Reserve cùng tenant/key: key lookup trước generation có thể thấy snapshot khác
   giữa hai statement. Thêm pg_advisory_xact_lock theo tenant/key trước lookup đầu
   tiên, giữ nguyên fingerprint, unique constraints, generation và wallet lock.
   6C/6D kiểm hai request chờ cùng controlled lock rồi một Created/một Replayed.
   Log6B không có enum từng response, không suy diễn duy nhất interleaving đã xảy ra.
2. Transition key lookup dùng SELECT FOR UPDATE trên append-only ledger gây42501
   với runtime chỉ SELECT/INSERT.6D bỏ khóa đọc riêng ở ledger; reservation/wallet
   locks và unique terminal/index vẫn giữ. Không cấp UPDATE/DELETE ledger.

Các lỗi package Windows path, EOF/hash normalization, git whitespace và helper
HTTP trùng tên module đã được sửa trong các revision. Chúng không phải bằng chứng
app-data corruption hoặc financial mutation. Guard/backup và DB lỗi được giữ.

## Wire mapping đã đối chiếu

- Billing OpenAPI0.1.0 tại contracts/billing-openapi.yaml, có trong baseline87af21d;
  catalog public là legacy DEMO compatibility, không phải giá được phép đặt mua.
- POST internal reservations:201 new,200 exact replay với Idempotent-Replayed=true;
  UUID key, tenant/actor/patient/billingOwner/analysis/units1/generation>=1.
  Payload đổi cùng key:409 ERR_IDEMPOTENCY_CONFLICT. Thiếu credit:402
  ERR_INSUFFICIENT_CREDIT.201/402 trên image mới chưa trực tiếp kiểm qua HTTP6F.
- Read/reserve/release của analysis-api cần các scope billing:read/reserve/release
  riêng và audience aura-billing. Billing re-resolves Profile trước reserve/replay.
  analysis-api không được cấp billing:capture; issuer403 và wrong-scope HTTP403
  đã đạt. Không mở rộng registry để vượt capture test.
- Capture/release terminal conflict được6D kiểm bằng repository enum; không nhầm
  với409 idempotency conflict qua HTTP6F. Capture không debit thêm available credit;
  release trả lại một credit; refund sau capture là quyết định khác chưa đóng.

## Runtime/data hiện tại

Billing mới chạy Development/TEST/non-mock sau6E. Ví5J vẫn balance0, một RESERVED
hold, CREDIT1/RESERVE1.6F chỉ replay key/payload cũ, không tạo credit/reservation/
ledger; negative terminal requests dùng reservation ID đã xác minh không tồn tại.
Original reservation ID/state/createdAt giữ nguyên. Data/roles/ACL metadata và
containers giữ nguyên, Analysis business HTTP vẫn disabled. Analysis vẫn hai
batch/image, một QUEUED job, hai outbox pending, ba journal attempts: hai ACCEPTED,
một ABORTED từ5M. Không Worker/publisher/AI, Blob, successful release/capture,
refill, application migration hoặc cleanup DB lỗi được thực hiện trong6E/6F.

## Coverage và các quyết định còn mở

Contract/test-plan giữ15 mã S307-BILL. Chỉ02/03 được đánh PASS HTTP đầy đủ trong
phạm vi đã mô tả; các composite case còn lại ghi PARTIAL/NOT RUN để không coi
repository coverage là event/HTTP acceptance. Không promote FR-11/12 hoặc FR/NFR
khác từ kết quả một phần. Notification/Chat test IDs và trạng thái vẫn chưa đổi.

- SCRUM-305: refund sau capture, retention/idempotency policy, contract governance
  và quyết định nghiệp vụ còn mở chưa được M1/người có thẩm quyền chốt qua log này.
- SCRUM-306: schema/repository hai lỗi đã kiểm, deploy mới đã đạt; catalog đang
  PoC, official price/version/order APIs và Redis invalidation còn cần đối chiếu.
  Không suy diễn6A price snapshot SQL là API order/payment đã hoạt động.
- SCRUM-307: bổ sung mapping/scoped actual results trong contract/test-plan/RTM;
  event-to-generation correlation, consumer, reconciliation15 phút/abort-to-release,
  targeted TEST/mock-to-LIVE boundary và positive terminal HTTP chưa nghiệm thu.
- Chat ownership/storage/retention và assignment-revocation history cần quyết định
  thiết kế M1/M2/M3; FCM không phải message store. Không tự chọn Mongo hoặc retention.
  Notification inbox/event consumer/Chat backend không được chứng minh bởi Billing.

Tiếp theo chuyển sang rà M4 SCRUM-308/309/310: preprocessing, feasibility risk/OCT,
contract/schema/examples và test hiện tại. Không cần chạy lại6A–6F cho patch tài
liệu6G. Source baseline sau6G có84 entries, nên runner pin80 cũ sẽ dừng là đúng;
không sửa guard/hash, checkout đè changes hoặc xóa dữ liệu để ép PASS.
