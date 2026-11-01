# AURA Sprint 3 — Billing Credit Decision Table & Integration Contract

**Document Version:** 1.0  
**Work Item:** SCRUM-326 (W3-M3-01: Hoàn thiện reservation và quyết toán credit cho một ảnh)  
**Author:** M3 (đảm nhiệm bởi M1 — Lead Architect)  
**Target Audience:** M2 (Analysis API & Worker Owner), M5 (Frontend Result & Billing UI), M6 (Profile & Tenant Owner)  
**Status:** Canonical Sprint 3 Specification — APPROVED  

---

## 1. Mục Đích & Nguyên Tắc Cốt Lõi (Core Principles)

Tài liệu này xác lập đặc tả kỹ thuật và hợp đồng tích hợp chính thức giữa module **Billing** với **Analysis API**, **Aura Worker**, và **Profile/Identity** cho Sprint 3.

### Các nguyên tắc bất biến:
1. **Tỷ lệ quy đổi 1:1**: Đúng **1 ảnh chụp đáy mắt (FUNDUS) hợp lệ = 1 unit credit**.
2. **Khấu trừ trước (Reserve First)**: Credit được tạm giữ (`RESERVED`) ngay khi request tạo phân tích được tiếp nhận hợp lệ tại Analysis API, **trước** khi gửi job xử lý vào hàng đợi.
3. **Capture đúng 1 lần khi thành công**: Khi phân tích hoàn tất (`COMPLETED`) và có kết quả AI hợp lệ (`valid_result = true`), hệ thống thực hiện quyết toán (`CAPTURE`). Available balance **không thay đổi** vì đã bị trừ từ bước Reserve.
4. **Release đúng 1 lần khi thất bại kết thúc**: Khi phân tích gặp lỗi kết thúc (`FINAL_FAILURE`, `VALIDATION_ABORT`, `STORAGE_ABORT`, `ANALYSIS_ABORT`, hoặc `LOW_QUALITY` không có kết quả hợp lệ), hệ thống hoàn trả (`RELEASE`) đúng 1 credit về ví (`balance + 1`).
5. **Lỗi tạm thời (Retryable Failure) KHÔNG giải phóng credit**: Khi Worker gặp lỗi có thể thử lại (`retryable = true`), reservation được giữ nguyên ở trạng thái `RESERVED` để phục vụ các lần retry tiếp theo.
6. **Loại trừ lẫn nhau (Mutual Exclusivity)**: Một reservation sau khi đã chuyển sang trạng thái cuối (`CAPTURED` hoặc `RELEASED`) thì **không bao giờ** được chuyển trạng thái lần nữa. Mọi nỗ lực capture một reservation đã release hoặc release một reservation đã capture đều bị từ chối với lỗi `409 ERR_RESERVATION_STATE_CONFLICT`.
7. **Idempotency tuyệt đối**: Mọi thao tác tài chính (Reserve, Capture, Release) bắt buộc phải có header `Idempotency-Key` (dạng UUID).
   - Replay cùng key và cùng payload: Trả về kết quả trước đó với header `Idempotent-Replayed: true` (hoặc HTTP 200 OK), không thay đổi số dư và không ghi thêm ledger.
   - Trùng key nhưng khác payload (fingerprint mismatch): Trả về `409 ERR_IDEMPOTENCY_CONFLICT`.
8. **Cô lập môi trường TEST và LIVE**: Kết quả mock/test (`is_mock = true`) tuyệt đối không được phép capture vào ví `LIVE`. Vi phạm sẽ bị từ chối với `409 ERR_MOCK_LIVE_FORBIDDEN`.
9. **Bảo mật và chống lộ thông tin nhạy cảm (Privacy & Security)**: Không bao giờ ghi log chứa Bearer token, client secret, thông tin bệnh nhân (PHI) hay dữ liệu ảnh thô.

---

## 2. Bảng Quyết Định Credit Toàn Diện (Authoritative Decision Table)

