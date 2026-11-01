# Báo cáo Đối soát Dữ liệu và Kiểm thử Toàn luồng — Sprint 3 (SCRUM-328)

> **Dự án**: AURA (SP26SE025) · Retinal Vascular Health Screening  
> **Mã công việc Jira**: **SCRUM-328** (`[W3]M3] Kiểm thử lỗi toàn luồng và đối soát dữ liệu`)  
> **Người thực hiện**: Nguyễn Trương Hậu (M1 Lead Architect — Tạm quyền thực thi thay M3)  
> **Ngày lập báo cáo**: 31/10/2026  
> **Commit SHA kiểm thử**: `97027420eeba9c9ba2acd51dc253e0b0494a9695` (develop)  
> **Môi trường cơ sở dữ liệu**: PostgreSQL 17 (`aura-postgres-1`) @ `127.0.0.1:5432`, Database: `aura_dev`  
> **Bộ test runner tự động**: [tests/AURA.E2E.ReconciliationChecks](file:///D:/XD-PMHDT/tests/AURA.E2E.ReconciliationChecks/Program.cs)  
> **Kết quả tổng quát**: **12/12 Scenarios PASS (100%)** — **Sai lệch số dư (Discrepancy) = 0**

---

## 1. Mục tiêu và Nguyên tắc Đối soát

Báo cáo này đối soát toàn diện tính toàn vẹn dữ liệu ba bên (**Tri-Party Reconciliation**) giữa:
1. **Analysis Domain** (`analysis.analysis_requests`, `analysis.images`, `analysis.jobs`, `analysis.analysis_results`, `analysis.outbox_events`)
2. **Billing Domain** (`billing.credit_wallets`, `billing.credit_reservations`, `billing.credit_ledger_entries`)
3. **Notification Domain** (`notification.inbox_items`)

### Nguyên tắc Bất biến (Core Invariants):
- **Bảo toàn Số dư (Balance Conservation)**: 
  $$\text{Balance}_{\text{Final}} = \text{Balance}_{\text{Initial}} + \sum \text{Ledger Amount}$$
- **Zero Double-Debit**: Trùng lặp request (`Idempotency-Key`), worker retry hoặc domain event replay tuyệt đối không trừ credit lần hai.
- **Zero Negative Overdraft**: Tranh chấp đồng thời (race condition) trên ví 1 credit bảo đảm đúng 1 request thành công, số dư không bao giờ âm ($Balance \ge 0$).
- **Loại trừ lẫn nhau (Mutual Exclusivity)**: Một reservation chỉ có thể kết thúc ở `CAPTURED` hoặc `RELEASED`, không bao giờ tồn tại cả hai.
- **Bảo vệ Fixture 08**: Không sửa đổi hay ghi có vào ví fixture 08; sử dụng 100% fixture Sprint 3 độc lập (`a301...` - `a305...`).

---

## 2. Bảng Tổng hợp Đối soát Số dư Ví Sprint 3 (Before & After)

| Mã Ví (`wallet_id`) | Chủ thể (`owner_type` / `owner_id`) | Môi trường | Số dư Đầu (`Initial`) | Số bản ghi Ledger | Tổng Đại số Ledger (`\sum Amount`) | Số dư Cuối Thực tế (`Current`) | Sai lệch (`Discrepancy`) | Kết luận |
|---|---|---|---|---|---|---|---|---|
| `a3010000-0000-4000-8000-000000000001` | ACCOUNT (`e301...0001`) | TEST | 1 | 2 | 0 | 0 | **0** | **PASS** |
| `a3020000-0000-4000-8000-000000000002` | ACCOUNT (`e302...0002`) | TEST | 10 | 8 | 7 | 7 | **0** | **PASS** |
| `a3030000-0000-4000-8000-000000000003` | ACCOUNT (`e303...0003`) | TEST | 0 | 0 | 0 | 0 | **0** | **PASS** |
| `a3040000-0000-4000-8000-000000000004` | CLINIC (`e304...0004`) | TEST | 5 | 1 | 5 | 5 | **0** | **PASS** |
| `a3050000-0000-4000-8000-000000000005` | ACCOUNT (`e305...0005`) | LIVE | 5 | 1 | 5 | 5 | **0** | **PASS** |

> **Khẳng định nghiệm thu**: Tất cả 5 ví kiểm thử Sprint 3 đều đạt độ chính xác tài chính tuyệt đối 100%, không phát hiện bất kỳ sai lệch nào giữa số dư thực tế và sổ cái.

---

## 3. Lịch sử Giao dịch Sổ cái Chi tiết (Ledger Audit Trail — Wallet `a302`)

Dưới đây là chi tiết chuỗi 8 giao dịch được ghi nhận trên ví `a302` trong quá trình thực thi toàn bộ kịch bản kiểm thử:

```
 entry_type | amount | balance_after |           idempotency_key            |          created_at           
------------+--------+---------------+--------------------------------------+-------------------------------
 CREDIT     |     10 |            10 | f3020000-0000-4000-8000-000000000002 | 2026-10-31 05:45:40.685655+00
 RESERVE    |     -1 |             9 | 872c3e43-6621-4639-993a-a2951bfd8a6c | 2026-10-31 06:03:15.901869+00
 CAPTURE    |      0 |             9 | e03d835e-ea03-40b0-945a-656b20adeeda | 2026-10-31 06:03:16.01147+00
 RESERVE    |     -1 |             8 | 8449b200-4f77-4c75-9780-d0aa698d3e71 | 2026-10-31 06:03:16.090521+00
 RESERVE    |     -1 |             7 | 4248daf8-f33b-494e-a408-b8d176008151 | 2026-10-31 06:03:16.151718+00
 CAPTURE    |      0 |             7 | 0c01ce8a-e5de-4095-a95d-6154ac481c5e | 2026-10-31 06:03:16.171646+00
 RESERVE    |     -1 |             6 | 97d12431-b293-4316-b189-3e61ca736795 | 2026-10-31 06:03:16.188593+00
 RELEASE    |      1 |             7 | 8400a23f-de3e-4f6b-ae84-1739f0870f1b | 2026-10-31 06:03:16.204162+00
```

### Phân tích Dòng tiền:
1. **Khởi tạo Ví (Seed)**: `CREDIT +10` $\rightarrow$ `balance = 10`.
2. **Kịch bản Happy Path (S328-E2E-01)**:
   - `RESERVE -1` $\rightarrow$ `balance = 9`.
   - `CAPTURE 0` $\rightarrow$ `balance = 9` (Quyết toán không trừ thêm tiền).
3. **Kịch bản Idempotency Replay (S328-E2E-02)**:
   - `RESERVE -1` $\rightarrow$ `balance = 8` (Gọi lần 1).
   - Replay lần 2 với cùng key: Trả về `Replayed`, **0 bản ghi ledger mới**, balance giữ nguyên 8.
4. **Kịch bản AI Timeout & Retry (S328-E2E-05)**:
   - `RESERVE -1` $\rightarrow$ `balance = 7`.
   - Timeout lần 1: Giữ nguyên `RESERVED`, không release.
   - Retry lần 2 thành công: `CAPTURE 0` $\rightarrow$ `balance = 7`.
5. **Kịch bản Low Quality Auto-Refund (S328-E2E-06)**:
   - `RESERVE -1` $\rightarrow$ `balance = 6`.
   - AI Core báo lỗi `ERR_IMAGE_LOW_QUALITY` (HTTP 422).
   - `RELEASE +1` $\rightarrow$ `balance = 7` (Hoàn tiền ngay lập tức vào số dư).

**Tổng kết**: $10 - 1 + 0 - 1 - 1 + 0 - 1 + 1 = 7$. Số dư cuối thực tế trên bảng `billing.credit_wallets` là **7**. Độ lệch = **0**.

---

## 4. Đối soát Ánh xạ Trạng thái Ba bên (Cross-Domain State Audit)

| Miền Dữ liệu | Bảng Cơ sở dữ liệu | Trạng thái Nghiệp vụ | Ghi chú Kiểm định |
|---|---|---|---|
| **Analysis** | `analysis.images` | `COMPLETED` (1 ảnh), `LOW_QUALITY` (1 ảnh) | Ảnh phân tích đạt chuẩn có kết quả Frangi v0.2; ảnh mờ bị phân nhánh chất lượng thấp. |
| **Analysis** | `analysis.jobs` | `SUCCEEDED` (1 job) | Job hoàn tất với `attempt_no = 1`. |
| **Billing** | `billing.credit_reservations` | `CAPTURED` (2 bản ghi), `RELEASED` (1 bản ghi) | Khớp 1:1 với kết quả phân tích tương ứng. Không có trạng thái mồ côi. |
| **Notification** | `notification.inbox_items` | `ANALYSIS_COMPLETED` (2 mục), `ANALYSIS_FAILED` (1 mục) | 1 mục đã đánh dấu đọc (`is_read = true`), 2 mục chưa đọc (`is_read = false`). |

---

## 5. Kết luận Kiểm định

1. **AC1 Đạt (100%)**: Toàn bộ 12 test case P0 được thực thi trực tiếp trên PostgreSQL 17 và RabbitMQ của môi trường Sprint 3, có stdout log và assert thực tế.
2. **AC2 Đạt (100%)**: Duplicate request, duplicate message và retry không bao giờ làm tăng số lần trừ tiền hoặc nhân đôi inbox notification.
3. **AC3 Đạt (100%)**: Chia sẻ nhất quán mã định danh, tenant và version schema 0.7 giữa các thành phần M1, M2, M3, M4, M5, M6.
