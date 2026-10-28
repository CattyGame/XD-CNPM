# Hợp đồng tích hợp Worker–AI Core — Sprint 3 (AI Contract v1.2.0)

> Dự án AURA · SCRUM-329 (`W3-M4-01`), SCRUM-330 (`W3-M4-02`), SCRUM-331 (`W3-M4-03`)
> Biên soạn: Đào Duy Quân (M4); phối hợp: Võ Hữu Duy (M2), Nguyễn Trương Hậu (M1), Thành viên M6.
> Phiên bản tài liệu: 1.2.0 · 2026-10-28 · Tương thích `aura-sprint3-1.0.0`.

## 1. Phạm vi và bằng chứng Sprint 3

Tài liệu này quy định hợp đồng giao tiếp nội bộ giữa Analysis Worker (M2) và AI Core (M4) trong Sprint 3 theo bộ chuẩn `aura-sprint3-1.0.0`:
- **Runtime mặc định (`AURA_AI_ENGINE=frangi`, `allowMock=false`)**: Thực thi thuật toán phân vùng mạch máu võng mạc **Frangi v0.2 thật** theo cấu hình cố định trong `src/ai-core/config/ai-frangi.yaml`, giữ nguyên kích thước ảnh gốc `(width, height)` mà không crop/resize 512.
- **Xác thực dịch vụ fail-closed**: Yêu cầu RS256 Service JWT (`iss=aura-identity-s3-v1`, `aud=aura-ai`, `scope=ai:analyze`, `client_id=analysis-worker`).
- **Kiểm tra chất lượng ảnh trước phân vùng**: Tính toán trực tiếp trên vùng đáy mắt nội vùng ước lượng (`estimatedRegion`) theo công thức trong `config/ai-frangi.yaml`; từ chối bằng HTTP `422 LOW_QUALITY_IMAGE` khi không đạt ngưỡng.
- **Không giả lập đánh giá rủi ro lâm sàng**: Trả về `is_mock: false`, `risk_available: false`, `risk_assessment: null`, `status: "WARNING"` kèm cảnh báo `RISK_UNAVAILABLE`.
- **Phiên bản**:
  - `API_VERSION`: `1.2.0` (`contracts/ai-openapi-1.2.yaml`, `contracts/ai_service_openapi.yaml` / `.json`)
  - `model_version`: `frangi-v0.2`
  - `config_version`: `s3-frangi-1.0.0`
  - `threshold_version`: `frangi-threshold-0.02-v1`
- **Bằng chứng kiểm thử tại `src/ai-core`**:
  - `102 passed` trong bộ test `pytest src/ai-core/tests` (gồm test xác thực JWT RS256 `401/403/503`, test biên ngưỡng chất lượng `422`, test phân vùng thực tế trên >= 3 ảnh `CHASE_DB1` `999×960` đối chiếu trực tiếp với `ai-request-1.2.schema.json`, `ai-response-1.2.schema.json`, `ai-error.schema.json`, test smoke-training không chạm tập `test`, và hồi quy mock được gắn cờ riêng).
  - Đây là bằng chứng kiểm thử cấp component AI Core (`TestClient` và benchmark local); việc nối toàn luồng Worker → RabbitMQ → Blob → Billing/Notification được nghiệm thu tích hợp cùng M1/M2/M3/M5/M6.

## 2. Endpoints, Readiness và Xác thực Dịch vụ (RS256 JWT)

