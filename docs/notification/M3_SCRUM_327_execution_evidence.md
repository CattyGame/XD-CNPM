# AURA Sprint 3 — SCRUM-327 (W3-M3-02) Formal Execution Evidence
## Triển Khai Notification Inbox Từ Sự Kiện Kết Quả (Notification & Chat)

**Work Item:** SCRUM-327 (`W3-M3-02`)  
**Assignee:** Nguyễn Trương Hậu (Lead Architect / M1 đảm nhiệm thay M3)  
**Parent Epic:** SCRUM-267 (E7 - Notification & Chat)  
**Execution Timestamp:** 2026-10-31T12:52:00+07:00  
**Environment:** Local Development (`aura_dev` PostgreSQL 17 on `aura-postgres-1`, RabbitMQ vhost `/aura-s3`, .NET 10.0.401)  
**Status:** **100% COMPLETE & VERIFIED PASS**  

---

## 1. Tóm Tắt Kết Quả Nghiệm Thu (Executive Summary)

Task **SCRUM-327 (W3-M3-02)** đã được thực hiện và nghiệm thu thành công toàn diện, đáp ứng chính xác 100% các tiêu chí AC1, AC2, và AC3:

| Tiêu Chí | Mô Tả Nghiệm Thu | Kết Quả Thực Tế | Trạng Thái |
|:---|---|---|:---:|
| **AC1** | Một kết quả hợp lệ tạo một inbox item đúng người; phát lại cùng event hoặc restart không tạo notification trùng lặp. | Nhận sự kiện `analysis.completed` hoặc `analysis.failed`, tạo bản ghi bền vững trong `notification.inbox_items` với đúng người nhận (`account_id`), gán `targetPath = /analyses/{analysisId}`. Ràng buộc duy nhất `UNIQUE(account_id, event_id)` bảo đảm chống trùng lặp (Dedup) tuyệt đối khi phát lại hoặc restart. | **PASS (100%)** |
| **AC2** | Tài khoản chỉ xem hoặc đánh dấu notification của mình; tổng unread thay đổi đúng và thao tác read lặp vẫn nhất quán. | Phân lập người dùng nghiêm ngặt: Account X không thể thấy hoặc đánh dấu thông báo của Account Y (bị chặn với 404/Forbidden). Đánh dấu đọc giảm chính xác số lượng unread count; thao tác đánh dấu đọc lặp lại đạt tính Idempotent (vẫn trả về HTTP 204 No Content và giữ nguyên số lượng unread). Phân trang Cursor ổn định theo thời gian (`createdAt DESC, id DESC`). | **PASS (100%)** |
| **AC3** | UI nhận dữ liệu thật từ API. FCM nếu chưa tích hợp được ghi đúng phạm vi; FCM acceptance không được gọi là người dùng đã nhận hoặc đọc. | Định dạng JSON trả về khớp 100% với hợp đồng OpenAPI `notification-openapi.yaml` và đặc tả M5 (`items`, `nextCursor`, `unreadCount`). Tách rời trạng thái lưu trữ inbox bền vững khỏi trạng thái FCM push; thất bại của FCM delivery không làm mất thông báo trong inbox. | **PASS (100%)** |

---

## 2. Các Sản Phẩm Bàn Giao (Deliverables & Code Artifacts)

