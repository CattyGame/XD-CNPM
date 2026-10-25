# Hợp đồng tích hợp Worker–AI Core — Sprint 2

> Dự án AURA · SCRUM-310 (W2-M4-03)
> Biên soạn ban đầu: Đào Duy Quân (M4); phối hợp: Võ Hữu Duy (M2).
> Bản rà soát 1.0.1 · 2026-10-24 · Git baseline 87af21d cùng local closeout patches.

## 1. Phạm vi và bằng chứng

Đây là đặc tả bàn giao cho Worker. M4 cung cấp endpoint mock và module
preprocessing riêng; endpoint hiện gọi `process_mock_analysis`, chưa gọi
`preprocess_fundus_image`. Không suy diễn đặc tả Worker, retry hoặc Billing
trong tài liệu này là luồng đã chạy.

Nguồn đối chiếu: `src/ai-core/app/main.py`, `app/schemas/analysis.py`,
`app/schemas/common.py`, `app/services/mock_service.py`,
`contracts/ai_service_openapi.yaml` / `.json` và `contracts/analysis-openapi.yaml`.
AI API version là `1.1.0`; service version `0.1.0`; model mock `mock-v0.1`.
Ba version này có ý nghĩa khác nhau.

Người thực hiện xác nhận 7A: 58/58 preprocessing tests PASS; 7B: toàn bộ AI
suite, OpenAPI sync `--check` và `git diff --check` PASS. Log 7B gửi lại chỉ
chứa kết quả tổng hợp, không ghi số test; không tự điền số lượng.
Đây là kiểm thử source/TestClient, chưa phải kiểm chứng Worker hoặc image
AI đang chạy. Checklist 43/43 trong bản cũ là ghi nhận lịch sử, đã được thay
bằng phạm vi bằng chứng ở đây.

## 2. Request và quyền truy cập

- Endpoint nội bộ: `POST http://ai-core:8000/api/v1/analyze`.
- Mạng dependency đã xác minh tại máy kiểm tra: `aura_aura_network`.
  Đây là tên Docker network thực tế; không nhầm với key mạng trong Compose.
- Không thêm route Gateway công khai tới endpoint AI. Cổng host loopback dùng
  phát triển không phải bằng chứng endpoint có service authentication.
- Worker phải tự xác minh quyền ở Analysis/Profile trước khi lấy ảnh.
  AI không dùng `patient_id` để cấp quyền. Gửi `patient_id: null` giảm thông
  tin định danh trong payload; riêng thao tác này không chứng minh ảnh đã
  được khử định danh hoặc đáp ứng một tiêu chuẩn y tế/pháp lý.
- Không log Base64, ảnh, bearer token, SAS hoặc connection string.

Headers dự kiến của Worker: `Content-Type: application/json`,
`X-Correlation-Id: <UUID>` và User-Agent có version của Worker thực tế.
`main.py` hiện chưa đọc/echo/persist `X-Correlation-Id`; truyền header không
chứng minh correlation đã được nối xuyên luồng. Worker phải kiểm tra
`response.request_id` trùng request, không dùng trường này thay kiểm soát tenant.

```json
{
  "request_id": "d0000000-0000-4000-8000-000000000001",
  "patient_id": null,
  "mode": "retina_vessels",
  "modality": "FUNDUS",
  "eye_side": "left",
  "image_base64": "<Base64 PNG/JPEG lấy từ private storage>",
  "include_mask": true,
  "include_overlay": true
}
```

Đây là template: thay placeholder bằng bytes ảnh tổng hợp khi kiểm thử;
không dùng placeholder làm ảnh hợp lệ hoặc đưa SAS vào `image_base64`.

| Trường | AI schema hiện tại | Quy ước Worker |
|---|---|---|
| `request_id` | String bắt buộc; schema không ép UUID | Dùng analysisId UUID; giữ nguyên qua retry. AI echo ID, không cung cấp durable idempotency/cache theo ID. |
| `image_base64` | String bắt buộc; decoder kiểm tra ảnh | Lấy bytes qua quyền private storage; không log. |
| `patient_id` | Optional, mặc định null | Gửi null; lưu binding bệnh nhân ở Analysis. |
| `mode` | Optional, mặc định `retina_vessels` | Gửi rõ `retina_vessels`; mode khác bị từ chối. |
| `modality` | Optional string, mặc định FUNDUS | Gửi rõ FUNDUS; OCT bị từ chối ở endpoint. |
| `eye_side` | Optional, mặc định unknown | left / right / unknown theo binding đã xác minh. |
| `include_mask`, `include_overlay` | Optional bool, mặc định true | Chỉ yêu cầu artifact cần lưu; không tự tạo FileRef cho null. |

## 3. Response và ánh xạ sang Analysis

Các đường dẫn public dưới đây nằm trong `AnalysisResult`; trạng thái xử lý
nằm trên `Analysis.processingStatus`. Đây là quy ước triển khai Worker,
không khẳng định mapper/persistence đã được kiểm thử runtime ở 7B.

