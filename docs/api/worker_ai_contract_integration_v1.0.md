# ĐẶC TẢ HỢP ĐỒNG TÍCH HỢP WORKER — AI CORE (AURA MODULE M4 — M2)

> **Dự án**: AURA — AI Retinal Analysis Platform  
> **Mã công việc Jira**: SCRUM-310 (W2-M4-03)  
> **Người biên soạn**: Đào Duy Quân (quandd2937@ut.edu.vn — M4: AI Engineer & NiFi)  
> **Phối hợp kỹ thuật**: Võ Hữu Duy (M2 — Analysis Service & Worker)  
> **Người duyệt**: Nguyễn Trương Hậu (M1 — Tech Lead)  
> **Phiên bản tài liệu**: v1.0.0  
> **Ngày hiệu lực**: 16/10/2026 (Khóa chuẩn Sprint 2)  
> **Tài liệu căn cứ**: `AURA_Sprint2_Contract_Cau_hinh_chung_v1.0.md` (Mục 9)

---

## 1. Mục đích và nguyên tắc kiến trúc

Tài liệu này chuẩn hóa giao thức giao tiếp nội bộ giữa **Analysis Worker** (phía backend M2) và **AI Core Service** (phía M4).
- **Phạm vi mạng**: Hoàn toàn nội bộ trong mạng Docker `aura_network`. Endpoint: `http://ai-core:8000/api/v1/analyze`.
- **Bảo mật & Phân quyền**: Trình duyệt và API Gateway tuyệt đối **không được định tuyến công khai** tới endpoint `/api/v1/analyze`.
- **Khử định danh (De-identification)**: Worker gửi `patient_id: null` mặc định sang AI Core nhằm bảo vệ danh tính bệnh nhân theo tiêu chuẩn an toàn dữ liệu y tế. AI Core không sử dụng `patient_id` để cấp quyền.

---

## 2. Đặc tả Giao thức Yêu cầu (Request Specification)

### 2.1. Cấu trúc HTTP Request
- **Method**: `POST`
- **URL**: `http://ai-core:8000/api/v1/analyze`
- **Headers**:
  - `Content-Type: application/json`
  - `X-Correlation-Id: <UUID>` (Truyền xuyên suốt từ Gateway $\rightarrow$ Analysis $\rightarrow$ Worker $\rightarrow$ AI Core)
  - `User-Agent: AURA.Analysis.Worker/0.6.0`

### 2.2. Chi tiết Payload đầu vào (JSON)

```json
{
  "request_id": "d0000000-0000-4000-8000-000000000001",
  "patient_id": null,
  "mode": "retina_vessels",
  "modality": "FUNDUS",
  "eye_side": "left",
  "image_base64": "<Chuỗi Base64 ảnh JPEG/PNG lấy từ Private Storage>",
  "include_mask": true,
  "include_overlay": true
}
```

| Trường | Kiểu dữ liệu | Bắt buộc | Ràng buộc & Quy tắc nghiệp vụ |
| :--- | :--- | :---: | :--- |
| `request_id` | String (UUID) | Có | Là `analysisId` chuỗi. Khi Worker retry, **phải giữ nguyên `analysisId`** này để AI nhận biết cùng một phiên phân tích. |
| `patient_id` | String hoặc null | Không | Gửi `null` theo quy định bảo mật khử định danh. |
| `mode` | String | Có | Bắt buộc là `"retina_vessels"`. Giá trị khác trả mã HTTP `422 UNSUPPORTED_MODE`. |
| `modality` | String | Có | Bắt buộc là `"FUNDUS"`. Nếu nhận `"OCT"` trả HTTP `422 UNSUPPORTED_MODALITY`. |
| `eye_side` | String | Có | Nhận `"left"`, `"right"`, hoặc `"unknown"` (chuyển đổi từ `eyeSide` public). |
| `image_base64` | String | Có | Chuỗi Base64 đọc trực tiếp từ container private `aura-originals`. Không log nội dung chuỗi này vào file log hay outbox. |
| `include_mask` | Boolean | Không | Mặc định `true` khi Worker cần lưu file phân đoạn nhị phân (`aura-annotations`). |
| `include_overlay` | Boolean | Không | Mặc định `true` khi Worker cần lưu file lớp phủ thị giác cho bác sĩ. |

