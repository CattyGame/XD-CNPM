# AURA — Notification Service Contract v0.1

## 1. Phạm vi và baseline

- Task liên quan: SCRUM-57, SCRUM-286, SCRUM-296.
- Ngày: 2026-10-11.
- Baseline Git: b2b3dde, cộng các thay đổi chưa commit trong đợt sửa M2–M5.
- Service: src/services/AURA.Notification.Api.
- API local: http://127.0.0.1:5003.
- Vite proxy: /api/v1/notifications -> API local.
- Đây là device registration và test-push PoC.
- Chưa có consumer Analysis event hoặc notification inbox toàn sản phẩm.
- Tài liệu mô tả implementation hiện tại; chưa phải runtime-generated OpenAPI.
- Bằng chứng: ../testing/m2_m5_notification_verification_2026-10-11.md.

## 2. Authentication và ownership

Các đường dẫn /api/v1/notifications yêu cầu:

Authorization: Bearer <Firebase ID token>

Backend xác minh Firebase ID token với kiểm tra revoked/disabled.
Không dùng FCM token làm bearer token hoặc định danh tài khoản.

Project lấy từ cấu hình backend; UID lấy từ token đã xác minh.
Client không được quyết định project hoặc UID của registration.
Quyền Firebase ở PoC không thay thế RBAC/tenant authorization của AURA.

Token lưu bằng AES-256-GCM, binding theo project/device/UID.
Database lưu fingerprint và dữ liệu mã hóa; không trả token đã lưu qua API.
Không đưa bearer/FCM token vào URL, log hoặc tài liệu bằng chứng.

## 3. Endpoint

| Method | Path | Thành công | Authentication |
| --- | --- | --- | --- |
| GET | /health | Health middleware trả trạng thái | Không yêu cầu Firebase |
| GET | /health/ready | 200 {"ready":true}; 503 nếu chưa sẵn sàng | Không yêu cầu Firebase |
| GET | /api/v1/notifications/me | 200 {"authenticated":true,"uid":"..."} | Firebase bearer |
| POST | /api/v1/notifications/devices | 200 {"registered":true,"deviceId":"..."} | Firebase bearer |
| DELETE | /api/v1/notifications/devices/{deviceId} | 204, không có body | Firebase bearer |
| POST | /api/v1/notifications/devices/{deviceId}/test-push | 200 {"accepted":true,"testId":"...","message":"..."} | Firebase bearer; chỉ Development và EnableTestPush=true |

Test-push không được map khi điều kiện môi trường/config không đạt.
Middleware authentication vẫn có thể trả 401 cho request vào prefix
notifications trước khi routing trả lỗi không tìm thấy endpoint.

## 4. Đăng ký thiết bị

POST /api/v1/notifications/devices
Content-Type: application/json

Body:

{
  "deviceId": "11111111-1111-4111-8111-111111111111",
  "token": "<FCM token; không ghi token thật vào tài liệu>"
}

| Field | Type | Ràng buộc |
| --- | --- | --- |
| deviceId | UUID | Không phải UUID rỗng |
| token | string | Không rỗng, không whitespace, tối đa 4096 byte UTF-8 |

Cùng project/device và cùng UID: upsert token, vẫn một registration.
Device thuộc UID khác: 409 ERR_DEVICE_CONFLICT.
Fingerprint token trùng registration khác trong cùng project:
409 ERR_DEVICE_CONFLICT.

Response 200:

{
  "registered": true,
  "deviceId": "11111111-1111-4111-8111-111111111111"
}

Endpoint không xác nhận token thực sự nhận được push.
Validation hình thức không thay thế kiểm chứng delivery từ FCM.

## 5. Hủy đăng ký

DELETE /api/v1/notifications/devices/{deviceId}

Chỉ xóa registration khớp project, UID và device.
Không có registration, hoặc device thuộc người khác: vẫn 204.
Không tiết lộ device tồn tại hay owner khác.
DELETE lặp là idempotent.

UUID sai định dạng không khớp route constraint.
UUID rỗng khớp định dạng nhưng bị validation trả 400.

## 6. Test-push

POST /api/v1/notifications/devices/{deviceId}/test-push

