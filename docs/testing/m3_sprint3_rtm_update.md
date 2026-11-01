# Cập nhật Ma trận Truy vết Yêu cầu (RTM Update) — Sprint 3 (SCRUM-328)

> **Dự án**: AURA (SP26SE025) · Retinal Vascular Health Screening  
> **Tài liệu nguồn gốc**: [docs/requirements/requirements_traceability_matrix_v0.1.md](file:///D:/XD-PMHDT/docs/requirements/requirements_traceability_matrix_v0.1.md)  
> **Mã công việc Jira**: **SCRUM-328** (`[W3]M3] Kiểm thử lỗi toàn luồng và đối soát dữ liệu`)  
> **Người thực hiện**: Nguyễn Trương Hậu (Lead Architect)  
> **Commit SHA**: `97027420eeba9c9ba2acd51dc253e0b0494a9695` (develop)  
> **Trạng thái**: Đã đối soát và nâng cấp trạng thái các yêu cầu chức năng (FR) và phi chức năng (NFR) liên quan đến Billing, Notification và End-to-End Reliability trong Sprint 3.

---

## 1. Bảng Cập nhật Trạng thái Truy vết Yêu cầu Chức năng (FR Delta)

| Mã Yêu cầu | Mô tả Nghiệp vụ | Trạng thái Cũ (v0.1) | Trạng thái Mới (Sprint 3) | Mã Kiểm thử (Test Cases) | Bằng chứng Thực thi (Evidence) |
|---|---|---|---|---|---|
| **FR-9** | **Thông báo kết quả sẵn sàng** | PARTIAL (Chỉ có device API / push PoC local) | **VERIFIED (Sprint 3 Scope)** | `TC-FR-09-inbox`, `TC-FR-09-dedup`, `TC-FR-09-cursor`, `S328-E2E-01`, `S328-E2E-08` | - [AURA.Notification.InboxChecks](file:///D:/XD-PMHDT/tests/AURA.Notification.InboxChecks/Program.cs): 19/19 PASS<br/>- Migration [005_notification_inbox.sql](file:///D:/XD-PMHDT/infra/postgres/migrations/005_notification_inbox.sql)<br/>- [M3_SCRUM_327_execution_evidence.md](file:///D:/XD-PMHDT/docs/notification/M3_SCRUM_327_execution_evidence.md) |
| **FR-11** | **Mua/gia hạn gói credit** | PARTIAL (Billing catalog PoC) | **IN_PROGRESS (Sprint 3 API Core)** | `TC-FR-11-catalog` | Bảng giá và danh mục gói cước; luồng cổng thanh toán VNPay/Stripe được lên kế hoạch ở Sprint 4. |
| **FR-12** | **Lịch sử thanh toán & Credit Ledger** | DESIGN (Chưa nghiệm thu ledger) | **VERIFIED (Sprint 3 Scope)** | `TC-FR-12-reserve`, `TC-FR-12-capture`, `TC-FR-12-release`, `TC-FR-12-idempotency`, `S328-E2E-01` -> `S328-E2E-06` | - [AURA.Billing.ReservationChecks](file:///D:/XD-PMHDT/tests/AURA.Billing.ReservationChecks/Program.cs): 32/32 PASS<br/>- [CreditSettlementService.cs](file:///D:/XD-PMHDT/src/services/AURA.Billing.Api/Services/CreditSettlementService.cs)<br/>- [M3_SCRUM_326_execution_evidence.md](file:///D:/XD-PMHDT/docs/billing/M3_SCRUM_326_execution_evidence.md)<br/>- [M3_SCRUM_328_reconciliation_report.md](file:///D:/XD-PMHDT/docs/billing/M3_SCRUM_328_reconciliation_report.md) |

---

## 2. Bảng Cập nhật Trạng thái Yêu cầu Phi Chức năng (NFR Delta)

| Mã Yêu cầu | Thuộc tính Chất lượng | Tiêu chuẩn Nghiệp vụ | Trạng thái Sprint 3 | Bằng chứng Kiểm định (Evidence) |
|---|---|---|---|---|
| **NFR-04** | **Tính Bất biến (Idempotency)** | Replay cùng request hoặc domain event không làm trừ tiền lần 2 hoặc nhân đôi bản ghi. | **VERIFIED** | - `S328-E2E-02` (Billing reserve replay trả về HTTP 200, balance không đổi).<br/>- `S328-E2E-07` (Worker duplicate guard trả về `AckDuplicate`).<br/>- `S328-E2E-08` (Notification dedup bởi ràng buộc `UNIQUE(account_id, event_id)`). |
| **NFR-05** | **Tính Toàn vẹn & Đối soát (Consistency & Reconciliation)** | Sai lệch số dư ví và sổ cái bằng 0 tuyệt đối; ánh xạ trạng thái giữa 3 miền đồng bộ 100%. | **VERIFIED** | - `S328-E2E-12` (Tri-Party Reconciliation Audit trên 5 ví đạt Discrepancy = 0).<br/>- Báo cáo [M3_SCRUM_328_reconciliation_report.md](file:///D:/XD-PMHDT/docs/billing/M3_SCRUM_328_reconciliation_report.md). |
| **NFR-07** | **Khả năng Phục hồi Lỗi (Fault Tolerance & Retry)** | Lỗi tạm thời (AI Timeout) không giải phóng credit sớm; lỗi kết thúc (Low Quality) tự động hoàn tiền. | **VERIFIED** | - `S328-E2E-05` (Timeout giữ `RESERVED` và hoàn tất khi retry).<br/>- `S328-E2E-06` (Low Quality tự động gọi `ReleaseReservationAsync` hoàn trả +1 credit). |
| **NFR-11** | **Cách ly Đa Người dùng (Cross-Tenant Isolation)** | Người dùng/tenant này tuyệt đối không thể đọc hay can thiệp dữ liệu của người dùng/tenant khác. | **VERIFIED** | - `S328-E2E-10` (Account 1 không thể xem hoặc đánh dấu đọc inbox của Account 2; tenant boundaries được bảo vệ bởi khóa ngoại). |

---

## 3. Kết luận Bàn giao

Các yêu cầu `FR-9`, `FR-12`, `NFR-04`, `NFR-05`, `NFR-07`, `NFR-11` trong phạm vi Sprint 3 đã chính thức chuyển sang trạng thái **VERIFIED** với đầy đủ bằng chứng thực thi tự động từ PostgreSQL 17 và RabbitMQ.