1. **Migration Schema Database:**
   - [005_notification_inbox.sql](file:///D:/XD-PMHDT/infra/postgres/migrations/005_notification_inbox.sql): Tạo bảng `notification.inbox_items`, ràng buộc `UNIQUE(account_id, event_id)`, ràng buộc kiểm tra trạng thái đọc, các chỉ mục tìm kiếm và phân quyền cho `aura_notification_app`. Đã áp dụng thành công trên PostgreSQL 17 (`aura_dev`).
2. **API & Consumer Codebase:**
   - [NotificationInboxModels.cs](file:///D:/XD-PMHDT/src/services/AURA.Notification.Api/NotificationInboxModels.cs): DTOs và helper phân trang Cursor Base64.
   - [NotificationInboxRepository.cs](file:///D:/XD-PMHDT/src/services/AURA.Notification.Api/NotificationInboxRepository.cs): Thao tác lưu trữ dedup, truy vấn danh sách lọc unread, đếm unread count và cập nhật đã đọc.
   - [AccountResolver.cs](file:///D:/XD-PMHDT/src/services/AURA.Notification.Api/AccountResolver.cs): Ánh xạ an toàn từ Firebase UID sang Account ID nội bộ.
   - [NotificationEventConsumer.cs](file:///D:/XD-PMHDT/src/services/AURA.Notification.Api/NotificationEventConsumer.cs): Background Consumer lắng nghe RabbitMQ queue `aura.s3.notification.events`, phân giải người nhận và lưu trữ inbox trước ACK.
   - [Program.cs](file:///D:/XD-PMHDT/src/services/AURA.Notification.Api/Program.cs#L355-L460): Cung cấp 3 endpoint RESTful cho M5 UI (`GET /inbox`, `GET /inbox/unread-count`, `POST /inbox/{id}/read`).
3. **Tài Liệu Hợp Đồng & Examples:**
   - [M3_Sprint3_notification_event_mapping.md](file:///D:/XD-PMHDT/docs/api/M3_Sprint3_notification_event_mapping.md): Bảng ánh xạ sự kiện chi tiết.
   - [notification-inbox-examples.md](file:///D:/XD-PMHDT/docs/api/notification-inbox-examples.md): Ví dụ request/response cho M5 UI.
4. **Bộ Kiểm Thử Tự Động:**
   - [AURA.Notification.InboxChecks.csproj](file:///D:/XD-PMHDT/tests/AURA.Notification.InboxChecks/AURA.Notification.InboxChecks.csproj)
   - [Program.cs](file:///D:/XD-PMHDT/tests/AURA.Notification.InboxChecks/Program.cs): 19/19 test cases PASS trực tiếp trên PostgreSQL 17.

---

## 3. Nhật Ký Kiểm Thử Tự Động (Automated Test Execution Log)

```text
====================================================================
AURA Task SCRUM-327 (W3-M3-02) Verification Suite: Notification Inbox
====================================================================

[AC1] Inbox Creation, Dedup & Event Mapping Verification:
  [PASS] AC1.1 Inbox Item Created: Event analysis.completed hợp lệ tạo thành công 1 inbox item
  [PASS] AC1.2 Event Dedup / Restart: Phát lại cùng eventId không tạo notification trùng lặp (trả về Deduplicated)
  [PASS] AC1.2 Database Uniqueness: Database chỉ chứa đúng 1 bản ghi duy nhất cho cặp (account_id, event_id)
  [PASS] AC1.3 Failed Event Mapping: Event analysis.failed tạo thành công inbox item với type ANALYSIS_FAILED
  [PASS] AC1.4 TargetPath Format: Đường dẫn targetPath khớp chính xác định dạng /analyses/{analysisId}
  [PASS] AC1.5 Multi-Recipient Dispatch: Cùng 1 eventId gửi cho 2 account khác nhau tạo độc lập 2 inbox item không xung đột

[AC2] Account Isolation, Unread Count & Idempotent Read Verification:
  [PASS] AC2.1 Account Boundary: Account X chỉ xem được đúng 3 thông báo của mình, hoàn toàn cô lập khỏi Account Y
  [PASS] AC2.2 Cross-Account Guard: Account X cố tình đánh dấu đọc thông báo của Account Y bị chặn (Forbidden)
  [PASS] AC2.3 Initial Unread Count: Tổng unread ban đầu của Account X chính xác bằng 3
  [PASS] AC2.4 Mark Read Success: Đánh dấu đã đọc thông báo của chính mình thành công (HTTP 204)
  [PASS] AC2.4 Unread Count Decrement: Sau khi đọc 1 thông báo, tổng unread giảm chính xác từ 3 xuống 2
  [PASS] AC2.5 Idempotent Read Mark: Đánh dấu đọc lặp lại trên cùng thông báo vẫn thành công (204 No Content)
  [PASS] AC2.5 Idempotent Unread Count: Đánh dấu đọc lặp lại không làm giảm thêm số unread (vẫn giữ 2)
  [PASS] AC2.6 Unread Only Filter: Lọc unreadOnly=true trả về đúng 2 thông báo chưa đọc, loại bỏ thông báo đã đọc
  [PASS] AC2.7 Cursor Pagination: Trang 1 giới hạn limit=1 trả về đúng 1 item và có nextCursor
  [PASS] AC2.7 Cursor Progression: Trang 2 đọc tiếp theo nextCursor trả về item tiếp theo không bị trùng lặp

[AC3] Frontend Contract Conformity & FCM Decoupling Verification:
  [PASS] AC3.1 Contract Conformity: JSON response chứa đầy đủ 9 trường chuẩn hóa theo notification-openapi.yaml
  [PASS] AC3.2 FCM Delivery Decoupling: Lưu trữ inbox bền vững độc lập với trạng thái gửi push notification của FCM
  [PASS] AC3.3 Cursor Codec: Mã hóa và giải mã Cursor base64 đạt độ chính xác mili-giây tuyệt đối

====================================================================
NOTIFICATION INBOX RESULTS: Passed: 19, Failed: 0
====================================================================
```

---

## 4. Cập Nhật Bảng Theo Dõi Dự Án

Cập nhật vào [README_PROJECT_MASTER.md](file:///D:/XD-PMHDT/README_PROJECT_MASTER.md):
- ✅ **SCRUM-326 (W3-M3-01 · Billing & Credit):** HOÀN THÀNH 100% (32/32 tests PASS).
- ✅ **SCRUM-327 (W3-M3-02 · Notification Inbox):** HOÀN THÀNH 100% (19/19 tests PASS).
- 🔄 **SCRUM-328 (W3-M3-03 · Kiểm thử lỗi toàn luồng và đối soát dữ liệu):** Sẵn sàng thực hiện.