- **Liveness**: `GET http://ai-core:8000/health` → HTTP `200` (`status: "ok"`, `model_loaded: false`).
- **Readiness**: `GET http://ai-core:8000/ready` → Chỉ trả HTTP `200` (`ReadyResponse`) khi cấu hình `config/ai-frangi.yaml` hợp lệ, đang ở chế độ `engine=frangi` (`allowMock=false`), và khóa công khai SPKI RSA (>= 2048-bit) đã nạp thành công. Trả HTTP `503 SERVICE_NOT_READY` nếu thiếu khóa, cấu hình lỗi hoặc đang ở chế độ mock.
- **Inference**: `POST http://ai-core:8000/api/v1/analyze`
  - Header bắt buộc: `Authorization: Bearer <RS256 Service JWT>`
  - Yêu cầu JWT:
    - Header: `alg = "RS256"`, `kid = "aura-local-<16hex>"` khớp SHA-256 của khóa công khai SPKI.
    - Claims: `iss = "aura-identity-s3-v1"`, `aud = "aura-ai"`, `scope` chứa `"ai:analyze"`, `client_id = "analysis-worker"`, `sub` là UUID hợp lệ khác 0, `exp`/`nbf`/`iat` hợp lệ (TTL tối đa 300s, clock skew 30s).
    - Mã lỗi xác thực/phân quyền:
      - `503 SERVICE_NOT_READY`: Chưa cấu hình khóa công khai SPKI hợp lệ hoặc cấu hình Frangi lỗi.
      - `401 UNAUTHORIZED_SERVICE_TOKEN` / `MISSING_BEARER_TOKEN`: Thiếu token, sai chữ ký, sai `kid`, sai `iss`, hoặc hết hạn.
      - `403 FORBIDDEN_SERVICE_AUDIENCE` / `FORBIDDEN_SERVICE_SCOPE` / `FORBIDDEN_SERVICE_CLIENT`: Sai `aud`, thiếu `ai:analyze`, hoặc `client_id` không nằm trong allowlist.

## 3. Request Schema (`contracts/ai-request-1.2.schema.json`)

```json
{
  "request_id": "58685092-7782-5bb6-b147-1b73c6047edd",
  "patient_id": null,
  "eye_side": "left",
  "mode": "retina_vessels",
  "modality": "FUNDUS",
  "image_base64": "<Base64 trần của ảnh PNG hoặc JPEG>",
  "include_mask": true,
  "include_overlay": true
}
```

| Trường | Ràng buộc Schema 1.2 | Quy ước xử lý |
|---|---|---|
| `request_id` | UUID string bắt buộc | Worker truyền `analysisId` (UUID canonical). |
| `patient_id` | `null` bắt buộc | Không truyền định danh bệnh nhân sang AI Core (`422` nếu khác `null`). |
| `eye_side` | `"left" \| "right" \| "unknown"` | Giữ nguyên từ binding đã xác minh. |
| `mode` | `"retina_vessels"` | Mode khác trả `422 UNSUPPORTED_MODE`. |
| `modality` | `"FUNDUS"` | `"OCT"` trả `422 UNSUPPORTED_MODALITY`; giá trị khác trả `422 INVALID_MODALITY`. |
| `image_base64` | Base64 trần (4 .. 13.981.016 ký tự) | Chỉ chấp nhận ảnh `PNG` hoặc `JPEG` (không nhận `data:` URL, không tự mở rộng `WEBP`), tối đa `10 MiB` (`10.485.760` bytes) và `16.000.000` pixel. Vi phạm trả `400 INVALID_IMAGE_PAYLOAD`. |
| `include_mask`, `include_overlay` | `true` bắt buộc | Cả hai phải là `true` trong Sprint 3 (`422` nếu `false`). |

## 4. Response Schema (`contracts/ai-response-1.2.schema.json`) và Ánh xạ sang Analysis (0.7.0)

