# Báo cáo Kiểm định CI & Bàn giao Vận hành Sprint 3

> **Dự án**: AURA (SP26SE025) · Retinal Vascular Health Screening  
> **Mã công việc Jira**: **SCRUM-319** (`[W3]M1] Chốt kiểm tra CI và bàn giao vận hành Sprint 3`)  
> **Người thực hiện (M1)**: Nguyễn Trương Hậu (Lead Architecture / Platform / DevOps)  
> **Phạm vi kiểm định**: Toàn bộ hệ thống Backend, ServiceAuth, Profile, DevEnvironment và CI Workflow  
> **Trạng thái kiểm định**: **PASS (456/456 automated checks passed · 0 failures · 0 skipped)**  
> **Ngày phê duyệt**: 30/10/2026

---

## 1. Mục tiêu và Điều kiện Nghiệm thu (Acceptance Criteria)

- **AC1 (Revision Bàn giao Đạt Chuẩn)**: Toàn bộ các bộ kiểm tra tự động trên mã nguồn bàn giao phải chạy đạt mà không có lỗi nghiêm trọng (P0) hoặc bypass bảo mật.
- **AC2 (Khả năng Tái lặp & Không Lộ Secret)**: Người nhận bàn giao có thể tự chạy lại toàn bộ test suite và smoke test môi trường từ tài liệu; các cấu hình và template kiểm thử không chứa mật khẩu, token hay private key thật.
- **AC3 (Minh bạch Dữ liệu & Trạng thái)**: Phân định ranh giới rõ ràng giữa dữ liệu nghiệp vụ đã tiếp nhận (`ACCEPTED`) và kết quả AI đã xử lý (`COMPLETED`); công bố rõ các giới hạn của mô hình và môi trường.

---

## 2. Tổng hợp Kết quả Kiểm tra Tự động (Automated Test Suites)

Hệ thống CI cục bộ và GitHub Actions đã thực thi toàn diện 5 bộ kiểm định chuyên sâu trên nhánh phát triển:

| Bộ Kiểm thử / Module | Dự án / Đường dẫn thực thi | Số lượng Test | Kết quả | Thời gian |
|---|---|:---:|:---:|:---:|
| **Analysis API & Integration Tests** | `src/services/AURA.Analysis.Api.Tests` | **288** | **288 PASS** | ~1.2s |
| **Service Authentication (Fail-Closed)**| `tests/AURA.ServiceAuth.Checks` | **70** | **70 PASS** | ~0.8s |
| **Journal State & Upload Recovery** | `tests/AURA.Analysis.JournalStateChecks` | **41** | **41 PASS** | ~0.6s |
| **Profile Identity & Token Resolution** | `tests/AURA.Profile.IdentityChecks` | **20** | **20 PASS** | ~0.5s |
| **Dev Environment & Migration Engine** | `tests/AURA.DevEnvironment.Checks` | **37** | **37 PASS** | ~1.5s |
| **TỔNG CỘNG** | **Toàn bộ hệ sinh thái AURA** | **456** | **456 PASS** | **< 5s** |

---

## 3. Chi tiết Các Khía cạnh Kỹ thuật Đã Kiểm định

### 3.1. Bảo mật & Xác thực Dịch vụ (ServiceAuth Fail-Closed)
- **Cơ chế**: Khóa ký RSA 3072-bit, thuật toán `RS256`, thời hạn token tối đa 300 giây.
- **70 tiêu chí kiểm tra đạt chuẩn**:
  - Từ chối token hết hạn (`exp`), token chưa đến hạn (`nbf`), token không có chữ ký hoặc dùng thuật toán yếu (RS384, HS256).
  - Từ chối nâng quyền trái phép (Escalation): client không thể tự mở rộng scope hoặc thay đổi audience.
  - Fail-closed hoàn toàn: khi thiếu file cấu hình private key hoặc registry bị lỗi cú pháp JSON, hệ thống dừng an toàn thay vì fallback sang cấu hình yếu.

### 3.2. Tính Bất biến & Phục hồi Tải lên (Upload Recovery & Journaling)
- **41 tiêu chí kiểm tra đạt chuẩn**:
  - Request chỉ được chuyển sang trạng thái `ACCEPTED` sau khi cả hai hiệu ứng (ghi blob storage và trừ reservation credit) đã được xác nhận ghi nhận (`acknowledged`).
  - Hủy tải lên (`ABORT`) đảm bảo giải phóng toàn bộ tài nguyên tạm thời và không bao giờ để lại dữ liệu rác mồ côi.
  - Kháng replay: việc gửi lại request đã xử lý không làm tăng revision hay trừ thêm credit.

### 3.3. Di chuyển Cơ sở Dữ liệu & Phân quyền Vai trò (Database RBAC)
- **37 tiêu chí kiểm tra đạt chuẩn**:
  - Kiểm tra 9 tập lệnh migration tuần tự (`001_notification_devices.sql` $\rightarrow$ `002_identity_and_profile.sql` $\rightarrow$ `003_doctor_assignments.sql` $\rightarrow$ `003_billing_poc_packages.sql` $\rightarrow$ `004_billing_business_schema.sql` $\rightarrow$ `001_analysis_core.sql` $\rightarrow$ `002_upload_recovery.sql` $\rightarrow$ `002_service_database_roles.sql`).
  - Phân quyền nghiêm ngặt: Các tài khoản ứng dụng chỉ có quyền `INSERT` trên bảng outbox và append-only trên audit logs; cấm triệt để quyền `DROP`, `TRUNCATE` hay `ALTER` ở cấp dịch vụ.

---

## 4. Kiểm tra Tương thích Hạ tầng Container (`compose.yaml`)

- Cấu hình Docker Compose hợp nhất đã được xác thực cú pháp:
  ```powershell
  docker compose --env-file .env.example -f compose.yaml -f compose.dev.yaml config -q
  ```
  $\rightarrow$ **Thành công (Mã thoát 0, không có lỗi cấu hình tham số hay xung đột cổng).**

- Toàn bộ 7 dịch vụ nền tảng Sprint 3 (Sandbox PostgreSQL 17.11, Identity Service, AI Core Frangi v0.2, RabbitMQ Quorum Cluster, Apache NiFi Ingestion, Reporting PostgreSQL) đã được kiểm chứng tính sẵn sàng và duy trì vận hành ổn định.

---

## 5. Danh mục Artifact Bàn giao & Tài liệu Kèm theo

1. **Tài liệu Topology Broker**: `docs/integration/M1_Sprint3_rabbitmq_broker_handover.md`
2. **Sổ tay Vận hành Môi trường**: `docs/development/sprint3_integrated_environment_handover.md`
3. **Sổ tay Cài đặt & Vận hành Toàn diện**: `docs/development/installation_and_operations_manual.md`
4. **File Bằng chứng Khởi động**: `epoch-checkpoint87C.json` và `postgres-sandbox-journal87S.json`.

**Xác nhận bàn giao**: Tất cả các tiêu chuẩn nghiệm thu của SCRUM-319 đã được đáp ứng trọn vẹn. Hệ thống sẵn sàng cho việc nghiệm thu Sprint 3 và bảo vệ trước hội đồng Viva.
