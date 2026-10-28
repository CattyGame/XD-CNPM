# M4 — Kế hoạch đánh giá và báo cáo bàn giao Sprint 3

> Ngày cập nhật: 2026-10-28 · Mã Jira: `SCRUM-329` (`W3-M4-01`), `SCRUM-330` (`W3-M4-02`), `SCRUM-331` (`W3-M4-03`) · Tương thích `aura-sprint3-1.0.0`.

## 1. Tổng hợp bằng chứng kiểm thử & tái lập Sprint 3

| Hạng mục kiểm chứng | Lệnh thực thi | Kết quả thực tế | Tệp bằng chứng / Báo cáo |
|---|---|---|---|
| Kiểm định gói Contract Sprint 3 | `python scripts/validate_pack.py` | `PASS` (173 checks + SHA256 manifest) | `Sprint_3/AURA_Sprint3_Contract_Config_v1/docs/validation-report.json` |
| Đồng bộ OpenAPI 1.2.0 YAML/JSON | `python src/ai-core/scripts/sync_openapi_contracts.py --check` | `OK: JSON and YAML contracts are 100% in sync (v1.2.0).` | `contracts/ai_service_openapi.yaml`, `contracts/ai_service_openapi.json`, `contracts/ai-openapi-1.2.yaml` |
| Toàn bộ Test Suite `src/ai-core` | `pytest src/ai-core/tests -q` | `102 passed in 14.10s` | `src/ai-core/tests/test_frangi_sprint3.py` (+ các bộ test hiện hữu) |
| Tái lập Benchmark Frangi v0.2 trên tập `test` | `python src/ai-core/scripts/evaluate_frangi_test.py` | `VERIFIED` (Dice `0.4201`, IoU `0.2662`, Sens `0.4198`, Spec `0.9565`; sai số `0.000000`) | `docs/benchmarks/frangi_test_v0.2.json`, `docs/benchmarks/frangi_test_v0.2_reproduced.json` |
| Benchmark Runtime API `POST /api/v1/analyze` (6 ảnh `test`) | `python src/ai-core/scripts/benchmark_frangi_runtime_api.py` | `SPRINT3_RUNTIME_API_BENCHMARK_VERIFIED` (Dice `0.4201`, IoU `0.2662`, median `2516.41 ms`, p95 `2595.46 ms`) | `docs/benchmarks/frangi_runtime_api_v1.2.json` |
| Smoke-Run Huấn luyện Phân vùng (`train`/`val`) | `python src/ai-core/scripts/run_training_smoke.py` | `SEGMENTATION_TRAINING_SMOKE_PASSED` (`test_split_touched: false`, `loss_decreased: true`) | `docs/benchmarks/segmentation_training_smoke_v0.1.json` |

## 2. Chi tiết kiểm chứng theo từng Task Sprint 3

### 2.1. `SCRUM-329` (`W3-M4-01`) — Cung cấp inference Fundus thực tế cho Worker
- **Runtime Frangi v0.2 thật**: `src/ai-core/app/services/frangi_service.py` đọc cấu hình `src/ai-core/config/ai-frangi.yaml`, chạy phân vùng mạch máu võng mạc trên kênh xanh lá đã làm mượt Gaussian (`sigma=1.0`), `sigmas=[1, 2, 3, 5, 8]`, `alpha=0.5`, `beta=0.5`, `gamma=None`, `black_ridges=True`, `mode='reflect'`, triệt ngoài vùng nội vùng ước lượng, chuẩn hóa theo giá trị cực đại từng ảnh (trả mask toàn `0` nếu phản hồi cực đại bằng `0`), và áp ngưỡng `0.02`.
- **Bảo toàn hình học gốc**: Không resize hoặc crop 512×512; trả về PNG mask (`mode="L"`, giá trị pixel `{0, 255}`) và PNG overlay (`mode="RGB"`) giữ nguyên `(width, height)` của ảnh đầu vào (`999 × 960` trên `CHASE_DB1`).
- **Xác thực RS256 Service JWT fail-closed (`src/ai-core/app/auth/service_jwt.py`)**:
  - Kiểm tra khóa công khai SPKI RSA `>= 2048` bit; từ chối khóa private hoặc cấu hình nhập nhằng bằng HTTP `503 SERVICE_NOT_READY`.
  - Xác minh chữ ký RS256, `kid` (`aura-local-<16hex>`), `iss="aura-identity-s3-v1"`, `exp`, `nbf`, `iat` (TTL tối đa `300s`, clock skew `30s`), `sub` UUID khác 0 (`401` khi vi phạm).
  - Xác minh `aud="aura-ai"`, `scope="ai:analyze"`, `client_id="analysis-worker"` (`403` khi vi phạm).
