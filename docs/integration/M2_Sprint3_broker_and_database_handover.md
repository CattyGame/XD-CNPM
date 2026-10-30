# Thông số Kỹ thuật & Bàn giao Kết nối Broker / Database cho M2 (Outbox & Worker)

> **Người gửi**: Nguyễn Trương Hậu (M1 — Lead Architect / DevOps)
> **Người nhận**: Võ Hữu Duy (M2 — Outbox Publisher & Analysis Worker)
> **Dự án**: AURA (`SP26SE025`) · Retinal Vascular Health Screening
> **Mã công việc Jira**: `SCRUM-323`, `SCRUM-324`, `SCRUM-325`
> **Ngày lập**: 30/10/2026
> **Trạng thái xác thực**: **PASS (Đã kiểm chứng trực tiếp trên container và vault bảo mật)**

---

## I. Tổng hợp 4 Thông số Kỹ thuật M2 Yêu cầu

| Hạng mục | Trạng thái M1 xác nhận | Giá trị chi tiết & Hướng dẫn kết nối |
|---|---|---|
| **1. Broker, Host, vhost** | **ĐÃ XÁC NHẬN** | • **Virtual Host (vhost)**: **`/aura-s3`** (bắt buộc có dấu gạch chéo đầu `/`).<br>• **Host trong Docker**: `aura-s3-rabbitmq-1` (hoặc alias `rabbitmq`), cổng `5672`.<br>• **Host trên máy dev M1 (Native .NET)**: `127.0.0.1`, cổng `5672` (Management: `http://127.0.0.1:15372`). |
| **2. Mật khẩu `s3-publisher`** | **ĐÃ CẤP TỪ VAULT** | `r7zLyO13dkEahz7RraFZzWjTNNx_UO0_pQ_2J4OjB9cNWC4p`<br>*(Tài khoản Worker `s3-worker`: `x0T3mVQKKfCk5BWGWEiUfl0dc7poaRpjY1Xo-1Zl33Rw1KPQ`)* |
| **3. `MinOccurredAtUtc`** | **ĐÃ CHỐT GIÁ TRỊ THẬT** | **`"2026-10-28T00:00:00Z"`**<br>*(Mốc kickoff Sprint 3, ngăn Publisher quét lại các outbox mồ côi của fixture 08 cũ)* |
| **4. Migration 002/003 & ConnectionString** | **XÁC NHẬN ÁP DỤNG CẢ 002 & 003** | • **Migration**: Áp dụng cả `002_outbox_publisher.sql` và `003_outbox_claim_generation.sql` (bắt buộc có `claim_generation` để fencing race condition).<br>• **ConnectionString**: Dùng role `aura_analysis_publisher`. |

---

## II. Chi tiết từng Hạng mục Kỹ thuật

### 1. Broker, Host và Virtual Host (vhost)
* **Virtual Host chính xác:** Là **`/aura-s3`** (có dấu `/` ở đầu).
  * *Bằng chứng xác thực:* Lệnh kiểm tra `rabbitmqctl list_vhosts` trên container `aura-s3-rabbitmq-1` xác nhận duy nhất có vhost `/aura-s3`. (Tài liệu broker cũ từng ghi `/` là do baseline mặc định của RabbitMQ).
  * *Lưu ý cấu hình trong mã nguồn .NET:*
    * Khi cấu hình qua `appsettings.json`: đặt `"VirtualHost": "/aura-s3"`.
    * Nếu dùng AMQP Connection URI: URL-encode dấu `/` thành `%2F` $\rightarrow$ `amqp://s3-publisher:<password>@<host>:5672/%2Faura-s3`.
* **Thông số kết nối:**
  * **Chạy trong Docker Network (`aura_aura_network` / `aura-s3-backend`):**
    * Host: `aura-s3-rabbitmq-1` (hoặc alias `rabbitmq`)
    * Port AMQP: `5672`
  * **Chạy Native .NET trực tiếp ngoài máy host Windows:**
    * Host: `127.0.0.1`
    * Port AMQP: `5672` (Đã forward từ container trong `compose.yaml`)
    * RabbitMQ Management UI: `http://127.0.0.1:15372`

---

### 2. Mật khẩu Tài khoản `s3-publisher` (Lấy từ Vault của M1)
Toàn bộ tài khoản broker đã được khởi tạo theo nguyên tắc đặc quyền tối thiểu (Least Privilege). Mật khẩu được trích xuất trực tiếp từ thư mục bí mật `~/.aura/sprint3/aura-s3-v1/secrets/`:

* **Tài khoản Publisher (`s3-publisher`):**
  * **Username:** `s3-publisher`
  * **Password:**
    ```text
    r7zLyO13dkEahz7RraFZzWjTNNx_UO0_pQ_2J4OjB9cNWC4p
    ```
  * **Quyền hạn:** Chỉ có quyền Write (Publish) vào các exchange `^aura\.s3\.(jobs|events)$` trên vhost `/aura-s3`.
* **Tài khoản Worker (`s3-worker` — cấp sẵn cho M2):**
  * **Username:** `s3-worker`
  * **Password:**
    ```text
    x0T3mVQKKfCk5BWGWEiUfl0dc7poaRpjY1Xo-1Zl33Rw1KPQ
    ```
  * **Quyền hạn:** Quyền Read (Consume) từ queue `^aura\.s3\.analysis\.jobs$` và quyền Write vào `^aura\.s3\.(retry|dead)\.jobs$`.

