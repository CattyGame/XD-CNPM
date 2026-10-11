# AURA — Test plan và định nghĩa phép đo M3 v0.1

## 1. Phạm vi bàn giao

| Thuộc tính | Giá trị |
| --- | --- |
| Task | [SCRUM-58 — W1/M3 Test plan và định nghĩa phép đo](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-58) |
| Plan ID | W1-M3-5 |
| Phiên bản/ngày | 0.1 / 2026-10-11 |
| Baseline mã nguồn | develop tại `9ba879ed3e9a0aa7933c2168efccaac5b79551d0`, sau merge PR #25 |
| Trạng thái tài liệu | Đã biên soạn kế hoạch; các ngưỡng đề xuất cần xác nhận trước nghiệm thu sản phẩm |
| Người tiếp nhận thực thi | Assignee SCRUM-58 và các owner nghiệp vụ, AI, UI, backend/hạ tầng theo phạm vi |

Tài liệu thiết kế bộ kiểm thử cho toàn bộ **39 FR và 23 NFR**.
Hoàn thành SCRUM-58 là bàn giao test plan và phép đo; không có nghĩa tất cả tính năng toàn dự án đã triển khai hoặc tất cả test đã chạy.
Các kết quả đã báo trong phiên làm việc được tổng hợp ở mục 9 với đúng giới hạn.
Không yêu cầu tạo lại ảnh chụp hay chạy lại bộ test đã đạt chỉ để hoàn thành tài liệu này.

