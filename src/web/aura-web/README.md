# AURA Web

Frontend AURA sử dụng React, TypeScript và Vite.

Phạm vi hiện tại gồm giao diện minh họa, kiểm tra kết nối API Gateway
và panel Demo Analysis sử dụng Mock Service Worker (MSW).

## Cài đặt

Chạy các lệnh từ thư mục `src/web/aura-web`:

```powershell
npm ci --include=dev
```

Môi trường đã kiểm thử: Node.js v24.21.0.

## Cấu hình môi trường

Các biến cấu hình trong `.env.example`:

```env
VITE_ENABLE_MOCK=false
VITE_API_BASE_URL=
```

Tạo file `.env.development` tại thư mục frontend để cấu hình local.
File này được Git bỏ qua và không được đưa vào commit.

- `VITE_ENABLE_MOCK=true`: bật MSW và panel Demo Analysis trong chế độ development.
- `VITE_ENABLE_MOCK=false`: tắt MSW, banner DEMO MODE và panel Demo Analysis.
- `VITE_API_BASE_URL`: địa chỉ API dùng bởi API client khi mock tắt.
  Giá trị rỗng sử dụng đường dẫn cùng origin.

Khi mock bật, API client sử dụng đường dẫn cùng origin để khớp MSW handlers.

Vite development server chuyển tiếp `/api` đến `http://127.0.0.1:5000`.
Cấu hình proxy nằm trong `vite.config.ts` và chỉ phục vụ development.

Sau khi thay đổi biến môi trường, khởi động lại Vite và tải lại trang.

## Chạy ứng dụng

```powershell
npm run dev
```

Mở địa chỉ Local được Vite in trong terminal.

Để chạy Demo Analysis, đặt trong `.env.development`:

```env
VITE_ENABLE_MOCK=true
VITE_API_BASE_URL=
```

Panel nằm phía trên footer. Chọn kịch bản rồi nhấn
**Chạy demo Analysis**.

## Các kịch bản Analysis

| Kịch bản | Request | Kết quả mô phỏng |
| --- | --- | --- |
| Tạo yêu cầu | POST `/api/v1/analyses` | 202, thông tin tiếp nhận và batch |
| Dữ liệu không hợp lệ | POST, `scenario=validation` | 400, `ERR_VALIDATION` |
| Thiếu credit | POST, `scenario=insufficient-credit` | 402, `ERR_INSUFFICIENT_CREDIT` |
| Danh sách trống | GET `/api/v1/analyses` | 200, `items` rỗng và thông tin phân trang |
| Đang phân tích | GET `/api/v1/analyses/:analysisId`, `scenario=processing` | Chưa có kết quả nguy cơ |
| Phân tích hoàn tất | GET, `scenario=completed` | Kết quả minh họa, trạng thái review và nhãn MOCK |
| Phân tích thất bại | GET, `scenario=failed` | Thông báo lỗi, chưa có kết quả nguy cơ |
| Ảnh bị từ chối giả lập | GET, `scenario=quality-reject` | Hướng dẫn chọn ảnh khác, chưa có kết quả nguy cơ |
| Chưa xác thực | GET, `scenario=401` | 401, `ERR_UNAUTHORIZED` |
| Không đủ quyền | GET, `scenario=403` | 403, `ERR_FORBIDDEN` |
| Hết thời gian chờ | GET, `scenario=timeout` | Client hủy request sau khoảng 3 giây và hướng dẫn thử lại |

Query parameter `scenario` dùng để chọn kịch bản demo.
Nó không phải yêu cầu của API backend chính thức.

## Contract và fixtures

Contract công khai:

`../../../contracts/analysis-openapi.yaml`

Fixtures:

`src/mocks/fixtures/`

Kiểm tra fixtures với examples chuẩn và schema OpenAPI:

```powershell
node scripts/check-fixtures.mjs
node scripts/validate-schema.mjs
```

