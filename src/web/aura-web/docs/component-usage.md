# AURA Frontend — Component Usage

## 1. LoginPage

**File:** `src/pages/LoginPage.tsx`

**Chức năng:** Cung cấp giao diện đăng nhập Firebase Authentication.

- Đăng nhập bằng Email và mật khẩu.
- Đăng nhập bằng Google.
- Duy trì phiên đăng nhập.
- Hiển thị thông báo lỗi khi xác thực thất bại.
- Điều hướng về trang chủ sau khi đăng nhập thành công.

**Phụ thuộc:** `firebase/auth`, `getFirebaseAuth()`, `react-router-dom`.

**Kiểm thử:** `src/pages/LoginPage.test.tsx` — 2 tests passed.

## 2. UploadPage

**File:** `src/pages/UploadPage.tsx`

**Chức năng:** Chọn và gửi ảnh võng mạc đến Backend AURA.

**Dữ liệu nhập:**

| Trường | Mô tả |
| --- | --- |
| patientId | UUID của bệnh nhân |
| eyeSide | `left`, `right`, `unknown` |
| images | Một hoặc nhiều ảnh JPEG/PNG |
| modality | Giá trị cố định `FUNDUS` |

**Kiểm tra phía Frontend:**

- Tối đa 100 ảnh.
- Tối đa 10 MB mỗi ảnh.
- Tối đa 16 triệu pixel mỗi ảnh.
- Yêu cầu đăng nhập trước khi gửi ảnh.

**API:** `POST /api/v1/analyses`.

**Headers:** `Authorization: Bearer <Firebase ID Token>` và `Idempotency-Key`.

**Body:** `multipart/form-data`.

**Phản hồi thành công:** HTTP 202 với `batchId`, `items`, `createdAt` và `statusUrl`.

**Kiểm thử:** `src/pages/UploadPage.test.tsx` — 17 tests passed.

## 3. Kiểm thử Component

Chạy từ thư mục `src/web/aura-web`:

```powershell
npm run test -- src/pages/LoginPage.test.tsx
npm run test -- src/pages/UploadPage.test.tsx
```

Các bài test sử dụng Firebase mock và API mock, không gửi dữ liệu đến Backend thật.

## 4. Giới hạn

Các bài test hiện tại chưa xác minh quá trình xử lý ảnh thực tế của Backend hoặc AI. Việc nhận HTTP 202 trong mock không đồng nghĩa Backend thật đã tiếp nhận yêu cầu.