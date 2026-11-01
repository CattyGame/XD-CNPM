# Sổ tay Vận hành Camera Ingestion Pipeline v1.2 — AURA

- **Mã công việc Jira**: [SCRUM-350 (W4-M1-05)](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-350)
- **Thành viên phụ trách**: Nguyễn Trương Hậu (M1 — Kiến trúc sư trưởng & Hạ tầng)
- **Phiên bản tài liệu**: 1.2.0 (Sprint 4–5 Tích hợp)
- **Hệ thống mục tiêu**: Apache NiFi 1.27.0, YARP API Gateway (.NET 9), AURA Analysis API, PostgreSQL 17 Reporting Sandbox.
- **File cấu hình bàn giao**: `infra/nifi/camera_ingestion_flow_v2.json`, `infra/nifi/flow.json.gz`.
- **Trạng thái**: Đã nghiệm thu toàn diện (Passed AC1, AC2, AC3, Deduplication & Database Sync).

---

## 1. Kiến trúc Tổng thể Luồng Camera Ingestion v2.0

```
                   LUỒNG CAMERA INGESTION PIPELINE (SPRINT 4–5)
 ┌──────────────────────┐
 │ Camera Simulator     │ (Bơm ảnh qua HTTP POST JSON trên cổng 8081)
 └──────────┬───────────┘
            │
            ▼
 ┌──────────────────────┐
 │ ListenHTTP (8081)    │
 └──────────┬───────────┘
            │
            ▼
 ┌──────────────────────┐
 │ EvaluateJsonPath     │ ──(Lỗi schema)──┐
 └──────────┬───────────┘                 │
            │ (matched)                   │
            ▼                             │
 ┌──────────────────────┐                 │
 │ RouteOnAttribute     │ ──(Sai Modality)┤
 └──────────┬───────────┘                 │
            │ (ValidFundus)               │
            ▼                             │
 ┌──────────────────────┐                 │
 │ ValidateImage & Hash │ ──(Ảnh hỏng)────┤
 └──────────┬───────────┘                 │
            │ (image.sha256)              │
            ▼                             │
 ┌──────────────────────┐                 │
 │ Deduplicate-By-Hash  │ ──(Trùng SHA256)─► [Drop & Log Cảnh báo]
 └──────────┬───────────┘                 │
            │ (Unique)                    │
            ▼                             │
 ┌──────────────────────┐                 │
 │ PrepareMultipart     │ ──(Lỗi build)───┤
 └──────────┬───────────┘                 │
            │                             │
            ▼                             │
 ┌──────────────────────┐                 │
 │ InvokeHTTP (Gateway) │ ──(HTTP 4xx)────┤
 └──────┬────────────┬──┘                 │
        │ (202)      │ (429/503/timeout)  │
        │            ▼                    │
        │   ┌─────────────────────────┐   │
        │   │ Bounded-Camera-Retry    │   │
        │   │ (Tối đa 10 lần backoff) │   │
        │   └──────┬──────────┬───────┘   │
        │          │ (retry)  │ (Exceed)  │
        │          └──Loop────┼───────────┤
        │                     │           │
        ▼                     ▼           ▼
 ┌──────────────────────┐   ┌───────────────────────────┐
 │ Record-Camera-Receipt│   │ UpdateAttribute-Quarantine│
 │ Safe-v2 (Postgres DB)│   └─────────────┬─────────────┘
 └──────────┬───────────┘                 │
            │                             ▼
            ▼               ┌───────────────────────────┐
 ┌──────────────────────┐   │ PutFile-Quarantine        │
 │ camera_receipts      │   │ (/opt/nifi/quarantine)    │
 │ (COMMITTED)          │   └───────────────────────────┘
 └──────────────────────┘
```

---

## 2. Thông số Thành phần trong Flow Definition v2

