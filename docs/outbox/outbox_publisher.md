# Outbox publisher – SCRUM-323 (W3-M2-01)

Phạm vi: đọc `analysis.outbox_events` chưa publish và phát lên RabbitMQ `/aura-s3` bằng publisher confirm. Đây là **at-least-once**, không phải exactly-once: consumer phải idempotent theo `messageId/eventId`.

## 1. Luồng một vòng (`OutboxPublisher.RunOnceAsync`)
1. `SweepExhausted`: record đã đủ 10 lần thử và hết lease → `needs_attention_at` (không xóa).
2. `Claim`: `FOR UPDATE SKIP LOCKED`, đặt `locked_until = now + lease`, tăng `delivery_attempts`. Chỉ lấy record chưa published, chưa NEEDS_ATTENTION, đến hạn `next_publish_at`, `occurred_at >= MinOccurredAtUtc`, tenant không bị loại.
3. Mỗi record: kiểm payload theo contract (`OutboxMessageMapper`) → publish (persistent, mandatory) → chờ confirm ≤ 5s.
4. Cập nhật DB:

| Kết quả broker | Ghi vào DB |
|---|---|
| Confirmed (ack, không bị return) | `published_at = now` (chỉ khi chưa published) |
| Nack / Returned / ConfirmTimeout / BrokerUnavailable | `last_error_code`, `next_publish_at = now + backoff[attempts-1]` (5,15,30,60,120s, giữ 120s) |
| Lần thử thứ 10 vẫn lỗi | `needs_attention_at`, `last_error_code = PUBLISH_ATTEMPTS_EXHAUSTED:<mã>` |
| Payload sai contract | `needs_attention_at` ngay, **không** gọi broker |
| Broker mất kết nối giữa batch | dừng batch; các record chưa thử được trả lại (`attempts-1`, bỏ lease) |

Crash sau khi broker confirm nhưng trước khi ghi `published_at`: lease hết hạn → record được phát lại với **cùng** `message_id`. Đây là hành vi dự kiến.

## 2. Quy tắc message (khớp CHOT_CONTRACT mục 3–4)
- Domain event: `eventId, eventType, schemaVersion "0.7", occurredAt (Z), tenantId, correlationId, analysisId, data` → exchange `aura.s3.events`, routing key = `eventType` (`analysis.requested|completed|failed`).
- Job: `messageId, messageType "analysis.job.run", schemaVersion "0.6", …, jobId, analysisId, batchId, imageFileId, attempt` → exchange `aura.s3.jobs`, routing key `analysis.job.run`. Không đổi thành eventId/eventType.
- `outbox_events.id` **phải bằng** `eventId/messageId`; `tenant_id` phải bằng `tenantId` trong payload; `event_type`/`schema_version` phải khớp. `analysis.reviewed` không được phát.
- Body ≤ 65536 byte; cấm các tên trường: base64, objectKey, sas/sasToken, token, password, secret, authorization, url (đệ quy).
- AMQP: `content_type=application/json`, `delivery_mode=2`, `message_id`, `type`, `correlation_id`.
- Publisher **không** declare/purge topology; dùng tài khoản Rabbit riêng chỉ có quyền write vào hai exchange trên.

## 3. Cấu hình (xem `appsettings.Outbox.example.json`)
`Outbox:Enabled` mặc định **false**. Khi bật phải có `MinOccurredAtUtc` (mốc bắt đầu Sprint 3, chặn quét outbox fixture 08), `LeaseSeconds >= BatchSize*ConfirmTimeoutSeconds+5`. Sai cấu hình → ứng dụng không khởi động. Secret (mật khẩu Rabbit, connection string) đặt ngoài repo: `RabbitMq__User`, `RabbitMq__Password`, `ConnectionStrings__OutboxPublisher` (role `aura_analysis_publisher`).

## 4. Theo dõi backlog và correlation ID
```sql
-- backlog theo trạng thái
SELECT state, count(*), min(occurred_at) AS oldest FROM analysis.v_outbox_status GROUP BY state;
-- theo dõi một luồng bằng correlationId
SELECT event_type, state, delivery_attempts, last_error_code, published_at
  FROM analysis.v_outbox_status WHERE correlation_id = 's3-demo-001' ORDER BY occurred_at;
-- record cần can thiệp
SELECT id, event_type, correlation_id, last_error_code, needs_attention_at
  FROM analysis.v_outbox_status WHERE state = 'NEEDS_ATTENTION';
```
Log có `eventId`, `type`, `correlationId`, `attempt`; không log payload, token hay URL.

## 5. NEEDS_ATTENTION và phát lại thủ công
Không tự xóa, không tự replay. Sau khi M1/M2 xử lý nguyên nhân (broker, topology, payload):
```sql
UPDATE analysis.outbox_events
   SET needs_attention_at = NULL, delivery_attempts = 0, next_publish_at = now(), last_error_code = NULL
 WHERE id = '<eventId>' AND published_at IS NULL;
```
Giữ nguyên `id` nên consumer vẫn dedupe đúng. Ghi lại ai/lúc nào/vì sao vào Jira.

## 6. Giới hạn đã biết
- Một broker outage kéo dài hơn khoảng 10 phút (10 lần thử với backoff trên) sẽ đưa record sang NEEDS_ATTENTION; record không mất.
- `RabbitMqBrokerPublisher` và `NpgsqlOutboxStore` cần package Npgsql và RabbitMQ.Client; chưa biên dịch trong môi trường soạn thảo.
- Chưa có test với broker thật (S3-03) và tải đồng thời nhiều instance trên PostgreSQL thật ngoài phép thử SKIP LOCKED một phiên.
