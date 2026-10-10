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
| `src/App.test.tsx` | 2 |
| `src/mocks/handlers.test.ts` | 10 |
| `src/components/AnalysisDemoPanel.test.tsx` | 12 |
| Tổng | 24 |

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