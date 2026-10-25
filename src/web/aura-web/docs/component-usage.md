# AURA Frontend — Component Usage hiện tại (8M)

## Session provider và route

`BackendSessionProvider` phải bọc router và các trang tài khoản thật. Provider giữ một store; `onAuthStateChanged` dẫn tới xác nhận Identity/Profile. Không tạo một Firebase observer khác ở trang chủ hoặc ProtectedRoute.

`ApplicationRoutes` là bảng route được `main.tsx` sử dụng thực tế. `/login` công khai; `/`, `/upload`, `/demo/*` được bảo vệ theo session ready. `/router-demo/*` chỉ là khu vực kiểm thử router/component có nhãn giả lập. URL không có route trả NotFoundPage.

## SessionHomePage

File: `src/pages/SessionHomePage.tsx`.

Lấy displayName và toàn bộ roles từ `useBackendSession()`. ROLE_LABELS chỉ đổi nhãn hiển thị, không tự gán hoặc nâng quyền. Không hiện Firebase UID, account/patient/tenant ID, giá trị risk hoặc số hồ sơ demo. Trang chủ không gọi API, không polling và không tự mở Notification/FCM demo.

Link `/upload` chỉ hiển thị theo cùng `canUploadPersonal` của UploadPage: có PATIENT và không SYSTEM_ADMIN. Link giao diện không thay thế authorization của API. Doctor có thêm PATIENT vẫn dùng được hồ sơ cá nhân; doctor-only/Clinic cần luồng riêng.

Nút Đăng xuất gọi `logout()` của context. Không gọi signOut/navigate trực tiếp từ trang này. Session loading/error làm mất UI tài khoản cũ; ProtectedRoute chỉ điều hướng login khi anonymous. Lỗi signOut dùng BackendSessionNotice và cho người dùng chủ động thử lại.

## LoginPage

Email/mật khẩu và Google qua Firebase SDK, browserLocalPersistence hiện có. Sau Firebase login, chờ store xác nhận `POST /api/v1/identity/bootstrap` và `GET /api/v1/profiles/me`; Firebase user chưa đủ để mở UI thật. Khi backend-ready, điều hướng `/` tới trang chủ thật.

Không ghi password/token vào log, storage của workspace hoặc dữ liệu gửi backend. Firebase SDK quản lý persistence của nó; ứng dụng không bổ sung cache token/profile.

## UploadPage

| Trường | Nguồn |
| --- | --- |
| patientId | BackendContext đã xác nhận, không có ô nhập tay |
| modality | FUNDUS |
| eyeSide | Người dùng chọn left/right/unknown |
| images | 1..100 JPEG/PNG, mỗi ảnh tối đa 10 MiB và 16 triệu pixel |
| Authorization | Firebase ID token của đúng phiên tại thời điểm gửi |
| Idempotency-Key | UUID bất biến của một lượt gửi |

`personalUploadWorkspace` giữ draft trong bộ nhớ của đúng context. `sendUploadAttempt` gửi cùng-origin multipart POST `/api/v1/analyses`, deadline 60 giây; không follow redirect hoặc gửi service token. Timeout/lỗi chưa rõ giữ key/payload, không tự gửi lại hoặc cho tạo lượt mới để bỏ qua kết quả cũ. 202 phải khớp receipt contract trước khi hiển thị tiếp nhận; không suy ra đã có kết quả AI.

## Khu vực minh họa

`App.tsx` giữ Dashboard/component demo cũ ở `/demo/*`, có banner nêu rõ role/data giả lập. `AppRouter` và roleMenus của demo vẫn ở `/router-demo/*`. Không dùng demoUserRole làm BackendContext hay gọi đó là quyền thật.

## Kiểm thử

Chạy `npm test`, `npm run lint`, `npm run build`. Bộ cũ 246 tests vẫn được giữ. Test mới nằm ở `SessionHomePage.test.tsx` và `ApplicationRoutes.test.tsx`; route test dùng đúng ApplicationRoutes, ProtectedRoute, LoginPage và session store, SDK/HTTP được giả lập. App.test.tsx vẫn kiểm thử Dashboard demo riêng.

Kiểm thử giả lập không thay cho Firebase/logout/Google, phân quyền backend, batch/file UI hoặc xử lý Worker/AI thật. 8K/8L chứng minh upload cá nhân theo phạm vi evidence, không chứng nhận toàn bộ M5.