Không có request body.
Chỉ gửi đến registration của UID đã xác minh.
Payload thông báo dùng nội dung chung, không chứa dữ liệu sức khỏe.
Data có kind=aura-test và testId dạng 32 ký tự hex.
TTL webpush: 120 giây.
Giới hạn: 3 request/phút theo UID, không có queue.
429 trả header Retry-After: 60.

Response accepted=true chỉ chứng minh FCM chấp nhận gửi.
Foreground receipt, background hiển thị và click cần bằng chứng riêng.

Khi FCM trả Unregistered:
- Xóa registration nếu project/UID/device và fingerprint token đã gửi còn khớp.
- Token đã được cập nhật thì giữ registration mới.
- Sau cleanup thành công trả 410 ERR_FCM_TOKEN_EXPIRED.
- Cleanup storage thất bại hoặc timeout trả 503, không báo cleanup hoàn tất.

## 7. Error envelope và status

Các lỗi do application tạo có dạng:

{
  "error": {
    "code": "ERR_VALIDATION",
    "message": "<thông báo cho người dùng>",
    "correlationId": "<request trace ID>",
    "retryable": false
  }
}

| Status | Code | Phạm vi |
| --- | --- | --- |
| 400 | ERR_VALIDATION | Device UUID rỗng hoặc token sai ràng buộc |
| 401 | ERR_UNAUTHORIZED | Thiếu/sai/hết hạn/revoked bearer hoặc tài khoản không hợp lệ |
| 404 | ERR_DEVICE_NOT_REGISTERED | Test-push không tìm thấy registration của người gọi |
| 409 | ERR_DEVICE_CONFLICT | Ownership hoặc fingerprint token xung đột |
| 410 | ERR_FCM_TOKEN_EXPIRED | FCM Unregistered; cleanup có điều kiện hoàn tất |
| 429 | ERR_RATE_LIMITED | Test-push vượt giới hạn |
| 500 | ERR_TOKEN_DECRYPTION | Không giải mã/xác minh được token đã lưu |
| 503 | ERR_AUTH_UNAVAILABLE | Provider xác thực không khả dụng |
| 503 | ERR_STORAGE_UNAVAILABLE | Lỗi/timeout thao tác database |
| 503 | ERR_PUSH_UNAVAILABLE | FCM không gửi được, ngoài trường hợp Unregistered |
| 503 | ERR_PUSH_TIMEOUT | Chưa xác nhận kết quả gửi trong thời gian cho phép |

Lỗi binding JSON, route constraint hoặc lỗi framework chưa được chuẩn hóa
đầy đủ vào envelope trên. Client không được giả định mọi response lỗi đều
có JSON envelope. Lỗi network không có HTTP response.

## 8. Lifecycle hiện tại

- Người dùng chủ động bấm bật thông báo để xin quyền và đăng ký lần đầu.
- Sau đăng ký thành công trong phiên, focus/visible kích hoạt đồng bộ lại.
- Lấy token qua SDK và upsert vào backend, kể cả token không đổi.
- Kiểm tra phiên còn hiện hành sau khi lấy token, trước khi đăng ký.
- Chặn yêu cầu trùng và thao tác đăng xuất trong lúc đồng bộ.
- Đăng xuất: DELETE backend -> xử lý deleteToken SDK -> Firebase signOut.
- Nếu DELETE hoặc thao tác SDK lỗi, giữ phiên để người dùng thử lại.
- Không lưu FCM token vào localStorage; device UUID được lưu theo project.
- Đây không phải đồng bộ liên tục khi tab đóng hoặc trình duyệt không chạy.

## 9. Bằng chứng và giới hạn

Đã có log: build API, 12/12 crypto checks, PostgreSQL checks,
81/81 frontend tests, lint/build và push local foreground/background/click.

Chưa kiểm chứng:
- Endpoint fault injection cho FCM Unregistered/storage failure.
- Token rotation thật trên trình duyệt.
- Click mở cửa sổ mới khi AURA không còn mở.
- Analysis event -> Notification delivery toàn hệ thống.
- Production TLS, RBAC nghiệp vụ, tenant isolation và NFR toàn sản phẩm.

Không dùng contract này để đánh dấu toàn bộ FR-9 VERIFIED.
