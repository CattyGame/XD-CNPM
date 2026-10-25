# M4 — Kế hoạch đánh giá và bàn giao Sprint 2

Ngày 2026-10-24 · SCRUM-308/309/310 · source baseline 87af21d + closeout patches.

## 1. Bằng chứng đã nhận

| Bước | Kết quả người chạy cung cấp | Phạm vi |
|---|---|---|
| 7A | 58 passed in 2.41s | 14 tests preprocessing cũ + 44 regression cấu hình/empty FOV; ảnh tổng hợp. |
| 7B | PASS: AI suite, OpenAPI synchronization and whitespace checks | Toàn bộ `src/ai-core/tests`, script sync với `--check`, `git diff --check`. Log tổng hợp không ghi số test. |

Không đổi bảng benchmark đã lưu hoặc tự điền latency, accuracy, số test 7B.
TestClient chạy trong tiến trình; không chứng minh image/container hiện hữu
đã có source mới, Worker đã gọi AI hoặc Billing consumer đã xử lý event.

## 2. Preprocessing và quality

Đầu vào module là PIL Image, bytes hoặc Base64 string; đầu ra `PreprocessResult`
gồm kích thước gốc/đã xử lý, green/enhanced image, FOV mask, QualityMetrics,
is_acceptable và rejection_reason. Kiểm tra cấu hình hợp lệ, NaN/Infinity,
mutation, ảnh lỗi, empty FOV, crop/pad/resize và mask giữ nhãn rời rạc.

Các ngưỡng chất lượng là heuristic cấu hình. Trước khi dùng để loại ảnh thật,
cần ảnh có nhãn chất lượng, quy tắc gán nhãn và phân tích false accept/reject;
ghi rõ nguồn ảnh, thiết bị/quần thể, số lượng và người đánh giá. Không xem
58 tests synthetic là hiệu chuẩn ngưỡng hay clinical validation.
Module vẫn có thể tạo ảnh output khi is_acceptable=false; caller phải đọc cờ.

## 3. Segmentation offline và tính tái lập

Giữ split theo đối tượng trong manifest CHASE_DB1 (train 16 / validation 6 /
test 6 ảnh). Giữ hai mắt cùng split, mask `_1stHO.png` làm tham chiếu chính;
không chọn threshold lại trên test hoặc sửa split để cải thiện kết quả.

Baseline Frangi đã lưu trong dataset card dùng full-image metrics, không
resize và threshold validation đã chốt 0.02. Quality module 512×512 không
tự trở thành preprocessing của benchmark đó. Bất kỳ pipeline thay đổi nào
phải có config/version và báo cáo mới riêng, không ghi đè báo cáo gốc.

Khi chạy lại cần lưu: source/config hash, manifest/file hashes, Python/deps,
seed, ảnh/đối tượng/split, nhãn, FOV/full-image rule, Dice/IoU/sensitivity/
specificity và thời gian có phạm vi đo rõ. Ghi số mẫu và độ bất định khi báo
cáo; 6 ảnh test từ 3 đối tượng chưa chứng minh khả năng tổng quát.
Chưa yêu cầu chạy lại dữ liệu/benchmark trong patch tài liệu 7C.

## 4. Contract, example và runtime

Các testcase source cần bao phủ request_id, optional/default fields,
version/provenance, warnings, lỗi schema, ảnh hỏng, OCT/mode không hỗ trợ,
PNG mask/overlay và mock scenario enablement. OpenAPI YAML/JSON sync không
đồng nghĩa mọi khác biệt với runtime schema đã được kiểm tra đầy đủ.

| Test tích hợp tiếp theo | Điều kiện đạt | Trạng thái trong closeout này |
|---|---|---|
| Worker mapper thực | `metrics` vào metrics camelCase, risk fields phẳng, version do Analysis quản lý, annotations FileRef | Chưa chứng nhận bằng 7B. |
| Request ID / version | Không nhận response sai ID; không trộn API/model/config/result version; retry giữ binding | Cần integration evidence. |
| WARNING | COMPLETED khi kết quả hợp lệ; lưu warnings/limitations/isMock/disclaimer | Source mock có tests; Worker path chưa chứng nhận. |
| LOW_QUALITY | Không có valid risk result; terminal transition/reason theo contract được chốt | Mock 422 có tests; không chứng minh quality module đã được gọi. |
| Artifact hình học | PNG hợp lệ, đúng tọa độ gốc; private storage và FileRef được cấp quyền | Mock giữ kích thước gốc; inverse transform của inference thật chưa chứng nhận. |
| Retry / crash recovery | Budget hữu hạn; không nhân result/ledger; hold được giữ qua retry | Cần fixture cô lập, không dùng hold 5J để thử terminal action. |
| Correlation / Billing | Source of truth terminal state, outbox/dedupe/reconcile và event correlation | Chưa chứng nhận; không tự bật consumer. |

Ví dụ request có placeholder chỉ là template. Các response/error/scenario ví dụ
chuẩn hiện có trong `contracts/ai_service_openapi.yaml` / `.json` và AI tests;
không đổi schema/wire trong 7C. Request_id là string theo AI schema, còn Worker
chọn UUID analysisId theo domain contract.

## 5. Risk / OCT và quyết định bàn giao

Risk cần target/cohort/nhãn/method/metric được chốt trước khi huấn luyện hoặc
đổi is_mock. OCT cần dataset/decoder/model/format/resource benchmark riêng;
giữ rejection hiện hữu. Không dùng metric Frangi làm risk accuracy hoặc tự
chọn giới hạn GPU, cỡ mẫu hay ngưỡng lâm sàng.

M4 source tests và tài liệu có thể được review theo phạm vi Sprint 2; triển
khai inference thật, consumer Billing, Worker retry và NFR end-to-end là các
đầu việc cần bằng chứng riêng. Người phụ trách/quyết định được ghi trong
closeout plan; 7C không sửa lịch Sprint hoặc trạng thái Jira.