Hiện tại:

- `check-fixtures.mjs` đối chiếu 8 fixtures với contract examples.
- `validate-schema.mjs` kiểm tra 10 fixtures, bao gồm lỗi 402 và danh sách trống.

Hai script có phạm vi kiểm tra khác nhau.

## Kiểm tra mã nguồn

```powershell
npm run lint
npm run test
npm run build
```

Các nhóm test:

| File | Số test |
| --- | ---: |
| `src/App.test.tsx` | 7 |
| `src/mocks/handlers.test.ts` | 10 |
| `src/components/AnalysisDemoPanel.test.tsx` | 12 |
| `src/components/FcmDemoPanel.test.tsx` | 7 |
| `src/components/NotificationSettings.test.tsx` | 30 |
| `src/components/NotificationPushTestPanel.test.tsx` | 9 |
| `src/firebase/tokenLifecycle.test.ts` | 6 |
| Tổng | 81 |

Test UI bao gồm các kịch bản Analysis, timeout và việc ẩn panel khi mock tắt.

## Bằng chứng kiểm tra local

Ngày kiểm tra: 2026-10-10.

- Đối chiếu fixtures: 8/8 PASS.
- Validation schema: 10/10 PASS.
- Lint: 0 warnings, 0 errors.
- Toàn bộ test: 24/24 PASS.
- TypeScript và production build: PASS.
- Kiểm tra trình duyệt: lỗi 402 hiển thị mã lỗi và hướng xử lý.
- Kiểm tra trình duyệt: ảnh bị từ chối không hiển thị kết quả nguy cơ.
- Kiểm tra trình duyệt: timeout hiển thị hướng thử lại.
- Người kiểm tra xác nhận banner và panel biến mất khi tắt mock.

Các kết quả này là bằng chứng local, không thay thế kết quả CI
hoặc review trên pull request.

## Kiểm tra bổ sung SCRUM-294

Ngày kiểm tra: 2026-10-11.

- Hồ sơ mẫu đang xử lý có `risk: null`.
- Bảng và trang chi tiết hiển thị “Chưa có kết quả nguy cơ”.
- Trang upload và modal chỉ nhận MIME JPEG/PNG do trình duyệt cung cấp.
- Chọn tệp không hợp lệ sẽ xóa tên ảnh cũ và hiển thị hướng chọn ảnh khác.
- Kiểm tra trình duyệt xác nhận nhận ảnh hợp lệ và từ chối tệp khác
  ở cả trang upload lẫn modal.
- Modal đưa focus vào trong khi mở, giữ vòng Tab/Shift+Tab,
  đóng bằng Esc và trả focus về nút mở.
- 5 test bàn phím modal PASS; người kiểm tra xác nhận trên trình duyệt.
- Kiểm tra toàn bộ: lint 0 warnings/errors, 29/29 test PASS và build PASS.

Kiểm tra MIME ở frontend chưa xác minh nội dung ảnh, ảnh đáy mắt
hoặc chất lượng ảnh thật. Demo chưa gửi tệp lên backend.
## Kiểm tra bổ sung SCRUM-296 — mốc MSW trước tích hợp push thật

Ngày kiểm tra: 2026-10-11.

- FCM API demo chỉ hiển thị khi bật mock ở development.
- Endpoint `/api/v1/demo/fcm-token` chỉ dùng cho MSW demo,
  chưa phải contract đăng ký token với backend.
- Trình duyệt đã kiểm tra các tình huống 200, 401, 403, 500 và timeout.
- Timeout khóa nút và dropdown trong khi chờ; sau khoảng 3 giây
  hiển thị hướng thử lại và mở khóa điều khiển.
- Khi tắt mock và khởi động lại, panel FCM API demo biến mất.
- Trang không tự yêu cầu quyền thông báo.
- Cài đặt thông báo có hướng dẫn khi quyền bị chặn,
  trình duyệt không hỗ trợ hoặc môi trường không an toàn.
