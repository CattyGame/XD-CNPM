# M4 Sprint 2 — trạng thái sau 7A/7B và bản rà soát 7C

Ngày 2026-10-24 · Git baseline 87af21da20489eda6d4899f7e1f3a428b24960db
cùng local closeout patches. Jira SCRUM-308/309/310 đều ghi Done khi đọc;
bản ghi này không sửa Jira, không thay thế review/merge hoặc nghiệm thu runtime.

## 1. Sửa và kiểm chứng source

7A thêm validate PreprocessConfig lúc tạo, trước load ảnh và direct quality
entry point để bắt cấu hình mutable sai kiểu/miền/NaN/Infinity. Empty FOV
luôn INVALID/rejected kể cả các min thresholds bằng 0. Trước sửa, black32×32
với toàn bộ min thresholds 0 có thể trả is_acceptable=true dù fov_ratio=0.
Default thresholds không đổi. Default black image đã bị từ chối trước sửa;
không ghi nhận một empty-mask crash không tái hiện được.

7A cũng sửa tài liệu risk enum MEDIUM thành MODERATE theo schema. Không đổi
main.py, AI schema/wire, model version, billing policy hoặc runtime flags.

| Bằng chứng người chạy gửi | Phạm vi / giới hạn |
|---|---|
| 7A: 58 passed in 2.41s | 14 tests cũ và44 regression preprocessing. Ảnh tổng hợp. |
| 7B: AI suite, OpenAPI synchronization and whitespace checks PASS | Toàn `src/ai-core/tests`, sync script --check, git diff --check. Log tổng hợp không ghi số test. |

Không coi kết quả này là bằng chứng image AI hiện hữu đã được rebuild hoặc
Worker đã được kích hoạt. Không chạy lại dataset/benchmark trong 7A–7C.

## 2. Các sai lệch tài liệu đã sửa trong 7C

- Dataset card: bỏ mô tả CLAHE; ghi đúng fixed threshold/morphology,
  autocontrast/unsharp, heuristic quality và caller phải đọc cờ. Module
  không tự trả HTTP 422/release credit; endpoint hiện vẫn gọi mock.
- Worker contract: AI wire là `metrics`; public risk fields phẳng và
  annotation dùng FileRef trong `annotations[]`, không top-level maskFileId.
  Phân biệt optional/default AI fields với Worker convention, result version
  với API/model/config version; giữ warnings/limitations và MODERATE.
- Giữ request_id qua retry là quy ước binding; AI echo ID không cung cấp
  durable idempotency. Header correlation hiện chưa được main.py xử lý.
- Phân biệt chính sách Worker/retry/Billing dự kiến với đường đã kiểm chứng;
  không suy ra consumer/capture/release/reconcile hoàn thành từ mock tests.
- Feasibility: tách segmentation/morphology/risk target, data/labels,
  model/metric/compute/OCT format và giới hạn. Bỏ ngưỡng lâm sàng, yêu cầu cỡ
  mẫu/VRAM/thời gian xử lý chưa có căn cứ cho AURA; thêm nguồn gốc nghiên cứu.
- Bổ sung kế hoạch đánh giá; giữ benchmark/split CHASE_DB1 đã lưu, không
  gán số liệu Frangi offline cho module resize mới hoặc cho risk accuracy.

## 3. Đối chiếu task và phần còn lại

| Task | Phạm vi Sprint 2 đã có | Cần phân biệt khi bàn giao |
|---|---|---|
| SCRUM-308 | Module/config/input-output/tests; regression7A và full suite7B PASS | Ngưỡng heuristic chưa hiệu chuẩn trên nhãn chất lượng; chưa nối pipeline vào inference endpoint. |
| SCRUM-309 | Báo cáo target/data/model/compute/format/metric và giới hạn được rà soát7C | Không có risk model lâm sàng hoặc OCT runtime; không yêu cầu bật chúng để chứng minh khảo sát hoàn thành. |
| SCRUM-310 | Mapping/examples theo AI/public schema, dataset card và evaluation plan được sửa7C; OpenAPI check7B PASS | Worker mapper, retry/crash, inverse annotation transform, event/correlation/Billing chưa được chứng nhận end-to-end. |

Phần source và tài liệu đủ để đưa vào review theo scope trên sau khi 7C apply
PASS. Không tự coi toàn M4 hoặc toàn Sprint 2 đã nghiệm thu tích hợp. Các giới
hạn inference/Worker thuộc công việc tích hợp tiếp, không được che bằng status
Done trên Jira. Không thêm clinical model/OCT hoặc mở rộng Sprint tự động.

## 4. Handoff và trạng thái application

- Theo evidence M2/M3 trước đó, primary Analysis HTTP vẫn disabled; hai batch/
  image, một QUEUED job, hai pending outbox, hai ACCEPTED và một ABORTED recovery
  attempt được giữ. Private original/duplicate Blob vẫn còn.
- TEST wallet5J balance0, reservation1 đang RESERVED, CREDIT1/RESERVE1; không
  capture/release/refill/cleanup để kiểm tài liệu. Không coi số liệu này được
  kiểm tra lại bằng package7C, vì package không gọi DB/Docker/HTTP.
- Các DB test thất bại6B/6C được giữ. Không đổi roles/ACL/credential/network.
- Sau review/apply7C chuyển rà soát M5 theo closeout plan; giữ manifest source
  mới cho bước tiếp, không ép runner cũ chấp nhận bằng sửa hash.

Tài liệu: [Worker–AI contract](../api/worker_ai_contract_integration_v1.0.md),
[dataset card](../data_cards/dataset_card.md),
[risk/OCT feasibility](../ai/feasibility_risk_model_and_oct_v1.0.md),
[evaluation plan](../testing/m4_evaluation_plan.md).