| Processor | Loại Processor | Chức năng trong Pipeline v2 |
|---|---|---|
| **`ListenHTTP-CameraSimulator`** | `standard.ListenHTTP` | Nhận request POST JSON từ thiết bị Camera hoặc Simulator trên port 8081 (`/ingest/camera`). |
| **`EvaluateJsonPath-Metadata`** | `standard.EvaluateJsonPath` | Trích xuất các thuộc tính `request_id`, `patient_id`, `device_id`, `modality`, `eye_side`. |
| **`RouteOnAttribute-Validation`** | `standard.RouteOnAttribute` | Kiểm định nghiệp vụ: Chỉ cho phép ảnh đáy mắt `FUNDUS`, rẽ nhánh `UnsupportedModality` vào Quarantine. |
| **`ExecuteScript-ValidateImage`** | `script.ExecuteScript` | Giải mã Base64, kiểm tra kích thước tối thiểu, kiểm tra magic bytes JPEG (`FF D8 FF`) / PNG (`89 50 4E 47`) và tính toán mã hash SHA-256 (`image.sha256`). |
| **`Deduplicate-By-Hash`** | `groovyx.ExecuteGroovyScript` | Kiểm tra chống gửi trùng ảnh: Duy trì cache SHA-256; phát hiện ảnh gửi lặp để drop an toàn và ghi log cảnh báo, chống nghẽn hệ thống. |
| **`ExecuteScript-PrepareMultipart`** | `script.ExecuteScript` | Đóng gói nhị phân thành chuẩn `multipart/form-data`, thiết lập boundary và headers chuẩn bị chuyển tiếp. |
| **`InvokeHTTP-PostToGateway`** | `standard.InvokeHTTP` | Đẩy request chính thức sang API Gateway (`http://host.docker.internal:5000/api/v1/analyses/camera`) kèm Service Token và Header `x-device-id`. |
| **`Bounded-Camera-Retry-Delivery-v2`** | `standard.RetryFlowFile` | Quản lý thử lại khi mạng bận hoặc Gateway quá tải (HTTP 429, 503, timeout): Tối đa 10 lần với thời gian chờ tăng dần (1s, 2s, 4s...); hết lượt chuyển sang `camera.exhausted`. |
| **`UpdateAttribute-Quarantine`** | `attributes.UpdateAttribute` | Gán nhãn nguyên nhân cách ly (`quarantine.reason`, `error.code`, `timestamp`) phục vụ tra cứu. |
| **`PutFile-QuarantinePersistence`** | `standard.PutFile` | Lưu payload lỗi vào volume cách ly an toàn (`/opt/nifi/quarantine`). |
| **`Record-Camera-Receipt-Safe-v2`** | `groovyx.ExecuteGroovyScript` | Giao tiếp JDBC an toàn với PostgreSQL Reporting DB, gọi stored procedure `record_camera_receipt` và gắn nhãn `COMMITTED`. |

---

## 3. Cấu hình Parameter Context (Bảo mật Không Lưu Secret)

Parameter Context **`CameraIngestionContext`** chuẩn hóa toàn bộ tham số môi trường:

| Tên Tham số | Giá trị Cấu hình | Mô tả | Nhạy cảm |
|---|---|---|:---:|
| `GatewayBaseUrl` | `http://host.docker.internal:5000` | Địa chỉ API Gateway định tuyến nội bộ | Không |
| `CameraIngestRoute` | `/api/v1/analyses/camera` | Route tiếp nhận ảnh camera tại Gateway | Không |
| `MaxRetryCount` | `10` | Số lần thử lại tối đa trước khi đưa vào hàng đợi kiệt sức | Không |
| `QuarantineDirectory` | `/opt/nifi/quarantine` | Thư mục lưu trữ các FlowFiles bị cách ly | Không |

> [!IMPORTANT]
> Toàn bộ các thông tin mật (Mật khẩu cơ sở dữ liệu `aura_reporting`, Khóa giải mã Service JWT) đều được nạp thông qua biến môi trường Docker hoặc Controller Service bảo mật (`DBCPConnectionPool`), tuyệt đối không lưu cứng trong file template JSON.

---

## 4. Cơ chế Vận hành Cốt lõi

### 4.1. Cơ chế Chống trùng lặp (Deduplication)
- Khi ảnh hợp lệ được giải mã, mã băm SHA-256 gồm 64 ký tự hex được tính toán.
- Bộ lọc `Deduplicate-By-Hash` đối chiếu với danh sách các hash đang xử lý.
- Nếu phát hiện hash đã tồn tại: FlowFile được chuyển sang nhánh loại bỏ với thuộc tính `dedup.status = DUPLICATE_DROPPED` và ghi nhật ký cảnh báo `log.warn`, ngăn ngừa việc gửi nhiều yêu cầu phân tích cho cùng một ảnh.

### 4.2. Cơ chế Phân luồng Cách ly (Quarantine Routing)
- Bất kỳ FlowFile nào gặp lỗi tại một trong các khâu:
  - JSON sai cú pháp hoặc thiếu trường bắt buộc $\rightarrow$ `INVALID_JSON` / `unmatched`.
  - Phương thức chụp không hỗ trợ (ví dụ ảnh OCT) $\rightarrow$ `UnsupportedModality`.
  - Dữ liệu ảnh bị hỏng (corrupted bytes) $\rightarrow$ `INVALID_OR_UNSUPPORTED_IMAGE`.
  - Gateway trả về mã lỗi phía client (HTTP 400, 422) $\rightarrow$ `GATEWAY_REJECTED`.
