# M1 Sprint 2 — Kiểm chứng local ngày 18/10/2026

Nguồn: develop tại 4ff7bff251b2062e6eec1b2cb959c0a60082ad07.
Môi trường: Windows PowerShell và Docker Desktop.

## Kết quả đã xác nhận

- 30 kiểm tra cấu hình PASS; bộ này không chạy Docker/HTTP/database thật.
- Schema Profile cũ được lưu tại profile_legacy_m1_20261018.
- Schema Profile mới được tạo thành công trong một transaction.
- Chạy đầy đủ migration và cấu hình runtime logins thành công.
- Các dịch vụ Compose đang chạy; 20 kiểm tra môi trường thật PASS.
- Development bootstrap Identity → Profile → PostgreSQL: 24 checks PASS.
- Bootstrap lặp giữ nguyên accountId.
- Ba storage container tồn tại và tắt anonymous public access.
- SAS chỉ đọc hoạt động, không ghi đè được; SAS hết hạn bị từ chối.
- Blob thử được xóa sau kiểm tra storage.
- Firebase user token qua Gateway tới Notification và Identity: 22 checks PASS.

Các số 20, 24 và 22 có các kiểm tra nền trùng nhau, không cộng dồn.

## Xử lý schema cũ

Script: scripts/dev/Repair-AuraLegacyProfile.ps1.

Script chỉ dành cho schema legacy đã kiểm tra khi các bảng nghiệp vụ
Identity/Profile còn rỗng, ngoại trừ danh mục identity.roles.
Yêu cầu backup pg_dump dạng custom và migration đúng phiên bản.
Script giữ bảng Profile cũ trong schema lưu trữ và từ chối chạy lần hai.
Không chạy lại script sửa schema sau khi đã bootstrap dữ liệu thử.

## Lưu ý nhập token

Dán bằng chuột phải đã chạy thành công trong terminal kiểm thử.
Chuẩn bị lệnh PowerShell trước khi sao chép token để tránh ghi đè clipboard.
Không lưu token, mật khẩu, Firebase JSON hoặc khóa AES vào repository.

## Giới hạn

Kết quả xác nhận môi trường local và các luồng nêu trên.
Chưa chứng nhận phân quyền Profile, upload/reservation hoặc Worker/AI end-to-end.
Chưa xác nhận pipeline CI trên GitHub của commit bổ sung này.