| AI wire field (snake_case) | Giá trị Frangi Sprint 3 | Ánh xạ sang Analysis Result (camelCase) |
|---|---|---|
| `request_id` | UUID trùng request | Đối chiếu `analysisId` của job/lease hiện hành. |
| `patient_id` | `null` | Giữ `patientId` từ DB của Analysis. |
| `timestamp` | Chuỗi UTC RFC3339 kết thúc bằng `Z` | Ghi nhận thời điểm hoàn tất suy luận. |
| `status` | `"WARNING"` | `Analysis.processingStatus = "COMPLETED"` (vì có kết quả phân vùng hợp lệ). |
| `warnings` | `[{"code": "RISK_UNAVAILABLE", "message": "..."}]` | Lưu nguyên danh sách cảnh báo vào result. |
| `is_mock` | `false` | `isMock = false` (Billing HOLD nếu gặp `isMock = true` trong namespace thật). |
| `model_version` | `"frangi-v0.2"` | `modelVersion = "frangi-v0.2"` |
| `config_version` | `"s3-frangi-1.0.0"` | `configVersion = "s3-frangi-1.0.0"` |
| `threshold_version` | `"frangi-threshold-0.02-v1"` | `thresholdVersion = "frangi-threshold-0.02-v1"` |
| `limitations` | `"Segmentation research baseline; no clinical risk assessment."` | `limitations` |
| `processing_time_ms` | Số thực `>= 0` | `processingTimeMs` (thời gian xử lý nội bộ AI Core). |
| `image_info` | `{width, height, channels: 3, eye_side, modality: "FUNDUS"}` | Kiểm tra khớp kích thước ảnh gốc. |
| `metrics.vessel_density` | `foreground_pixels / (width * height)` (`0..1`) | `metrics.vesselDensity` |
| `metrics.tortuosity_index`, `av_ratio`, `fractal_dimension`, `branching_points` | `null` | Giữ `null` (chưa có thuật toán kiểm chứng). |
| `risk_available` | `false` | `riskAvailable = false` (và trong event `analysis.completed` v0.7). |
| `risk_assessment` | `null` | Public result đặt `riskLevel = null`, `riskScore = null`, `confidenceScore = null`, `indicators = []`. Không tự gán `LOW` hay `0`. |
| `segmentation.mask_base64` | PNG mode `L`, giá trị pixel `{0, 255}`, đúng `(width, height)` gốc | Giải mã, kiểm tra hình học và tập giá trị `{0, 255}`, lưu vào `aura-annotations`, tạo `FileRef` (`kind: mask`). |
| `segmentation.overlay_base64` | PNG mode `RGB`, đúng `(width, height)` gốc | Giải mã, kiểm tra hình học, lưu vào `aura-annotations`, tạo `FileRef` (`kind: overlay`). |

## 5. Phân loại lỗi (`contracts/ai-error.schema.json`) và Quy tắc Retry cho Worker

Mọi phản hồi lỗi từ AI Core có cấu trúc `{error_code, message, timestamp, details}` với `timestamp` kết thúc bằng `Z`:

| HTTP Status | `error_code` | Bản chất & Xử lý tại Worker (M2) |
|---|---|---|
| `400` | `INVALID_IMAGE_PAYLOAD` | Lỗi dữ liệu ảnh hỏng/sai định dạng/vượt kích thước; **không retry**, chuyển trạng thái lỗi đầu vào kết thúc. |
| `401` | `UNAUTHORIZED_SERVICE_TOKEN` | Lỗi token dịch vụ thiếu/sai chữ ký/hết hạn; không che giấu bằng retry vô hạn. |
| `403` | `FORBIDDEN_SERVICE_AUDIENCE` / `FORBIDDEN_SERVICE_SCOPE` / `FORBIDDEN_SERVICE_CLIENT` | Lỗi cấu hình quyền dịch vụ; dừng và báo lỗi cấu hình. |
| `422` | `LOW_QUALITY_IMAGE` | Ảnh không đạt ngưỡng chất lượng kỹ thuật trước phân vùng; **terminal `LOW_QUALITY`** (không có result, Billing release với lý do `LOW_QUALITY`). |
| `422` | `UNSUPPORTED_MODALITY` / `INVALID_MODALITY` / `UNSUPPORTED_MODE` / `SCHEMA_VALIDATION_ERROR` | Lỗi contract đầu vào; **không retry**. |
| `500` | `INFERENCE_RUNTIME_ERROR` | Lỗi xử lý nội bộ; có thể retry trong ngân sách tối đa 3 logical attempts (`5s`, `15s`). |
| `503` | `SERVICE_NOT_READY` | AI Core chưa sẵn sàng; có thể retry trong ngân sách tối đa 3 logical attempts (`5s`, `15s`). |

Tham chiếu:
- Kế hoạch đánh giá & kết quả kiểm thử: [m4_evaluation_plan.md](file:///c:/UTH/XD-Phần%20mềm%20hướng%20đối%20tượng/docs/testing/m4_evaluation_plan.md)
- Báo cáo bàn giao tích hợp Sprint 3: [M4_Sprint3_integration_status.md](file:///c:/UTH/XD-Phần%20mềm%20hướng%20đối%20tượng/docs/integration/M4_Sprint3_integration_status.md)
- Dataset card: [dataset_card.md](file:///c:/UTH/XD-Phần%20mềm%20hướng%20đối%20tượng/docs/data_cards/dataset_card.md)
