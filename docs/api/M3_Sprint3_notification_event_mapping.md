# AURA Sprint 3 — Notification Inbox Event Mapping & Integration Specification

**Document Version:** 1.0  
**Work Item:** SCRUM-327 (W3-M3-02: Triển khai Notification inbox từ sự kiện kết quả)  
**Author:** M3 (đảm nhiệm bởi M1 — Lead Architect)  
**Target Audience:** M2 (Worker & Publisher), M5 (Frontend Result Viewer & Notification Inbox)  
**Status:** Canonical Sprint 3 Specification — APPROVED  

---

## 1. Giới Thiệu & Nguyên Tắc Vận Hành

Tài liệu này xác lập quy tắc ánh xạ chính thức giữa **Domain Events** (do M2 Outbox Publisher phát lên RabbitMQ exchange `aura.s3.events`) và **Notification Inbox Items** (được lưu trữ bền vững trong schema `notification.inbox_items` và cung cấp cho M5 UI).

### Nguyên tắc bất biến:
1. **Duyệt trước khi ACK (Persist Before ACK):** Consumer tiếp nhận thông điệp từ RabbitMQ queue `aura.s3.notification.events`, xác định người nhận (`account_id`) và lưu thành công vào cơ sở dữ liệu `notification.inbox_items` trước khi gửi `BasicAck`.
2. **Khóa chống trùng (Deduplication Key):** Ràng buộc duy nhất `UNIQUE(account_id, event_id)` đảm bảo rằng việc phát lại sự kiện (redelivery), consumer restart hoặc thử lại nhiều lần tuyệt đối không tạo thêm thông báo trùng lặp cho cùng một người dùng.
3. **Tách rời trạng thái Inbox khỏi FCM Delivery:** Việc lưu trữ inbox là nguồn dữ liệu chuẩn (authoritative source of truth). Trạng thái nhận hay đọc của người dùng được xác định bởi bảng inbox, **không phụ thuộc** vào việc FCM Web Push có được gửi thành công hay người dùng có bật trình duyệt hay không.
4. **Phân lập người dùng tuyệt đối (Multi-Tenant & Account Boundary):** API chỉ cho phép người dùng xem và thao tác trên thông báo của chính tài khoản mình. Không có tham số `accountId` từ phía client; backend tự trích xuất `account_id` từ token xác thực Firebase.

---

## 2. Bảng Ánh Xạ Sự Kiện (Event-to-Inbox Mapping Table)

| Tên Sự Kiện RabbitMQ | Routing Key | Loại Thông Báo (`type`) | Tiêu Đề Mặc Định (`title`) | Đường Dẫn Điều Hướng (`targetPath`) | Người Nhận (`account_id`) |
|---|---|---|---|---|---|
| `analysis.completed` | `analysis.completed` | `ANALYSIS_COMPLETED` | Phân tích ảnh đáy mắt hoàn tất | `/analyses/{analysisId}` | `data.recipientAccountIds` hoặc `data.actorAccountId` (Bác sĩ/KTV tạo phân tích) |
| `analysis.failed` | `analysis.failed` | `ANALYSIS_FAILED` | Phân tích ảnh đáy mắt không thành công | `/analyses/{analysisId}` | `data.recipientAccountIds` hoặc `data.actorAccountId` (Bác sĩ/KTV tạo phân tích) |

---

## 3. Cấu Trúc Payload Chi Tiết

### 3.1. Sự kiện Thành Công (`analysis.completed`)
```json
{
  "eventId": "a0010000-0000-4000-8000-000000000001",
  "eventType": "analysis.completed",
  "schemaVersion": "0.7",
  "occurredAt": "2026-10-31T05:40:00Z",
  "tenantId": "c0010000-0000-4000-8000-000000000001",
  "correlationId": "corr-notif-01",
  "analysisId": "d0010000-0000-4000-8000-000000000001",
  "data": {
    "patientId": "b0010000-0000-4000-8000-000000000001",
    "actorAccountId": "e0010000-0000-4000-8000-000000000001",
    "recipientAccountIds": [
      "e0010000-0000-4000-8000-000000000001"
    ],
    "riskLevel": "MODERATE",
    "riskAvailable": true,
    "resultVersion": 1,
    "isMock": false
  }
}
```

### 3.2. Sự kiện Thất Bại (`analysis.failed`)
```json
{
  "eventId": "a0020000-0000-4000-8000-000000000002",
  "eventType": "analysis.failed",
  "schemaVersion": "0.7",
  "occurredAt": "2026-10-31T05:41:00Z",
  "tenantId": "c0010000-0000-4000-8000-000000000001",
  "correlationId": "corr-notif-02",
  "analysisId": "d0020000-0000-4000-8000-000000000002",
  "data": {
    "patientId": "b0010000-0000-4000-8000-000000000001",
    "actorAccountId": "e0010000-0000-4000-8000-000000000001",
    "recipientAccountIds": [
      "e0010000-0000-4000-8000-000000000001"
    ],
    "errorCode": "ERR_IMAGE_LOW_QUALITY",
    "retryable": false,
    "terminalStatus": "LOW_QUALITY"
  }
}
```

---

## 4. Đặc Tả Dữ Liệu Bảng `notification.inbox_items`

| Cột | Kiểu Dữ Liệu | Ràng Buộc | Ý Nghĩa Nghiệp Vụ |
|---|---|---|---|
| `id` | `uuid` | `PRIMARY KEY` | Khóa chính của thông báo (Notification ID) |
| `account_id` | `uuid` | `NOT NULL` | Người nhận thông báo |
| `tenant_id` | `uuid` | `NOT NULL` | Phòng khám / Tổ chức của phân tích |
| `event_id` | `uuid` | `NOT NULL` | ID sự kiện nguồn (dùng để dedup) |
| `analysis_id` | `uuid` | `NOT NULL` | Phân tích liên quan |
| `type` | `varchar(32)` | `CHECK IN ('ANALYSIS_COMPLETED', 'ANALYSIS_FAILED')` | Phân loại thông báo |
| `title` | `varchar(255)` | `NOT NULL` | Tiêu đề hiển thị trên UI |
| `is_read` | `boolean` | `DEFAULT false` | Trạng thái đã đọc hay chưa |
| `created_at` | `timestamptz` | `DEFAULT now()` | Thời điểm tạo thông báo |
| `read_at` | `timestamptz` | `NULL` nếu chưa đọc, có giá trị nếu đã đọc | Thời điểm người dùng nhấn đọc |
| `target_path` | `varchar(128)` | `NOT NULL` | Đường dẫn mở kết quả chi tiết trên UI (`/analyses/{analysisId}`) |
| `metadata` | `jsonb` | `DEFAULT '{}'::jsonb` | Dữ liệu mở rộng đính kèm |

**Ràng buộc Dedup:**  
`CONSTRAINT uq_notification_inbox_dedup UNIQUE (account_id, event_id)`
