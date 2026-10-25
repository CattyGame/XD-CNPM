# M5 — Backend context: bước 8A

Ngày: 2026-10-24. Phạm vi: lớp API và kiểm thử độc lập trước khi nối UI.

## Vấn đề hiện tại

LoginPage/ProtectedRoute đang xác nhận phiên Firebase. UploadPage vẫn nhận patientId
nhập tay và tạo idempotency key mới mỗi lần gửi. Bước 8A chưa sửa các trang này;
chưa xác nhận SCRUM-311/312/313 hoặc toàn bộ M5 hoàn thành.

## Helper mới

File: `src/web/aura-web/src/api/backendContext.ts`.

`loadBackendContext(signal?: AbortSignal)` lấy người dùng Firebase hiện tại và ID
token; gọi tuần tự qua Gateway cùng origin:

1. `POST /api/v1/identity/bootstrap`, không gửi body hoặc các ID do UI tự đặt.
2. Khi account ACTIVE và profileProvisioningStatus READY:
   `GET /api/v1/profiles/me` với cùng Firebase bearer.

Bootstrap có thể tạo account, gán role mặc định và provision hồ sơ trên backend.
Helper không tự retry hoặc polling. Khi tích hợp UI cần gọi có chủ đích cho phiên
đăng nhập hiện tại, quản lý loading/error và hủy tác vụ cũ khi logout/đổi tài khoản.

Helper chưa được import vào UI. Chạy test/build ở bước 8A không gọi các API thật.

Context thành công chứa accountId, firebaseUid, roles, patientId, personalTenantId,
displayName và profileVersion. Các UUID phải hợp lệ, khác UUID rỗng; accountId của
Profile phải khớp Identity, firebaseUid phải khớp phiên Firebase. Role lấy từ mảng
roles do Identity trả về, giữ nhiều role và không gán PATIENT mặc định ở frontend.

Context và mảng role được freeze; không lưu token/context vào localStorage hoặc
cache module. Mỗi lần gọi lấy token/context lại. Kiểm tra phiên sau các điểm await
ngăn trả context cũ khi Firebase User đã bị thay thế hoặc logout trong lúc tải.
Hàm hỗ trợ AbortSignal, timeout 15 giây cho từng HTTP request và không tự retry.

PENDING trả BackendContextError: status 409, code ERR_PROFILE_NOT_READY,
retryable=true; không gọi Profile ở lần này. HTTP 401/403/409/5xx giữ status và
metadata lỗi hợp lệ; message dùng câu cố định thay vì hiển thị message thô từ
Firebase/server. Sai schema/khớp ID thất bại trả ERR_INVALID_RESPONSE. Lỗi mạng
trả ERR_NETWORK; hủy bởi caller trả ERR_CANCELLED; đổi phiên trả ERR_SESSION_CHANGED.

Helper chặn chế độ mock hiện có (`isMockEnabled`). Đường dẫn tương đối dùng proxy
`/api` của Vite hoặc reverse proxy cùng origin khi triển khai. Bước 8A không dùng
VITE_API_BASE_URL cho bearer và chưa thêm cấu hình cross-origin.

Các kiểm tra UI chỉ phục vụ hiển thị/điều phối. Backend vẫn phải xác minh token,
trạng thái account và quyền truy cập mỗi request. Context không phải chứng cứ
cấp quyền vĩnh viễn; personalTenantId không phải billingOwner tự quyết định ở UI.
Backend Profile/Analysis tiếp tục resolve quyền và Billing owner như hiện tại.

## Kết quả kiểm tra tại môi trường chuẩn bị

- Baseline M5: 9 file test, 101 test PASS.
- Sau 8A: 10 file test, 156 test PASS, gồm 55 test context mới.
- TypeScript/Vite build: PASS.
- Oxlint: 0 lỗi; 2 cảnh báo có sẵn ở ProtectedRoute và UploadPage.
- Không kiểm Firebase/Identity/Profile HTTP thật hoặc luồng người dùng thật.

Kiểm thử mới dùng Firebase/fetch giả lập để kiểm chứng contract, việc dừng chuỗi
request, cancellation, thay đổi phiên và không trả context từ lần tải trước.
Build thành công chưa chứng minh proxy/runtime Identity/Profile đã hoạt động.

## Xác minh trên máy phát triển

Tại root repository, PowerShell:

```powershell
Push-Location src/web/aura-web
try {
    npm test
    if ($LASTEXITCODE -ne 0) { throw '8A full web tests failed. Stop.' }

    npm run lint
    if ($LASTEXITCODE -ne 0) { throw '8A lint failed. Stop.' }

    npm run build
    if ($LASTEXITCODE -ne 0) { throw '8A build failed. Stop.' }
}
finally {
    Pop-Location
}

git diff --check
if ($LASTEXITCODE -ne 0) { throw '8A whitespace check failed. Stop.' }
```

Kỳ vọng: toàn bộ web 156 PASS, gồm 55 test context mới. Dùng node_modules hiện có và
Node theo `.nvmrc`; nếu thiếu môi trường hoặc dependencies, giữ log để chẩn đoán,
chưa cài/nâng cấp package ngoài kế hoạch. Không đổi package.json/package-lock.

## Công việc tiếp theo

- Nối context vào trạng thái phiên/LoginPage/ProtectedRoute; chỉ điều hướng sau
  khi backend xác nhận và hiển thị đúng trường hợp PENDING/disabled/network error.
- Dùng patientId từ context cho upload cá nhân, bỏ nhập ID tự do trong luồng đó.
- Quản lý idempotency key theo một yêu cầu upload bất biến; giữ key/payload khi
  retry kết quả chưa rõ, không tạo key mới chỉ vì timeout hoặc nhấn gửi lại.
- Kiểm thử UI và nghiệm thu runtime theo baseline/fixture hiện tại ở bước sau.
