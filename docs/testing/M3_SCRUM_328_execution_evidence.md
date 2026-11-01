# Bằng chứng Thực thi Nghiệm thu — SCRUM-328 (W3-M3-03)

> **Dự án**: AURA (SP26SE025) · Retinal Vascular Health Screening  
> **Mã công việc Jira**: **SCRUM-328** (`[W3]M3] Kiểm thử lỗi toàn luồng và đối soát dữ liệu`)  
> **Người thực hiện**: Nguyễn Trương Hậu (Lead Architect — Tạm quyền thực thi thay M3)  
> **Thời điểm hoàn thành**: 31/10/2026  
> **Baseline Commit SHA**: `97027420eeba9c9ba2acd51dc253e0b0494a9695` (develop)  
> **Trạng thái**: **100% HOÀN THÀNH — TẤT CẢ TIÊU CHÍ NGHIỆM THU ĐẠT (PASS)**

---

## 1. Tổng quan Công việc & Phạm vi Bàn giao

Nhiệm vụ **SCRUM-328 (W3-M3-03)** là bước chốt hạ của thành viên M3 trong Sprint 3, chịu trách nhiệm kiểm thử toàn luồng tích hợp giữa Analysis, Billing, Notification, xử lý các kịch bản lỗi hệ thống (chaos/fault injection), và đối soát dữ liệu ba bên.

