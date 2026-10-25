# M5 — Trang chủ tài khoản thật và đường logout chung (8M)

Ngày: 25/10/2026. Baseline trước thay đổi: source202 và closeout 8L `67dbf067ffbb479fbb083358aa116dfd`, giữ primary/fixture sau 8K `a248fdd42e5d4c7ab5a030289a0e696c`.

## Vấn đề và hành vi mới

Trước 8M, login backend-ready điều hướng `/`, nhưng route wildcard render Dashboard demo với System Admin và bệnh nhân/risk cố định. Nút logout tại Dashboard gọi Firebase trực tiếp thay vì session API chung. Các giá trị demo không phải quyền hoặc dữ liệu của tài khoản Patient đã xác nhận.

Sau 8M, `/` render SessionHomePage, lấy displayName/roles từ context chung và dùng policy upload cá nhân hiện có. Nút logout đi qua store đã có ở 8B; không đổi logic store hoặc persistence. Dashboard cũ giữ tại `/demo/*` và router demo tại `/router-demo/*`, cùng nhãn giả lập rõ ràng. Route không tồn tại trả 404.

Trang chủ không tự fetch, bật FCM, gọi Billing/Analysis/Blob hoặc hiển thị kết quả giả. Dòng role chỉ là nhãn của context đã xác nhận, không thay đổi RBAC. Form upload/helper/context/login/ProtectedRoute hiện có không bị thay đổi bởi 8M.

## Đối chiếu ba task gốc

Đã đọc trực tiếp mô tả Jira ngày 25/10/2026, chỉ đọc, không chỉnh sửa:

- [SCRUM-311](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-311): layout/router/component dùng chung; task gốc nêu demoUserRole cố định và dashboard placeholder như giới hạn. Trạng thái đọc được: Done.
- [SCRUM-312](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-312): Email/Google, logout, persistence/reload, bảo vệ trang, upload API thật và phản hồi/lỗi. Trạng thái đọc được: In Review; mô tả còn ghi API 404, chưa phản ánh evidence UI402/202 mới.
- [SCRUM-313](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-313): tests login/upload/error, README/component/API mapping và giữ fixtures. Trạng thái đọc được: Done; mô tả còn ghi 101 tests/mock-only của lần cũ.

Batch-status UI và file-access UI không được nêu là yêu cầu bắt buộc của ba task trên. Chúng vẫn là khả năng chưa nghiệm thu trong chuỗi này, không tự mở rộng thành task mới hoặc dùng để trì hoãn kết luận phần upload. Không tự đổi trạng thái Jira; cần review cả các giới hạn còn lại trước khi kết luận toàn M5 Done.

## Thay đổi source

Sửa main.tsx và hai tài liệu web; thêm SessionHomePage TSX/CSS/test, ApplicationRoutes TSX/test và tài liệu này. Tổng 9 file, thêm 6 file; baseline sau thay đổi là source208. Không thay package/lockfile, env, API contract, backend/DB/role/credential, LoginPage, session store, ProtectedRoute hoặc UploadPage.

## Kiểm chứng và giới hạn

Test mới kiểm tra role/context thật, policy Patient/Admin, XSS text rendering, route loading/error/anonymous/ready, phân biệt demo, 404, logout thành công/thất bại/pending callback và Back vào upload. Route test dùng đúng bảng route mà main.tsx import, không dựng một bảng route thay thế. Test khởi động import main.tsx thật và render cây Provider/router thật để kiểm tra session được nối vào entrypoint.

Kiểm tra trên frontend tái dựng theo hash baseline trong môi trường tạo gói: 14 test files / 277 tests PASS (246 cũ + 31 mới), lint không báo warning/error, TypeScript/Vite build PASS. Đây chưa phải kết quả chạy trên máy Windows của người dùng.

8M là patch source + kiểm thử giả lập. Nút logout/Firebase/Google runtime của source mới cần runner riêng sau khi test/lint/build trên Windows PASS; chưa mở Vite hoặc gọi thêm bootstrap/upload trong bước apply. 8K/8L vẫn giữ bằng chứng source202; không sửa các JSON đó. Checkpoint 8M ghi tường minh source202→source208 và dữ liệu hiện tại không đổi.

Giữ fixture/RESERVED hold/Blob 5J và 8K. Không publish outbox, capture/release/refill, chạy Worker/AI hoặc rollout primary. Không chạy lại 8K/8J2/8H/8L sau khi source đổi; runner sau phải đọc checkpoint 8M.
