# Báo cáo Code Review & Hướng dẫn hoàn thiện Sprint 3 cho M4 (AI Core)

> **Người gửi**: Nguyễn Trương Hậu (M1 — Lead Architect / DevOps)
> **Người nhận**: Đào Duy Quân (M4 — AI Core Lead)
> **Dự án**: AURA (`SP26SE025`) · **Sprint**: 3 (`aura-sprint3-1.0.0`)
> **Mã công việc Jira liên quan**: `SCRUM-329`, `SCRUM-330`, `SCRUM-331`
> **Nhánh rà soát**: `feature/SCRUM-329-s3-ai-frangi` (PR #60) / `fix/sprint3-team-handoff-20261030`
> **Ngày lập**: 30/10/2026

---

## I. Đánh giá tổng quan từ M1 (Tech Lead / DevOps)

* **Điểm sáng xuất sắc**:
  * Đã chuyển đổi thành công từ Mock sang **phân vùng mạch máu Frangi v0.2 thật** theo đúng cấu hình đã đóng băng (`config/ai-frangi.yaml`).
  * Thực thi chuẩn mực bảo mật **Fail-Closed với RS256 Service JWT**: kiểm tra nghiêm ngặt `issuer`, `audience="aura-ai"`, `scope="ai:analyze"`, từ chối khóa yếu $< 2048$ bit, tự động trả `503` khi thiếu signing key.
  * Bảo toàn tuyệt đối kích thước hình học ảnh gốc ($999 \times 960$ pixel) cho cả Mask PNG và Overlay PNG, không làm lệch hệ tọa độ của Worker và Annotations.
  * Giữ vững tính trung thực học thuật: Trả rõ `risk_available: false`, `risk_assessment: null`, `status: "WARNING"` kèm mã `RISK_UNAVAILABLE`.
  * Không rò rỉ dữ liệu kiểm thử (Zero test split leakage) với phân chia mức đối tượng (subject-level split, `seed=42`).

* **Mục tiêu hoàn thiện**:
  * Bổ sung truy vết phân tán (`X-Correlation-Id`).
  * Điền thông tin Description & Acceptance Criteria cho 3 thẻ Jira.
  * Làm rõ ranh giới kiến trúc giữa `preprocessing.py` và `frangi_service.py`.
  * Ghi chú các ràng buộc Contract 1.2 cho M2 (Worker).

---

## II. Tin nhắn gửi nhanh (Copy gửi M4 qua Slack/Zalo/Teams)

```text
Gửi Quân (M4 - AI Core Lead),

M1 đã hoàn tất code review nhánh Sprint 3 (feature/SCRUM-329-s3-ai-frangi / PR #60) của bạn.

Đánh giá chung: Thuật toán phân vùng Frangi v0.2 thật, cơ chế xác thực Service JWT RS256 fail-closed và việc bảo toàn kích thước ảnh gốc (999x960) được bạn triển khai rất chuẩn chỉ, đúng tinh thần kiến trúc bảo mật và trung thực học thuật.

Để chuẩn bị nghiệm thu Sprint 3 và tích hợp mượt mà với Worker của M2, bạn hỗ trợ xử lý nốt 4 đầu việc nhỏ sau nhé:
1. Bổ sung middleware đọc/echo header X-Correlation-Id trong app/main.py để phục vụ tracing liên dịch vụ.
2. Cập nhật Description cho 3 thẻ Jira SCRUM-329, SCRUM-330, SCRUM-331 (M1 đã soạn sẵn mẫu bên dưới, chỉ cần copy-paste).
3. Thêm docstring lưu ý kiến trúc trong preprocessing.py để giải thích việc Frangi v0.2 chạy trực tiếp trên raw green channel mà không qua CLAHE (tránh hiểu lầm dead code).
4. Nhắc M2 (Worker) các điều kiện nghiêm ngặt của Contract 1.2: patient_id bắt buộc là null, include_mask/overlay phải là true, và timeout Worker >= 10-15s.

Chi tiết hướng dẫn kỹ thuật và code mẫu đã được lưu tại docs/integration/M4_Sprint3_code_review_and_action_items.md. Cảm ơn Quân!
```

---

## III. Chi tiết các hạng mục cần cập nhật

### 1. Bổ sung `X-Correlation-Id` Middleware vào `src/ai-core/app/main.py`

* **Vấn đề**: Worker của M2 gửi header `X-Correlation-Id` qua HTTP, nhưng AI Core hiện chưa ghi nhận hoặc phản hồi lại header này trong response.
* **Vị trí sửa**: Thêm middleware sau vào ngay sau đoạn `app.add_middleware(CORSMiddleware, ...)` trong file `src/ai-core/app/main.py`:

```python
@app.middleware("http")
async def correlation_id_middleware(request: Request, call_next):
    """
    Echo hoặc tự sinh correlation ID để phục vụ distributed tracing xuyên suốt
    Web -> Gateway -> Analysis API -> Worker -> AI Core.
    """
    correlation_id = request.headers.get("X-Correlation-Id") or str(uuid.uuid4())
    response = await call_next(request)
    response.headers["X-Correlation-Id"] = correlation_id
    return response
```

---

### 2. Mẫu nội dung cập nhật Description cho 3 thẻ Jira của M4

M4 mở từng thẻ tương ứng trên Jira board và dán nội dung sau vào mục **Description**:

#### Thẻ `SCRUM-329` — `[W3]M4] Cung cấp inference Fundus thực tế cho Worker`
```markdown
h3. Mục tiêu:
Cung cấp endpoint phân vùng mạch máu võng mạc Frangi v0.2 thực tế (non-mock), bảo toàn hình học gốc và tích hợp xác thực RS256 Service JWT fail-closed.

h3. Phạm vi triển khai & Acceptance Criteria:
* [x] Cung cấp endpoint POST /api/v1/analyze chạy thuật toán Frangi v0.2 theo cấu hình config/ai-frangi.yaml.
* [x] Giữ nguyên kích thước gốc (999x960), không resize/crop làm lệch hệ toạ độ của Worker/Annotations.
* [x] Xác thực RS256 Service JWT (Bearer token): kiểm tra issuer ("aura-identity-s3-v1"), audience ("aura-ai"), scope ("ai:analyze"), RSA key >= 2048 bit.
* [x] Cơ chế Fail-Closed: Thiếu/sai key công khai trả 503; token lỗi/hết hạn trả 401; thiếu scope trả 403.
* [x] Cung cấp endpoint /health (liveness) và /ready (readiness kiểm tra Frangi config và signing key).
* [x] Kết quả trả về đúng Contract 1.2: status="WARNING", risk_available=false, risk_assessment=null kèm cảnh báo RISK_UNAVAILABLE.

h3. Bằng chứng kiểm thử & Bàn giao:
* Mã nguồn: PR #60 (nhánh feature/SCRUM-329-s3-ai-frangi).
* Tài liệu bàn giao: docs/integration/M4_Sprint3_integration_status.md.
```

#### Thẻ `SCRUM-330` — `[W3]M4] Hoàn thiện kiểm tra chất lượng và pipeline dữ liệu`
```markdown
h3. Mục tiêu:
Xây dựng chốt chặn chất lượng (Quality gates) từ chối ảnh xấu trước khi phân vùng và chuẩn hóa pipeline chia tập dữ liệu CHASE-DB1 không rò rỉ dữ liệu.

h3. Phạm vi triển khai & Acceptance Criteria:
* [x] Kiểm tra kỹ thuật chất lượng ảnh đầu vào: luminance, contrast, sharpness, và tỷ lệ vùng võng mạc (region ratio).
* [x] Từ chối ảnh kém chất lượng với HTTP 422 LOW_QUALITY_IMAGE mà không thực hiện phân vùng (tiết kiệm tài nguyên).
* [x] Chuẩn hóa chia tập dữ liệu CHASE-DB1 (seed=42): 16 train, 6 validation, 6 test theo mức đối tượng (subject-level split).
* [x] Kịch bản huấn luyện thử nghiệm (smoke-training) xác minh test_split_touched == False và subject_leakage_detected == False.

h3. Bằng chứng kiểm thử & Bàn giao:
* Script kiểm thử: src/ai-core/scripts/run_training_smoke.py.
* Báo cáo kết quả: docs/benchmarks/segmentation_training_smoke_v0.1.json.
```

#### Thẻ `SCRUM-331` — `[W3]M4] Kiểm thử AI contract và bàn giao benchmark`
```markdown
h3. Mục tiêu:
Đồng bộ OpenAPI/JSON Schema phiên bản 1.2, thực hiện kiểm thử tự động trên ảnh võng mạc thật và bàn giao báo cáo benchmark hiệu năng runtime API.

h3. Phạm vi triển khai & Acceptance Criteria:
* [x] Đồng bộ OpenAPI 1.2.0: contracts/ai-openapi-1.2.yaml, ai-request-1.2.schema.json, ai-response-1.2.schema.json.
* [x] Suite test tự động với pytest (102 passed) bao phủ toàn bộ các mã lỗi HTTP (400, 401, 403, 422, 500, 503).
* [x] Kiểm thử ít nhất 3 ảnh Fundus thật từ CHASE-DB1 qua endpoint non-mock xác thực schema hợp lệ.
* [x] Báo cáo benchmark runtime trên 6 ảnh test: Latency trung bình 2,516 ms (P95: 2,595 ms), Dice mean: 0.420, Specificity: 0.956.

h3. Bằng chứng kiểm thử & Bàn giao:
* Test suite: src/ai-core/tests/test_frangi_sprint3.py.
* Báo cáo benchmark: docs/benchmarks/frangi_runtime_api_v1.2.json.
```

---

### 3. Ghi chú kiến trúc cho `src/ai-core/app/services/preprocessing.py`

* **Vấn đề**: Hiện có hai mô-đun tiền xử lý song song (`preprocessing.py` và `frangi_service.py`), dễ gây hiểu lầm `preprocessing.py` bị bỏ quên.
* **Cách khắc phục**: Thêm docstring giải thích ngay đầu file `preprocessing.py`:

```python
"""
AURA AI Preprocessing Module (Sprint 2 Exploration).

LƯU Ý KIẾN TRÚC:
- Module này chứa các hàm tiền xử lý nâng cao (CLAHE, resize 512x512, FOV mask) phục vụ
  nghiên cứu và huấn luyện mô hình Deep Learning trong tương lai.
- Trong Sprint 3, endpoint phân vùng Frangi v0.2 bắt buộc giữ nguyên gradient ảnh gốc
  và kích thước ban đầu (không áp dụng CLAHE hay resize để đảm bảo đúng ngưỡng baseline 0.02).
  Các kiểm tra chất lượng phục vụ Frangi được thực thi trực tiếp tại frangi_service.py.
"""
```

---

### 4. Biên bản phối hợp liên dịch vụ gửi M2 (Worker Integration Notes)

M4 gửi lưu ý 3 điểm sống còn cho **M2 (Võ Hữu Duy)** khi tích hợp Worker gọi AI Core:

1. **`patient_id` bắt buộc phải là `null`**: Schema 1.2 quy định `patient_id: null` để đảm bảo không rò rỉ dữ liệu định danh bệnh nhân vào AI container. Nếu Worker gửi chuỗi ID, AI Core sẽ từ chối với lỗi HTTP `422 SCHEMA_VALIDATION_ERROR`.
2. **`include_mask: true` và `include_overlay: true` là bắt buộc**: Cả hai cờ này phải luôn được đặt là `true` trong payload gửi sang AI Core.
3. **Cấu hình Worker Timeout $\ge 10 - 15$ giây**: Thuật toán Frangi v0.2 xử lý trên CPU cho ảnh kích thước lớn ($999 \times 960$) mất trung bình $\approx 2.5$ giây/ảnh. Worker không được dùng timeout mặc định 2 giây (sẽ gây timeout gãy luồng).
4. **Header xác thực Bearer token**: Worker phải gọi Identity API để lấy token dịch vụ với `audience: "aura-ai"` và `scope: "ai:analyze"` trước khi gọi AI Core.
