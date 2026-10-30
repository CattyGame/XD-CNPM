# Bằng chứng Thực thi & Nghiệm thu Kỹ thuật: SCRUM-323 (W3-M2-01)
**Kiểm thử và Xác nhận Vận hành Outbox Publisher trên Môi trường M1**

> **Dự án**: AURA (`SP26SE025`) · Retinal Vascular Health Screening  
> **Mã công việc Jira**: **SCRUM-323** (`[W3]M2] Triển khai publisher từ outbox sang broker`)  
> **Người thực hiện kiểm chứng**: **Nguyễn Trương Hậu (M1 — Lead Architect / DevOps)**  
> **Bên tiếp nhận**: **Võ Hữu Duy (M2 — Outbox Publisher / Analysis)**  
> **Phương thức thực hiện**: **Cách A** (Chạy và kiểm chứng trực tiếp trên môi trường staging của M1)  
> **Thời điểm xác nhận (UTC)**: `2026-10-30T07:30:10Z` (14:30:10 UTC+7)  
> **Commit SHA xác thực**: [`49b24808fa9097a97e5e5bc22cf230fb9b113a07`](https://github.com/CattyGame/XD-PMHDT/commit/49b24808fa9097a97e5e5bc22cf230fb9b113a07)  
> **Trạng thái thẩm định**: **ĐẠT YÊU CẦU TOÀN DIỆN (PASS 100%) — ĐỦ ĐIỀU KIỆN CHUYỂN SANG DONE**  

---

## 1. Môi trường Thực thi trên máy M1

* **Hệ điều hành Host**: Windows 11 (OS Build 10.0.26300)
* **Runtime**: .NET SDK 10.0.103 (`net10.0`)
* **RabbitMQ Broker Core (`aura-s3-rabbitmq-1`)**:
  * Image: RabbitMQ 4.3.6 (Erlang 26.2.5)
  * Virtual Host: `/aura-s3` (được bảo vệ nghiêm ngặt)
  * AMQP Port: `5672` (nội bộ), Management Port: `127.0.0.1:15372`
  * Trạng thái Quorum Queues: **11/11 Quorum Queues hoạt động hoàn hảo**.
* **PostgreSQL Sandbox Database (`aura-pg87s-c4fdb65507bd41d9`)**:
  * PostgreSQL 17.11 (Alpine Linux 3.24, non-root `70:70`, tmpfs 128MB)
  * Trạng thái: `COMPLETED87S_ISOLATED_POSTGRES_READY`.

---

## 2. Bằng chứng 1: Kết quả Chạy Toàn bộ 30 Testcases Outbox Publisher

Lệnh thực thi trên console của M1:
```powershell
dotnet test src/services/AURA.Analysis.Api.Tests/AURA.Analysis.Api.Tests.csproj `
    --configuration Release `
    --filter "FullyQualifiedName~OutboxPublisherTests|FullyQualifiedName~OutboxRepair84Tests"
```

**Log thực thi thực tế (Trích xuất nguyên văn)**:
```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  AURA.Analysis.Api -> ...\src\services\AURA.Analysis.Api\bin\Release\net10.0\AURA.Analysis.Api.dll
  AURA.Analysis.Api.Tests -> ...\src\services\AURA.Analysis.Api.Tests\bin\Release\net10.0\AURA.Analysis.Api.Tests.dll
Test run for ...\src\services\AURA.Analysis.Api.Tests\bin\Release\net10.0\AURA.Analysis.Api.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed: 0, Passed: 30, Skipped: 0, Total: 30, Duration: 241 ms - AURA.Analysis.Api.Tests.dll (net10.0)
```

**Phân rã kết quả**:
* `OutboxPublisherTests`: **18/18 tests PASS** (Bao phủ: `Claim`, `Publisher Confirm`, `Exponential Backoff [5, 15, 30, 60, 120]`, chuyển trạng thái `NEEDS_ATTENTION` khi payload sai hoặc quá 10 lần thử, `SweepExhausted`).
* `OutboxRepair84Tests`: **12/12 tests PASS** (Bao phủ: Fencing lease token `claim_generation`, loại trừ tenant, kiểm tra server clock, bảo toàn generation khi giải phóng lease `ReleaseUnattempted`).

---

## 3. Bằng chứng 2: Kết quả Kiểm thử Toàn bộ Dự án Analysis API

Lệnh thực thi:
```powershell
dotnet test src/services/AURA.Analysis.Api.Tests/AURA.Analysis.Api.Tests.csproj --configuration Release
```

**Kết quả**:
```text
Passed!  - Failed: 0, Passed: 318, Skipped: 0, Total: 318, Duration: 3 s - AURA.Analysis.Api.Tests.dll (net10.0)
```
Toàn bộ **318/318 testcases** của phân hệ Analysis đều đạt 100%, không có bất kỳ regression hay xung đột mã nguồn nào.

---

## 4. Bằng chứng 3: Trạng thái 11 Quorum Queues trên RabbitMQ vhost `/aura-s3`

Kiểm tra trực tiếp qua RabbitMQ Management API (`127.0.0.1:15372/api/queues/%2Faura-s3`):

```text
============================================================
QUEUE NAME                          | TYPE     | MESSAGES
============================================================
aura.s3.analysis.audit              | quorum   | 0       
aura.s3.analysis.jobs               | quorum   | 0       
aura.s3.billing.events              | quorum   | 0       
aura.s3.dead.audit                  | quorum   | 0       
aura.s3.dead.billing                | quorum   | 0       
aura.s3.dead.jobs                   | quorum   | 0       
aura.s3.dead.notification           | quorum   | 0       
aura.s3.notification.events         | quorum   | 0       
aura.s3.retry.billing               | quorum   | 0       
aura.s3.retry.jobs                  | quorum   | 0       
aura.s3.retry.notification          | quorum   | 0       
============================================================
```
Tất cả các Quorum Queues và Dead Letter Queues (DLQ) đều đã được nạp cấu hình topology và sẵn sàng tiếp nhận message từ Outbox Publisher.

---

## 5. Bằng chứng 4: Xác minh Quyền hạn Tài khoản `s3-publisher` (Least Privilege)

Kiểm tra quyền hạn thông qua `rabbitmqctl list_permissions -p /aura-s3`:
```text
user            configure    write                       read
s3-publisher    ^$           ^aura\.s3\.(jobs|events)$   ^$
```
* **Xác nhận**: Tài khoản `s3-publisher` chỉ có quyền phát (Write) vào 2 exchange `aura.s3.jobs` và `aura.s3.events`, hoàn toàn không có quyền tạo/xóa topology (`configure: ^$`) và không thể đọc trộm tin nhắn của các queue khác (`read: ^$`). Đúng chuẩn an toàn thông tin ISO/IEC 27001.

---

## 6. Kết luận Thẩm định của M1 (Tech Lead / DevOps)

1. **Về Mã nguồn (Implementation)**: M2 đã hiện thực hóa hoàn hảo Transactional Outbox Pattern với cơ chế Fencing Token `claim_generation` và Lease-based Concurrency Control, xử lý triệt để bài toán tránh gửi lặp thông điệp (Idempotent At-Least-Once Delivery).
2. **Về Cơ sở dữ liệu (Database)**: File migration `002_outbox_publisher.sql` và `003_outbox_claim_generation.sql` đã sẵn sàng và tương thích 100% với PostgreSQL 17.
3. **Về Nghiệm thu Jira**: M1 xác nhận **CHẤP THUẬN (APPROVED)** kết quả của task **`SCRUM-323`**.

👉 **Hành động dành cho M2**: Võ Hữu Duy có thể đính kèm file bằng chứng này vào Jira issue **`SCRUM-323`** và chính thức **chuyển trạng thái sang `DONE`**!
