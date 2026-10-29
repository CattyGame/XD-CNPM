# Outbox publisher – SCRUM-323 (W3-M2-01)

**Bản sửa scope/fencing/schema đang review:**
[bàn giao Sprint 3](../integration/sprint3_team_handoff.md#4-bản-sửa-publisher-a8484v).
Nội dung dưới đây là thiết kế ban đầu; các quy tắc cập nhật ở đây được ưu tiên:
Sweep và Backlog dùng cùng scope với Claim; Claim tăng `claim_generation`; mọi
Renew/Published/Retry/Attention/Release kiểm generation và lease còn hạn theo
server clock; renew thất bại thì không publish. Release refund attempt giữ nguyên
generation. Không reset generation khi replay. Validator kiểm exact supported
event0.7/job0.6 branches; Publisher vẫn disabled mặc định. Migration003, PostgreSQL
hai session và broker integration còn pending. API/adapters đã compile trong
native84,32/32tests được84V xác minh; không suy broker/SQL runtime đã đạt.

Phạm vi: đọc `analysis.outbox_events` chưa publish và phát lên RabbitMQ `/aura-s3` bằng publisher confirm. Đây là **at-least-once**, không phải exactly-once: consumer phải idempotent theo `messageId/eventId`.

## 1. Luồng một vòng (`OutboxPublisher.RunOnceAsync`)
1. `SweepExhausted`: chỉ record trong cùng cutoff/excluded-tenant scope, đã đủ10lần thử và hết lease → `needs_attention_at` (không xóa).
2. `Claim`: `FOR UPDATE SKIP LOCKED`, đặt `locked_until = now + lease`, tăng `delivery_attempts` và `claim_generation`. Chỉ lấy record chưa published, chưa NEEDS_ATTENTION, đến hạn `next_publish_at`, `occurred_at >= MinOccurredAtUtc`, tenant không bị loại.
3. Mỗi record: renew đúng generation/lease còn hạn; mất ownership thì bỏ qua broker. Kiểm exact supported payload theo contract (`OutboxMessageMapper`) → publish persistent/mandatory → chờ confirm≤5s.
4. Cập nhật DB:

| Kết quả broker | Ghi vào DB |
|---|---|
| Confirmed (ack, không bị return) | `published_at = now` chỉ khi đúng generation/lease và chưa published; affected rows=1 mới tính thành công |
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
Không tự xóa hoặc replay. Sau khi xử lý nguyên nhân, M1/M2 lập plan có audit cho
đúng event/tenant trên fixture mới: kiểm không còn active lease, giữ nguyên eventId và
claim_generation, xác định budget/needs_attention cần thay đổi và consumer dedup.
Không reset generation hoặc dùng một UPDATE không scope/ownership trên outbox đang chạy.
Không có lệnh replay tự động trong bản bàn giao này.

## 6. Giới hạn đã biết
- Một broker outage kéo dài hơn khoảng 10 phút (10 lần thử với backoff trên) sẽ đưa record sang NEEDS_ATTENTION; record không mất.
- API/adapters đã compile qua native84;84V xác nhận32tests pass. Fake tests không chứng nhận Npgsql/RabbitMQ I/O thực tế.
- Bản sửa generation chưa chạy migration/concurrency hai session hoặc broker confirm/return/crash thật. A87 đang chờ kết quả; giữ Publisher tắt.