---

## 3. Ma trận Ánh xạ Dữ liệu (AI Core Snake_case $\longrightarrow$ Public CamelCase)

Worker có trách nhiệm ánh xạ chính xác kết quả phân tích từ AI Core sang schema công khai của Analysis Service theo bảng dưới đây:

| Trường AI Core (nội bộ - snake_case) | Trường Analysis Public (camelCase) | Kiểu dữ liệu | Ý nghĩa & Quy tắc ánh xạ |
| :--- | :--- | :--- | :--- |
| `status` | `status` | String | `"SUCCESS"` hoặc `"WARNING"` $\rightarrow$ Analysis chuyển thành `COMPLETED`. |
| `is_mock` | `isMock` | Boolean | Phản ánh tính chất giả lập. Hiện tại **bắt buộc `true`** theo Sprint 2 baseline. |
| `model_version` | `modelVersion` | String | Phiên bản mô hình AI (VD: `"mock-v0.1"` hoặc `"frangi-v0.2"`). |
| `threshold_version`| `thresholdVersion`| String | Phiên bản ngưỡng phân đoạn (VD: `"v0.1"`). |
| `config_version` | `configVersion` | String | Phiên bản cấu hình thuật toán. |
| `processing_time_ms`| `processingTimeMs`| Float | Thời gian thực thi đo đạc tại runtime AI Core (ms). |
| **`vessel_metrics`** | | Object | Các chỉ số hình thái mạch máu võng mạc |
| `.vessel_density` | `.vesselDensity` | Float | Tỷ lệ diện tích mạch máu trên diện tích FOV (0.0 – 1.0). |
| `.tortuosity_index`| `.tortuosityIndex`| Float | Chỉ số độ cong uốn lượn của mạch máu. |
| `.av_ratio` | `.avRatio` | Float | Tỷ lệ đường kính động mạch / tĩnh mạch (AVR). |
| `.fractal_dimension`| `.fractalDimension`| Float | Chiều fractal đặc trưng cho mức độ phân nhánh vi mạch. |
| `.branching_points`| `.branchingPoints`| Integer | Số lượng điểm ngã ba phân nhánh của mạng lưới mạch. |
| **`risk_assessment`** | | Object | Đánh giá nguy cơ tim mạch/đột quỵ sơ bộ |
| `.risk_score` | `.riskScore` | Float | Điểm số rủi ro chuẩn hóa (0.0 – 1.0). |
| `.risk_level` | `.riskLevel` | Enum | Mức độ rủi ro: `"LOW"`, `"MEDIUM"`, `"HIGH"`. |
| `.confidence_score`| `.confidenceScore`| Float | Độ tin cậy của thuật toán (0.0 – 1.0). |
| `.indicators` | `.indicators` | Array[String]| Danh sách căn cứ chỉ dấu hình thái học. |
| `.disclaimer` | `.disclaimer` | String | Cảnh báo pháp lý y khoa bắt buộc (không được lược bỏ). |
| **`segmentation`** | | Object | Dữ liệu nhị phân phân đoạn mạch |
| `.mask_base64` | `maskFileId` (UUID) | String | Worker giải mã PNG Base64, lưu vào `aura-annotations`, cấp `fileId`. |
| `.overlay_base64` | `overlayFileId` (UUID)| String | Worker giải mã PNG Base64, lưu vào `aura-annotations`, cấp `fileId`. |

