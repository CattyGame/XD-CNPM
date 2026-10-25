# M5 Sprint2 — phạm vi nghiệm thu sau8Q5

Ngày đối chiếu:25/10/2026, múi giờ Asia/Ho_Chi_Minh. Phạm vi: intake cá nhân và session của tài khoản TEST hiện hữu. Tài liệu này bổ sung các checkpoint cũ, không sửa report STOP/PASS cũ và không chứng nhận toàn bộ M5/Sprint2.

## Nguồn và cách đọc kết quả

Log Windows do người dùng cung cấp ghi nhận kết quả dưới đây. Runner8R chỉ ghi tài liệu này sau khi đối chiếu JSON8Q5 gốc, package, source211 và các backup/evidence trên máy.8R là kiểm tra provenance/source offline, không SQL/HTTP/Docker/Firebase/Blob và không tái chứng nhận runtime hiện tại.

| Checkpoint | Nội dung đã đạt | Evidence tại `%USERPROFILE%\.aura\checks` |
|---|---|---|
|8E|Cùng TEST ACTIVE/READY; Identity/Profile gateway và direct200, IDs/roles/ETag khớp|`m5-authenticated-context-74fd4381730544df81311fbd9d6430fa.json`|
|8F|Private anonymous redirect, đăng nhập Email, đúng hồ sơ và reload, đóng phiên riêng tư|`m5-browser-verification-c050efe8aa6a45bf8b8ca629dd90b40b.json`|
|8H v2|Đúng một upload UI thật trả402; một ABORTED operation/attempt mới; ví0/hold5J cũ bảo toàn|`m5-ui-zero-credit-9ebb52a88df649adb927dade5c7ae2f0.json`|
|8K v2|Đúng một upload UI thật trả202/QUEUED trong fixture DB cô lập|`m5-ui-acceptance-a248fdd42e5d4c7ab5a030289a0e696c.json`|
|8L|Closeout upload402/202 chỉ đọc; không fresh Blob inventory|`m5-closeout-67dbf067ffbb479fbb083358aa116dfd.json`|
|8N4|Email home/persistence trong phiên/logout/Back/reload,4 contextAPI200; không API sau freeze|`m5-home-logout-bcd0c42c1e5445fca29d82163276d0f3.json`|
|8P|SDK/manual Console link Google vào cùng TEST UID, Password+Google, đã signout; không app-login acceptance|`m5-google-link-4b12339c0392416899a5b0a387d3ebd5.json`|
|8Q4|Exact năm file source208→211 page-restore patch, hai source backup, kiểm tra bảo toàn cuối lượt|`m5-page-restore-patch-2d2a424db278420d8fa420474b41ab13.json`|
|8Q5|5/5 quan sát Google login, home đúng hồ sơ/Bệnh nhân, reload, logout/Back không cần F5, direct-upload/reload anonymous;4 contextAPI200 và không API sau freeze|`m5-google-page-restore-3fc7b4eaa88d45489710e6062fc3d159.json`|

8Q5 gate chỉ giải mã `sub` và `firebase.sign_in_provider=google.com` trong RAM, không tự kiểm chữ ký JWT; backend auth hiện có phải trả200. UI/Google choice/IDs-version-roles/logout/Back là quan sát thủ công; metrics/counts/forwarding/final-state comparison là kiểm tra tự động.8R không đọc lại credential/token/response body hay ghi UID vào log.

## Hành vi được chấp nhận trong phạm vi này

- Anonymous vào `/upload` được chuyển `/login`. Firebase login chưa đủ để vào nghiệp vụ: shared backend session phải xác nhận Identity/Profile, ACTIVE/READY, PATIENT và binding đúng.
- Trang `/` dùng tên/vai trò từ backend context thật; Dashboard/admin/số liệu giả nằm trong khu vực demo có nhãn. Route guard UI không thay thế API authorization. Clinic/Doctor/System Admin runtime chưa được nghiệm thu bởi chuỗi personal TEST này.
- Đăng nhập Email và Google trên cùng TEST đã được kiểm tra ở các lượt riêng. Reload giữ cùng hồ sơ trong phiên riêng tư. Google/logout/Back trên source211 đạt ở8Q5; không cần F5 để xóa giao diện cũ.
- Upload tự lấy patientId/tenant/owner từ context, không cho nhập tự do trên form cá nhân. Chọn ảnh/eyeSide, gửi Authorization và một Idempotency-Key cho một lượt; UI phân biệt lỗi và thông tin tiếp nhận.402 và202 đã kiểm tra bằng hai fixture/lượt riêng có gate.
-202 là tiếp nhận/QUEUED, chưa phải kết quả AI. Batch ID/Status URL hiển thị trong receipt không có nghĩa batch-status UI hoặc AI end-to-end đã nghiệm thu.

## Dữ liệu và giới hạn cần giữ

Tại lần kiểm tra cuối8Q5: fixture8K vẫn có một accepted request/image/job, hai pending outbox, một CREDIT và một RESERVE(-1), balance0/hold RESERVED; private original Blob đã có proof8K. Primary và ví0/hold RESERVED5J cũ được bảo toàn; owned candidates đã gỡ, owned Vite dừng, primary Analysis HTTP disabled. Các kiểm tra sau8K chỉ dùng proof Blob đã lưu, không cấp chứng nhận Blob tồn tại hiện tại.

