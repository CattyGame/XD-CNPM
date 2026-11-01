# AURA Sprint 3 — SCRUM-326 (W3-M3-01) Formal Execution Evidence
## Hoàn Thiện Reservation và Quyết Toán Credit Cho Một Ảnh (Billing)

**Work Item:** SCRUM-326 (`W3-M3-01`)  
**Assignee:** Nguyễn Trương Hậu (Lead Architect / M1 đảm nhiệm thay M3)  
**Parent Epic:** SCRUM-268 (E8 - Billing)  
**Execution Timestamp:** 2026-10-31T11:49:00+07:00  
**Environment:** Local Development (`aura_dev` PostgreSQL 17, Docker Sandbox `aura-pg87s-c4fdb65507bd41d9`, .NET 10.0.401)  
**Status:** **100% COMPLETE & VERIFIED PASS**  

---

## 1. Tóm Tắt Kết Quả Nghiệm Thu (Executive Summary)

Task **SCRUM-326 (W3-M3-01)** đã hoàn thành toàn diện, đáp ứng đầy đủ và vượt mức 3 Tiêu chí Nghiệm thu (Acceptance Criteria):

| Tiêu Chí | Mô Tả Nghiệm Thu | Kết Quả Thực Tế | Trạng Thái |
|:---|---|:---|:---:|
| **AC1** | Ví TEST một unit không bị reserve vượt số dư khi hai request đồng thời; cùng idempotency key không reserve/capture/release thêm lần nữa; dùng lại key khác payload bị từ chối 409; hết credit bị từ chối 402. | 2 request đồng thời tranh 1 credit -> đúng 1 `201 Created` / `RESERVED`, 1 bị từ chối `402 Payment Required` (`ERR_INSUFFICIENT_CREDIT`). Số dư ví hạ từ 1 xuống 0, không âm. Replay cùng key trả về `200 OK` (`Idempotent-Replayed: true`), không trừ ví thêm. Khác payload trả về `409 Conflict`. | **PASS (100%)** |
| **AC2** | Hoàn tất chỉ capture một lần; retry trung gian giữ reservation. Lỗi kết thúc được release đúng chính sách, không vừa capture vừa release; mock trên ví LIVE bị chặn. | Capture chuyển `RESERVED -> CAPTURED`, balance không đổi, ledger delta = 0. Replay capture trả về 200 Replayed. Retryable failure giữ nguyên `RESERVED`. Terminal failure (`FINAL_FAILURE`, `LOW_QUALITY`, `VALIDATION_ABORT`) chuyển `RESERVED -> RELEASED`, hoàn 1 credit về ví (`balance + 1`), ledger delta = +1. Capture gọi trên reservation đã Release hoặc ngược lại đều bị chặn với `409 ERR_RESERVATION_STATE_CONFLICT`. Mock trên LIVE bị chặn `409 ERR_MOCK_LIVE_FORBIDDEN`. | **PASS (100%)** |
| **AC3** | Mỗi case đối chiếu được balance, reservation và ledger; API trái quyền bị chặn và log không lộ thông tin nhạy cảm. | Đối soát tam giác: $\text{Balance} = \text{Initial} + \sum (\text{Ledger})$ đạt 100%. Tính toàn vẹn 1-1 giữa reservation và terminal ledger được bảo toàn. Kiểm thử phân quyền JWT RS256: thiếu token (401), token sai (401), sai audience (403), sai scope (403), caller `analysis-api` gọi capture bị từ chối (403). Kiểm tra log: không rò rỉ token, password, secret hay PHI. | **PASS (100%)** |

---

## 2. Các Sản Phẩm Bàn Giao (Deliverables & Code Artifacts)