Nguồn chuẩn:
- [SRS v0.1](../requirements/srs_v0.1.md).
- [RTM v0.1](../requirements/requirements_traceability_matrix_v0.1.md): giữ nguyên mã TC-FR-01…39 và TC-NFR-01…23.
- [Clinic/campaign use cases](../requirements/clinic_campaign_use_cases_v0.1.md).
- [Admin/configuration use cases](../requirements/admin_configuration_use_cases_v0.1.md).
- [Profile/Billing/Notification design](../requirements/profile_billing_notification_design_v0.1.md).
- [Ma trận quyền và negative cases — SCRUM-281](https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/1966110).
- [Firebase/FCM implementation — PR #25](https://github.com/CattyGame/XD-PMHDT/pull/25).

Đọc các nguồn repo tại baseline trên khi tái lập. RTM có cập nhật mới trong đợt sửa M2–M5; khi dùng snapshot cũ:
không sao chép trạng thái cũ thành kết luận mới và không đổi toàn bộ requirement sang VERIFIED từ test một module.
Mỗi nhóm TC có nhiều biến thể; khi ghi kết quả dùng hậu tố, ví dụ TC-FR-09-foreground, TC-NFR-09-token và TC-NFR-09-TLS.

## 2. Chiến lược, ưu tiên và trách nhiệm

| Lớp | Phạm vi | Người thực hiện đề xuất |
| --- | --- | --- |
| Unit/component | Validation, state, crypto, callback/UI lifecycle | Owner module |
| Contract | OpenAPI/schema, examples, fixtures và lỗi | Backend + M2/M4 |
| Integration | API–DB, private storage, Worker–AI, billing, Firebase | Backend/hạ tầng và owner liên quan |
| E2E | Workflow thực theo bốn role và hai tenant | M3 phối hợp M2/M5 |
| NFR | Load, uptime, restore, encryption, privacy, accessibility | Hạ tầng/M4/M5; reviewer chính sách theo control |

P0: mất/rò dữ liệu, sai quyền, mất ảnh đã accepted, trừ tiền hai lần, tạo risk từ lỗi, mất nguồn gốc AI.
P1: luồng nghiệp vụ chính, contract, performance, khả năng phục hồi.
P2: tương thích trình duyệt, trình bày và tiện ích ngoài luồng chính.
Tất cả kiểm tra quyền và integrity trong mỗi TC là P0, dù happy path của TC là P1.
Owner thực thi, thời điểm và SHA được điền khi chạy, không tự gán người đã thực hiện.

Thứ tự chạy: contract/unit → integration và P0 quyền → E2E chính → NFR.
Đối với tính năng chưa có backend, ghi BLOCKED và điều kiện cần, vẫn giữ test case trong kế hoạch.
Đối với quy tắc chưa chốt (clinical trend, billing, export, workload), chốt oracle với tài liệu nghiệp vụ trước khi đánh giá PASS.

## 3. Môi trường và dữ liệu

| Môi trường | Dùng cho | Giới hạn |
| --- | --- | --- |
| Local mock/MSW | Component, scenario, client/contract | Không chứng minh backend hoặc latency thật |
| Local integration | Vite 5173, Notification API 5003, PostgreSQL publish 127.0.0.1:5432 | HTTP localhost không chứng minh production TLS/uptime |
| Staging thực | Backend/Worker/AI/private storage/payment sandbox và HTTPS | Cần ghi SHA của từng service, hardware, versions, region và network |
| Restore tách biệt | Khôi phục DB/file từ backup | Không restore đè dữ liệu đang dùng |

Dữ liệu chuẩn: Patient A/B, Doctor assigned/unassigned/revoked, Clinic A/B, Admin;
UID và device UUID độc lập, tenant không trùng. Có tài khoản disabled và token hết hạn.
Mọi ảnh/hồ sơ/PII canary là dữ liệu tổng hợp hoặc dataset có quyền sử dụng; không dùng bệnh nhân thật làm fixture.
Ảnh hợp lệ, hỏng, thiếu modality, quá giới hạn; batch 100 ảnh; Unicode; danh sách rỗng/phân trang;
analysis queued/processing/completed/failed/quality-reject; AI/config vA/vB; giao dịch sandbox trùng callback.

Giữ API credentials, service account, encryption key và database password ngoài repo.
FCM token/ID token không xuất vào report/log; chỉ ghi test ID, outcome và metadata không nhạy cảm.
Dữ liệu nghiên cứu qua pipeline ẩn danh theo policy; không mặc nhiên coi hash định danh là ẩn danh đầy đủ.
Sau mỗi lượt có thao tác ghi: hủy đăng ký thiết bị, xóa dữ liệu test và xác nhận cleanup.
Các test gây crash/load/restore chạy ở môi trường riêng đã xác định.

## 4. Điều kiện vào, dừng và kết thúc

Vào lượt thực thi khi: ghi baseline/environment; dependencies sẵn sàng; test accounts đúng quyền;
contract/expected behavior và dữ liệu đã xác định; có cách cleanup.
Test FCM thực cần permission, worker active, Firebase đúng project, API sẵn sàng và mock tắt.
Test push chỉ dùng endpoint development được bật rõ; không bật panel/endpoint test cho môi trường production.

Dừng lượt bị ảnh hưởng khi có rò dữ liệu, mất integrity, phụ thuộc hỏng hoặc môi trường không xác định.
Ghi lỗi/blocked thay vì chuyển test sang PASS từ thông báo HTTP 200 đơn lẻ.

Kết thúc một phạm vi nghiệm thu khi: mọi case trong scope có kết quả; P0 không còn FAIL;
P1 được xử lý hoặc có quyết định chấp nhận cụ thể; NFR được đo bằng protocol đã chốt;
cleanup hoàn tất. NOT_RUN/BLOCKED không tính PASS.
Kết thúc bàn giao tài liệu SCRUM-58 khi đủ mapping 39 FR/23 NFR, test data/steps/oracles,
phép đo, mẫu báo cáo và khoảng trống đã ghi; không đòi toàn bộ tính năng tương lai hoàn tất.

## 5. Test cases chức năng

Tiền điều kiện chung: môi trường mục 3 sẵn sàng, account/role đúng và scope có backend tương ứng.
Dữ liệu/tiền điều kiện riêng, bước thực hiện và oracle của từng case dưới đây.
Chạy mọi biến thể được liệt kê; nếu chỉ chạy một biến thể thì chỉ biến thể đó có kết quả.

### TC-FR-01 — FR-1: Đăng ký/đăng nhập email, Google/social

- Mục tiêu: đăng ký/đăng nhập hợp lệ; lỗi tài khoản/provider; phiên hết hạn.
- Tiền điều kiện/dữ liệu: Patient; tài khoản email mới, Google hợp lệ, token hết hạn.
- Các bước: Đăng ký; đăng nhập từng provider được cấu hình; lặp với email trùng, sai mật khẩu và phiên hết hạn.
- Kết quả kỳ vọng: Phiên hợp lệ đúng UID; lỗi không tiết lộ tài khoản; hết hạn yêu cầu xác thực lại.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-02 — FR-2: Upload một/nhiều ảnh Fundus/OCT

- Mục tiêu: ảnh hợp lệ/hỏng/quá giới hạn; nhiều ảnh; modality chưa hỗ trợ.
- Tiền điều kiện/dữ liệu: Patient; JPEG/PNG tổng hợp hợp lệ, ảnh hỏng, quá giới hạn, OCT.
- Các bước: Upload một rồi nhiều ảnh; thử từng loại không hợp lệ; truy cập file bằng Patient khác.
- Kết quả kỳ vọng: Lưu ảnh hợp lệ đúng chủ; lỗi từng ảnh theo contract; modality chưa hỗ trợ bị từ chối; file riêng tư.
- Loại: contract/component + integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-03 — FR-3: Xem AI result và risk

- Mục tiêu: completed hiển thị đúng nguồn/version; processing/failed không tạo risk.
- Tiền điều kiện/dữ liệu: Patient; analysis queued/processing/completed/failed/quality-reject.
- Các bước: Mở từng trạng thái; chuyển sang completed; đối chiếu response và AI version.
- Kết quả kỳ vọng: Chỉ completed hợp lệ có kết quả; AI tách doctor review; failed/quality-reject không tạo risk.
- Loại: contract/component + integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-04 — FR-4: Xem ảnh chú thích

- Mục tiêu: annotation đúng ảnh, quyền truy cập file và trường hợp file lỗi.
- Tiền điều kiện/dữ liệu: Patient; hai ảnh khác nhau và annotation tương ứng.
- Các bước: Mở overlay; đổi ảnh; thử URL annotation hết hạn và tài khoản khác.
- Kết quả kỳ vọng: Overlay đúng ảnh/kích thước; lỗi tải có hướng xử lý; không truy cập annotation ngoài quyền.
- Loại: contract/component + integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-05 — FR-5: Khuyến nghị/cảnh báo tự động

- Mục tiêu: nội dung có căn cứ, disclaimer, không cảnh báo từ failed/mock như kết quả thật.
- Tiền điều kiện/dữ liệu: Patient; kết quả hợp lệ và các trạng thái lỗi/mock.
- Các bước: Mở khuyến nghị; so rule/content version; thử failed, quality-reject và mock.
- Kết quả kỳ vọng: Nội dung theo rule đã duyệt, có giới hạn sử dụng; không suy ra chẩn đoán/cảnh báo thật từ mock/lỗi.
- Loại: contract/component + integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-06 — FR-6: Lịch sử phân tích cá nhân

- Mục tiêu: history, pagination, empty state và quyền sở hữu.
- Tiền điều kiện/dữ liệu: Patient; lịch sử tổng hợp nhiều trang và tài khoản rỗng.
- Các bước: Đọc lịch sử, đổi trang; đổi UID và sửa resource ID.
- Kết quả kỳ vọng: Đúng chủ/thứ tự; không lặp hoặc thiếu bản ghi; empty state rõ; không lộ dữ liệu người khác.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-07 — FR-7: Báo cáo PDF/CSV

- Mục tiêu: nội dung report, định dạng, version và download authorization.
- Tiền điều kiện/dữ liệu: Patient; báo cáo hợp lệ có AI version và review.
- Các bước: Xuất PDF/CSV; mở/parse; đối chiếu; thử tải với UID khác và liên kết hết hạn.
- Kết quả kỳ vọng: Nội dung/Unicode/version đúng; download được phân quyền; hết hạn không lộ file.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-08 — FR-8: Hồ sơ cá nhân/y tế

- Mục tiêu: xem/sửa own profile, validation, cấm sửa role/tenant.
- Tiền điều kiện/dữ liệu: Patient; profile hợp lệ/thiếu/sai định dạng.
- Các bước: Xem/sửa; tải lại; gửi thêm role, tenantId hoặc UID khác.
- Kết quả kỳ vọng: Lưu đúng trường cho phép; validation rõ; cấm đổi quyền/chủ sở hữu bằng payload.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-09 — FR-9: Thông báo kết quả sẵn sàng

- Mục tiêu: event-to-notification, permission, token binding và nhận push thật.
- Tiền điều kiện/dữ liệu: Patient A/B; thiết bị web có quyền push.
- Các bước: Đăng ký; phát sự kiện kết quả thật; nhận foreground/background; click; hủy đăng ký; thử B truy cập thiết bị A.
- Kết quả kỳ vọng: Nhận nội dung chung đúng người; click mở AURA; không lộ dữ liệu y tế/token; sau hủy không gửi; lặp không tạo thiết bị trùng.
- Loại: contract/component + integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-10 — FR-10: Nhắn tin bác sĩ được phân công

- Mục tiêu: gửi/nhận đúng quan hệ, unauthorized, revoke assignment.
- Tiền điều kiện/dữ liệu: Patient; Doctor được phân công và Doctor ngoài phân công.
- Các bước: Gửi/nhận; thử sai recipient; thu hồi assignment rồi gửi lại.
- Kết quả kỳ vọng: Tin đúng quan hệ; backend chặn ngoài quyền và sau thu hồi; lỗi delivery không giả thành công.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-11 — FR-11: Mua/gia hạn gói

- Mục tiêu: purchase/renew, payment failure, duplicate callback.
- Tiền điều kiện/dữ liệu: Patient; gói hợp lệ, hết hạn và payment sandbox.
- Các bước: Mua/gia hạn; gây payment thất bại; replay callback; tải lại entitlement.
- Kết quả kỳ vọng: Chỉ callback đã xác thực cập nhật gói; retry không mua/trừ tiền hai lần; lỗi giữ trạng thái rõ.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-12 — FR-12: Payment history và credit

- Mục tiêu: lịch sử, số dư, reserve/capture/release và retry không trừ hai lần.
- Tiền điều kiện/dữ liệu: Patient; ledger reserve/capture/release và giao dịch sandbox.
- Các bước: Đọc history/số dư; phân tích thành công/lỗi; replay event và đối chiếu ledger.
- Kết quả kỳ vọng: Số dư khớp ledger; capture/release đúng rule; idempotent; chỉ xem lịch sử của mình.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-13 — FR-13: Login và bệnh nhân được phân công

- Mục tiêu: login, assigned patients, hết hiệu lực phân công, cross-tenant.
- Tiền điều kiện/dữ liệu: Doctor; Patient được phân công, ngoài phân công, khác tenant.
- Các bước: Đăng nhập; đọc danh sách; sửa ID; thu hồi assignment rồi đọc lại.
- Kết quả kỳ vọng: Chỉ tập được phân công; không lộ dữ liệu ngoài tenant; quyền bị thu hồi có hiệu lực backend.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-14 — FR-14: Xem AI result và annotation

- Mục tiêu: dữ liệu đúng bệnh nhân, quyền file, AI/review status tách biệt.
- Tiền điều kiện/dữ liệu: Doctor; analysis của Patient được phân công.
- Các bước: Đọc result/annotation; thử ngoài assignment và URL file riêng tư.
- Kết quả kỳ vọng: Đúng bệnh nhân/version; AI và review tách biệt; cả API và file enforce quyền.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-15 — FR-15: Xác nhận/sửa nhận định AI

- Mục tiêu: approve/correct, giữ AI gốc, revision và concurrent edit.
- Tiền điều kiện/dữ liệu: Doctor; AI gốc, review hiện tại và hai phiên sửa.
- Các bước: Approve/correct; đọc lại AI gốc; sửa đồng thời từ hai phiên.
- Kết quả kỳ vọng: Giữ AI gốc; lưu reviewer/revision/time; phát hiện xung đột theo contract; có audit.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-16 — FR-16: Ghi chú/chẩn đoán/khuyến nghị

- Mục tiêu: lưu nội dung hợp lệ, quyền sửa, audit và lỗi lưu.
- Tiền điều kiện/dữ liệu: Doctor; ghi chú hợp lệ, sai và lưu thất bại.
- Các bước: Lưu chẩn đoán/khuyến nghị; tải lại; thử ngoài quyền và lỗi DB.
- Kết quả kỳ vọng: Lưu nội dung đúng người; lỗi không giả thành công; audit người sửa; từ chối ngoài quyền.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-17 — FR-17: Lịch sử và xu hướng bệnh nhân

- Mục tiêu: lịch sử đúng thứ tự/quyền; dữ liệu thiếu; tính trend đã chốt.
- Tiền điều kiện/dữ liệu: Doctor; nhiều analysis theo thời gian, dữ liệu thiếu/version khác.
- Các bước: Mở lịch sử/trend; so thứ tự và rule tính; thử thiếu dữ liệu.
- Kết quả kỳ vọng: Trend đúng định nghĩa được duyệt; không suy diễn từ dữ liệu thiếu/không tương thích; đúng assignment.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-18 — FR-18: Tìm/lọc ID, tên, risk

- Mục tiêu: filter/search/pagination trong tập được phép; risk chưa có.
- Tiền điều kiện/dữ liệu: Doctor; danh sách có tên/ID/risk và kết quả chưa hoàn tất.
- Các bước: Tìm/lọc/kết hợp phân trang; nhập ký tự đặc biệt; thử tên khác tenant.
- Kết quả kỳ vọng: Truy vấn trong tập được phép; pending không có risk giả; xử lý input an toàn; tổng/count đúng.
- Loại: contract/component + integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-19 — FR-19: Phản hồi cải thiện AI

- Mục tiêu: feedback gắn kết quả/version; quyền; dữ liệu retraining được xử lý đúng.
- Tiền điều kiện/dữ liệu: Doctor; kết quả có AI version và feedback tổng hợp.
- Các bước: Gửi feedback; đọc lại; thử sai analysis và export sang retraining.
- Kết quả kỳ vọng: Feedback gắn đúng version/result/reviewer; enforce quyền; retraining nhận dữ liệu qua bước ẩn danh.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-20 — FR-20: Chat tư vấn Patient

- Mục tiêu: Doctor chỉ chat theo quan hệ hợp lệ; revoke và delivery errors.
- Tiền điều kiện/dữ liệu: Doctor; Patient được phân công và quan hệ bị thu hồi.
- Các bước: Gửi/nhận; thu hồi; retry message và thử Patient khác.
- Kết quả kỳ vọng: Chỉ quan hệ hợp lệ; retry không tạo trùng; backend chặn sau revoke; báo delivery error đúng.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-21 — FR-21: Thống kê hoạt động Doctor

- Mục tiêu: phạm vi dữ liệu, khoảng thời gian và đối chiếu số liệu nguồn.
- Tiền điều kiện/dữ liệu: Doctor; dữ liệu hoạt động có timestamp đã biết.
- Các bước: Chọn cửa sổ thời gian; so số liệu với nguồn; thử Doctor khác.
- Kết quả kỳ vọng: Thống kê đúng phạm vi/thời gian; công thức rõ; không lộ hoạt động người khác.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-22 — FR-22: Đăng ký/xác minh Clinic

- Mục tiêu: đăng ký, thiếu hồ sơ, approval/rejection và trạng thái tổ chức.
- Tiền điều kiện/dữ liệu: Clinic; hồ sơ hợp lệ, thiếu và trạng thái chờ/xác minh/từ chối.
- Các bước: Đăng ký; gửi thiếu hồ sơ; Admin duyệt/từ chối; thử hoạt động trước duyệt.
- Kết quả kỳ vọng: Transition đúng use case; lỗi có nguyên nhân phù hợp; trạng thái chưa duyệt không vượt quyền.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-23 — FR-23: Quản lý Doctor/Patient members

- Mục tiêu: add/remove members, role constraints và cross-tenant.
- Tiền điều kiện/dữ liệu: Clinic A/B; Doctor/Patient membership.
- Các bước: Thêm/xóa thành viên; thử role không cho phép/cross-tenant; kiểm quyền sau xóa.
- Kết quả kỳ vọng: Chỉ quản lý tenant hợp lệ; membership không tự nâng role; quyền bị xóa mất hiệu lực.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-24 — FR-24: Bulk upload và batch

- Mục tiêu: nhiều ảnh, lỗi riêng từng ảnh, partial success và batch state.
- Tiền điều kiện/dữ liệu: Clinic; batch 100 ảnh gồm hợp lệ/hỏng/trùng.
- Các bước: Upload batch; đọc trạng thái từng ảnh; retry ảnh lỗi; đối chiếu terminal state.
- Kết quả kỳ vọng: Partial success rõ; không mất ảnh đã nhận; không nhân đôi job/charge; batch state theo contract.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-25 — FR-25: Theo dõi báo cáo và aggregate risk

- Mục tiêu: report scope, aggregate theo clinic, loại dữ liệu chưa hợp lệ.
- Tiền điều kiện/dữ liệu: Clinic; tập completed và tập pending/failed.
- Các bước: Đọc report/aggregate; so tập đủ điều kiện; đổi Clinic.
- Kết quả kỳ vọng: Tổng hợp đúng denominator; loại dữ liệu không hợp lệ; đúng tenant và quyền dữ liệu cá nhân.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-26 — FR-26: Báo cáo chiến dịch

- Mục tiêu: campaign scope, inclusion criteria và số liệu báo cáo.
- Tiền điều kiện/dữ liệu: Clinic; campaign với thành viên/ảnh trong và ngoài kỳ.
- Các bước: Tạo chiến dịch; thêm lượt sàng lọc; xuất báo cáo; đổi khoảng thời gian.
- Kết quả kỳ vọng: Đúng campaign/clinic/window và inclusion rules; không đếm ảnh ngoài phạm vi hoặc đếm trùng.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-27 — FR-27: Image count và package usage

- Mục tiêu: đối chiếu count/ledger, retry và các ảnh failed.
- Tiền điều kiện/dữ liệu: Clinic; ảnh thành công/lỗi/retry và ledger.
- Các bước: Đọc image count/usage; phát retry và đối chiếu ledger.
- Kết quả kỳ vọng: Count theo định nghĩa; credit không trừ hai lần; failed xử lý theo billing rule đã chốt.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-28 — FR-28: Mua/gia hạn Clinic package

- Mục tiêu: quyền mua, payment, renewal và duplicate callback.
- Tiền điều kiện/dữ liệu: Clinic; người có/không có quyền mua và payment sandbox.
- Các bước: Mua/gia hạn; gửi callback trùng/sai chữ ký; đọc entitlement.
- Kết quả kỳ vọng: Đúng quyền; chỉ callback hợp lệ thay đổi gói; gia hạn idempotent; audit giao dịch.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-29 — FR-29: High-risk/trend alerts

- Mục tiêu: ngưỡng/căn cứ hợp lệ, recipients và data minimization.
- Tiền điều kiện/dữ liệu: Clinic; high-risk hợp lệ, dưới ngưỡng, failed và trend thiếu dữ liệu.
- Các bước: Phát sự kiện; so recipients/rule version; kiểm nội dung push.
- Kết quả kỳ vọng: Chỉ rule đủ căn cứ kích hoạt; đúng recipients; dữ liệu thiếu/lỗi không thành risk; push chung.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-30 — FR-30: Export thống kê nghiên cứu/quản lý

- Mục tiêu: export permission, aggregate, ẩn danh và định dạng.
- Tiền điều kiện/dữ liệu: Clinic; aggregate tổng hợp và bộ dữ liệu có PII giả.
- Các bước: Export nghiên cứu/quản lý; kiểm scope và trường dữ liệu; thử ngoài quyền.
- Kết quả kỳ vọng: Định dạng đúng; ẩn danh theo policy; không xuất danh tính/trường ngoài phạm vi; audit export.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-31 — FR-31: Quản lý tài khoản

- Mục tiêu: enable/disable/edit, authorization và audit.
- Tiền điều kiện/dữ liệu: Admin; tài khoản đang hoạt động/disabled.
- Các bước: Sửa/disable/enable; thử phiên cũ; Patient gọi API quản trị.
- Kết quả kỳ vọng: Đúng transition và audit; disabled không tiếp tục hoạt động; API quản trị chặn sai role.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-32 — FR-32: Quản lý role/permission

- Mục tiêu: grant/revoke, cấm self-escalation và enforcement backend.
- Tiền điều kiện/dữ liệu: Admin; bốn role và quyền hiện hành.
- Các bước: Grant/revoke; thử tự nâng quyền; đọc resource sau revoke.
- Kết quả kỳ vọng: Backend enforce role, tenant, ownership/assignment; không self-escalation; audit thay đổi.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-33 — FR-33: AI parameters/threshold/retraining

- Mục tiêu: validation config, version, audit, activation và rollback.
- Tiền điều kiện/dữ liệu: Admin; config/threshold/model version hợp lệ và sai.
- Các bước: Validate; activate khi có job; rollback; đọc version lưu trên result.
- Kết quả kỳ vọng: Config được version hóa; invalid bị chặn; job/result truy vết version; activation/rollback có audit.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-34 — FR-34: Package/pricing/billing model

- Mục tiêu: update đúng quyền, validation giá/credit, invalidation và audit.
- Tiền điều kiện/dữ liệu: Admin; catalog và giá/credit biên.
- Các bước: Cập nhật; thử giá âm/sai quyền; đọc cache; so giao dịch đang chạy.
- Kết quả kỳ vọng: Validation và quyền đúng; cache phản ánh version; giao dịch giữ chính sách giá đã chốt; audit.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-35 — FR-35: Global dashboard usage/revenue/AI

- Mục tiêu: nguồn dữ liệu, quyền toàn cục và đối chiếu số liệu.
- Tiền điều kiện/dữ liệu: Admin; usage/revenue/AI records tổng hợp.
- Các bước: Chọn window/filter; đối chiếu nguồn/ledger; thử role khác.
- Kết quả kỳ vọng: Chỉ Admin hợp lệ; số liệu truy được công thức/nguồn; không tính mock vào metric sản phẩm.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-36 — FR-36: Image/risk/error analytics

- Mục tiêu: định nghĩa numerator/denominator, window và loại mock data.
- Tiền điều kiện/dữ liệu: Admin; trạng thái analysis và lỗi đã biết.
- Các bước: Tính image/risk/error counts theo mục 7; so numerator/denominator; replay event.
- Kết quả kỳ vọng: Không đếm trùng; chỉ risk hợp lệ vào risk rate; window/filters rõ; error categories không lẫn quality-reject.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-37 — FR-37: Compliance/audit/privacy

- Mục tiêu: audit access, privacy change, retention và dữ liệu log.
- Tiền điều kiện/dữ liệu: Admin; audit record và dữ liệu PII/token giả.
- Các bước: Đọc audit đúng/sai quyền; thử retention/xóa theo policy; kiểm log/error/export.
- Kết quả kỳ vọng: Log không chứa token/PII ngoài chính sách; audit được bảo vệ; retention kiểm được; không mặc nhiên chứng nhận tuân thủ.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-38 — FR-38: Approve/suspend Clinic

- Mục tiêu: approval/suspension, quyền, audit và enforcement sau suspend.
- Tiền điều kiện/dữ liệu: Admin; Clinic pending/active/suspended.
- Các bước: Approve/reject/suspend; dùng phiên Clinic cũ gọi API; thử người không đủ quyền.
- Kết quả kỳ vọng: Workflow đúng; suspension có hiệu lực backend; quyền và audit rõ; lỗi không đổi trạng thái sai.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

### TC-FR-39 — FR-39: Notification templates/policies

- Mục tiêu: template validation, version, quyền và cấm dữ liệu sức khỏe trong push.
- Tiền điều kiện/dữ liệu: Admin; template/version và nội dung nhạy cảm giả.
- Các bước: Tạo/sửa/activate template; thử sai quyền và trường sức khỏe trong push.
- Kết quả kỳ vọng: Validation cấm dữ liệu y tế trong push; version/audit rõ; người không đủ quyền không thay policy.
- Loại: integration + E2E; ưu tiên happy path P1, quyền/integrity P0.
- Kết quả thực tế: NOT_RUN cho toàn phạm vi; phần đã báo chạy xem mục 9. SHA, người chạy, thời điểm, tham chiếu kết quả và bug điền theo mục 10.

## 6. Protocol kiểm thử và phép đo NFR

Mỗi protocol là một test case với ID giữ từ RTM. Loại: NFR + integration/E2E hoặc review có kiểm tra kỹ thuật.
Tiền điều kiện chung là môi trường thực phù hợp mục 3; kết quả hiện tại toàn phạm vi: NOT_RUN.
Ngưỡng ghi “đề xuất” là định nghĩa vận hành cần owner xác nhận, không sửa yêu cầu SRS đã duyệt.
Không dùng mock, thời gian build hoặc thời gian test suite làm số đo hiệu năng sản phẩm.

### TC-NFR-01 — NFR-1: AI một ảnh 10–20 giây

- Tiền điều kiện/dữ liệu: 100 ảnh Fundus hợp lệ tổng hợp; cùng model/hardware/version; warm-up 10 ảnh, đo 100 ảnh ở tải đơn và tải đồng thời đã ghi.
- Các bước/phép đo: Dùng monotonic timer quanh lời gọi AI thực (preprocess + inference + postprocess); đo queue_wait và accepted-to-terminal riêng; ghi p50/p95/p99/max, timeout và lỗi.
- Kết quả kỳ vọng/gate: SRS nêu 10–20 giây/ảnh; đề xuất gate p95 AI service <=20s, không ép thời gian tối thiểu 10s. Gate và ranh giới phải được M4 xác nhận trước nghiệm thu; mock delay không tính.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-02 — NFR-2: Tối thiểu 100 ảnh/batch

- Tiền điều kiện/dữ liệu: Clinic A; batch 100 ảnh hợp lệ, lượt khác có ảnh lỗi; queue/worker thật.
- Các bước/phép đo: Gửi >=100 ảnh; theo dõi từng ID đến terminal; gây lỗi một ảnh và retry; đối chiếu số ảnh nhận, terminal, duplicate và ledger.
- Kết quả kỳ vọng/gate: Nhận và xử lý được >=100 ảnh/batch; tất cả ảnh có trạng thái giải thích được; không mất ảnh hoặc nhân đôi. Thời hạn batch cần chốt riêng, không suy từ 20s x100.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-03 — NFR-3: Dashboard/result dưới 3 giây

- Tiền điều kiện/dữ liệu: Dashboard/result đã có dữ liệu; browser/mạng/hardware ghi rõ; 30 lượt cold và 30 warm.
- Các bước/phép đo: Đo click/navigation đến nội dung chính sẵn sàng thao tác bằng browser Performance API; đo API riêng; ghi median/p95/max và lỗi.
- Kết quả kỳ vọng/gate: SRS <3s; gate đề xuất mọi lượt hợp lệ trong mẫu <3000ms cho luồng đã chốt. Loại thời gian chờ AI khỏi result-view latency, nhưng báo riêng end-to-end.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-04 — NFR-4: Uptime >=99%

- Tiền điều kiện/dữ liệu: Staging chạy liên tục 30 ngày; probe mỗi 60s; lịch maintenance phê duyệt trước.
- Các bước/phép đo: Probe workflow có API + DB readiness; mỗi interval không phục vụ tính downtime; missing probe ghi UNKNOWN; tính uptime theo mục 7.
- Kết quả kỳ vọng/gate: >=99% trong cửa sổ đủ dữ liệu sau loại maintenance đã khai báo; probe health đơn lẻ và local run ngắn không chứng minh uptime.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-05 — NFR-5: Graceful failure, bảo toàn ảnh

- Tiền điều kiện/dữ liệu: Ảnh đã được lưu bền vững và job hợp lệ; AI timeout/5xx, worker crash.
- Các bước/phép đo: Gây từng lỗi; restart; kiểm checksum ảnh, trạng thái lỗi/retry và ledger; replay job.
- Kết quả kỳ vọng/gate: Ảnh đã accepted không mất; trạng thái lỗi đúng contract; retry/idempotency; không xuất risk cho failed/quality-reject.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-06 — NFR-6: Backup ít nhất hàng ngày

- Tiền điều kiện/dữ liệu: DB + private file store có dữ liệu tổng hợp; job backup tự động và máy restore riêng.
- Các bước/phép đo: Quan sát tối thiểu 7 ngày lịch backup; kiểm artifact/checksum; restore DB/file sang môi trường sạch; so quan hệ/hash; đo RPO/RTO.
- Kết quả kỳ vọng/gate: Backup ít nhất hàng ngày (không có khoảng trống >24h); restore đọc được dữ liệu và file nhất quán. Mục tiêu RPO/RTO cụ thể cần chủ vận hành xác nhận.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-07 — NFR-7: Horizontal scaling AI

- Tiền điều kiện/dữ liệu: Cùng hardware tổng cho báo cáo so sánh; AI thật 1/2/3 instance; workload cố định.
- Các bước/phép đo: Warm-up 2 phút; mỗi cấu hình đo 10 phút; gửi cùng tải; kiểm routing, duplicate, version, latency, throughput, CPU/RAM.
- Kết quả kỳ vọng/gate: Có thể thêm/bớt instance và chia tải không mất/nhân đôi job; ngưỡng tăng throughput cần chốt theo tài nguyên, không mặc định tuyến tính.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-08 — NFR-8: Nhiều Clinic/hàng nghìn User

- Tiền điều kiện/dữ liệu: Đề xuất staging 10 Clinic, 2000 User đăng ký; 20/50/100 concurrent users; dữ liệu tổng hợp.
- Các bước/phép đo: Warm-up 2 phút, giữ mỗi mức 10 phút; workload đọc 70%, upload 20%, quản trị 10% được phép; kiểm tenant isolation và báo error/latency/resource.
- Kết quả kỳ vọng/gate: Đề xuất read p95 <3s, server error <1%, không cross-tenant leak; số Clinic/workload và ngưỡng suy giảm phải được xác nhận trước gate. Đây là tải thiết kế, chưa là kết quả.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-09 — NFR-9: AES-256 khi lưu, TLS 1.2+ khi truyền

- Tiền điều kiện/dữ liệu: Môi trường mục tiêu gồm DB/file/backup, key store và endpoint HTTPS; canary token/PII giả.
- Các bước/phép đo: Kiểm cấu hình AES-256 at rest toàn bộ phạm vi; thử khóa sai/sửa ciphertext; kiểm TLS handshake từ chối <1.2; tìm canary trong DB/log/backup; kiểm rotate/access khóa.
- Kết quả kỳ vọng/gate: AES-256 khi lưu và TLS >=1.2 khi truyền; không có canary dạng rõ ngoài phạm vi cho phép. Token AES-GCM PASS chỉ bao phủ token, localhost HTTP không đạt TLS mục tiêu.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-10 — NFR-10: Bảo vệ dữ liệu y tế

- Tiền điều kiện/dữ liệu: Policy privacy/retention/consent đã xác định; dữ liệu y tế tổng hợp; reviewer chính sách.
- Các bước/phép đo: Review phân loại dữ liệu, mục đích, consent, quyền, export, retention/deletion và incident process; kiểm các control tương ứng.
- Kết quả kỳ vọng/gate: Các control được review theo chính sách áp dụng; yêu cầu pháp lý/đánh giá chuyên môn phải do người có trách nhiệm xác nhận; test kỹ thuật không là chứng nhận tuân thủ.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-11 — NFR-11: Ẩn danh trước retraining

- Tiền điều kiện/dữ liệu: Dataset tổng hợp chứa tên/ID/email/metadata và identifier trên ảnh giả.
- Các bước/phép đo: Chạy pipeline trước retraining; kiểm metadata, pixel, filenames, mapping và quyền dataset; thử bỏ qua bước ẩn danh.
- Kết quả kỳ vọng/gate: Không chuyển trực tiếp danh tính sang retraining; policy ẩn danh được enforce và kiểm rủi ro tái nhận diện; mapping nếu giữ được bảo vệ riêng.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-12 — NFR-12: RBAC bốn vai trò

- Tiền điều kiện/dữ liệu: Patient/Doctor/Clinic/Admin; tenant A/B; assigned/unassigned/revoked/disabled users.
- Các bước/phép đo: Chạy bốn role x các resource; thêm missing/expired token, giả ID, cross-tenant, assignment revoke; chạy bộ negative cases SCRUM-281.
- Kết quả kỳ vọng/gate: Không xác thực trả 401; đã xác thực thiếu quyền theo contract 403 hoặc concealment 404; không lộ dữ liệu; DELETE idempotent 204 không đồng nghĩa có quyền với resource.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-13 — NFR-13: Responsive/accessibility

- Tiền điều kiện/dữ liệu: Chrome/Edge/Firefox và trình duyệt FCM được hỗ trợ; viewport 360/768/1440; UI chính.
- Các bước/phép đo: Thực hiện workflow bằng keyboard; kiểm focus/modal/label, zoom 200%, overflow và contrast; kiểm mobile touch.
- Kết quả kỳ vọng/gate: Không mất chức năng hoặc nội dung ở viewport chốt; các tiêu chí accessibility cụ thể cần M5 xác nhận (đề xuất WCAG 2.2 AA), không gán nhãn toàn sản phẩm từ vài component tests.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-14 — NFR-14: Upload/xem kết quả tối đa 3 click

- Tiền điều kiện/dữ liệu: Doctor đã login; ảnh hợp lệ; màn bắt đầu hành trình được M5 chốt.
- Các bước/phép đo: Quay/count click từ dashboard đến upload và view completed; đếm nút mở luồng, chọn file, submit/view; ghi thao tác trong OS picker riêng; đo error route riêng.
- Kết quả kỳ vọng/gate: <=3 click trong ứng dụng trên happy path theo start/end đã duyệt; không tự loại nút View để đạt mục tiêu; chờ AI không là click.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-15 — NFR-15: Annotation/risk explanation rõ

- Tiền điều kiện/dữ liệu: Result thực + annotation đúng ảnh; 3–5 reviewer có vai trò phù hợp, proposal.
- Các bước/phép đo: Cho reviewer xác định ảnh, risk, AI version, Doctor review và giới hạn; ghi hoàn thành đúng/sai, thời gian và vấn đề hiểu nhầm.
- Kết quả kỳ vọng/gate: Không nhầm AI với chẩn đoán đã duyệt; tiêu chí hiểu đúng/clinical adequacy cần reviewer chuyên môn chốt; demo không chứng minh tính phù hợp lâm sàng.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-16 — NFR-16: Update model/config không downtime

- Tiền điều kiện/dữ liệu: Staging có job đang chạy và request liên tục; model/config vA/vB hợp lệ.
- Các bước/phép đo: Activate vB khi đang tải; theo dõi request lỗi, job cũ/mới, version trên result; rollback vA.
- Kết quả kỳ vọng/gate: Không downtime do đổi version; mỗi result truy đúng version/config thực dùng; job không trộn version; rollback hoàn tất. Báo outage interval và error count.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-17 — NFR-17: Kiến trúc modular

- Tiền điều kiện/dữ liệu: ADR/contracts và mã nguồn AI/Web/business/data tại cùng SHA.
- Các bước/phép đo: Review dependency/data ownership; build/test module riêng; thử thay AI implementation giữ contract; ghi dependency violations.
- Kết quả kỳ vọng/gate: Ranh giới module có owner và contract; không truy cập DB ngoài ownership trái thiết kế; integration tương thích; kết quả là review checklist cụ thể.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-18 — NFR-18: Central logging/audit/error tracking

- Tiền điều kiện/dữ liệu: Central log/audit/error backend; request có correlationId; canary token/PII giả.
- Các bước/phép đo: Gây success/error xuyên API-worker-AI; truy cùng correlation; thử đọc log sai quyền; tìm canary và test retention.
- Kết quả kỳ vọng/gate: Trace xuyên service; audit actor/action/resource/time đúng; quyền đọc và retention enforce; không ghi bearer/FCM token hoặc dữ liệu bệnh nhân ngoài policy.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-19 — NFR-19: Tích hợp camera Fundus

- Tiền điều kiện/dữ liệu: Camera protocol đã chọn; simulator và thiết bị thật được phân biệt; NiFi/Gateway/storage thật.
- Các bước/phép đo: Gửi ảnh hợp lệ/lỗi/trùng; gây mất mạng và retry; kiểm checksum/correlation/analysis; ghi nguồn simulator hay camera.
- Kết quả kỳ vọng/gate: Pipeline nhận và lưu đúng ảnh; retry không trùng; lỗi có trạng thái; simulator chỉ chứng minh giao thức mô phỏng, camera thật cần lượt riêng.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-20 — NFR-20: PDF/CSV và chuẩn y tế khi khả thi

- Tiền điều kiện/dữ liệu: Report tổng hợp Unicode và trường version; PDF reader/CSV parser; chuẩn y tế cụ thể nếu được chốt.
- Các bước/phép đo: Xuất/mở/parse; so nguồn, encoding, schema; kiểm file access; nếu chuẩn y tế được chọn chạy validator riêng.
- Kết quả kỳ vọng/gate: PDF/CSV hợp lệ và đúng dữ liệu/quyền; chuẩn y tế khi khả thi phải ghi tên/version/phạm vi trước test, chưa chọn ghi BLOCKED cho phần đó.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-21 — NFR-21: RESTful AI API

- Tiền điều kiện/dữ liệu: AI contract 1.1.0 và Analysis contract baseline repo; AI runtime/Worker thật và mock riêng.
- Các bước/phép đo: Validate OpenAPI/schema/examples; gửi valid/invalid/auth/error; Worker gọi runtime thật; đối chiếu status/fields/version.
- Kết quả kỳ vọng/gate: Runtime theo contract; lỗi đúng schema; integration Worker-AI PASS riêng. Mock/fixtures PASS chỉ xác nhận contract hoặc giả lập.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-22 — NFR-22: Explainable AI outputs

- Tiền điều kiện/dữ liệu: Ảnh nguồn và output AI thật có mask/overlay; reference alignment.
- Các bước/phép đo: Kiểm dimensions/coordinates/transform; overlay ảnh; so vùng giải thích với thuật toán đã mô tả; review giới hạn.
- Kết quả kỳ vọng/gate: Output gắn đúng ảnh/version/phương pháp; không dùng mask mock như explainability thật; clinical interpretation cần review riêng.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

### TC-NFR-23 — NFR-23: Truy vết AI version/threshold

- Tiền điều kiện/dữ liệu: Analysis tạo dưới config vA và vB; DB/report thực; restart.
- Các bước/phép đo: Tạo dưới vA; đổi vB; đọc lại/restart/export; so modelVersion/threshold/config ID và AI gốc.
- Kết quả kỳ vọng/gate: Version/config lưu bền, giữ qua API/report; đổi config không viết đè nguồn gốc analysis cũ; field thiếu phải phát hiện.
- Báo cáo: environment/SHA, mẫu số, cửa sổ đo, kết quả thực, giới hạn và owner theo mục 10; quyền/integrity P0, gate vận hành P1.

## 7. Từ điển metric

Mọi số liệu ghi UTC và cửa sổ nửa mở [start, end); ghi tenant, filters, dataset, model/config,
environment và cách loại dữ liệu. Dùng analysis/image/job ID duy nhất để chống đếm trùng.
Bản retry không tự tạo thêm một ảnh hợp lệ trong số đếm.
Số liệu sản phẩm loại mock, synthetic test và replay fixture; báo metric test riêng.

| Metric | Công thức và quy tắc |
| --- | --- |
| API latency | Monotonic duration từ client bắt đầu gửi request đến nhận body hoàn chỉnh; server duration ghi riêng |
| AI service latency | Từ bắt đầu lời gọi AI đến output/error hoàn chỉnh; gồm preprocess/inference/postprocess; không gồm queue_wait |
| Queue wait | job execution start trừ thời điểm enqueue; không trộn với inference |
| Analysis turnaround | terminal time trừ accepted time; includes queue/retry; lỗi báo riêng, không loại im lặng |
| UI time to usable | Từ click/navigation đến nội dung chính dùng được; ghi cold/warm/cache/network riêng |
| Percentile | Sắp n mẫu hợp lệ tăng dần; pX là phần tử thứ ceil(X*n/100), index bắt đầu 1; báo n/min/median/p95/p99/max |
| Error rate | failed server requests / tổng attempted requests; ghi timeout/network/4xx/5xx riêng; expected negative 4xx không tính server failure |
| Throughput | Số unique terminal jobs / số giây cửa sổ; completed/failed/quality-reject báo riêng; accepted throughput ghi riêng |
| Uptime | 100 * (eligible seconds - unavailable seconds) / eligible seconds; eligible loại maintenance khai báo trước; UNKNOWN báo riêng, không coi UP |
| Backup age / RPO | Age = now - latest usable backup timestamp; RPO thực = thời gian nguồn mới nhất trừ dữ liệu mới nhất khôi phục được |
| RTO | Thời gian từ bắt đầu quy trình restore đến dữ liệu/service được kiểm tra dùng được; ghi restore scope |
| Risk distribution | Số unique completed analyses có clinical risk hợp lệ trong từng nhóm / tổng completed analyses có risk hợp lệ; không lấy failed/pending/quality-reject làm risk |
| Image counts | Unique accepted images, completed, failed, quality-reject báo riêng; chọn cohort theo accepted_at hoặc terminal_at và ghi rõ; không trộn hai cohort |
| Campaign coverage | Unique participants đã sàng lọc / eligible enrolled participants trong cùng campaign/window; thiếu denominator thì N/A |
| Package consumption | Sum ledger captures hợp lệ, đối chiếu reserve/release và unique transaction ID; không suy từ số request |
| Revenue | Sum settled payment amounts trừ refunds trong cùng currency/window; không cộng nhiều currency nếu chưa có quy tắc chuyển đổi |
| FCM delivery | accepted gửi, foreground receipt, background hiển thị và click là bốn kết quả riêng; accepted không chứng minh device đã nhận |
| FCM receipt latency | receipt time - send initiation trên cùng clock client/test ID; giới hạn là latency lượt test, không tự coi Firebase delivery SLA |
| Test pass rate | PASS / executed cases; executed = PASS + FAIL; báo NOT_RUN/BLOCKED/SKIP riêng và coverage = executed / in-scope |

Mẫu nhỏ không đủ chứng minh tail latency ổn định; vẫn báo percentile và n, không suy rộng.
UNKNOWN uptime làm kết luận thiếu dữ liệu nếu có thể ảnh hưởng gate 99%.
Gate performance giữ cả lỗi/timeout trong báo cáo, không chỉ chọn các request nhanh thành công.

## 8. Bộ hồi quy Firebase và quyền

Các case sau bổ sung TC-FR-09, TC-NFR-09 và TC-NFR-12; không thay toàn bộ bộ negative cases SCRUM-281.
Bộ NT-01…NT-36 theo tài liệu quyền được chạy riêng khi các resource API có implementation; không ghi 36 PASS từ device API.

| Nhóm | Dữ liệu và bước | Oracle |
| --- | --- | --- |
| CRYPTO-01…12 | Unicode roundtrip; nonce mới; sai project/device/UID; sửa ciphertext/tag/nonce; sai khóa cùng key ID; key ID thiếu; nonce sai độ dài; token rỗng | Chỉ dữ liệu/context hợp lệ giải mã; input không hợp lệ bị từ chối |
| DB-01 | Register canary token; đọc encrypted columns; decrypt nội bộ và so fingerprint | Ciphertext không là token rõ; hash 32 byte, nonce 12, tag 16, có key ID |
| DB-02 | Update cùng project/device/UID bằng token mới | Một bản ghi; hash/ciphertext/nonce đổi; token mới giải mã đúng |
| AUTH-01 | Không có/expired/invalid bearer; call qua Vite và trực tiếp API | Lỗi auth theo contract; không trả token hoặc dữ liệu thiết bị |
| OWN-01…07 | A register; A repeat; B cùng device; B delete A; cùng token khác device; A delete; A delete lặp | 200, một row, conflict, không xóa A, conflict, xóa đúng, delete idempotent; không tiết lộ owner |
| FCM-FG | Permission granted, worker active, mock tắt; gửi test với ID duy nhất khi tab visible | API accepted và receipt cùng test ID; UI không hiển thị private payload |
| FCM-BG | Chuyển tab ẩn sau khi SDK worker active; gửi từ môi trường test độc lập; kiểm notification hệ điều hành | Nhận nội dung chung; không bị duplicate; không suy PASS từ foreground |
| FCM-CLICK | Click notification từ FCM-BG, thử cửa sổ AURA có/không có | Focus cửa sổ phù hợp hoặc mở AURA; không đưa token/PII vào URL |
| FCM-REVOKE | Hủy đăng ký/logout theo flow; gửi lại; cấp permission lại và register | Thiết bị đã xóa không còn gửi; đăng ký lại đúng UID; không để listener phiên cũ |
| FCM-ERROR | Permission denied, unsupported, network timeout, API error và unmount khi pending | UI lỗi rõ; không giả thành công; cleanup listener; cho phép thử lại phù hợp |
| PROD-TEST | Kiểm production build/config và call test push ở môi trường không được bật | Panel/endpoint test không khả dụng; rate limit/timeout được kiểm ở development |

Cho FCM-BG/CLICK dùng cửa sổ test độc lập hoặc backend test runner để tab nhận thực sự hidden.
Ghi visibility, permission, worker version/state và test ID; không ghi payload chứa thông tin bệnh nhân.
Service worker được bundle/minify nên kiểm bằng registration/behavior, không dựa vào tìm literal tên SDK trong bundle.

## 9. Snapshot kết quả khi biên soạn, trước đợt sửa M2–M5

Nguồn là log người thực hiện đã gửi trong phiên làm việc và PR #25.
Các test dưới đây không được chạy lại bởi lần biên soạn tài liệu này.
Không gán SHA của từng lần chạy khi log không ghi SHA; baseline mã nguồn sau merge được ghi ở mục 1.

| Nhóm | Kết quả đã báo | Phạm vi/giới hạn |
| --- | --- | --- |
| Crypto checks | PASS 12/12 | TokenCipher và binding/context, không là encryption toàn môi trường |
| PostgreSQL token | PASS encrypt/decrypt/fingerprint, update một row và cleanup | Local API/DB token registration |
| Device ownership | PASS các kiểm tra A/B, conflict và delete idempotent | Device API, không là RBAC mọi resource |
| API/proxy | Health 200, readiness thành công; qua Vite 401 / ERR_UNAUTHORIZED | Kết nối local và unauthenticated path |
| Frontend | 69 tests PASS trên 6 test files; lint 0 warnings/0 errors; build PASS | Component/client/MSW và panel; không là E2E backend toàn dự án |
| Test file breakdown | NotificationSettings 24; NotificationPushTestPanel 9; App 7; handlers 10; FcmDemoPanel 7; AnalysisDemoPanel 12 | Tổng = 69 |
| Contract fixtures | 8 fixtures match examples; 10 selected fixtures schema PASS | Phạm vi selected fixtures |
| Real foreground FCM | Đã báo nhận receipt cùng test ID trên browser | Luồng foreground test push, không là analysis event-to-notification đầy đủ |
| SDK background + click | NOT_RUN/ chưa có xác nhận cho worker cuối đã bundle | Toast ở mốc worker cũ không thay test worker cuối |
| Production NFR | NOT_RUN | TLS toàn phạm vi, uptime 30 ngày, restore, load, clinical/policy review chưa có kết quả |

RTM có kết quả AI mock/offline và hạ tầng của mốc trước; tham chiếu nguồn đó theo scope.
Không nâng những kết quả này thành full E2E hoặc clinical acceptance.
Task này hoàn tất phần thiết kế kiểm thử; các lượt chưa chạy là backlog thực thi, không là lý do giả PASS.

### 9.1. Cập nhật kiểm chứng Notification trong đợt sửa M2–M5

Ngày thực thi: 2026-10-11.
Bản sửa nằm trong working tree của nhánh fix/sprint1-m2-m5-completion,
HEAD b2b3dde; chưa commit.

| Kiểm tra | Kết quả và phạm vi |
| --- | --- |
| Notification API | Debug build PASS |
| TokenCipher | 12/12 PASS |
| PostgreSQL | Registration/update/fingerprint/cleanup PASS; xóa có điều kiện giữ token mới và kiểm scope project/UID/device |
| Frontend | Lần chạy cuối 81/81 PASS trên 7 file; lint 0 warnings/errors; TypeScript/Vite build PASS |
| Browser registration/sync | Có ảnh giao diện xác nhận thành công |
| Foreground FCM | Có ảnh receipt với mã gửi/nhận khớp |
| Background FCM | PASS theo quan sát người thao tác |
| Click notification | PASS theo quan sát người thao tác; focus tab AURA đang mở |
| Worker | File local và nội dung Vite phục vụ có cùng SHA256 |

Một lần frontend có 80/81 PASS; test lỗi chạy riêng PASS,
lần chạy toàn bộ tiếp theo 81/81 PASS. Chưa xác định nguyên nhân.

Token rotation được kiểm thử bằng mock.
Repository cleanup được kiểm thử với PostgreSQL;
chưa ép FCM thật trả Unregistered hoặc fault-inject endpoint.
Chưa kiểm tra click mở cửa sổ mới khi AURA không còn mở.
Chưa nghiệm thu Analysis event-to-notification hoặc NFR production.
- [Notification contract](../api/notification_service_contract_v0.1.md).
- [Bằng chứng kiểm thử](m2_m5_notification_verification_2026-10-11.md).

## 10. Mẫu ghi kết quả và báo cáo

Mỗi biến thể có record riêng; mặc định NOT_RUN. Không yêu cầu ảnh chụp nếu structured result/log đã đủ mô tả outcome.

```yaml
run_id: <unique>
case_id: TC-FR-09-foreground
requirement_ids: [FR-9, NFR-9, NFR-12]
priority: P0
type: integration
environment: <local-real-or-staging>
service_commits: {web: <sha>, notification: <sha>}
versions: {browser: <version>, sdk: <version>, database: <version>}
preconditions: <permission-worker-auth-project-mock-state>
data_ref: <synthetic-fixture-id-no-token>
started_at_utc: <timestamp>
finished_at_utc: <timestamp>
executor: <person>
expected: <oracle>
actual: <observed-outcome>
status: NOT_RUN # PASS | FAIL | BLOCKED | SKIP | NOT_RUN
metrics: {sample_count: null, p95_ms: null}
result_ref: <structured-log-or-report-reference-if-available>
bug_ref: null
limitations: <scope-and-missing-controls>
cleanup: <outcome>
```

Báo cáo một đợt gồm scope/SHA/environment; PASS/FAIL/BLOCKED/NOT_RUN/SKIP;
coverage theo requirement và biến thể; P0/P1 còn mở; metrics theo protocol;
các quyết định ngưỡng còn chờ; cleanup và đề xuất đợt tiếp theo.
Một requirement chỉ đạt nghiệm thu khi mọi biến thể bắt buộc trong phạm vi được chốt đều đạt,
không tính số lượng unit assertions làm số requirement PASS.

## 11. Lệnh hồi quy local

Chạy PowerShell ở repository trên Windows. Lệnh không in secret và không tạo dữ liệu DB ngoài crypto test mặc định.
Database checks có tham số/phụ thuộc riêng: xem chương trình test trước khi bật và cleanup sau khi chạy.

```powershell
$ErrorActionPreference = "Stop"

dotnet build src/services/AURA.Notification.Api/AURA.Notification.Api.csproj --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Notification API build chưa đạt." }

dotnet run --project tests/AURA.Notification.CryptoChecks/AURA.Notification.CryptoChecks.csproj --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Crypto checks chưa đạt." }

Push-Location src/web/aura-web
try {
    npm run lint
    if ($LASTEXITCODE -ne 0) { throw "Lint chưa đạt." }

    npm run test
    if ($LASTEXITCODE -ne 0) { throw "Frontend tests chưa đạt." }

    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build chưa đạt." }
}
finally {
    Pop-Location
}

git diff --check
if ($LASTEXITCODE -ne 0) { throw "Cần sửa whitespace trước commit." }
```

Worker có build config riêng. Khi đổi Firebase config/worker source, chạy từ thư mục web:

```powershell
npx vite build --config vite.firebase-sw.config.ts --mode development
if ($LASTEXITCODE -ne 0) { throw "Firebase worker build chưa đạt." }
```

Dùng mode/env của môi trường test tương ứng; package.json tại baseline chưa có script worker riêng. Không sửa csproj bằng nội dung Vite config.
Không chạy lại worker build chỉ để ghi phép đo latency ứng dụng.
Các lệnh schema/fixture lấy từ README/script repo tại baseline, tránh đặt tên script chưa tồn tại.

## 12. Bàn giao thực thi tiếp theo

| Phần | Việc tiếp theo | Owner cần phối hợp |
| --- | --- | --- |
| Firebase | Review bằng chứng background/click PoC local đã có; kiểm tra cửa sổ mới, token rotation thật và event-to-notification theo giai đoạn triển khai | M3/backend + UI |
| Nghiệp vụ/quyền | Chạy TC-FR-* và bộ negative cases trên API/resource thật khi có implementation | M2 + backend + M3 |
| AI | Chốt latency boundary/gate, clinical explanation và workload; chạy AI thật/Worker integration | M4 + backend |
| Hiệu năng/vận hành | Chốt staging load; uptime đủ window; backup/restore; TLS và at-rest toàn phạm vi | Hạ tầng |
| UI | Chốt start/end click count, viewport/browser và accessibility criteria | M5 |
| Chính sách | Xác nhận privacy/retention/retraining/export controls áp dụng | Owner chính sách + nghiệp vụ |

## 13. Lịch sử thay đổi

| Phiên bản | Ngày | Nội dung |
| --- | --- | --- |
| 0.1 | 2026-10-11 | Thiết kế 39 FR cases và 23 NFR protocols; định nghĩa metric, dữ liệu/môi trường, regression Firebase, execution template và giới hạn kết quả hiện có |

## Kiểm chứng bổ sung ngày 2026-10-12

Nguồn: log PowerShell người thao tác cung cấp.
Nhánh fix/sprint1-m2-m5-completion; thay đổi chưa commit.

- Frontend: 7 file test, 82/82 test PASS.
- tokenLifecycle: 7 test; NotificationSettings: 30 test.
- Lint: 0 lỗi và 0 cảnh báo.
- TypeScript và Vite production build: PASS.
- Bổ sung kiểm tra phiên sau khi backend đăng ký hoàn tất.
  Nếu phiên đã đổi, lifecycle trả cancelled thay vì ready.
- Loại bỏ dòng gán operationInFlight bị lặp.

Kết quả 81 test trước đó là bằng chứng của lần chạy trước.
Kết quả mới không thay thế kiểm thử FCM thật, phân quyền
backend, usability/accessibility hoặc nghiệm thu NFR.
