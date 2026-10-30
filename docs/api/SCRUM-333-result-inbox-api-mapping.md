# SCRUM-333 — API mapping: Analysis Result & Notification Inbox

**Trạng thái tài liệu:** Frontend đã triển khai theo contract; **chưa xác nhận E2E với backend thật**.
**PR:** [CattyGame/XD-PMHDT #69](https://github.com/CattyGame/XD-PMHDT/pull/69) — `feature/SCRUM-333-result-inbox` (`ebd4335`).
**Jira:** [SCRUM-333](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-333).
**Phụ thuộc:** [SCRUM-325](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-325) (Result/file access) và [SCRUM-327](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-327) (Inbox/unread/read).

## 1. Phạm vi triển khai và nguồn contract

- Analysis REST 0.7: `infra/sprint3/contracts/analysis-openapi-0.7.yaml`.
- Notification inbox target: `infra/sprint3/contracts/notification-openapi.yaml`.
- Response tham chiếu: `infra/sprint3/examples/analysis-completed-shape.json` và `infra/sprint3/examples/public-result-shape.json`. **Các file ví dụ là fixture/shape, không phải bằng chứng Worker đã thực thi.**
- Trang React: `src/web/aura-web/src/pages/AnalysisResultPage.tsx` và `NotificationInboxPage.tsx`.
- API client: `src/web/aura-web/src/api/analysisResult.ts` và `notificationInbox.ts`.
- Điều hướng bảo vệ bởi `ProtectedRoute`: `/analyses/:analysisId` và `/notifications`; Batch Progress chỉ hiển thị liên kết “Xem kết quả chi tiết” khi `processingStatus=COMPLETED`.

## 2. Bảng ánh xạ API — Analysis Result

| UI / hành động | Phương thức & endpoint | Dữ liệu đọc từ response | Quy tắc ở frontend |
|---|---|---|---|
| Mở chi tiết analysis | `GET /api/v1/analyses/{analysisId}` | `analysisId`, `batchId`, `processingStatus`, `reviewStatus`, `updatedAt`, `image`, `result` | So khớp UUID của response với ID yêu cầu; `result` chỉ chấp nhận khi `COMPLETED`; chưa hoàn tất sẽ không dựng kết quả AI |
| Hiển thị kết quả | Cùng endpoint | `result.version`, `modelVersion`, `configVersion`, `thresholdVersion`, `warnings[]`, `limitations`, `disclaimer`, `metrics`, `isMock`, `riskAvailable` | Hiển thị theo backend; `riskAvailable=false` bắt buộc risk level/score/confidence là `null`, `indicators=[]`; không tự sinh chẩn đoán hay risk score |
| Ảnh gốc | Cùng endpoint | `image.fileId`, `image.kind=original` | Chỉ lấy tham chiếu file; không công khai object key |
| Mask / overlay / annotation | Cùng endpoint | `result.annotations[]` gồm `fileId`, `kind` | Chỉ hiển thị loại file thực sự có trong response, không tự tạo mask/overlay |
| Xin quyền xem từng ảnh | `GET /api/v1/analyses/{analysisId}/files/{fileId}/access` | `url`, `expiresAt` | Gọi bằng Firebase bearer; URL ký sử dụng trực tiếp cho ảnh, không gắn bearer vào URL; không lưu vào localStorage; xóa URL khỏi state khi hết hạn |
| Làm mới / polling | GET analysis | `processingStatus` | Khi chưa terminal, đọc lại theo chu kỳ; không phát POST upload/job hoặc giữ kết quả khi đổi analysis/tài khoản |

**Các trạng thái terminal ở UI:** `COMPLETED`, `FAILED`, `LOW_QUALITY`, `REJECTED`, `DUPLICATE`. HTTP `202` **không** được coi là kết quả hoàn tất.

**Quyền và lỗi:** Gửi `Authorization: Bearer <Firebase ID token>` ở mỗi GET; `cache: no-store`, `credentials: omit`, `redirect: error`. Frontend xử lý `401` (phiên), `403` (vai trò), `404` (không tìm thấy/ngoài phạm vi), timeout/network và phản hồi không đúng contract. **Quyền OWN/ASSIGNED/TENANT phải được backend kiểm tra**; frontend không thể tự chứng minh phân quyền đa tenant.

## 3. Bảng ánh xạ API — Notification Inbox

| UI / hành động | Phương thức & endpoint | Dữ liệu đọc từ response | Quy tắc ở frontend |
|---|---|---|---|
| Danh sách | `GET /api/v1/notifications/inbox?limit=20` | `items[]`, `nextCursor` | Chỉ dùng token tài khoản đang đăng nhập; không gửi tham số accountId |
| Lọc chưa đọc | `GET /api/v1/notifications/inbox?limit=20&unreadOnly=true` | `items[]` | Chuyển filter, hủy yêu cầu cũ và tải lại |
| Xem thêm | `GET /api/v1/notifications/inbox?limit=20&cursor=...` | `items[]`, `nextCursor` | Mã hóa cursor và gộp theo `notificationId`, tránh trùng bản ghi |
| Số chưa đọc | `GET /api/v1/notifications/inbox/unread-count` | `unreadCount` (số nguyên không âm) | Dùng số do API trả, không tự suy ra từ danh sách phân trang |
| Đánh dấu đã đọc | `POST /api/v1/notifications/inbox/{notificationId}/read` | HTTP `204 No Content` | Chỉ công nhận thành công khi trả `204`; sau đó GET lại danh sách + unread count |
| Chuyển trang kết quả | Dữ liệu từ inbox | `analysisId`, `targetPath` | Kiểm tra `targetPath` có dạng `/analyses/{analysisId}` và mở đường dẫn nội bộ; trang Analysis GET lại, backend kiểm quyền lần nữa |

Các trường trên từng item: `notificationId`, `eventId`, `analysisId`, `type` (`ANALYSIS_COMPLETED` hoặc `ANALYSIS_FAILED`), `title`, `isRead`, `createdAt`, `readAt`, `targetPath`.

**Lỗi và bảo mật:** Client từ chối UUID/response không hợp lệ, ID trùng trong một trang, cursor sai kiểu, đường dẫn không trùng với `analysisId`, và không thực hiện HTTP khi Firebase user khác phiên. `401/403/404` hiển thị lỗi thay vì dữ liệu giả. **Việc ngăn xem hoặc đánh dấu thông báo người khác phải do Notification API thực thi.**

## 4. Kiểm thử đã thực hiện

| Hạng mục | Kết quả đã ghi nhận | Mức bằng chứng |
|---|---|---|
| `npm --prefix src/web/aura-web run build` | PASS — TypeScript và Vite | Biên dịch frontend |
| `npm --prefix src/web/aura-web run lint` | 0 warnings, 0 errors | Phân tích tĩnh |
| `analysisResult.test.ts` + `notificationInbox.test.ts` | 2/2 file, 21/21 test PASS | Unit/API client sử dụng fetch mô phỏng trong Vitest |
| GitHub PR #69 | 6/6 CI checks PASS, không conflict tại thời điểm kiểm tra | CI repository |
| E2E với Worker, file private, Notification consumer thật | **CHƯA XÁC NHẬN** | Chờ backend SCRUM-325 / SCRUM-327 |

**Lưu ý:** 21 bài test không thay thế test tương tác giao diện React, cũng không chứng minh các API backend đã triển khai hoặc hoạt động trên Docker.

## 5. Checklist E2E và ảnh minh chứng cần bổ sung

Khi backend của nhóm sẵn sàng, ghi rõ **môi trường, tài khoản kiểm thử (ẩn thông tin cá nhân), thời điểm, Analysis ID, Batch ID, Notification ID**. Chỉ ghi PASS nếu quan sát được từ API/backend thật.

| Mã minh chứng | Thao tác cần kiểm | ID cần thể hiện | Kết quả / trạng thái |
|---|---|---|---|
| E01 | Mở batch và item `COMPLETED`, nhấn “Xem kết quả chi tiết” | `batchId`, `analysisId` | Chưa thực hiện E2E |
| E02 | Trang Result hiển thị ảnh gốc, mask/overlay nếu backend trả về, version, warnings, review | `analysisId`, `fileId`, `version` | Chưa thực hiện E2E |
| E03 | File private: truy cập hợp lệ, hết hạn, bị thu hồi quyền; không sử dụng public container | `analysisId`, `fileId` | Chưa thực hiện E2E |
| E04 | Inbox hiển thị danh sách và unread count đúng API | `notificationId`, `analysisId` | Chưa thực hiện E2E |
| E05 | Nhấn “Đánh dấu đã đọc”, đối chiếu HTTP 204, unread count cập nhật, thử lặp thao tác | `notificationId` | Chưa thực hiện E2E |
| E06 | Đổi tài khoản/tenant hoặc đổi batch, xác nhận không thấy dữ liệu trước đó và 401/403/404 phù hợp | ID kiểm thử không nhạy cảm | Chưa thực hiện E2E |
| E07 | Từ thông báo mở đúng analysis, backend kiểm quyền khi GET | `notificationId`, `analysisId`, `batchId` | Chưa thực hiện E2E |

**Gợi ý tên ảnh:** `SCRUM-333-E01-batch-completed.png`, `SCRUM-333-E02-analysis-result.png`, `SCRUM-333-E04-inbox-unread.png`, v.v. Không chụp bearer token, URL ký đầy đủ, dữ liệu bệnh nhân thật hay thông tin tài khoản nhạy cảm. Ảnh cần lưu tại nơi nhóm quy định và dẫn liên kết từ Jira/Confluence.

## 6. Trạng thái bàn giao / rủi ro tồn đọng

1. **Đã thực hiện:** React pages, điều hướng bảo vệ, API client, kiểm tra schema phía client, test API client, build/lint/CI.
2. **Chờ SCRUM-325:** Endpoint GET chi tiết analysis; response từ Worker; cấp URL ảnh private theo quyền và thời hạn; các kịch bản quyền/expired.
3. **Chờ SCRUM-327:** Consumer lưu inbox, GET danh sách, unread count, POST read, dedup/idempotency và kiểm tra account/tenant.
4. **Cần sau khi backend sẵn sàng:** E2E bằng dữ liệu có ID; ảnh minh chứng; cập nhật bảng kết quả thực tế trước khi đánh dấu SCRUM-333 Done.

**Kết luận:** PR #69 có thể được review như phần frontend chuẩn bị tích hợp; không được ghi “đã chạy API thật/E2E PASS” khi chưa có bằng chứng backend tương ứng.