| AI wire field | Analysis public field | Quy tắc |
|---|---|---|
| `request_id` | `Analysis.analysisId` | Đối chiếu binding hiện hữu; không lấy ID tùy ý từ response để tạo quyền. |
| `status` = SUCCESS hoặc WARNING | `Analysis.processingStatus` = COMPLETED | Chỉ commit khi response/artifact hợp lệ; WARNING không tự thành LOW_QUALITY. |
| `is_mock` | `isMock` | Giữ true với mock hiện tại. |
| `model_version`, `threshold_version`, `config_version` | `modelVersion`, `thresholdVersion`, `configVersion` | Giữ nguyên giá trị; không suy ra từ API version. |
| `processing_time_ms` | `processingTimeMs` | Thời gian component; không phải latency toàn luồng. |
| `metrics.vessel_density` | `metrics.vesselDensity` | AI dùng `metrics`, không có object `vessel_metrics`. Giá trị hiện là mock. |
| `metrics.tortuosity_index` | `metrics.tortuosityIndex` | Giữ nhãn mock. |
| `metrics.av_ratio` | `metrics.avRatio` | Không coi là phép đo AVR đã xác thực. |
| `metrics.fractal_dimension` | `metrics.fractalDimension` | Không suy ra độ chính xác từ schema parse thành công. |
| `metrics.branching_points` | `metrics.branchingPoints` | Giữ dữ liệu và provenance. |
| `risk_assessment.risk_score` | `riskScore` | Public field phẳng, không phải `riskAssessment.riskScore`. |
| `risk_assessment.risk_level` | `riskLevel` | LOW / MODERATE / HIGH. |
| `risk_assessment.confidence_score` | `confidenceScore` | Mock, chưa hiệu chuẩn. |
| `risk_assessment.indicators`, `.disclaimer` | `indicators`, `disclaimer` | Giữ đầy đủ disclaimer, không biến dữ liệu mock thành kết quả thật. |
| `warnings`, `limitations` | `warnings`, `limitations` | Lưu nguyên cảnh báo/giới hạn cùng kết quả hợp lệ. |
| `segmentation.mask_base64` | `annotations[]` với `kind: mask` và `fileId` | Decode PNG, kiểm tra hình học, lưu private `aura-annotations`, tạo FileRef. |
| `segmentation.overlay_base64` | `annotations[]` với `kind: overlay` và `fileId` | Cùng quy tắc; public schema không có top-level `maskFileId`/`overlayFileId`. |
| Không có AI field tương ứng | `version` | Version kết quả do Analysis quản lý, không dùng API/model/config version thay thế. |

Mask và overlay mock có kích thước ảnh đầu vào. Module preprocessing riêng có
thể crop/resize; khi nối inference thật phải lưu phép biến đổi và đưa annotation
về hệ tọa độ ảnh gốc theo public contract. Chưa chứng minh đường biến đổi này.
Không trả object key hoặc Base64 như public FileRef; cấp quyền tải qua Analysis.

## 4. Error, retry và Billing — yêu cầu tích hợp chưa được chứng nhận

| AI outcome | Hành vi Worker dự kiến |
|---|---|
| 200 SUCCESS / WARNING có kết quả hợp lệ | Commit kết quả và terminal state; WARNING vẫn có thể COMPLETED. |
| 400 INVALID_IMAGE_PAYLOAD | Terminal validation failure, không retry payload hỏng. |
| 422 LOW_QUALITY_IMAGE | Terminal LOW_QUALITY, không có kết quả risk hợp lệ. Mock scenario có `quality_assessment_performed: false`. |
| 422 UNSUPPORTED_MODALITY / INVALID_MODALITY / UNSUPPORTED_MODE / SCHEMA_VALIDATION_ERROR | Terminal input/config error; kiểm tra nguyên nhân, không retry nguyên payload vô hạn. |
| Network timeout / 500, hoặc 503 từ dependency | Có thể transient; kiểm tra retry budget. 503 không phải nhánh response đang được endpoint mock khai báo. |

Chính sách bàn giao từ bản v1.0: tối đa 3 lần gọi, chờ 5s rồi 15s; giữ cùng
analysisId và hold trong retry. Đây là lịch retry hữu hạn, chưa có bằng chứng
Worker đã áp dụng cấu hình này. Không tuyên bố AI tự dedupe request lặp.
Sau hết retry, việc FAILED/DEAD_LETTER và phát event cần transaction/outbox,
dedupe và recovery theo contract Analysis. Không xóa ảnh gốc theo lỗi AI.

Billing chỉ được chuyển hold sau outcome có thẩm quyền, theo
`billing_analysis_integration_contract_v0.1.md`. Không dùng bảng lỗi AI như
bằng chứng release đã thành công; chưa chốt event/reconcile toàn luồng.
Không tự chọn chuỗi reason mới, mở scope capture cho analysis-api hoặc gọi
đồng thời direct release và event release mà không thiết kế idempotency.
Giữ nguyên reservation 5J trong đợt closeout này.

## 5. Điều kiện kiểm chứng tiếp

- Đối chiếu response đầy đủ với schema, ID, version và range; validate lỗi và
  PNG trước khi commit. Không chỉ kiểm tra JSON có các key mong muốn.
- Kiểm thử mapper thực, transaction/result version, annotation coordinates,
  retry/crash recovery, correlation và consumer Billing bằng fixture cô lập.
- Không dùng số test source, enum mapping hoặc YAML/JSON sync để suy ra
  Worker/event/financial end-to-end đã hoàn thành.

Kế hoạch chi tiết: [M4 evaluation plan](../testing/m4_evaluation_plan.md).
Trạng thái bàn giao: [M4 integration status](../integration/M4_Sprint2_integration_status.md).