| STT | Trạng thái Nghiệp vụ / Event | Điều kiện Kết quả | Thao tác Billing | Trạng thái Reservation | Thay đổi Balance | Bản ghi Ledger | Mã HTTP Trả về |
|:---:|---|---|:---:|:---:|:---:|:---:|:---:|
| **1** | Tạo phân tích mới (`POST /internal/v1/billing/reservations`) | Số dư ví $\ge 1$ unit, Profile hợp lệ | `RESERVE` | `RESERVED` | **$-1$** | `RESERVE` (amount: -1) | `201 Created` |
| **2** | Tạo phân tích mới | Số dư ví $< 1$ unit | `REJECT` | Không tạo | **$0$** | Không ghi | `402 Payment Required` (`ERR_INSUFFICIENT_CREDIT`) |
| **3** | Tạo phân tích trùng lặp (`Idempotency-Key` trùng, payload khớp) | Payload khớp fingerprint | `REPLAY` | `RESERVED` | **$0$** | Không ghi | `200 OK` (`Idempotent-Replayed: true`) |
| **4** | Tạo phân tích trùng key nhưng khác payload | Fingerprint khác nhau | `REJECT` | Không đổi | **$0$** | Không ghi | `409 Conflict` (`ERR_IDEMPOTENCY_CONFLICT`) |
| **5** | Retry trung gian (`analysis.failed`, `retryable = true`) | Đang chờ retry / queue lại | `NONE` | `RESERVED` | **$0$** | Không ghi | N/A (Giữ nguyên reservation) |
| **6** | Xử lý thành công (`analysis.completed`) | Authoritative `COMPLETED`, có valid result | `CAPTURE` | `CAPTURED` | **$0$** | `CAPTURE` (amount: 0) | `200 OK` |
| **7** | Thất bại kết thúc (`analysis.failed`, `retryable = false`) | Authoritative `FAILED`, không kết quả | `RELEASE` (`FINAL_FAILURE`) | `RELEASED` | **$+1$** | `RELEASE` (amount: +1) | `200 OK` |
| **8** | Ảnh chất lượng kém (`LOW_QUALITY`) | Không thể chẩn đoán, không có kết quả hợp lệ | `RELEASE` (`LOW_QUALITY`) | `RELEASED` | **$+1$** | `RELEASE` (amount: +1) | `200 OK` |
| **9** | Hủy / Hủy bỏ tải lên (`VALIDATION_ABORT` / `STORAGE_ABORT`) | Client hủy hoặc lưu ảnh thất bại | `RELEASE` (`ABORT`) | `RELEASED` | **$+1$** | `RELEASE` (amount: +1) | `200 OK` |
| **10** | Gọi Release khi đã Capture | Đã là `CAPTURED` | `REJECT` | `CAPTURED` | **$0$** | Không ghi | `409 Conflict` (`ERR_RESERVATION_STATE_CONFLICT`) |
| **11** | Gọi Capture khi đã Release | Đã là `RELEASED` | `REJECT` | `RELEASED` | **$0$** | Không ghi | `409 Conflict` (`ERR_RESERVATION_STATE_CONFLICT`) |
| **12** | Race condition Capture và Release đồng thời | Hai luồng xử lý cùng lúc | Luồng đến trước thắng, luồng sau bị chặn | `CAPTURED` hoặc `RELEASED` | Tuân thủ luồng thắng | Đúng 1 ledger terminal | Luồng 1: `200 OK`, Luồng 2: `409 Conflict` |
| **13** | Mock phân tích trên ví LIVE | `is_mock = true` & ví `LIVE` | `REJECT` | `RESERVED` | **$0$** | Không ghi | `409 Conflict` (`ERR_MOCK_LIVE_FORBIDDEN`) |
| **14** | Gọi API thiếu/sai Service JWT Token | Token không hợp lệ / hết hạn | `DENY` | Không đổi | **$0$** | Không ghi | `401 Unauthorized` |
| **15** | Gọi API sai Scope (vd: `billing:read` gọi reserve) | Thiếu scope bắt buộc | `DENY` | Không đổi | **$0$** | Không ghi | `403 Forbidden` |

---

## 3. Đặc Tả Wire Contract & Giao Thức Gọi Nội Bộ

### 3.1. Reserve Credit (`POST /internal/v1/billing/reservations`)
- **Caller:** `analysis-api`
- **Scope bắt buộc:** `billing:reserve`
- **Audience:** `aura-billing`
- **Headers:**
  - `Authorization: Bearer <service_token>`
  - `Idempotency-Key: <UUID>`
  - `X-Correlation-Id: <string_max_64>`
