# Cập Nhật Ma Trận Truy Vết Yêu Cầu (RTM Update) — Sprint 4 (SCRUM-349)

> **Dự án**: AURA (SP26SE025) · Retinal Vascular Health Screening System  
> **Tài liệu nguồn gốc**: [docs/requirements/requirements_traceability_matrix_v0.1.md](file:///D:/XD-PMHDT/docs/requirements/requirements_traceability_matrix_v0.1.md)  
> **Cập nhật tiền nhiệm**: [docs/testing/m3_sprint3_rtm_update.md](file:///D:/XD-PMHDT/docs/testing/m3_sprint3_rtm_update.md)  
> **Mã công việc Jira**: **SCRUM-349** (`[W4]M3] Kiểm thử Khả năng Phục hồi Billing/FCM, Ma trận Đối soát và Cập nhật RTM`)  
> **Người thực hiện**: Nguyễn Trương Hậu (NH / Lead Architect M1)  
> **Bộ test nghiệm thu**: [AURA.Billing.ReservationChecks](file:///D:/XD-PMHDT/tests/AURA.Billing.ReservationChecks/Program.cs) (67/67 tests PASS)  
> **Báo cáo đối soát đi kèm**: [docs/testing/m3_billing_resilience_report.md](file:///D:/XD-PMHDT/docs/testing/m3_billing_resilience_report.md)  
> **Trạng thái**: Tất cả các yêu cầu chức năng (FR) và phi chức năng (NFR) thuộc Epic E8 (Billing) và E13 (Testing) trong Sprint 4 đã được nghiệm thu và nâng cấp trạng thái chính thức.

---

## 1. Bảng Cập Nhật Trạng Thái Yêu Cầu Chức Năng (Functional Requirements - FR Delta)

| Mã Yêu Cầu | Tên Nghiệp Vụ | Trạng Thái Cũ (Sprint 3) | Trạng Thái Mới (Sprint 4) | Mã Kiểm Thử (Test Cases) | Bằng Chứng Kỹ Thuật (Evidence) |
|---|---|---|---|---|---|
| **FR-10** | **Quản lý Ví Tín dụng Cấp Tổ chức (Clinic Enterprise Wallet)** | DESIGN (Chưa hỗ trợ ví chia sẻ phòng khám) | **VERIFIED** | `TC-348.1`, `TC-348.2`, `TC-348.3`, `TC-348.4` | - Gói Phòng khám Enterprise (500 credits, 7,000,000 VND).<br/>- Migration [009_billing_clinic_and_reconciliation.sql](file:///D:/XD-PMHDT/infra/postgres/migrations/009_billing_clinic_and_reconciliation.sql).<br/>- Cách ly phòng khám: 403 Forbidden khi trừ ví chéo giữa các Clinic. |
| **FR-11** | **Mua/Gia hạn Gói Credit qua Cổng Thanh toán** | IN_PROGRESS (Mới chỉ có API Catalog) | **VERIFIED** | `TC-346.1`, `TC-346.2`, `TC-346.3`, `TC-346.4`, `TC-346.5`, `TC-349.2` | - Endpoint `POST /api/v1/billing/orders` snapshot gói cước.<br/>- Sandbox Mock Payment Gateway `POST /api/v1/billing/sandbox/pay`.<br/>- Webhook HMAC SHA-256 xác thực chống giả mạo (`TC-346.3`).<br/>- Redis Distributed Lock bảo vệ nạp credit (`TC-346.5`). |
| **FR-12** | **Sổ cái Giao dịch & Tự động Hoàn Tiền (Ledger & Auto-Refund)** | VERIFIED (Chỉ có thủ công & lỗi AI) | **VERIFIED (Production-Ready)** | `TC-348.2`, `TC-348.4`, `TC-349.1`, `TC-349.2` | - Hosted Worker [StaleReservationReconciliationWorker.cs](file:///D:/XD-PMHDT/src/services/AURA.Billing.Api/Services/StaleReservationReconciliationWorker.cs) tự động quét đơn treo > 30 phút.<br/>- API Batch Partial Refund `POST /internal/v1/billing/reservations/batch-refund`.<br/>- Bất biến bảo toàn tài chính 100 nạp / 500 chạy đạt độ lệch = 0. |

---

## 2. Bảng Cập Nhật Trạng Thái Yêu Cầu Phi Chức Năng (Non-Functional Requirements - NFR Delta)

| Mã Yêu Cầu | Thuộc Tính Chất Lượng | Tiêu Chuẩn Nghiệp Vụ Sprint 4 | Trạng Thái Sprint 4 | Bằng Chứng Kiểm Định (Evidence) |
|---|---|---|---|---|
| **NFR-04** | **Tính Bất Biến (Idempotency)** | Replay webhook nạp tiền, tạo đơn trùng hoặc batch refund không được nhân đôi số dư hay phát sinh giao dịch ma. | **VERIFIED** | - `TC-346.2b`: Gọi lại tạo đơn hàng trả về HTTP 200 kèm header `Idempotent-Replayed: true`.<br/>- `TC-346.5`: Tấn công Replay Webhook nạp thẻ 5 lần liên tiếp bị chặn hoàn toàn, ví chỉ cộng đúng 1 lần.<br/>- `TC-348.4`: Batch Refund đảm bảo tính bất biến khi gọi lại. |
| **NFR-05** | **Tính Toàn Vẹn & Đối Soát (Consistency & Reconciliation)** | Bất biến dòng tiền bảo toàn tuyệt đối: Số dư ví + Credit đã captured == Tổng credit nạp vào; Sổ cái khớp chính xác với ví. | **VERIFIED** | - `TC-349.2`: Ma trận 100 đơn nạp (14,267 credits) và 500 ca phân tích: Số dư ví (13,919) + Captured (348) = 14,267 (Sai số 0.0000%).<br/>- Báo cáo [m3_billing_resilience_report.md](file:///D:/XD-PMHDT/docs/testing/m3_billing_resilience_report.md). |
| **NFR-07** | **Khả Năng Phục Hồi Lỗi & Tự Động Hóa (Resilience & Auto-Recovery)** | Tự động hoàn tiền các phân tích bị treo do crash hệ thống; bảo toàn message thông báo khi mất kết nối mạng. | **VERIFIED** | - `TC-348.2`: Background Worker tự động giải phóng các reservation treo > 30 phút với lý do `STALE_RESERVATION_AUTO_REFUND`.<br/>- `TC-349.3`: Dead Letter Queue (DLQ) trong RabbitMQ lưu giữ message khi mất kết nối Firebase FCM.<br/>- `TC-349.3`: Tự động dọn dẹp các token FCM hết hạn (`UNREGISTERED`) khỏi `notification.device_registrations`. |
| **NFR-08** | **Khả Năng Chịu Tải & Chống Đua (Concurrency & Anti-Race Condition)** | 10 luồng đồng thời tranh chấp 1 credit cuối cùng: chỉ 1 luồng được cấp phát, 9 luồng bị từ chối; ví không bị âm. | **VERIFIED** | - `TC-349.1`: Khóa bi quan PostgreSQL (`SELECT FOR UPDATE`) ngăn ngừa race condition tuyệt đối, số dư ví không bao giờ âm. |
| **NFR-11** | **Cách Ly Đa Tổ Chức (Multi-Tenant & Clinic Isolation)** | Bác sĩ / nhân sự phòng khám A tuyệt đối không thể can thiệp ví tín dụng hoặc hoàn tiền của phòng khám B. | **VERIFIED** | - `TC-348.3`: Gọi API trừ tiền ví phòng khám khác bị chặn ngay lập tức với HTTP 403 Forbidden. |

---

## 3. Tổng Kết & Quyết Định Bàn Giao

Toàn bộ các mục tiêu trọng tâm trong Sprint 4 thuộc phạm vi M1 / M3:
- **SCRUM-346**: Xây dựng Cổng Thanh toán Sandbox, Gói cước và Cấp Credit tự động
- **SCRUM-348**: Triển khai Billing Cấp Tổ chức (Clinic Packages) và Tự động Hoàn Credit Treo
- **SCRUM-349**: Kiểm thử Khả năng Phục hồi Billing/FCM, Ma trận Đối soát và Cập nhật RTM

Đã hoàn thành đạt 100% tiêu chí chấp nhận DoD, vượt qua 67/67 kịch bản kiểm thử tự động, sẵn sàng cho công tác nghiệm thu và chuyển giao sản phẩm.