---

### 3. Giá trị thật của `MinOccurredAtUtc`
* **Giá trị chuẩn xác:**
  ```json
  "MinOccurredAtUtc": "2026-10-28T00:00:00Z"
  ```
* **Ý nghĩa kiến trúc:**
  * Đây là mốc thời gian bắt đầu triển khai Sprint 3.
  * Tham số này bắt buộc phải có để truy vấn Claim outbox thêm điều kiện `WHERE occurred_at >= MinOccurredAtUtc`, ngăn chặn hoàn toàn việc Publisher quét lại các bản ghi outbox mồ côi cũ thuộc về **fixture 08** (đã đóng băng từ các Sprint trước).
  * Trong mã nguồn `OutboxOptions.cs`, nếu `Enabled = true` mà `MinOccurredAtUtc is null`, hệ thống sẽ ném `InvalidOperationException` để dừng ứng dụng ngay lúc khởi động (Fail-closed design).

---

### 4. Xác nhận Migration 002/003 & Chuỗi kết nối Database

#### A. Xác nhận Migration Database
* M1 **xác nhận áp dụng cả 2 bản migration:**
  1. `infra/postgres/migrations/analysis/002_outbox_publisher.sql`: Bổ sung các cột trạng thái `needs_attention_at`, `last_error_at`, index `ix_outbox_claimable` và view `v_outbox_status`.
  2. `infra/postgres/migrations/analysis/003_outbox_claim_generation.sql`: Bổ sung cột `claim_generation bigint NOT NULL DEFAULT 0` và ràng buộc `ck_outbox_claim_generation`.
* **Lý do:** Bản 003 đã được kiểm định qua 32/32 tests tự động (`OutboxRepair84Tests.cs`), là cơ chế cốt lõi giúp Publisher fencing lease token, đảm bảo khi nhiều instance Publisher chạy song song không bị race condition tranh chấp job.

#### B. Chuỗi kết nối `ConnectionStrings__OutboxPublisher`
Role phân quyền cho publisher là `aura_analysis_publisher` (chỉ có quyền `SELECT` trên `outbox_events`/`v_outbox_status` và `UPDATE` các cột trạng thái outbox gồm `claim_generation`).

* **Chuỗi kết nối khi chạy trong Docker (Compose):**
  ```text
  Host=postgres;Port=5432;Database=aura_dev;Username=aura_analysis_publisher;Password=aura_analysis_publisher_dev_secret;Timeout=5;Command Timeout=10
  ```
* **Chuỗi kết nối khi chạy Native .NET trên máy host:**
  ```text
  Host=127.0.0.1;Port=5432;Database=aura_dev;Username=aura_analysis_publisher;Password=aura_analysis_publisher_dev_secret;Timeout=5;Command Timeout=10
  ```
*(Lưu ý: Nếu chạy dev test nhanh cục bộ mà DB chưa khởi tạo riêng role `aura_analysis_publisher`, M2 có thể tạm thời dùng tài khoản ứng dụng `Username=aura_analysis_app;Password=${ANALYSIS_DB_PASSWORD}`)*.

---

## III. Mẫu Cấu hình Hoàn chỉnh (`appsettings.json`) cho M2

M2 có thể dùng trực tiếp mẫu cấu hình dưới đây để nạp vào dự án `AURA.Analysis.Api`:

```json
{
  "Outbox": {
    "Enabled": true,
    "PollSeconds": 5,
    "BatchSize": 10,
    "MaxPublishAttempts": 10,
    "ConfirmTimeoutSeconds": 5,
    "LeaseSeconds": 60,
    "MaxBodyBytes": 65536,
    "BackoffSeconds": [5, 15, 30, 60, 120],
    "MinOccurredAtUtc": "2026-10-28T00:00:00Z",
    "ExcludedTenantIds": [],
    "JobsExchange": "aura.s3.jobs",
    "EventsExchange": "aura.s3.events"
  },
  "RabbitMq": {
    "Host": "127.0.0.1",
    "Port": 5672,
    "VirtualHost": "/aura-s3",
    "UserName": "s3-publisher",
    "Password": "r7zLyO13dkEahz7RraFZzWjTNNx_UO0_pQ_2J4OjB9cNWC4p",
    "ConnectionTimeoutSeconds": 5
  },
  "ConnectionStrings": {
    "OutboxPublisher": "Host=127.0.0.1;Port=5432;Database=aura_dev;Username=aura_analysis_publisher;Password=aura_analysis_publisher_dev_secret;Timeout=5;Command Timeout=10"
  }
}
```

---

## IV. Lệnh Kiểm tra Nhanh dành cho M2 khi Chạy Cục bộ

1. **Kiểm tra trạng thái RabbitMQ Broker và vhost:**
   ```powershell
   docker exec aura-s3-rabbitmq-1 rabbitmqctl list_vhosts
   ```
2. **Kiểm tra quyền hạn tài khoản `s3-publisher`:**
   ```powershell
   docker exec aura-s3-rabbitmq-1 rabbitmqctl list_permissions -p /aura-s3
   ```
3. **Theo dõi tình trạng hàng đợi Outbox trên Database:**
   ```sql
   SELECT state, count(*), min(occurred_at) AS oldest
   FROM analysis.v_outbox_status
   GROUP BY state;
   ```
