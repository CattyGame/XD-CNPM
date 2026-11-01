# AURA Notification Inbox API — Request & Response Examples

**Version:** Sprint 3 Target & Implemented Specification  
**Conformance:** `infra/sprint3/contracts/notification-openapi.yaml` & `docs/api/SCRUM-333-result-inbox-api-mapping.md`  

---

## 1. Lấy Danh Sách Thông Báo (`GET /api/v1/notifications/inbox`)

### Request
```http
GET /api/v1/notifications/inbox?limit=20&unreadOnly=false HTTP/1.1
Host: 127.0.0.1:5003
Authorization: Bearer <Firebase_ID_Token>
Accept: application/json
```

### Response (`200 OK`)
```json
{
  "items": [
    {
      "notificationId": "f0010000-0000-4000-8000-000000000001",
      "eventId": "a0010000-0000-4000-8000-000000000001",
      "analysisId": "d0010000-0000-4000-8000-000000000001",
      "type": "ANALYSIS_COMPLETED",
      "title": "Phân tích ảnh đáy mắt hoàn tất",
      "isRead": false,
      "createdAt": "2026-10-31T05:40:00Z",
      "readAt": null,
      "targetPath": "/analyses/d0010000-0000-4000-8000-000000000001"
    },
    {
      "notificationId": "f0020000-0000-4000-8000-000000000002",
      "eventId": "a0020000-0000-4000-8000-000000000002",
      "analysisId": "d0020000-0000-4000-8000-000000000002",
      "type": "ANALYSIS_FAILED",
      "title": "Phân tích ảnh đáy mắt không thành công",
      "isRead": true,
      "createdAt": "2026-10-31T05:35:00Z",
      "readAt": "2026-10-31T05:38:00Z",
      "targetPath": "/analyses/d0020000-0000-4000-8000-000000000002"
    }
  ],
  "nextCursor": "MTc2MTg4OTAwMDAwMHxmMDAyMDAwMC0wMDAwLTQwMDAtODAwMC0wMDAwMDAwMDAwMDI="
}
```

---

## 2. Lấy Số Lượng Thông Báo Chưa Đọc (`GET /api/v1/notifications/inbox/unread-count`)

### Request
```http
GET /api/v1/notifications/inbox/unread-count HTTP/1.1
Host: 127.0.0.1:5003
Authorization: Bearer <Firebase_ID_Token>
Accept: application/json
```

### Response (`200 OK`)
```json
{
  "unreadCount": 1
}
```

---

## 3. Đánh Dấu Thông Báo Đã Đọc (`POST /api/v1/notifications/inbox/{notificationId}/read`)

### Request
```http
POST /api/v1/notifications/inbox/f0010000-0000-4000-8000-000000000001/read HTTP/1.1
Host: 127.0.0.1:5003
Authorization: Bearer <Firebase_ID_Token>
```

### Response (`204 No Content`)
Thao tác thành công, không trả nội dung body. Thao tác có tính chất **Idempotent** (gọi lại nhiều lần trên cùng thông báo vẫn trả về HTTP 204 mà không phát sinh lỗi).

### Error Responses
- `401 Unauthorized`: Khi thiếu hoặc sai Bearer token.
- `404 Not Found`: Khi `notificationId` không tồn tại hoặc thuộc về tài khoản khác.