Không rerun8H/8K/8Q3/8Q4/8Q5 hoặc runner source208; không replay/refill/capture/release/refund, tiêu thụ outbox/hold, kích hoạt Worker/AI/publisher, rollout primary hay cleanup. Firebase sign-in/provider metadata có thể đã đổi do các lượt login/link được phép.

Lượt335 vẫn STOP: Back sau logout hiện Upload/hồ sơ, F5 về login.8Q5 không tái hiện hành vi đó sau patch. Không ghi `pageshow.persisted`, nên native BFcache path và nguyên nhân335 vẫn chưa được chứng minh. Không chứng nhận independent JWT signature audit, Password login sau link, cross-tab revocation, browser-restart persistence, live Blob inventory, mọi HTTP retry/conflict/replay branch, Clinic/Doctor hay toànM5.

## Kiểm thử và thay đổi8R

Windows source211 trước8R:15 files/288 tests PASS, lint0 warnings/0 errors, TypeScript/Vite build PASS88 modules. Đây là local Windows evidence, không phải GitHub CI của source211.

8R chỉ sửa helper `enableForSync` trong `NotificationSettings.test.tsx` để `await act(async () => enableNotifications())` trước khi phát focus/visibility events; không tăng timeout, bỏ assertion hoặc đổi code ứng dụng. Cùng helper trên frontend develop6d2ee tái hiện lỗi visibility test trước sửa:100/101 PASS,1 FAIL; sau sửa101/101 PASS. Isolated source211+fix đạt288/288, lint0/0, build88 modules. Tests dùng mock, không đăng nhập hay gọi Notification/Firebase thật.

8R thêm tài liệu này và sửa một test, tạo source212. Mã ứng dụng auth/router/upload/lifecycle giữ nguyên source211 đã nghiệm thu.8R không tự cấp runtime certificate mới cho source212. Source212 phải được người dùng chạy test/lint/build trước review/commit; không dùng lại runner211 trên212.

## Đối chiếu Jira và tích hợp Git — chưa đóng toàn Sprint2

Đọc Jira25/10/2026: SCRUM-311 Done, SCRUM-312 In Review, SCRUM-313 Done. Trên tập18 task W2 SCRUM-299..316:8 Done/10 In Review (44,4% theo trạng thái workflow, không phải phần trăm kỹ thuật). Không sửa trạng thái/mô tả Jira trong lượt này.

- [SCRUM-311](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-311): nền tảng layout/router/components đã có; mô tả lịch sử gốc đang nói `/` là Dashboard demo/SYSTEM_ADMIN, cần bổ sung phân biệt personal home thật với demo sau source208/211.
- [SCRUM-312](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-312): mục tiêu Email/Google/logout/persistence/protected routes/upload API thật đã có evidence trong phạm vi trên. Đoạn hiện tại nói API chưa có/404 và cho nhập patientId đã lỗi thời; cần cập nhật có điều kiện402/202, context cá nhân và giới hạnfixture. Chưa tự chuyển Done khi local changes chưa được review/tích hợp.
- [SCRUM-313](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-313): số101 tests/2 warnings và mô tả mock-only là lịch sử, cần bổ sung288 tests/lint0/0 cùng phân biệt mock tests và UI runtime evidence; không viết rằng toàn bộAI end-to-end đã PASS.

GitHub develop đã nhận [PR57](https://github.com/CattyGame/XD-PMHDT/pull/57), head `6d2ee7888eb113f3f8b2681fc3f650c264b16e54`, khác local baseline `87af21da20489eda6d4899f7e1f3a428b24960db`. PR thay App.css/App.tsx/LoginPage.css/LoginPage.tsx/LoginPage.test.tsx. LoginPage trên develop còn điều hướng từ Firebase observer/sign-in; local source211 dùng shared backend-ready session. Cần tích hợp có chọn lọc UI vào session-aware login, không ghi đè file local bằng bản remote hoặc pull/reset/stash mù.

Khi đọc GitHub: [AURA CI run40153486488](https://github.com/CattyGame/XD-PMHDT/actions/runs/40153486488) trên6d2ee bị FAIL ở React Test, React Build skipped; .NET/Python/Docker-Gateway PASS. Repository checks PASS. Connector không cho đọc annotation endpoint, nên test fail cục bộ là bằng chứng tái hiện riêng; chưa chứng minh toàn bộ nguyên nhân jobCI. Kết quả101/101 local sau fix không phải CI đã xanh trở lại.

Việc còn lại: review diff của các thay đổi local theo module; chuẩn bị nhánh/worktree tích hợp sau khi giữ nguyên local/evidence; port phần UI cần thiết củaPR57 vào backend-session policy; chạy suite/build và review/CI trên đúng commit tích hợp. Sau đó owner cập nhật biên bản/Jira bằng nội dung đã review. Worker/publisher ở Sprint3 theo SCRUM-302/303; không dùng chúng làm điều kiện bổ sung để thử lại fixture8K hiện có.
