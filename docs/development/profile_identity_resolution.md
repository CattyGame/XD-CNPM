# Profile public identity resolution — bản sửa M1 ngày 6–7

Nguyên nhân tại baseline 43756b0: GET/PATCH `/api/v1/profiles/me` chỉ phân giải
mock token bằng MD5 của UID. Firebase ID token thật luôn bị từ chối; accountId
suy ra ở Profile cũng có thể khác accountId được Identity tạo và lưu trong DB.

## Luồng sau bản sửa

1. Profile chỉ nhận bearer token từ Authorization; bỏ qua X-Account-Id,
   X-Role và X-User-Authorization do client tự gửi.
2. Profile dùng client riêng `profile-api`, audience `aura-identity`, scope
   `identity:context` để lấy service JWT từ Identity.
3. Profile POST `/internal/v1/identity/context`: service JWT trong Authorization;
   token người dùng thực tế trong X-User-Authorization. Identity xác minh
   Firebase và tra UID→accountId. Profile không giải mã/đoán accountId từ token.
4. Chỉ account ACTIVE được GET/PATCH hồ sơ của chính account đó. Profile chỉ
   truy cập schema Profile bằng runtime role hiện tại. Không thay đổi Billing.

Không cache ngữ cảnh người dùng. HttpClient không theo redirect và không log
hai header xác thực. Development mock token được chuyển tới Identity theo cùng
luồng; không còn nhánh Profile tự băm UID hoặc hardcode accountId.

## Cấu hình

- Registry mẫu thêm principal `a1000000-0000-4000-8000-000000000004` cho profile-api.
- Máy mới dùng công cụ AURA.ServiceIdentity.Setup hiện có với registry mới.
- Máy đã có keys dùng `scripts/dev/Register-AuraProfileServiceClient.py`:
  mặc định chỉ kiểm tra, `--apply` bổ sung client và backup registry ngoài repo.
  Giữ nguyên khóa ký/public key và credentials analysis-api/identity-api/billing-api.
  Nếu phát hiện đăng ký dở dang, dừng để đối soát; không sinh lại toàn bộ keys.
- Compose Profile dùng ServiceAuth__ClientId=profile-api và
  ServiceAuth__ClientSecret từ PROFILE_SERVICE_CLIENT_SECRET.
- Set-AuraDevEnvironment.ps1 nạp secret từ clients/profile-api.json. Không
  đưa credential, JWT hoặc thư mục service-auth vào Git/tài liệu bàn giao.
- Identity đọc registry khi khởi động, nên phải reload sau khi thêm client.
  Profile cần recreate để nhận secret mới; chỉ restart Profile không nạp env mới.

## Kết quả lỗi công khai

| Tình huống | HTTP / code |
|---|---|
| Thiếu/sai token người dùng | 401 / ERR_UNAUTHORIZED |
| Identity không tìm thấy account đã bootstrap | 409 / ERR_PROFILE_NOT_READY, retryable=true |
| Account không ACTIVE | 403 / ERR_ACCOUNT_DISABLED |
| Thiếu service credentials, lỗi issuance, scope hoặc Identity không sẵn sàng | 503 / ERR_IDENTITY_DEPENDENCY_UNAVAILABLE, retryable=true |
| Profile chưa provision khi GET | Giữ 409 / ERR_PROFILE_NOT_READY |
| Thiếu/sai If-Match hoặc xung đột ETag | Giữ quy tắc PATCH hiện tại |

GET/PATCH cùng dùng UserIdentityResolver; DTO hồ sơ, quy tắc If-Match và các
endpoint provision/access/resolve nội bộ giữ nguyên. OpenAPI Profile lên 1.1.0
để ghi rõ 403/503. Không tuyên bố sửa đầy đủ mọi ca ngoài phạm vi xác thực
Profile public, ví dụ cấp caller cho các endpoint nội bộ hoặc concurrency provision.

## Kiểm chứng

1. Build AURA.Profile.Api bằng .NET SDK trong global.json.
2. Chạy tests/AURA.Profile.IdentityChecks: 20 trường hợp với HTTP giả lập;
   kiểm tra account mapping, header giả, account disabled, cấu hình/timeout/lỗi
   dependency và cancellation. Bộ này không chứng minh Firebase thật.
3. Chạy tests/AURA.DevEnvironment.Checks/Run-Checks.ps1 để kiểm tra cấu hình.
4. Sau khi thêm client và rebuild/recreate Profile: dùng token Firebase thật,
   bootstrap qua Gateway, GET/PATCH /profiles/me; accountId phải bằng Identity.
   Kiểm tra token hỏng/thiếu, spoofed headers và ETag/conflict trên tài khoản thử.
5. Kiểm tra container Profile gọi Identity nội bộ thành công, không in token;
   lỗi dependency trả 503 và không truy vấn/cập nhật hồ sơ bằng account giả.

CI bổ sung chạy resolver checks, tạo secret Profile riêng và mask trước khi
xuất biến môi trường. CI chưa chứng minh Firebase thật. Kết quả build/runtime
cần ghi từ môi trường thực thi; không ghi PASS dựa trên việc chỉ áp dụng patch.
