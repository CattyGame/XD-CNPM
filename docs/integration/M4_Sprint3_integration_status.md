# Báo cáo trạng thái tích hợp và bàn giao Sprint 3 — Thành viên M4 (AI Core)

> **Dự án**: AURA · **Sprint**: 3 (`aura-sprint3-1.0.0`)
> **Thành viên**: Đào Duy Quân (M4 — AI Engineer)
> **Mã công việc Jira**: `SCRUM-329` (`W3-M4-01`), `SCRUM-330` (`W3-M4-02`), `SCRUM-331` (`W3-M4-03`)
> **Ngày cập nhật**: 2026-10-28

---

## 1. Bảng đối chiếu nghiệm thu Sprint 3 của M4 (`docs/NGHIEM_THU.md`)

| Mã Checklist | Hạng mục yêu cầu | Trạng thái tại nhánh M4 | Bằng chứng xác thực |
|---|---|---|---|
| **W3-M4-01 (`SCRUM-329`)** | Inference Frangi v0.2 thật theo `config/ai-frangi.yaml`, giữ nguyên hình học gốc, xác thực RS256 Service JWT (`401/403/503`), cung cấp `/health` và `/ready` | **HOÀN THÀNH (PASS)** | `src/ai-core/app/services/frangi_service.py`, `src/ai-core/app/auth/service_jwt.py`, `src/ai-core/app/main.py`, `src/ai-core/tests/test_frangi_sprint3.py` |
| **W3-M4-02 (`SCRUM-330`)** | Kiểm tra chất lượng trước phân vùng đúng công thức và ngưỡng biên (`422 LOW_QUALITY_IMAGE`), kiểm chứng manifest `CHASE_DB1` (`seed=42`), smoke-run huấn luyện phân vùng trên `train`/`validation` không chạm `test` | **HOÀN THÀNH (PASS)** | `src/ai-core/config/ai-frangi.yaml`, `src/ai-core/config/segmentation-training-smoke.yaml`, `src/ai-core/scripts/run_training_smoke.py`, `docs/benchmarks/segmentation_training_smoke_v0.1.json` |
| **W3-M4-03 (`SCRUM-331`)** | Đồng bộ OpenAPI `1.2.0`, kiểm thử tự động `>= 3` ảnh Fundus thật qua endpoint non-mock với JSON Schema 1.2, cập nhật báo cáo benchmark runtime và tài liệu bàn giao | **HOÀN THÀNH (PASS)** | `contracts/ai-openapi-1.2.yaml`, `contracts/ai_service_openapi.yaml`, `docs/benchmarks/frangi_runtime_api_v1.2.json`, `102 passed` trong `pytest` |
| **S3-10 (phần M4)** | Phân loại mã lỗi HTTP chuẩn (`400`, `401`, `403`, `422`, `500`, `503`) để Worker thực hiện retry hữu hạn tối đa 3 attempts | **SẴN SÀNG TÍCH HỢP** | `docs/api/worker_ai_contract_integration_v1.0.md` |
| **S3-11 (M4)** | Frangi thật và quality: mask PNG `L` (`0/255`) và overlay PNG `RGB` đúng kích thước gốc `999×960`; `LOW_QUALITY` không trả result; không tính ảnh giả `1×1` làm benchmark | **HOÀN THÀNH (PASS)** | `docs/benchmarks/frangi_runtime_api_v1.2.json` |
| **S3-12 (phần M4)** | Risk unavailable: `is_mock=false`, `risk_available=false`, `risk_assessment=null`, `status="WARNING"` kèm `RISK_UNAVAILABLE` | **HOÀN THÀNH (PASS)** | `src/ai-core/tests/test_frangi_sprint3.py` |

---

## 2. Cấu hình và Biến môi trường cần thiết khi tích hợp với M1 / M2 / M6

Khi M1 dựng Docker Compose cho namespace `aura-s3`, container `ai-core` sử dụng các cấu hình sau:

| Biến môi trường | Giá trị mặc định / Khuyến nghị Sprint 3 | Mô tả |
|---|---|---|
| `AURA_AI_ENGINE` | `frangi` | Bật thuật toán phân vùng Frangi v0.2 thật (`allowMock=false`). |
| `AURA_ALLOW_MOCK` | `false` | Khoá chế độ mock trong runtime Sprint 3. |
| `AURA_FRANGI_CONFIG_PATH` | `/app/config/ai-frangi.yaml` | Đường dẫn file cấu hình thuật toán và ngưỡng chất lượng đã đóng gói trong image. |
| `ServiceAuth__Issuer` (hoặc `AURA_SERVICE_AUTH_ISSUER`) | `aura-identity-s3-v1` | Issuer của Service JWT do Identity API cấp. |
| `ServiceAuth__Audience` (hoặc `AURA_SERVICE_AUTH_AUDIENCE`) | `aura-ai` | Audience bắt buộc của token gọi AI Core. |
| `ServiceAuth__RequiredScope` (hoặc `AURA_SERVICE_AUTH_SCOPE`) | `ai:analyze` | Scope bắt buộc của Worker khi gọi `POST /api/v1/analyze`. |
| `ServiceAuth__PublicKeyPath` (hoặc `AURA_SERVICE_AUTH_PUBLIC_KEY_PATH`) | `/run/aura-service-auth/public/signing-public.pem` | Đường dẫn bind-mount read-only chứa khóa công khai SPKI RSA (`>= 2048` bit). |

---

## 3. Điểm phụ thuộc tích hợp liên thành viên

- **Phụ thuộc vào M1 & M6**: Mount khóa công khai `/run/aura-service-auth/public/signing-public.pem` vào container `ai-core` và cấp client credential `analysis-worker` (scope `ai:analyze`, audience `aura-ai`) trong namespace `aura-s3`.
- **Phụ thuộc vào M2**: Worker gọi `POST http://ai-core:8000/api/v1/analyze` với Bearer token dịch vụ, kiểm tra invariants của response `1.2.0`, lưu PNG `mask` và `overlay` vào `aura-annotations`, ánh xạ `risk_available=false` / `risk_assessment=null` sang `riskAvailable=false` và phát sự kiện `analysis.completed` (`schemaVersion: "0.7"`).

