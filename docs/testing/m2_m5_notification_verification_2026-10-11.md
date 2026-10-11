# AURA — Kiểm chứng Notification trong đợt sửa M2–M5

## 1. Môi trường và phiên bản

- Task liên quan: SCRUM-57, SCRUM-58, SCRUM-286, SCRUM-296.
- Ngày thực thi do người thao tác cung cấp: 2026-10-11.
- Thời điểm lập bản ghi: 2026-10-11T23:26:40.9492105+07:00.
- HEAD: b2b3dde12d71f73fb387569feddd5dff3ccc3db9.
- Nhánh: fix/sprint1-m2-m5-completion.
- Working tree có thay đổi chưa commit; HEAD không chứa toàn bộ bản sửa.
- Windows; Node.js v24.21.0; npm 11.19.0; .NET SDK 10.0.401.
- Web: http://localhost:5173.
- Notification API: http://127.0.0.1:5003.
- PostgreSQL local; dữ liệu kiểm thử dùng token giả và device ID mới.
- Worker local SHA256: 7FEA005F6DB0406B44BF6C83B00D74A87A5684CA5FA1CE9052965BFDDF78036F.
- Worker đang được Vite phục vụ có cùng SHA256.
- Hash trên không tự chứng minh bytes của worker đã cài trong trình duyệt.

## 2. Kết quả có bằng chứng

| Kiểm tra | Kết quả | Nguồn và phạm vi |
| --- | --- | --- |
| Notification API build Debug | PASS | Log PowerShell; xác nhận biên dịch |
| TokenCipher | 12/12 PASS | Log CryptoChecks; dữ liệu giả |
| Database registration | PASS | Đọc/giải mã/fingerprint, cập nhật một row và nonce mới |
| Xóa có điều kiện | PASS | Token cũ không xóa token mới; sai project/UID/device không xóa; token khớp xóa được; xóa lặp không lỗi |
| Dọn dữ liệu kiểm thử | PASS | Xác nhận không còn registration kiểm thử |
| Frontend | 81/81 PASS, 7 file test | Lần chạy cuối; unit/component/MSW |
| Lint | PASS | 0 lỗi, 0 cảnh báo |
| Frontend build | PASS | TypeScript và Vite |
| Runtime dependency audit | 0 vulnerabilities tại bước kiểm tra trước | Chưa chạy lại sau đợt sửa; chưa thay dependency |
| Đồng bộ registration trên browser | PASS | Ảnh giao diện người thao tác cung cấp |
| Foreground FCM | PASS | Ảnh giao diện có mã gửi/nhận khớp nhau |
| Background FCM | PASS theo quan sát người thao tác | Chuyển sang tab khác và nhận thông báo |
| Notification click | PASS theo quan sát người thao tác | Bấm thông báo đưa tab AURA đang mở lên trước |

## 3. Ghi nhận lỗi kiểm thử

Một lần chạy toàn bộ frontend có 80/81 PASS:
test retry sau lỗi backend không tìm được alert trong thời gian chờ.
Chạy riêng test đó PASS; lần chạy toàn bộ tiếp theo 81/81 PASS.
Chưa xác định nguyên nhân; không ghi lỗi này là đã được sửa.

## 4. Giới hạn còn lại

- Chưa kiểm tra click khi không còn cửa sổ AURA đang mở.
- Token rotation được kiểm tra bằng mock; chưa ép token thật thay đổi.
- Xóa có điều kiện đã kiểm tra PostgreSQL; chưa ép FCM thật trả Unregistered.
- Chưa kiểm thử đầy đủ endpoint xử lý lỗi FCM và lỗi storage bằng fault injection.
- Chưa có luồng Analysis event -> Notification toàn hệ thống.
- HTTP localhost không chứng minh TLS hoặc bảo mật môi trường production.
- Chưa nghiệm thu toàn bộ FR-9 hoặc các NFR toàn sản phẩm.
- Bản ghi này không thay thế review và cập nhật trạng thái Jira.

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
