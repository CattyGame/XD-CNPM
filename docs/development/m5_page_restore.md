# M5 — khôi phục tài liệu sau Back/logout

Lượt33528ffb32cd4bf49d02cf507d3bb25c dừng ở quan sát4/5: sau logout, Back hiện lại Upload và hồ sơ cũ; F5 về login. Gate ghi đúng bốn request LOGIN/RELOAD trước freeze, không ghi API sau freeze. Đây là lỗi quan sát UI, chưa chứng minh backend vẫn cho phép truy cập sau logout. Lượt335 không ghi pageshow.persisted nên chưa chứng minh nguyên nhân bfcache.

Bản sửa source208→211:

- Provider gắn lifecycle listeners trước khi start store và tháo chúng khi unmount/StrictMode cleanup.
- pagehide che root đồng bộ bằng hidden/inert/visibility:hidden!important, rồi suspend store: hủy context request, tháo observer và xóa context khỏi snapshot.
- Store đã suspend vì navigation không được start/retry/logout hoặc callback cũ kích hoạt lại. Không signOut hoặc xóa Firebase persistence khi rời tài liệu.
- pageshow.persisted=true giữ root bị che và tự reload một lần. Document mới khởi tạo Firebase từ persistence hiện tại, đi qua auth/backend validation thông thường. Document cũ không tự gửi bootstrap/Profile bằng user giữ trong RAM.
- Normal pageshow, focus/visibility khi mở popup Google và điều hướng trong cùng document không reload theo cơ chế này. Same-document Back sau logout vẫn dùng ProtectedRoute.

Không sửa LoginPage, route policy, API helper, token persistence, backend/env/Docker/Billing/Blob/Worker. Không dùng unload hoặc vô hiệu hóa bfcache toàn browser.

Kiểm thử cục bộ: test hồi quy che root thất bại với provider/store cũ và đạt sau sửa.11 test mới dùng lifecycle events trong jsdom, Firebase/context mock: che root, reload một lần, normal pageshow/popup, pending abort/late completion, callback cũ, store không hồi sinh, fresh document anonymous, same-document Back và StrictMode cleanup. Tổng15 files/288 tests PASS, lint ứng dụng sạch, TypeScript/Vite build PASS88 modules. Không chạy Google/Firebase thật hoặc Windows browser.

Chưa nghiệm thu bfcache thật trên Cốc Cốc/Chrome, Google/logout sau patch, cross-tab revocation/browser restart persistence, live Blob hoặc toàn M5. Sau khi áp dụng và chạy test/lint/build, cần runner browser mới dùng đúng checkpoint/source211; không chạy lại8Q3/source208. Back phải tự về login, không bấm F5, không hiển thị hồ sơ/Upload cũ và không API sau logout freeze.

Tham khảo: https://web.dev/articles/bfcache và https://developer.chrome.com/docs/web-platform/bfcache-ccns .