1. **Hợp đồng & Bảng quyết định credit chính thức:**
   - [M3_Sprint3_billing_decision_table_and_contract.md](file:///D:/XD-PMHDT/docs/api/M3_Sprint3_billing_decision_table_and_contract.md)
   - Chốt nguyên tắc 1 ảnh FUNDUS = 1 unit credit.
   - Bảng 15 quy tắc quyết toán credit cho toàn bộ các tình huống xử lý của Worker và Analysis.
2. **Database Migration & Fixtures ví TEST Sprint 3 (Cô lập hoàn toàn, không đụng vào ví fixture 08):**
   - [test_007_sprint3_billing_fixtures.sql](file:///D:/XD-PMHDT/infra/postgres/migrations/tests/test_007_sprint3_billing_fixtures.sql) (Tạo 5 ví độc lập: Single-credit, Multi-credit, Zero-credit, Clinic, LIVE).
   - [test_007_sprint3_billing_verification.sql](file:///D:/XD-PMHDT/infra/postgres/migrations/tests/test_007_sprint3_billing_verification.sql) (Script kiểm chứng mức database PostgreSQL 17).
3. **Domain Service API cho Worker & Analysis:**
   - [CreditSettlementService.cs](file:///D:/XD-PMHDT/src/services/AURA.Billing.Api/Services/CreditSettlementService.cs) (Xử lý mapping event sang Capture/Release và thực thi atomic transaction).
   - [Program.cs](file:///D:/XD-PMHDT/src/services/AURA.Billing.Api/Program.cs#L80-L85) (Đăng ký DI `CreditSettlementService`).
4. **Bộ kiểm thử tự động toàn diện .NET 10:**
   - [AURA.Billing.ReservationChecks.csproj](file:///D:/XD-PMHDT/tests/AURA.Billing.ReservationChecks/AURA.Billing.ReservationChecks.csproj)
   - [Program.cs](file:///D:/XD-PMHDT/tests/AURA.Billing.ReservationChecks/Program.cs) (32/32 checks PASS).

---

## 3. Nhật Ký Kiểm Thử Tự Động (Automated Test Execution Log)

### 3.1. Kết Quả Chạy `AURA.Billing.ReservationChecks` (32/32 PASS)
```text
====================================================================
AURA Task SCRUM-326 (W3-M3-01) Verification Suite: Billing & Credit
====================================================================

[AC1] Concurrency, Race Condition, & Idempotency Verification:
  [PASS] AC1.1 Concurrency Race: 2 request tranh 1 unit credit -> đúng 1 thành công (Created) và 1 bị từ chối (InsufficientCredit)
  [PASS] AC1.1 Concurrency Race: Số dư ví không bị âm hoặc vượt số dư (balance = 0)
  [PASS] AC1.2 Idempotent Replay: Cùng idempotency key và cùng payload trả về Replayed (HTTP 200)
  [PASS] AC1.2 Idempotent Replay: Trả về chính xác reservationId ban đầu
  [PASS] AC1.2 Idempotent Replay: Không trừ thêm số dư ví lần hai (balance vẫn = 0)
  [PASS] AC1.2 Idempotent Replay: Không tạo thêm bản ghi ledger RESERVE
  [PASS] AC1.3 Idempotency Conflict: Dùng lại key với payload khác bị từ chối 409 ERR_IDEMPOTENCY_CONFLICT
  [PASS] AC1.4 Insufficient Credit: Yêu cầu phân tích khi ví 0 credit trả về 402 ERR_INSUFFICIENT_CREDIT
  [PASS] AC1.5 Generation Guard: Phân tích generation > 1 khi generation trước chưa release bị chặn với Conflict

[AC2] Lifecycle, Settlement & Mutual Exclusivity Verification:
  [PASS] AC2.1 Capture Success: Phân tích thành công chuyển trạng thái sang CAPTURED
  [PASS] AC2.1 Capture Balance Invariant: Capture không trừ thêm số dư ví (đã trừ từ bước Reserve, balance = 9)
  [PASS] AC2.1 Capture Ledger: Ghi bản ghi ledger CAPTURE với delta = 0
  [PASS] AC2.2 Capture Replay: Replay capture trả về Replayed (HTTP 200) với state CAPTURED
  [PASS] AC2.2 Capture Exactly Once: Capture chỉ ghi ledger đúng 1 lần duy nhất
  [PASS] AC2.3 Intermediate Retry: Lỗi tạm thời (retryable=true) KHÔNG giải phóng credit, giữ reservation ở RESERVED
  [PASS] AC2.4 Release Terminal: Lỗi kết thúc FINAL_FAILURE chuyển state sang RELEASED
  [PASS] AC2.4 Release Refund: Release hoàn trả đúng 1 credit về ví (balance tăng lại từ 8 lên 9)
  [PASS] AC2.4 Release Ledger: Ghi bản ghi ledger RELEASE với amount = +1
  [PASS] AC2.5 Low Quality: Ảnh chất lượng kém giải phóng credit với reason LOW_QUALITY
  [PASS] AC2.5 Abort Release: Hủy phân tích giải phóng credit với reason VALIDATION_ABORT
  [PASS] AC2.6 Mutual Exclusivity: Không được RELEASE reservation đã CAPTURED (trả về 409 Conflict)
  [PASS] AC2.6 Mutual Exclusivity: Không được CAPTURE reservation đã RELEASED (trả về 409 Conflict)
  [PASS] AC2.7 Mock-to-LIVE Boundary: Kết quả mock bị chặn tuyệt đối không được capture vào ví LIVE (ERR_MOCK_LIVE_FORBIDDEN)

[AC3] Tri-Party Reconciliation, Authorization & Privacy Verification:
  [PASS] AC3.1 Tri-Party Reconciliation: Balance hiện tại khớp tuyệt đối với Tổng đại số Ledger (Initial + Sum(Ledger) = 8)
  [PASS] AC3.2 Ledger Terminal Invariant: Mỗi reservation có đúng 1 bản ghi terminal duy nhất (CAPTURE hoặc RELEASE, không bao giờ cả hai)
  [PASS] AC3.3 Auth Guard: Request thiếu Service Token bị chặn với 401 Unauthorized
  [PASS] AC3.4 Auth Guard: Token giả mạo/không hợp lệ bị chặn với 401 Unauthorized
  [PASS] AC3.5 Auth Guard: Token sai audience (aura-profile thay vì aura-billing) bị chặn với 403 Forbidden
  [PASS] AC3.6 Auth Guard: Token có scope billing:read nhưng gọi reserve bị chặn với 403 Forbidden
  [PASS] AC3.7 Service Boundary: Caller analysis-api không có scope billing:capture bị từ chối 403 khi gọi Capture
  [PASS] AC3.8 Service Auth: Token RS256 hợp lệ, đúng audience và scope được chấp thuận 200 OK
  [PASS] AC3.9 Privacy & Redaction: Error envelopes và logs tuyệt đối không lộ Bearer token, password, secret hay PHI

====================================================================
BILLING VERIFICATION RESULTS: Passed: 32, Failed: 0
====================================================================
```

### 3.2. Kết Quả Chạy Kiểm Tra Mức Database PostgreSQL 17 (`aura_dev`)
```text
BEGIN
SET
NOTICE:  PASS: Sprint 3 PostgreSQL DB Verification: Concurrency overdraft guard, capture invariant, terminal transition guard (both directions), and ledger terminal uniqueness.
DO
ROLLBACK
```

---

## 4. Bảng Đối Soát Số Dư Trước và Sau (Balance Audit Trail)

| Tình Huống Kiểm Thử | Ví Kiểm Thử (ID) | Số Dư Ban Đầu | Thao Tác Thực Hiện | Số Dư Sau Thao Tác | Thay Đổi Balance | Trạng Thái Reservation | Bản Ghi Ledger Sinh Ra |
|:---|---|:---:|:---:|:---:|:---:|:---:|:---|
| **AC1 Concurrency Race** | `a3010000-0000-4000-8000-000000000001` | 1 | Request 1 (Winner) | 0 | -1 | `RESERVED` | `RESERVE` (-1) |
| | | | Request 2 (Competing) | 0 | 0 | Bị từ chối (402) | Không ghi |
| **AC1 Idempotent Replay**| `a3010000-0000-4000-8000-000000000001` | 0 | Replay Request 1 | 0 | 0 | `RESERVED` | Không ghi |
| **AC2 Success Capture** | `a3020000-0000-4000-8000-000000000002` | 10 | 1. Reserve phân tích | 9 | -1 | `RESERVED` | `RESERVE` (-1) |
| | | | 2. Quyết toán Capture | 9 | 0 | `CAPTURED` | `CAPTURE` (0) |
| | | | 3. Replay Capture | 9 | 0 | `CAPTURED` | Không ghi |
| **AC2 Terminal Release** | `a3020000-0000-4000-8000-000000000002` | 9 | 1. Reserve phân tích | 8 | -1 | `RESERVED` | `RESERVE` (-1) |
| | | | 2. Release (`FINAL_FAILURE`) | 9 | +1 | `RELEASED` | `RELEASE` (+1) |
| **AC2 State Conflict** | `a3020000-0000-4000-8000-000000000002` | 9 | Release trên reservation đã Capture | 9 | 0 | `CAPTURED` | Bị từ chối (409) |

---

## 5. Kết Luận & Sẵn Sàng Chuyển Sang Bước 2

Task **SCRUM-326 (W3-M3-01)** đã hoàn thành và đạt chất lượng cao:
- Toàn bộ 32 test cases tự động PASS 100%.
- Kiểm chứng database PostgreSQL 17 PASS 100%.
- Tất cả các deliverables đều đã được tạo, commit và lưu vết.
- Sẵn sàng chuyển giao cho M2 và tiếp tục tiến hành **Bước 2 — SCRUM-327 (W3-M3-02: Notification Inbox)**.
