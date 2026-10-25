# M5 — Upload cá nhân và replay cùng lượt gửi (8C)

Ngày: 2026-10-24. Baseline: 8B, 190 test PASS, branch
fix/sprint2-closeout-20261023, HEAD 87af21da20489eda6d4899f7e1f3a428b24960db.

## Phạm vi và nguyên nhân

Trước 8C, UploadPage nhận patientId nhập tay, phát sinh Idempotency-Key mới mỗi
lần nhấn gửi, không có timeout và tin response 202 chỉ bằng ép kiểu TypeScript.
Khi mất phản hồi, gửi lại có thể trở thành một yêu cầu nghiệp vụ mới.

8C sửa UploadPage và UploadPage.test.tsx; thêm analysisUpload.ts,
analysisUpload.test.ts, personalUploadWorkspace.ts và tài liệu này. Tổng 6 file.
Không đổi helper/session 8A/8B, package/lockfile, API contract, backend hoặc DB.

UploadPage lấy patientId từ BackendContext đã được 8A/8B xác nhận, hiển thị tên
hồ sơ và bỏ input patientId. Đây là luồng upload cá nhân có role PATIENT và không
có SYSTEM_ADMIN. Tài khoản DOCTOR có thêm PATIENT vẫn có thể dùng hồ sơ cá nhân;
doctor-only/clinic upload cho bệnh nhân khác chưa triển khai ở bước này. Backend
vẫn là nơi quyết định quyền trên mỗi request; UI không tự cấp tenant/Billing owner.

## Một lượt gửi bất biến

Sau khi ảnh được kiểm tra, lần gửi đầu tạo một UUID và snapshot bất biến gồm
context, eyeSide và mảng File theo thứ tự. FormData được dựng lại mỗi lần gửi
với cùng metadata và cùng File references/bytes; boundary multipart có thể khác
và không nằm trong fingerprint nghiệp vụ của M2. Token Firebase được lấy lại ở
mỗi lần gửi. Không gửi tenantId, actorAccountId, owner hoặc clinicContextId.

Endpoint duy nhất là POST cùng origin `/api/v1/analyses`, modality FUNDUS. Không
dùng VITE_API_BASE_URL để chuyển bearer tới origin khác, không tự set Content-Type
multipart, không cookie và không follow redirect. Không dùng service token.

Một lần click chỉ gửi một POST; không tự polling/retry. Khi đang gửi, chọn ảnh,
eyeSide và submit lặp bị chặn. Sau lỗi, lần retry thủ công giữ nguyên key/ảnh/
metadata. 409 ERR_REQUEST_IN_PROGRESS giữ key; 409 ERR_IDEMPOTENCY_KEY_REUSED
bị chặn để đối chiếu, không tự sinh key khác.

Quy tắc mở lượt mới:

| Kết quả | Hành vi |
| --- | --- |
| Chưa gửi HTTP hoặc lần đầu bị từ chối rõ ràng trước xử lý | Có thể chủ động chọn lại ảnh nếu chưa từng có kết quả chưa rõ |
| Timeout/mất kết nối/5xx/429/402/in-progress/phản hồi 202 sai | Giữ key và khóa payload; không cho tạo lượt mới |
| Lỗi 4xx tới sau một lần chưa rõ kết quả | Vẫn giữ trạng thái chưa rõ; lỗi sau không chứng minh lần đầu chưa tạo dữ liệu |
| 409 key reused | Không tự retry/đổi key; giữ mã yêu cầu để đối chiếu |
| 202 hợp lệ | Hiển thị tiếp nhận; nút gửi bị khóa; chỉ nút tạo lượt mới rõ ràng mới đặt lại draft |

Các từ chối được coi là rõ ràng phải có envelope hợp lệ, retryable=false và mã
phù hợp: 400 ERR_VALIDATION/ERR_UNSUPPORTED_MODALITY, 401 ERR_UNAUTHORIZED,
403 ERR_FORBIDDEN/ERR_ACCOUNT_DISABLED, 404 ERR_NOT_FOUND,
413 ERR_PAYLOAD_TOO_LARGE, 415 ERR_UNSUPPORTED_MEDIA_TYPE. Các nhánh này xảy ra
trước side effect theo source M2 hiện đã pin. Error HTML/JSON sai không chứng minh
không có side effect. 402 được giữ thận trọng vì liên quan journal/reservation;
không tự nạp ví, release/capture, xóa dữ liệu hoặc đổi generation ở UI.

