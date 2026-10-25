# Kế hoạch sửa và nghiệm thu Sprint 2

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


Ngày lập: 2026-10-23. Baseline: `87af21da20489eda6d4899f7e1f3a428b24960db`.
Nguồn: kế hoạch 7 ngày do M1 cung cấp, 18 task Jira Sprint 2 và mã nguồn
`develop`. Jira lúc đối chiếu có 5 Done, 13 In Review. Bảng này là danh sách
sửa/nghiệm thu; không tự thay đổi trạng thái Jira hoặc xác nhận task đã Done.

## Phạm vi

Mục tiêu tích hợp Sprint 2 là đăng nhập → bootstrap/Profile → upload →
private storage → Billing reserve → transaction Job/Outbox → `QUEUED`.
Worker/publisher đầy đủ thuộc Sprint 3. Báo cáo khả thi risk/OCT được nghiệm
thu theo kết luận và giới hạn có căn cứ, không yêu cầu model risk/OCT thật.
SCRUM-307 yêu cầu chốt thiết kế/ownership Chat, không yêu cầu xây Chat backend
trong bước sửa Sprint 2 này. Payment provider thật ngoài phạm vi Sprint 2.

## Danh sách đủ 18 task

| Task | Owner | Việc cần sửa hoặc xác nhận trước khi nghiệm thu |
| --- | --- | --- |
| SCRUM-299 / W2-M1-01 | M1 | Giữ Gateway route/CORS/limits/timeout/correlation; kiểm lại route upload/batch/file sau khi M2 có endpoint. |
| SCRUM-300 / W2-M1-02 | M1 | Giữ dữ liệu/credentials; đối chiếu private storage, SAS, runtime roles và quyền service theo adapter thật của M2. |
| SCRUM-301 / W2-M1-03 | M1 | Đồng bộ hướng dẫn đủ tám migration, service identity, sơ đồ/cấu hình và trình tự môi trường mới/cũ. |
| SCRUM-302 / W2-M2-01 | M2 | Chạy migration lặp và bộ assertion hiện tại trên PostgreSQL 17 cô lập; kiểm đủ 8 bảng, idempotency, tenant FK, role và append-only. |
| SCRUM-303 / W2-M2-02 | M2 | Nối HTTP/auth/DI, PostgreSQL repository, Billing adapter và Azurite; giữ validation/dedupe/idempotency; chỉ trả 202 sau durable commit; giải mã ảnh thật và xử lý commit chưa rõ kết quả. |
| SCRUM-304 / W2-M2-03 | M2 | Bổ sung HTTP/dependency integration tests cho upload, batch, file access, ownership, concurrency và failure; đồng bộ contract/ERD/sequence với code. |
| SCRUM-305 / W2-M3-01 | M3 | Chốt nguồn contract, idempotency/generation và các quyết định còn mở; tách refund sau capture khỏi release. M1 phải xác nhận quyết định nghiệp vụ cần thẩm quyền. |
| SCRUM-306 / W2-M3-02 | M3 | Đối chiếu package/version/snapshot/cache với đường API; kiểm schema và transition reserve/capture/release đúng phạm vi; giữ TEST/LIVE tách biệt. |
| SCRUM-307 / W2-M3-03 | M3 | Đồng bộ Billing–Analysis contract, test plan/RTM bằng actual result; chốt Chat ownership/storage/retention ở mức thiết kế cùng M1/M2. |
| SCRUM-308 / W2-M4-01 | M4 | Rà preprocessing, ảnh lỗi, cấu hình và output; chạy lại bộ test theo code hiện tại; phân biệt module dùng chung với việc đã nối runtime. |
| SCRUM-309 / W2-M4-02 | M4 | Rà kết luận risk/OCT, nguồn dữ liệu và giới hạn; giữ mock trung thực, không mở OCT runtime hoặc tuyên bố giá trị lâm sàng chưa kiểm chứng. |
| SCRUM-310 / W2-M4-03 | M4 | Đối chiếu request/version/warning/quality/retry với schema thật; đồng bộ YAML/JSON/examples/dataset card; tách hợp đồng khỏi Worker chưa triển khai. |
| SCRUM-311 / W2-M5-01 | M5 | Giữ layout/components; tách demo role cố định khỏi luồng tài khoản thật; rà router và trạng thái loading/error. |
| SCRUM-312 / W2-M5-02 | M5 | Nối login → bootstrap → Identity/Profile; lấy patient từ context tin cậy; upload thật; giữ key/payload khi retry và hiển thị lỗi/trạng thái theo contract. |
| SCRUM-313 / W2-M5-03 | M5 | Cập nhật tests/README/API mapping theo luồng mới; chạy lint/test/build và xác nhận UI với API thật ngoài bộ mock. |
| SCRUM-314 / W2-M6-01 | M6 | Giữ UID mapping không link theo email; loại fallback service secret/key không an toàn; kiểm malformed/missing config, Firebase token và account status. |
| SCRUM-315 / W2-M6-02 | M6 | Giữ bản sửa Profile identity/ETag; đối chiếu Clinic/membership/assignment, tenant isolation, role và provisioning theo schema/code thật. |
| SCRUM-316 / W2-M6-03 | M6 | Bỏ secret demo/fallback ID trong script test; đồng bộ sequence/OpenAPI/data dictionary và bằng chứng auth/permission/validation. |

