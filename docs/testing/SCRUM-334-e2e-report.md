# SCRUM-334 — Báo cáo kiểm thử giao diện toàn luồng và demo NiFi

**Dự án:** AURA — XD-PMHDT · **Jira:** [SCRUM-334](https://nguyentruonghau2202.atlassian.net/browse/SCRUM-334)
**Nhánh:** `feature/SCRUM-334-e2e-nifi-demo` (tạo từ `develop` tại `9702742`)
**Giai đoạn ghi nhận:** 30–31/10/2026
**Trạng thái:** **Báo cáo tạm thời — chưa nghiệm thu E2E toàn luồng**

## 1. Phạm vi và điều kiện nghiệm thu

- **AC1:** Web Upload → Result và Camera → NiFi → cùng UI theo dõi đều dùng API thật; Batch/Analysis ID đối chiếu backend. Fixture chỉ được dùng trong unit/component tests.
- **AC2:** Refresh, reconnect và lỗi GET không tạo upload/charge trùng; trạng thái low quality, hết phiên và sai tenant hiển thị chính xác.
- **AC3:** Không còn lỗi P0 được xác nhận sau khi kiểm thử đủ các hành trình; component tests/lint/build đạt; M1 có thể chạy lại demo.

**Lưu ý:** HTTP 202/receipt chỉ chứng minh backend tiếp nhận. Ảnh `DUPLICATE` không có nghĩa là một lượt AI mới `COMPLETED`.

## 2. Môi trường kiểm thử đã quan sát

| Thành phần | Kết quả |
|---|---|
| Frontend | Vite `http://localhost:5173/`; `VITE_ENABLE_MOCK=false` trong `.env.local` và `.env.development` |
| API Gateway | `GET http://127.0.0.1:5000/health` → HTTP 200, `Healthy` |
| Analysis API | `GET http://127.0.0.1:5001/health/ready` → HTTP 200, `{"status":"ready"}`; `Analysis__EnableHttpEndpoints=true` |
| Docker | Trước khi dừng WSL: 11 container chạy; PostgreSQL, Redis, RabbitMQ, AI Core `healthy`; Notification API hoạt động |
| Analysis Worker | Mã nguồn .NET 8 build được; không có container Worker hoặc Dockerfile trong thư mục Worker |
| RabbitMQ | `list_vhosts` chỉ có `/`; không có `/aura-s3`; `list_queues -p /` không liệt kê queue |
| NiFi | Không có container tên chứa `nifi` trên máy M5 |

## 3. Kiểm thử tự động

| ID | Hạng mục | Kết quả | Kết luận |
|---|---|---|---|
| AT01 | `npm --prefix "src/web/aura-web" run test -- src/pages/UploadPage.test.tsx src/pages/BatchProgressPage.test.tsx` | **44/44 tests**, 2/2 files PASS | PASS — component tests |
| AT02 | `npm --prefix "src/web/aura-web" run lint` | **0 warnings / 0 errors**; 81 files, 116 rules | PASS |
| AT03 | `npm --prefix "src/web/aura-web" run build` | `tsc -b && vite build` thành công; **98 modules transformed** | PASS |
| AT04 | `dotnet build .\src\services\Aura.Worker.Analysis\Aura.Worker.Analysis.csproj --nologo` | .NET 8 Worker build thành công | PASS — chỉ biên dịch |

**Sự cố môi trường:** Lint từng báo `memory allocation of 300 bytes failed`. Sau khi dừng WSL (`wsl --shutdown`) và kiểm tra `docker-desktop: Stopped`, bộ nhớ ảo trống tăng lên **24,4 GB**; lint chạy lại PASS. Không coi sự cố máy kiểm thử là bug React khi chưa có bằng chứng.

## 4. Kiểm thử thủ công Web Upload → Batch

| ID | Kịch bản | Quan sát thực tế | Đánh giá |
|---|---|---|---|
| MT01 | Đăng nhập, mở trang Upload | Trang chủ và form chọn mắt/ảnh hiển thị; ảnh mẫu `vongMac.jpg` được chọn | PASS — UI smoke |
| MT02 | Gửi ảnh bằng API thật | Backend tiếp nhận lượt gửi, trả Batch ID; ảnh được phát hiện trùng (`DUPLICATE`) | PASS — nhận diện trùng, **không phải AI COMPLETED mới** |
| MT03 | Mở trang Batch Progress | `total=1`, `active=0`, `succeeded=0`, `failed=0`, `duplicate=1` | PASS — đọc Batch |
| MT04 | Ctrl+R và xem Chrome DevTools Network | `GET /api/v1/batches/{batchId}` → **200 OK**, `Cache-Control: no-store`; trong phiên quan sát không có `POST /api/v1/analyses` | PASS — kiểm tra phía trình duyệt |
| MT05 | Đối chiếu không charge trùng | Chưa kiểm tra ledger/transaction Billing | **CHƯA XÁC NHẬN** |
| MT06 | Worker xử lý ảnh mới đến COMPLETED → Result | Không có Worker container và RabbitMQ vhost/queue phù hợp | **BLOCKED** |
| MT07 | Camera → NiFi → Batch/Result | Không có NiFi container trên máy M5 | **BLOCKED** |
| MT08 | Low quality, GET lỗi, session hết hạn, tenant khác, notification/reconnect | Chưa có minh chứng runtime cho toàn bộ ca | **CHƯA KIỂM THỬ** |

**Batch ID quan sát:** `e5fea903-9a8b-5eca-a8b3-850ca8e1fadd`
**Analysis ID được tham chiếu cho ảnh trùng:** `0b44d559-003e-5dc3-8f2c-395e0cb5db86`

Đây là các ID quan sát từ UI/API trong môi trường M5, chưa chứng minh Worker đã tạo kết quả mới. Ảnh chụp kiểm thử đã được trao đổi trong phiên làm việc nhưng **chưa được đính kèm vào file Markdown này**.

## 5. Defect list / blocker

| Mã | Ưu tiên sơ bộ | Mô tả | Trạng thái |
|---|---|---|---|
| DEF-334-01 | P2 — UI | Batch chỉ có ảnh `DUPLICATE`, `succeeded=0` nhưng nhãn tổng thể hiện **“Đã hoàn tất AI”**, dễ gây hiểu nhầm đã phân tích mới thành công. Nên hiển thị “Batch đã kết thúc — ảnh trùng được bỏ qua” hoặc phân biệt rõ `batchStatus` và số ca AI thành công. | **OPEN** — cần chỉnh UI và test |
| ENV-334-01 | Blocker | Chưa có Worker runtime; vhost `/aura-s3` và queue chưa hiện diện. `compose.workers.yaml` hiện tham chiếu Dockerfile của Analysis API, chưa xác nhận cách chạy Worker. | **OPEN** — phối hợp M1/M2 |
| ENV-334-02 | Blocker | Không có NiFi container trên môi trường M5. | **OPEN** — phối hợp M1 |
| ENV-334-03 | Cấu hình | Notification API từng liên tục khởi động lại vì mount Firebase Service Account vào đường dẫn placeholder dạng thư mục. Sau khi đặt khóa ở ngoài repo, nạp biến môi trường và recreate riêng container, API đã `Up 9 minutes`. | **FIXED LOCAL** — chưa chứng minh Inbox E2E |
| ENV-334-04 | Tài nguyên | Oxlint lỗi cấp phát bộ nhớ khi Docker/WSL chiếm RAM/bộ nhớ ảo. | **FIXED LOCAL** — lint đã PASS |

**P0:** Chưa quan sát thấy P0 trong phạm vi kiểm thử đã chạy, nhưng chưa đủ dữ liệu để tuyên bố không còn P0 cho hai hành trình E2E.

## 6. Minh chứng còn cần bổ sung

- `SCRUM-334-E01-web-receipt.png` — receipt/Batch ID của Web Upload.
- `SCRUM-334-E02-batch-duplicate.png` — trạng thái ảnh trùng và nhãn giao diện.
- `SCRUM-334-E03-refresh-network.png` — request GET 200 khi refresh, không có POST upload.
- `SCRUM-334-E04-result-completed.png` — **chưa có**; Worker trả COMPLETED mới, mask/overlay nếu có, version và warnings.
- `SCRUM-334-E05-nifi-camera-source.png` — **chưa có**; NiFi receipt, camera source/device, Batch/Analysis ID và correlation ID khớp backend.
- `SCRUM-334-E06-errors.png` — **chưa có**; low quality, 401/403/404, tenant khác, reconnect, notification.

Chỉ dùng tài khoản/ảnh mẫu được phép thử nghiệm; **không lưu token, mật khẩu, Service Account JSON, URL ký đầy đủ hoặc thông tin bệnh nhân nhạy cảm** vào ảnh/report/Git.

## 7. Hướng dẫn demo frontend sau khi M1/M2 chuẩn bị môi trường

1. Xác nhận image/commit và các service ready; `VITE_ENABLE_MOCK=false`.
2. Đăng nhập tài khoản thử nghiệm có quyền; dùng ảnh mẫu mới (chưa trùng), ghi Batch ID và Analysis ID từ backend.
3. Theo dõi GET Batch từ QUEUED/PROCESSING đến COMPLETED; phân biệt receipt và kết quả AI; mở trang Result, đối chiếu versions/warnings/ảnh với backend.
4. Refresh/reconnect trong lúc xử lý, theo dõi Network không phát sinh POST upload; kiểm tra Billing để xác nhận không charge trùng.
5. Phía M1 chạy Camera/NiFi bằng môi trường và flow đã cấp; đối chiếu device/source receipt, checksum, correlation ID, Batch/Analysis ID trên cùng UI.
6. Chạy các ca low quality, quyền, session hết hạn, notification; lưu ảnh/video và defect ID.
7. **Không tự tạo RabbitMQ vhost/queue hoặc nhập NiFi flow vào môi trường chung khi chưa được M1/M2 chấp thuận.**

## 8. Đánh giá nghiệm thu

| AC | Trạng thái | Lý do |
|---|---|---|
| AC1 | **CHƯA ĐẠT** | Web có receipt/GET thật nhưng chỉ thấy DUPLICATE; chưa có Worker COMPLETED mới hoặc NiFi Camera E2E |
| AC2 | **MỘT PHẦN** | Đã xác nhận GET 200 và không POST upload ở một lần refresh; chưa đối chiếu Billing, reconnect, quyền và các lỗi khác |
| AC3 | **MỘT PHẦN** | Tests/lint/build PASS; chưa có demo tái hiện đầy đủ, chưa đủ bằng chứng xác nhận không P0 |

**Kết luận:** Đủ dữ liệu để review hoạt động frontend và blocker/defect đã biết. **Không đánh dấu SCRUM-334 Done hoặc E2E toàn luồng PASS** trước khi bổ sung các ca còn thiếu.