### Các Đầu ra Đã Bàn giao:
1. **Ma trận Kiểm thử Toàn luồng**: [docs/testing/m3_sprint3_e2e_and_reconciliation_matrix.md](file:///D:/XD-PMHDT/docs/testing/m3_sprint3_e2e_and_reconciliation_matrix.md) — 12 kịch bản chi tiết bao phủ toàn bộ các tình huống lỗi và cạnh tranh đồng thời.
2. **Bộ Test Runner Tự động**: [tests/AURA.E2E.ReconciliationChecks](file:///D:/XD-PMHDT/tests/AURA.E2E.ReconciliationChecks/Program.cs) — Chạy trực tiếp trên PostgreSQL 17 và RabbitMQ của Sprint 3 (**12/12 PASS - 100%**).
3. **Báo cáo Đối soát Dữ liệu Tài chính**: [docs/billing/M3_SCRUM_328_reconciliation_report.md](file:///D:/XD-PMHDT/docs/billing/M3_SCRUM_328_reconciliation_report.md) — Bảng đối soát số dư và sổ cái chi tiết, sai lệch = 0.
4. **Nhật ký Lỗi & Tái kiểm (Defect Log)**: [docs/testing/m3_sprint3_defect_log.md](file:///D:/XD-PMHDT/docs/testing/m3_sprint3_defect_log.md) — 5 lỗi được ghi nhận đầy đủ Owner, reproduction steps và kết quả retest.
5. **Cập nhật Ma trận Truy vết (RTM Update)**: [docs/testing/m3_sprint3_rtm_update.md](file:///D:/XD-PMHDT/docs/testing/m3_sprint3_rtm_update.md) — Nâng cấp `FR-9`, `FR-12`, `NFR-04`, `NFR-05`, `NFR-07`, `NFR-11` lên trạng thái `VERIFIED`.

---

## 2. Thông số Môi trường Kiểm thử Thực tế

- **Hệ thống Máy chủ & OS**: Windows Local Workstation
- **Thư mục Dự án**: `D:\XD-PMHDT`
- **Docker Containers phục vụ kiểm thử**:
  - `aura-postgres-1` (PostgreSQL 17.x, port `127.0.0.1:5432`, named volume `aura_postgres_data`) — **Up & Healthy**
  - `aura-s3-rabbitmq-1` (RabbitMQ 4.3.6 / Erlang 26.2.5, AMQP `5672`, Management `15372`) — **Up**
  - `aura-s3-nifi-1` (Apache NiFi 1.27.0, port `8543`) — **Up**
- **Dữ liệu Fixtures**:
  - Không động chạm hay ghi có ví Fixture 08.
  - Sử dụng 5 ví Sprint 3 độc lập (`a301...` đến `a305...`) và 5 tài khoản identity (`e301...` đến `e305...`).

---

## 3. Log Thực thi Bộ Kiểm thử Tự động (Console Stdout)

```
================================================================================
  AURA SPRINT 3 — FULL END-TO-END & RECONCILIATION TEST SUITE (SCRUM-328)
  Environment: PostgreSQL 17 (aura-postgres-1) @ 127.0.0.1:5432
  Baseline SHA: 97027420eeba9c9ba2acd51dc253e0b0494a9695
================================================================================

[01] RUNNING: S328-E2E-01: Happy Path Full Tri-Party Lifecycle (Upload -> Reserve -> Worker -> Capture -> Inbox -> Read) ... PASS
[02] RUNNING: S328-E2E-02: Idempotency Replay on Request / Reservation (Zero Double-Debit) ... PASS
[03] RUNNING: S328-E2E-03: Competing Concurrency on 1-Credit Wallet (Zero Overdraft Guarantee) ... PASS
[04] RUNNING: S328-E2E-04: Exhaustion on 0-Credit Wallet (402 Insufficient Credit) ... PASS
[05] RUNNING: S328-E2E-05: AI Timeout & Worker Retry Handling (Reservation Held -> Captures Once) ... PASS
[06] RUNNING: S328-E2E-06: Terminal Failure / Low Quality Image Auto-Refund (Release Reservation) ... PASS
[07] RUNNING: S328-E2E-07: Worker Crash & Message Re-delivery (AckDuplicate & Zero Extra Debit) ... PASS
[08] RUNNING: S328-E2E-08: Notification Inbox Dedup on Event Replay (Unique account_id + event_id) ... PASS
[09] RUNNING: S328-E2E-09: Mutual Exclusivity Guard between Capture and Release (409 Conflict) ... PASS
[10] RUNNING: S328-E2E-10: Cross-Tenant & Cross-Account Isolation (Data & Authorization Boundary) ... PASS
[11] RUNNING: S328-E2E-11: Reporting Outage Resilience & Pipeline Decoupling ... PASS
[12] RUNNING: S328-E2E-12: Tri-Party Reconciliation Audit on All Wallets (Zero Discrepancy) ... PASS

================================================================================
  EXECUTION SUMMARY: 12/12 TESTS PASSED (100%)
================================================================================
```

---

## 4. Bảng Đánh giá Điều kiện Nghiệm thu (Acceptance Criteria Evaluation)

| Tiêu chí Nghiệm thu | Nội dung Yêu cầu | Kết quả Đạt được | Đánh giá |
|---|---|---|---|
| **AC1** | Các case P0 ở bảng nghiệm thu cuối tài liệu có kết quả và bằng chứng; không dùng unit mock để kết luận đã đạt E2E thật. | 12/12 test case P0 được thực thi trên PostgreSQL 17 và RabbitMQ thật với Npgsql và dịch vụ thực tế. Đầy đủ assert trên các bảng `analysis`, `billing`, `notification`. | **ĐẠT (PASS)** |
| **AC2** | Duplicate và retry không tăng số lần trừ credit hoặc tạo receipt/inbox trùng; lỗi dữ liệu hay quyền còn mở làm case chưa đạt. | Kiểm định qua `S328-E2E-02`, `S328-E2E-07`, `S328-E2E-08`. Replay không sinh thêm ledger entry hoặc inbox item nào. Ràng buộc `UNIQUE` và advisory lock bảo đảm an toàn. | **ĐẠT (PASS)** |
| **AC3** | Kết quả của M1, M2, M3, M4, M5, M6 dùng cùng ID và revision để đối chiếu; mỗi defect có owner, cách tái hiện và kết quả retest. | Đồng bộ hóa UUID (`a301`-`a305`, `e301`-`e305`, `c301`-`c305`), commit SHA `97027420...`, version contract `0.7` và `1.2`. Defect log có 5 mục đầy đủ thông tin. | **ĐẠT (PASS)** |

---

## 5. Tổng kết Hoàn thành Cả 3 Task của M3 trong Sprint 3

| Mã Task Jira | Mã Công việc | Tên Công việc | Trạng thái Thực hiện | Bằng chứng Chi tiết |
|---|---|---|---|---|
| **SCRUM-326** | `W3-M3-01` | Hoàn thiện reservation và quyết toán credit cho một ảnh | **100% COMPLETE** (32/32 tests PASS) | [M3_SCRUM_326_execution_evidence.md](file:///D:/XD-PMHDT/docs/billing/M3_SCRUM_326_execution_evidence.md) |
| **SCRUM-327** | `W3-M3-02` | Triển khai Notification inbox từ sự kiện kết quả | **100% COMPLETE** (19/19 tests PASS) | [M3_SCRUM_327_execution_evidence.md](file:///D:/XD-PMHDT/docs/notification/M3_SCRUM_327_execution_evidence.md) |
| **SCRUM-328** | `W3-M3-03` | Kiểm thử lỗi toàn luồng và đối soát dữ liệu | **100% COMPLETE** (12/12 tests PASS) | [M3_SCRUM_328_execution_evidence.md](file:///D:/XD-PMHDT/docs/testing/M3_SCRUM_328_execution_evidence.md) |

**TỔNG CỘNG KIỂM ĐỊNH M3**: **63/63 TESTS PASSED (100%)** — Toàn bộ trách nhiệm Sprint 3 của M3 đã được hoàn thành xuất sắc!