- **Readiness (`/ready`) và Liveness (`/health`)**:
  - `/health` trả `200` cho liveness.
  - `/ready` chỉ trả `200` khi engine là `frangi` (`allowMock=false`), cấu hình `ai-frangi.yaml` hợp lệ và khóa công khai JWT đã nạp sẵn sàng; trả `503` nếu ở chế độ mock hoặc thiếu khóa.

### 2.2. `SCRUM-330` (`W3-M4-02`) — Hoàn thiện kiểm tra chất lượng và pipeline dữ liệu
- **Công thức chất lượng trước phân vùng**: Thực hiện đúng công thức toán học trong `config/ai-frangi.yaml` (`grayscale = 0.299*R + 0.587*G + 0.114*B`, `luminance` trung bình, `contrast` độ lệch chuẩn quần thể `ddof=0`, `sharpness` phương sai quần thể `ddof=0` của Laplacian 4 lân cận với biên `reflect` trên vùng `estimatedRegion`).
- **Kiểm thử biên ngưỡng (`test_quality_threshold_exact_equality_accepted_and_below_rejected`)**: Chứng minh trường hợp bằng đúng ngưỡng biên (`regionRatio == 0.15`, `luminance == 15.0` / `240.0`, `contrast == 8.0`, `sharpness == 3.5`) được chấp nhận, và thấp/cao hơn ngưỡng bị từ chối bằng HTTP `422 LOW_QUALITY_IMAGE` mà không tạo kết quả phân vùng giả.
- **Tách biệt dữ liệu & Smoke-Training (`run_training_smoke.py`)**: Xác nhận 14 đối tượng chia theo `seed=42` (`train`: 16 ảnh / 8 đối tượng; `validation`: 6 ảnh / 3 đối tượng; `test`: 6 ảnh / 3 đối tượng) không rò rỉ đối tượng; pipeline huấn luyện khói chỉ chạy trên `train`/`validation` (`test_split_touched: false`) và không suy diễn nhãn rủi ro lâm sàng (`risk_labels_used: false`).

### 2.3. `SCRUM-331` (`W3-M4-03`) — Kiểm thử AI contract và bàn giao benchmark
- **Xác thực JSON Schema 2020-12 tự động**: Mọi request/response/error trong `test_frangi_sprint3.py` và `benchmark_frangi_runtime_api.py` đều được kiểm tra bằng `jsonschema.Draft202012Validator` với `FormatChecker()` đối chiếu trực tiếp với `contracts/ai-request-1.2.schema.json`, `contracts/ai-response-1.2.schema.json` và `contracts/ai-error.schema.json`.
- **Báo cáo Benchmark Runtime API (`docs/benchmarks/frangi_runtime_api_v1.2.json`)**:
  - Độ chính xác trên 6 ảnh `test` qua HTTP endpoint khớp tuyệt đối với baseline (`Mean Dice = 0.4201`, `Mean IoU = 0.2662`, `Mean Sensitivity = 0.4198`, `Mean Specificity = 0.9565`).
  - Thời gian xử lý nội bộ AI Core (`processing_time_ms`): trung vị `2516.41 ms`, p95 `2595.46 ms`, max `2600.48 ms` (trên CPU Windows 11, Python 3.12.14, ảnh `999 × 960`).
  - Phạm vi đo: bao gồm xác thực JWT, giải mã Base64, kiểm tra chất lượng, phân vùng Frangi v0.2 và mã hóa 2 ảnh PNG (`mask` + `overlay`); **không** bao gồm độ trễ mạng Docker, tải/ghi Blob Storage, hàng đợi RabbitMQ hoặc giao dịch cơ sở dữ liệu của Worker.