- Test FCM demo: 7/7 PASS.
- Test UI cài đặt thông báo với quyền và Service Worker giả lập:
  10/10 PASS.
- Kiểm tra toàn bộ: fixtures khớp examples, schema PASS,
  lint 0 warnings/errors, 46/46 test PASS và build PASS.
- `@grpc/grpc-js` được cố định bằng override ở phiên bản `1.14.6`.
  Kết quả `npm audit`: 0 vulnerabilities.

Kết quả trên là snapshot lịch sử của demo MSW và UI local.
Kết quả tích hợp Firebase/Notification mới được ghi trong mục
"Cập nhật Notification sau đợt sửa M2–M5" bên dưới.
## Giới hạn hiện tại

- Demo Analysis sử dụng dữ liệu giả lập và không tải ảnh thật lên backend.
- POST trong panel phục vụ mô phỏng tiếp nhận và lỗi; chưa triển khai
  request upload ảnh theo contract backend.
- Các lỗi 401 và 403 là phản hồi mô phỏng, chưa chứng minh xác thực
  hoặc phân quyền backend.
- Kịch bản ảnh bị từ chối không đánh giá chất lượng ảnh thật.
- Chưa xác minh luồng backend, AI và database từ đầu đến cuối.
- Các hồ sơ mẫu và chức năng minh họa khác trong App vẫn là dữ liệu demo.
  Tắt mock chỉ ẩn banner MSW và panel Demo Analysis.
- Kết quả minh họa không dùng để chẩn đoán y khoa.
- Kiểm thử thông báo FCM thuộc phạm vi riêng, chưa được xác nhận
  bằng bộ test Analysis này.

## Cập nhật Notification sau đợt sửa M2–M5

Ngày thực thi: 2026-10-11.
HEAD b2b3dde cùng working tree có các thay đổi chưa commit;
không coi HEAD là phiên bản chứa toàn bộ bản sửa.

- Frontend lần chạy cuối: 81/81 tests PASS trên 7 file.
- NotificationSettings: 30; NotificationPushTestPanel: 9;
  tokenLifecycle: 6; App: 7; handlers: 10;
  FcmDemoPanel: 7; AnalysisDemoPanel: 12.
- Lint: 0 warnings/errors; TypeScript và Vite build PASS.
- Notification API Debug build PASS.
- CryptoChecks: 12/12 PASS.
- PostgreSQL checks PASS, gồm xóa có điều kiện và giữ token mới.
- Browser xác nhận đăng ký/đồng bộ và foreground receipt.
- Người thao tác xác nhận background hiển thị và click focus tab AURA đang mở.
- Worker local và worker Vite phục vụ khớp SHA256.

Một lần frontend có 80/81 PASS; test lỗi chạy riêng PASS,
lần chạy toàn bộ tiếp theo 81/81 PASS. Chưa xác định nguyên nhân.

Đăng ký lần đầu cần thao tác chủ động của người dùng.
Sau đăng ký thành công trong phiên, focus/visible đồng bộ token.
Không coi các kết quả trên là nghiệm thu Analysis event-to-notification,
production TLS/RBAC, token rotation thật hoặc toàn bộ FR/NFR.

- [Notification contract](../../../docs/api/notification_service_contract_v0.1.md).
- [Bằng chứng kiểm thử](../../../docs/testing/m2_m5_notification_verification_2026-10-11.md).

## Kiểm chứng bổ sung ngày 2026-10-12

Nguồn: log PowerShell người thao tác cung cấp.
Nhánh fix/sprint1-m2-m5-completion; thay đổi chưa commit.

- Frontend: 7 file test, 82/82 test PASS.
- tokenLifecycle: 7 test; NotificationSettings: 30 test.
- Lint: 0 lỗi và 0 cảnh báo.
- TypeScript và Vite production build: PASS.
- Bổ sung kiểm tra phiên sau khi backend đăng ký hoàn tất.
  Nếu phiên đã đổi, lifecycle trả cancelled thay vì ready.