- **Request Body:**
  ```json
  {
    "analysisId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tenantId": "c0010000-0000-4000-8000-000000000001",
    "actorAccountId": "a0010000-0000-4000-8000-000000000001",
    "patientId": "b0010000-0000-4000-8000-000000000001",
    "billingOwner": {
      "type": "ACCOUNT",
      "id": "a0010000-0000-4000-8000-000000000001"
    },
    "units": 1,
    "generation": 1
  }
  ```
- **Responses:**
  - `201 Created`: Tạo mới reservation thành công.
  - `200 OK` (với header `Idempotent-Replayed: true`): Replay thành công request cũ.
  - `402 Payment Required` (`ERR_INSUFFICIENT_CREDIT`): Số dư khả dụng không đủ 1 credit.
  - `409 Conflict` (`ERR_IDEMPOTENCY_CONFLICT` hoặc `ERR_RESERVATION_STATE_CONFLICT`).

### 3.2. Capture Reservation (`POST /internal/v1/billing/reservations/{reservationId}/capture`)
- **Caller:** Domain Settlement Consumer / Billing Internal Worker
- **Scope bắt buộc:** `billing:capture`
- **Audience:** `aura-billing`
- **Headers:**
  - `Authorization: Bearer <service_token>`
  - `Idempotency-Key: <UUID>`
  - `X-Correlation-Id: <string_max_64>`
- **Responses:**
  - `200 OK`: Quyết toán thành công, state chuyển thành `CAPTURED`.
  - `404 Not Found`: Không tìm thấy reservation.
  - `409 Conflict` (`ERR_RESERVATION_STATE_CONFLICT`): Đã bị release hoặc không ở trạng thái `RESERVED`.

### 3.3. Release Reservation (`POST /internal/v1/billing/reservations/{reservationId}/release`)
- **Caller:** `analysis-api` / Domain Settlement Consumer
- **Scope bắt buộc:** `billing:release`
- **Audience:** `aura-billing`
- **Headers:**
  - `Authorization: Bearer <service_token>`
  - `Idempotency-Key: <UUID>`
  - `X-Correlation-Id: <string_max_64>`
- **Request Body:**
  ```json
  {
    "reason": "FINAL_FAILURE"
  }
  ```
  *(Các reason hợp lệ: `VALIDATION_ABORT`, `STORAGE_ABORT`, `ANALYSIS_ABORT`, `FINAL_FAILURE`, `LOW_QUALITY`)*
- **Responses:**
  - `200 OK`: Giải phóng thành công, hoàn 1 credit về ví, state chuyển thành `RELEASED`.
  - `404 Not Found`: Không tìm thấy reservation.
  - `409 Conflict` (`ERR_RESERVATION_STATE_CONFLICT`): Đã bị capture hoặc không ở trạng thái `RESERVED`.

---

## 4. Công Thức Đối Soát Tam Giác (Tri-Party Reconciliation Formula)

Để đáp ứng nghiệm thu **AC3**, hệ thống thực hiện đối soát tự động theo 3 mối quan hệ bất biến:

$$\text{Balance Hiện Tại} = \text{Balance Khởi Tạo} + \sum (\text{Ledger Entries.amount})$$

Trong đó:
- `entry_type = 'CREDIT'`: $amount > 0$ (Nạp credit vào ví)
- `entry_type = 'RESERVE'`: $amount = -1$ (Tạm giữ khi tạo analysis)
- `entry_type = 'CAPTURE'`: $amount = 0$ (Quyết toán sau khi phân tích thành công)
- `entry_type = 'RELEASE'`: $amount = +1$ (Hoàn trả khi phân tích thất bại)

### Tính nhất quán giữa Reservation và Ledger:
1. Mỗi bản ghi `billing.credit_reservations` có đúng **1 bản ghi ledger `RESERVE`**.
2. Nếu reservation ở trạng thái `CAPTURED`, bắt buộc có đúng **1 bản ghi ledger `CAPTURE`** và **0 bản ghi `RELEASE`**.
3. Nếu reservation ở trạng thái `RELEASED`, bắt buộc có đúng **1 bản ghi ledger `RELEASE`** và **0 bản ghi `CAPTURE`**.
4. Tuyệt đối **không có bản ghi mồ côi (orphan)** giữa ví, reservation và ledger.