- Toàn bộ đều được định tuyến tức thời ($< 1$ giây) vào thư mục cách ly mà không làm gián đoạn luồng xử lý của các ảnh camera bình thường khác.

### 4.3. Cơ chế Tự động Thử lại có Giới hạn (Limited Exponential Backoff Retry)
- Khi Gateway hoặc mạng tạm thời mất kết nối (HTTP 503 Service Unavailable, 429 Too Many Requests, hoặc Connection Refused):
  - Processor `InvokeHTTP` rẽ nhánh sang `Bounded-Camera-Retry-Delivery-v2`.
  - Thuộc tính `camera.retry.count` tăng dần từ 1 đến 10.
  - NiFi áp dụng hình phạt độ trễ (Penalization) tăng dần theo hàm số mũ ($2^n$ giây) để tránh làm sập Gateway khi phục hồi.
  - Sau 10 lần thử lại không thành công, FlowFile được chuyển an toàn sang trạng thái `camera.exhausted`.

---

## 5. Bằng chứng Thực nghiệm Nghiệm thu (Definition of Done Verification)

Bộ kiểm thử tự động `scripts/dev/Test-Sprint4-Task5-NiFi-Ingestion.ps1` đã thực thi và kiểm chứng 100% các tiêu chí nghiệm thu của Jira **SCRUM-350 / W4-M1-05**:

### AC1 — Bơm 10 ảnh camera thành công qua NiFi & Gateway vào PostgreSQL
- **Kịch bản**: Bơm 10 file ảnh võng mạc đáy mắt từ dataset chuẩn `CHASE_DB1` qua luồng Camera Ingestion.
- **Kết quả**:
  - 10/10 ảnh được chuyển tiếp qua Gateway và nhận mã phản hồi **`HTTP 202 Accepted`**.
  - 10 mã biên lai duy nhất được khởi tạo kèm Batch ID tương ứng.
  - Cơ sở dữ liệu PostgreSQL `reporting.camera_ingestion_receipts` ghi nhận đầy đủ 10 bản ghi với trạng thái `COMMITTED`.

### AC2 — Cách ly file ảnh hỏng (Corrupted Bytes) vào Quarantine trong $< 1$s
- **Kịch bản**: Bơm 1 file ảnh cố tình làm sai lệch header (corrupted bytes / invalid magic bytes).
- **Kết quả**:
  - NiFi phát hiện ngay lập tức tại bước kiểm tra cấu trúc nhị phân.
  - File được chuyển nhánh vào thư mục `/opt/nifi/quarantine/INVALID_OR_UNSUPPORTED_IMAGE` trong thời gian **$< 0.2$ giây**.
  - Pipeline tiếp tục vận hành bình thường, không gây tắc nghẽn các ảnh sau.

### AC3 — Tắt Gateway tạm thời, Auto-Retry theo Backoff và Phục hồi Không Mất Dữ liệu
- **Kịch bản**: Tắt tạm thời API Gateway, bơm ảnh camera, sau đó khởi động lại Gateway.
- **Kết quả**:
  - NiFi tự động kích hoạt nhánh Retry, FlowFile được giữ an toàn trong hàng đợi có áp dụng Backoff.
  - Khi Gateway phục hồi, ảnh được tự động gửi tiếp thành công nhận HTTP 202.
  - Toàn bộ dữ liệu được lưu vết đầy đủ trong Data Provenance, tỷ lệ mất mát dữ liệu: **0%**.

---

## 6. Lệnh Vận hành & Bảo trì Thường nhật

### Kiểm tra trạng thái Pipeline NiFi
```powershell
# Kiểm tra số lượng FlowFiles trong hàng đợi
docker exec aura-s3-nifi-1 ls -la /opt/nifi/quarantine

# Kiểm tra dữ liệu biên lai trong PostgreSQL
docker exec -e PGPASSWORD="VgoMrxHYz3jxj3SABUh4gb1HdjC2UBIijNNtlNP00drjxB77" aura-s3-reporting-postgres-1 psql -U aura_s3_admin -d aura_reporting -c "SELECT request_id, device_id, image_sha256, recorded_at FROM reporting.camera_ingestion_receipts ORDER BY recorded_at DESC LIMIT 10;"
```

### Chạy Suite Kiểm thử Tự động Sprint 4 Task 5
```powershell
powershell -ExecutionPolicy Bypass -File D:\XD-PMHDT\scripts\dev\Test-Sprint4-Task5-NiFi-Ingestion.ps1
```
