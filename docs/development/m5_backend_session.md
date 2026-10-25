# M5 — Phiên đăng nhập được xác nhận bởi backend (8B)

Ngày: 2026-10-24. Baseline: 8A v2, branch fix/sprint2-closeout-20261023,
HEAD 87af21da20489eda6d4899f7e1f3a428b24960db, index rỗng.

## Phạm vi

8B nối helper của 8A vào LoginPage và ProtectedRoute thông qua một
BackendSessionProvider ở ngoài BrowserRouter. Provider giữ context trong bộ nhớ
cho một phiên UI; không lưu context/token vào localStorage và không tạo role
mặc định. Cơ chế persistence Firebase hiện có vẫn giữ nguyên.

LoginPage không điều hướng chỉ vì Firebase sign-in promise trả thành công hoặc
chỉ vì callback có Firebase User. Chỉ trạng thái ready, sau bootstrap ACTIVE/READY
và Profile khớp ID của helper 8A, mới cho phép điều hướng vào `/`.
ProtectedRoute áp dụng cùng điều kiện cho `/` và `/upload`, kể cả mở URL trực tiếp.

Các trạng thái:

| Trạng thái | Hiển thị và hành vi |
| --- | --- |
| loading | Thông báo đang xác nhận; chưa mount nội dung được bảo vệ |
| anonymous | ProtectedRoute chuyển tới `/login`; cho đăng nhập Email/Google |
| ready | Cho hiển thị children; context có thể dùng qua useBackendSession |
| error | Chặn children; hiển thị thông báo an toàn và mã đối chiếu nếu có |

PENDING/ERR_PROFILE_NOT_READY, lỗi mạng hoặc lỗi có retryable=true cho phép nhấn
thử lại thủ công. Không polling hay tự retry theo timer. HTTP 401/403 không cho
retry context dù server gắn retryable; người dùng có thể đăng xuất/đổi tài khoản.
Mất cấu hình Firebase, response sai schema, mock mode hoặc tài khoản bị khóa đều
không cho vào ứng dụng. Không hiển thị thông báo lỗi thô hoặc token từ provider.

Store hủy request cũ bằng AbortController và kiểm generation/User hiện tại trước
khi nhận kết quả. Context cũ bị bỏ khi logout, chuyển tài khoản, lỗi observer hoặc
unmount provider. Callback của listener đã dispose và kết quả sign-out cũ không
được ghi đè phiên mới. Sự kiện auth lặp cho cùng User không gọi bootstrap lại,
kể cả khi đang báo lỗi. Thử lại thủ công mới khởi tạo một lần kiểm tra khác.

React StrictMode có thể tạo lại effect. Bộ kiểm thử dùng cả provider thật và
helper 8A thật để kiểm chứng chỉ có một chuỗi bootstrap/Profile khi observer cũ
đã dispose trước callback, đồng thời ngăn nhận kết quả cũ sau cleanup.
Hủy request phía trình duyệt không thể thu hồi một bootstrap đã đến server;
backend vẫn phải giữ bootstrap idempotent như hiện tại.

## Các file

Sửa bốn file web:

- src/main.tsx: thêm provider chung.
- src/pages/LoginPage.tsx: điều hướng theo backend session; chặn submit lặp.
- src/routes/ProtectedRoute.tsx: dùng session chung; bỏ listener Firebase riêng.
- src/pages/LoginPage.test.tsx: mở rộng thành 33 kiểm thử luồng đăng nhập/route.

Thêm năm file dưới src/auth: backendSessionStore.ts, backendSession.ts,
BackendSessionProvider.tsx, BackendSessionNotice.tsx, backendSessionFlow.test.tsx.
File test mới có 3 ca tích hợp provider với helper 8A và HTTP giả lập.
Thêm tài liệu này. Tổng cộng 10 file trong patch; các file 8A giữ nguyên.

## Xác minh

Tại môi trường chuẩn bị: 11 file test / 190 test PASS; TypeScript/Vite build PASS;
Oxlint 0 lỗi, còn 1 warning cũ ở UploadPage/ImagePreview. Các kiểm thử không kết
nối Firebase hoặc backend thật. Không dùng service token, Billing, Blob, AI hoặc
Worker. Payload được kiểm cùng baseline M5 sau 8A.

Trên máy phát triển, từ root repo, chạy:

```powershell
$ErrorActionPreference = 'Stop'
Push-Location src/web/aura-web
try {
    npm test
    if ($LASTEXITCODE -ne 0) { throw '8B web tests failed. Stop.' }
    npm run lint
    if ($LASTEXITCODE -ne 0) { throw '8B lint failed. Stop.' }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw '8B build failed. Stop.' }
}
finally {
    Pop-Location
}
git diff --check
if ($LASTEXITCODE -ne 0) { throw '8B whitespace check failed. Stop.' }
```

Kỳ vọng 190 test PASS; còn 1 warning cũ như trên. Không đổi package hoặc cài/nâng
dependency để vượt lỗi môi trường. Giữ log nếu không khớp kỳ vọng.

## Giới hạn và bước sau

Sau khi mở frontend mới với phiên Firebase còn đăng nhập, provider sẽ tự gọi
POST /api/v1/identity/bootstrap rồi GET /api/v1/profiles/me. Bootstrap có thể tạo
account/provision hồ sơ. Vì vậy dừng Vite và đóng tab web trước apply để tránh
HMR tự chạy luồng này; chỉ mở lại khi thực hiện bước runtime có fixture phù hợp.
Apply/test/lint/build không gọi các endpoint thật. Chưa nghiệm thu login runtime.

Guard frontend chỉ điều phối giao diện. Mọi API backend vẫn phải xác minh token,
account và quyền mỗi request. Context ready không thay thế việc cấp quyền ở
backend, không tự polling phát hiện role/status bị thu hồi giữa phiên và không
cho UI tự quyết định Billing owner/tenant truy cập.

`/router-demo/*`, RouteGuard, menu/vai trò mẫu và dashboard demo chưa chuyển thành
luồng phân quyền production ở 8B. Dashboard vẫn có nội dung và nhãn demo có sẵn.
8B không nghiệm thu RBAC, các chức năng quản trị, hoặc toàn bộ SCRUM-311/312/313.

UploadPage vẫn giữ logic patientId nhập tay và idempotency key cũ. Bước tiếp theo
sẽ lấy patientId từ context cho upload cá nhân và giữ key/payload khi retry một
yêu cầu chưa rõ kết quả. Chưa bật Analysis HTTP hoặc chạy upload/Worker/Billing.