- Loại bỏ dòng gán operationInFlight bị lặp.

Kết quả 81 test trước đó là bằng chứng của lần chạy trước.
Kết quả mới không thay thế kiểm thử FCM thật, phân quyền
backend, usability/accessibility hoặc nghiệm thu NFR.
## Cập nhật SCRUM-313 — Kiểm thử và tài liệu Frontend

Ngày kiểm thử: 2026-10-17.

### 1. Chức năng đăng nhập

Frontend sử dụng Firebase Authentication để hỗ trợ:

- Đăng nhập bằng Email và mật khẩu.
- Đăng nhập bằng Google.
- Duy trì phiên đăng nhập bằng browser local persistence.
- Chuyển về trang chủ khi đăng nhập thành công.
- Hiển thị thông báo khi đăng nhập thất bại.

Component: `src/pages/LoginPage.tsx`.

### 2. Chức năng Upload ảnh võng mạc

Component: `src/pages/UploadPage.tsx`.

Frontend hỗ trợ chọn nhiều ảnh JPEG/PNG, hiển thị ảnh xem trước và nhập Patient ID theo định dạng UUID.

Các điều kiện kiểm tra:

- Tối đa 100 ảnh mỗi lần.
- Mỗi ảnh không vượt quá 10 MB.
- Độ phân giải không vượt quá 16 triệu pixel.
- Người dùng phải đăng nhập Firebase trước khi gửi ảnh.

Frontend gửi yêu cầu `POST /api/v1/analyses` bằng `multipart/form-data`, kèm Firebase ID Token và Idempotency-Key.

### 3. API Mapping

| Chức năng | API hoặc Firebase SDK |
| --- | --- |
| Đăng nhập Email | `signInWithEmailAndPassword()` |
| Đăng nhập Google | `signInWithPopup()` |
| Theo dõi phiên đăng nhập | `onAuthStateChanged()` |
| Lấy ID Token | `currentUser.getIdToken()` |
| Gửi ảnh phân tích | `POST /api/v1/analyses` |
| Tiếp nhận yêu cầu | HTTP 202 Accepted |
| Dữ liệu không hợp lệ | HTTP 400 |
| Chưa xác thực | HTTP 401 |
| Không đủ quyền | HTTP 403 |
| Dung lượng vượt giới hạn | HTTP 413 |
| Quá nhiều yêu cầu | HTTP 429 |
| Lỗi máy chủ | HTTP 500/502/503 |

### 4. Kiểm thử Frontend

Chạy tại `src/web/aura-web`:

```powershell
npm run lint
npm run test
npm run build
```

Kết quả kiểm thử ngày 2026-10-17:

| Hạng mục | Kết quả |
| --- | --- |
| LoginPage | 2/2 PASS |
| UploadPage | 17/17 PASS |
| Toàn bộ Frontend | 101/101 PASS |
| ESLint | 0 errors, 2 warnings |
| Production Build | PASS |

Các bài test sử dụng Firebase mock và phản hồi API giả lập, không cần tài khoản Firebase hoặc dữ liệu bệnh nhân thật.

Một bài test NotificationSettings từng thất bại khi chạy toàn bộ nhưng thành công khi chạy riêng và khi chạy lại toàn bộ. Cần tiếp tục theo dõi tính ổn định.

### 5. Giới hạn kiểm thử

- HTTP 202 trong bài test là phản hồi giả lập, chưa chứng minh Backend thật tiếp nhận ảnh.
- Chưa xác nhận quy trình Upload → Backend → AI → Database hoàn chỉnh.
- Kiểm tra MIME, dung lượng và độ phân giải phía Frontend không thay thế kiểm tra nội dung và chất lượng ảnh ở Backend.
- Kết quả phân tích minh họa không dùng để chẩn đoán y khoa.
