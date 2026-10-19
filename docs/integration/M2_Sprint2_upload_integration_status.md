# M2 Sprint 2 — trạng thái tích hợp upload/file access

Ngày rà soát: 2026-10-19. Baseline đối chiếu: `develop` tại repository `CattyGame/XD-PMHDT` và source ZIP được cung cấp. Contract REST giữ nguyên `contracts/analysis-openapi.yaml` v0.6.0.

## SCRUM-302 — Analysis DB

### Đã bổ sung trong patch
- SQL test có `ON_ERROR_STOP=1`; lỗi assertion làm script kết thúc non-zero.
- Kiểm tra PostgreSQL 17+, đủ 8 bảng, constraint quan trọng, partial index `ix_outbox_unpublished`, runtime role và quyền DML.
- Test hành vi idempotency scope (cùng tenant/requester/key phải bị từ chối; key cùng giá trị ở tenant khác được phép), fingerprint, modality FUNDUS và FK không cho tham chiếu request khác tenant.
- Test fixture nằm trong transaction và được `ROLLBACK`.

### Cần chạy để nghiệm thu
- Chạy `psql` trên database kiểm thử cô lập sau migration; lưu log, version, exit code và commit SHA.
- Xác nhận role `aura_analysis_app` được provision bởi migration/infra. Script cố ý fail nếu role thiếu, không tự tạo role.
- Đối chiếu quyền với runtime role thật, không chạy API bằng bootstrap/migration role.

## SCRUM-303 — upload/idempotency domain

### Đã bổ sung trong patch
- PNG: CRC32 được xác minh cho từng chunk, bao gồm IHDR, IDAT và IEND.
- PNG: yêu cầu chunk IHDR đầu tiên, IEND rỗng và không có bytes thừa sau IEND.
- Fixtures tạo CRC hợp lệ; tests mới bao gồm CRC IDAT sai và bytes thừa sau IEND.

### Chưa hoàn thành / phụ thuộc tích hợp
- Inspector vẫn không giải mã toàn bộ JPEG hoặc giải nén dữ liệu IDAT PNG. CRC hợp lệ không chứng minh ảnh giải mã thành công. Để kiểm tra hỏng dữ liệu nén thật, cần thư viện decoder được nhóm phê duyệt và tests với ảnh JPEG/PNG thật/corrupt.
- `Program.cs` trong baseline chỉ map `/health` và `/api/v1/platform/ping`; chưa map endpoint nghiệp vụ `/api/v1/analyses`, batch status hoặc file access theo OpenAPI.
- Chưa có concrete `IUploadRepository`/`IFileAccessRepository` PostgreSQL, Billing reservations adapter hoặc Azure Blob/Azurite adapter trong thư mục Analysis API được rà soát.
- Cần map requester từ authenticated principal; không lấy requester/owner từ multipart form. Profile permission phải được kiểm tra trước replay; không trả 202 khi chưa durable commit.
- Cần kiểm thử crash/ambiguous commit và reconciliation. Không release reservation hoặc xóa object nếu chưa xác nhận transaction abort.

## SCRUM-304 — tests và bàn giao

### Bằng chứng có sẵn từ source snapshot
`docs/testing/scrum304_upload_test_results.txt` ghi 59 unit/domain tests PASS, 0 FAIL tại 2026-10-17; đây là kết quả đã được ghi lại trước patch, không phải kết quả chạy patch mới.

### Việc còn mở
- Chạy test .NET sau patch.
- Thêm HTTP integration tests cho status/headers/schema và xác thực.
- Chạy test với PostgreSQL/Azurite thật; kiểm tra cùng key replay, payload khác trả 409, concurrency chỉ tạo một batch/reservation, file access trái quyền trả 404 và URL ký không lộ object key.
- Bổ sung evidence từ endpoint/runtime sau khi các adapter có mặt. Không tuyên bố end-to-end hoàn tất chỉ dựa vào fake/stub tests.

## Điều kiện để đóng cả ba task

1. SCRUM-302: SQL test chạy PASS trên PostgreSQL 17+; lưu output và exit code.
2. SCRUM-303: domain tests mới PASS; API/DB/Billing/storage adapters được nối đúng contract và có HTTP integration tests.
3. SCRUM-304: có test report thực chạy và tài liệu/sequence khớp triển khai; bằng chứng quyền sở hữu, replay, concurrency và cleanup trên dependency thật.
4. M1/M3/M6 xác nhận cấu hình storage/DB, DTO Billing và Profile access/auth; không thay đổi OpenAPI 0.6.0 nếu chưa có đồng thuận chính thức.