Deadline 60 giây bao trùm lấy token, fetch và đọc JSON. Hủy request ở browser
không hủy giao dịch đã tới server. Token/User phải vẫn khớp snapshot trước gửi
và sau các điểm await. Đổi tài khoản không được nhận kết quả của tài khoản cũ.

## Vòng đời giao diện và ảnh

Workspace được giữ bằng WeakMap theo đúng đối tượng BackendContext trong phiên.
Rời route khi đang gửi sẽ abort phía browser và giữ kết quả là chưa rõ; quay lại
route với cùng context vẫn thấy key/payload đó, không tự POST. Kết quả cũ về muộn
không ghi đè lượt hiện tại. Context mới không dùng draft/ảnh của context trước.

Không lưu ảnh, token hoặc context vào localStorage/sessionStorage/IndexedDB.
Workspace không tồn tại qua reload, đóng tab, remount toàn bộ provider hoặc đổi
phiên backend. Trình duyệt có beforeunload guard khi đang gửi/chưa rõ kết quả,
nhưng không thể đảm bảo mọi trình duyệt luôn hiện cảnh báo. UI hiện mã yêu cầu
và nhắc lưu mã/đối chiếu trước khi tạo lượt mới sau khi mất phiên. Không tuyên bố
idempotency client được bảo toàn qua restart hoặc giữa các tab.

Kiểm tra ảnh trước gửi giữ giới hạn UI cũ: 1..100 ảnh JPEG/PNG, mỗi ảnh 1..10 MiB,
tối đa 16 triệu pixel. Backend vẫn áp policy cấu hình, xác thực MIME/decoder và
kiểm tra quyền. Bitmap được close; kết quả decode cũ sau cleanup bị bỏ. Preview
dùng callback ref React 19 với cleanup revokeObjectURL, bỏ setState-in-effect.

## Kiểm tra tiếp nhận

Chỉ nhận HTTP 202 với batchId/analysisId UUID hợp lệ, đúng số item, timestamp
hợp lệ và statusUrl cùng đường dẫn batch hiện tại. Không dùng URL bất kỳ từ
response làm đích gửi bearer. 202 sai cấu trúc giữ nguyên key và coi chưa rõ.
QUEUED là đang chờ xử lý; DUPLICATE phải có existingAnalysisId và lỗi duplicate
phù hợp. UI không biến ảnh trùng thành kết quả AI mới hoặc khẳng định đã phân tích.
Header Idempotent-Replayed=true được hiển thị như phản hồi gốc phát lại.
Không tự gọi batch status, file access, Blob, AI hay Worker sau 202.

## Kết quả tại môi trường chuẩn bị

- Toàn bộ web: 12 file test / 246 test PASS.
- Trong đó: 39 test helper upload, 34 test UploadPage; các test 8A/8B vẫn đạt.
- TypeScript/Vite build: PASS. Oxlint: 0 lỗi, 0 warning.
- Kiểm bằng Firebase/fetch/decoder giả lập; chưa nghiệm thu upload HTTP thật.

Tại root repo, PowerShell:

```powershell
$ErrorActionPreference = 'Stop'
Push-Location src/web/aura-web
try {
    npm test
    if ($LASTEXITCODE -ne 0) { throw '8C web tests failed. Stop.' }
    npm run lint
    if ($LASTEXITCODE -ne 0) { throw '8C lint failed. Stop.' }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw '8C build failed. Stop.' }
}
finally {
    Pop-Location
}
git diff --check
if ($LASTEXITCODE -ne 0) { throw '8C whitespace check failed. Stop.' }
```

Giữ Vite dừng và tab AURA đóng khi apply/kiểm code. Chưa bật Analysis HTTP,
restart/deploy, thực hiện request nghiệp vụ, đổi ví/hold 5J hoặc xóa evidence.
Chưa nghiệm thu runtime Firebase/Identity/Profile/Analysis, RBAC production,
luồng clinic/doctor, batch-status UI hay toàn bộ M5/SCRUM-311/312/313.