> [!IMPORTANT]
> **Quy tắc về Warning và Limitations**:
> Khi AI trả về `status: WARNING`, Worker **không được tự ý đánh dấu là `LOW_QUALITY`**. Phải lưu trữ đầy đủ `warnings` và `limitations` đi kèm kết quả hợp lệ để hiển thị minh bạch cho bác sĩ.

---

## 4. Xử lý Mã lỗi và Cơ chế Thử lại (Error Handling & Retry Strategy)

### 4.1. Bảng quy đổi mã lỗi và hành vi Worker

| Mã HTTP AI Core | Mã lỗi nội bộ | Tính chất lỗi | Hành vi của Worker đối với Analysis & Billing |
| :---: | :--- | :---: | :--- |
| **400** | `INVALID_IMAGE_PAYLOAD` | Terminal (Không retry) | • Đánh dấu Analysis: `FAILED` (`retryable: false`).<br>• Gửi yêu cầu Billing: `release` 1 credit với lý do `VALIDATION_ABORT`. |
| **422** | `LOW_QUALITY_IMAGE` | Terminal (Chất lượng kém) | • Đánh dấu Analysis: `LOW_QUALITY` (không có risk result).<br>• Gửi yêu cầu Billing: `release` 1 credit với lý do `LOW_QUALITY`. |
| **422** | `UNSUPPORTED_MODALITY`<br>`UNSUPPORTED_MODE` | Terminal (Sai cấu hình) | • Đánh dấu Analysis: `FAILED` (`retryable: false`).<br>• Gửi yêu cầu Billing: `release` 1 credit. |
| **500 / 503** | `INFERENCE_RUNTIME_ERROR`<br>hoặc Network Timeout | Transient (Lỗi tạm thời) | • **Thực hiện Retry** theo chính sách giãn cách bên dưới.<br>• Giữ nguyên reservation credit trong suốt các lần retry. |

### 4.2. Chính sách Retry giãn cách (Exponential Backoff)
- **Số lần thử tối đa**: Tổng cộng **3 lần** (1 lần gọi đầu tiên + tối đa 2 lần retry).
- **Thời gian chờ giữa các lần**:
  - Thử lần 1 thất bại $\longrightarrow$ Chờ **5 giây** (`Worker__RetryDelaysSeconds__0=5`) $\longrightarrow$ Thử lần 2.
  - Thử lần 2 thất bại $\longrightarrow$ Chờ **15 giây** (`Worker__RetryDelaysSeconds__1=15`) $\longrightarrow$ Thử lần 3.
- **Vượt quá 3 lần thất bại**:
  - Đẩy message vào hàng đợi chết (`DEAD_LETTER` queue).
  - Cập nhật trạng thái Analysis: `FAILED` (`retryable: false`), lưu `lastError`.
  - Phát sự kiện `analysis.failed` để Billing thực hiện `release` credit hoàn tiền cho bệnh nhân.
  - Tuyệt đối **không làm mất ảnh gốc** trong Private Storage.

---

## 5. Danh mục Kiểm tra Nghiệm thu Tích hợp (Verification Checklist)

- [x] AI Core expose cổng 8000 nội bộ trong `aura_network`, Swagger UI sẵn sàng tại `/docs`.
- [x] Kích thước PNG mask và overlay trả về trùng khớp tuyệt đối với kích thước ảnh gốc hoặc kích thước chuẩn hóa.
- [x] Schema và tài liệu OpenAPI YAML (`contracts/ai_service_openapi.yaml`) đồng bộ 100% với file JSON (`contracts/ai_service_openapi.json`).
- [x] Test suite 43/43 tests tự động bao phủ toàn bộ các kịch bản thành công, cảnh báo, từ chối định dạng và ảnh kém chất lượng.

---
*Biên soạn bởi: Đào Duy Quân (M4 — AI & NiFi)*  
*Xác nhận phối hợp: Module M4 (AI Core) & Module M2 (Analysis Worker)*