## Trình tự thực hiện

1. M1: đồng bộ baseline/tài liệu, giữ nguyên credentials và dữ liệu. Bước đầu
   sửa các tài liệu lỗi thời; chưa chạy migration hoặc thay cấu hình runtime.
2. M2: xác nhận schema trên PostgreSQL 17, chuẩn bị adapter và HTTP theo
   contract. Chốt các dependency Profile/Billing cùng M3/M6 trước khi chạy thật.
3. M3: sửa các chỗ contract/price/version/transition cần thiết; kiểm thử các
   thay đổi bằng schema/API thật. M1 chốt các quyết định nghiệp vụ còn mở.
4. M4: sửa các sai lệch tìm được ở preprocessing/contract/tài liệu và kiểm lại.
5. M5: nối bootstrap/Profile/upload/retry với backend đã đạt các bước trước.
6. M6: hoàn tất cấu hình service auth, quyền và script/tài liệu; các sửa đổi
   đang chặn M2/M3 được ưu tiên trước trong cùng chuỗi công việc.
7. M1 kiểm lại toàn luồng và đủ 18 task; tạo bản tổng hợp actual result để
   người dùng tự cập nhật Jira, push hoặc mở PR khi phù hợp.

## Bằng chứng đã có và giới hạn

- CI commit baseline: Repository checks và AURA CI đạt; xem
  [AURA CI](https://github.com/CattyGame/XD-PMHDT/actions/runs/39929651879).
- Người dùng đã chạy Billing reserve 6/6 nhóm và replay sau restart giữ dữ
  liệu; không suy rộng thành capture/release/reconciliation đã đạt.
- Người dùng đã chạy Firebase qua Gateway đến Identity/Profile, PATCH/ETag
  và 12/12 ca HTTP âm; không suy rộng thành mọi quyền Clinic/token lifecycle.
- Log SQL cũ và tests dùng fake HTTP/mock không chứng minh các adapter hoặc
  API thật đã hoạt động. Bộ configuration checks giả lập Docker không phải
  bằng chứng migration thực đã chạy.

Mỗi bước sửa phải ghi rõ tệp/commit, lệnh kiểm tra và kết quả thực chạy.
Không sửa Analysis schema 0.6 hoặc lựa chọn datastore Chat chỉ để vượt test.
Không in credentials, JWT, SAS hoặc dữ liệu sức khỏe trong output bàn giao.
