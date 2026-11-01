# AURA — KẾ HOẠCH HÀNH ĐỘNG VỀ ĐÍCH TIẾP NỐI 4 SPRINT JIRA (SPRINT 5, 6, 7 & 8)

> **Mã đề tài**: `SP26SE025` · **Repository**: `XD-PMHDT` · **Jira Project**: `SCRUM`  
> **Tính kế thừa**: Tiếp nối chính xác sau 4 Sprint trên Jira (kết thúc tại **`SCRUM-361`**).  
> **Dải task các Sprint tiếp theo**: **`SCRUM-362` đến `SCRUM-433`** (Tổng cộng 72 task chuẩn hóa).  
> **Cập nhật nhân sự**: **M3 đã rời nhóm (out nhóm)**. Toàn bộ các công việc của M3 trong Sprint 5, 6, 7, 8 được tái phân bổ tập trung cho 5 thành viên còn lại (**M1, M2, M4, M5, M6**).  
> **Nguyên tắc điều phối kỹ thuật bắt buộc**:
> 1. **Toàn bộ tác vụ liên quan đến Apache NiFi và Firebase (FCM Push, Auth, Admin policies) giao tập trung cho M1 (Nguyễn Trương Hậu)** tiếp nhận và chỉ đạo hạ tầng.
> 2. **M1 tiếp quản toàn bộ nền tảng Billing Engine, Sổ cái & Tự động Hoàn tiền** (đã hoàn thiện thành công từ Sprint 4 qua SCRUM-346, SCRUM-348, SCRUM-349).
> 3. **M2 tiếp nhận phân hệ Backend Tư vấn Trực tuyến (Consultation Chat SignalR Hub)**.
> 4. **M4 tập trung chuyên sâu đưa 2 dataset y khoa mới vào AI Core**:
>    - Huấn luyện mô hình Phân loại Bệnh Võng Mạc Đái Tháo Đường (DR Grading từ tập `datasets/HRF`).
>    - Xây dựng giải thuật Phân đoạn Động/Tĩnh mạch & Đo Tỷ Số $AVR$ (từ tập `datasets/INSPIRE_AVR`) tầm soát Tăng Huyết Áp & Nguy cơ Tim Mạch / Đột Quỵ.
> 5. **M6 tiếp quản vai trò Trưởng nhóm Kiểm thử (QA Lead)**, điều phối kiểm thử E2E và cập nhật Ma trận Truy vết Yêu cầu (Final RTM Matrix).

---

## 1. Bản Đồ Kế Thừa Jira & Dải Task Lũy Kế

```
   SPRINT 1 (Closed)          SPRINT 2 (Closed)          SPRINT 3 (Done)            SPRINT 4 (Active MVP)
  SCRUM-261..SCRUM-274      SCRUM-275..SCRUM-316       SCRUM-317..SCRUM-337       SCRUM-338..SCRUM-361
 ─────────────────────►    ─────────────────────►    ─────────────────────►    ─────────────────────────┐
                                                                                                        │
 ┌──────────────────────────────────────────────────────────────────────────────────────────────────────┘
 │
 ▼ DẢI TASK KẾ HOẠCH MỚI TIẾP NỐI TRỰC TIẾP (5 THÀNH VIÊN: M1, M2, M4, M5, M6):
 ├─► SPRINT 5 (02/11 – 08/11/2026): SCRUM-362 đến SCRUM-379 (Clinic, Batch ≥ 100, DR & AVR Training)
 ├─► SPRINT 6 (09/11 – 15/11/2026): SCRUM-380 đến SCRUM-397 (Admin Portal, Chat, Cảnh báo & Camera) [CODE FREEZE]
 ├─► SPRINT 7 (16/11 – 22/11/2026): SCRUM-398 đến SCRUM-415 (Hiệu năng, Bảo mật, Đo đạc 23 NFRs & E2E Test)
 └─► SPRINT 8 (23/11 – 29/11/2026): SCRUM-416 đến SCRUM-433 (Release Candidate, Hồ sơ Đồ án & Demo Bảo vệ)
```

### Phân Bổ Nhân Sự Chính Thức (5 Thành Viên):
* **M1 — Nguyễn Trương Hậu** (**Lead Architect / DevOps / Platform, Ingestion, Billing & Firebase Lead**):  
  * *Tiếp nhận toàn bộ mảng Apache NiFi*: Ingestion pipeline, chống trùng SHA-256 (Dedup), cách ly ảnh lỗi (Quarantine), Bounded Retry / Chaos engineering recovery, Camera Service Principal.
  * *Tiếp nhận toàn bộ mảng Firebase*: Firebase Admin SDK backend, Firebase Auth token validation, Firebase Cloud Messaging (FCM) push service & dọn dẹp token rác.
  * *Tiếp nhận toàn bộ Billing & Tài chính*: Cổng Sandbox Mock Gateway, HMAC SHA-256 signatures, Credit Ledger, Clinic Wallets, Stale Reservation Reconciliation Worker.
  * *Hạ tầng nòng cốt*: API Gateway (YARP), Cụm Prometheus & Grafana, Cụm Cache Redis, Docker Compose, Backup/Restore DR, Centralized Logging.
* **M2 — Võ Hữu Duy** (**Backend Lead / Worker, Pipeline & Consultation Backend**):  
  `AURA.Analysis.Api`, `Aura.Worker.Analysis`, Batch Engine ($\ge 100$ ảnh), Doctor Review Workspace API (Optimistic Locking), Báo cáo Y khoa PDF chuẩn QuestPDF / CSV, Phân hệ Backend Chat thời gian thực (SignalR WebSocket Hub).
* **M4 — Đào Duy Quân** (**AI Core Lead / Computer Vision & Clinical Deep Learning Specialist**):  
  `ai-core` (FastAPI), Thuật toán lọc mạch máu Frangi 2D đa tỷ lệ tối ưu, Mô hình Phân loại Bệnh Võng Mạc Đái Tháo Đường (DR Grading từ HRF), Mô hình Phân đoạn Động/Tĩnh mạch & Đo chỉ số $AVR$ (từ INSPIRE-AVR), Đóng gói Model Card v2.0 và ONNX Runtime export.
* **M5 — Lâm Trần Nguyễn Hoàng** (**Frontend Lead**):  
  `src/web/aura-web` (React 18 / Vite / Tailwind), Doctor Review Workspace UI (Side-by-side zoom/pan, thanh trượt Opacity, hiển thị gợi ý ICDR và lớp phủ Động/Tĩnh mạch A/V Map), Bulk Upload UI $\ge 100$, Giao diện Chat tư vấn SignalR Client, Clinic Admin Portal.
* **M6 — Dung Hoang** (**Security, Auth, Identity & QA Lead**):  
  `AURA.Identity.Api`, `AURA.Profile.Api`, Phân quyền Đa Tenant Clinic Lifecycle, Phân công Bác sĩ (Doctor Assignment), Phân hệ Chiến dịch Tầm soát (Campaign API/UI), Điều phối Kiểm thử Chất lượng (QA Lead), Quản lý Ma trận RTM và Báo cáo Đồ án Toàn văn.

---

## 2. SPRINT 5 — Clinic, Batch Processing $\ge 100$ Ảnh, DR & AVR Training
> **Thời gian**: 02/11/2026 – 08/11/2026  
> **Dải mã Jira**: **`SCRUM-362` đến `SCRUM-379`** (18 Tasks)  
> **Mục tiêu**: Vận hành xử lý Batch lớn $\ge 100$ ảnh qua RabbitMQ Worker song song; hoàn thiện NiFi Dedup & Quarantine; tích hợp FCM Push Backend; hoàn thiện ví Clinic & tự động hoàn tiền; huấn luyện mô hình Đái tháo đường (HRF) và phân đoạn Động/Tĩnh mạch đo $AVR$ (INSPIRE-AVR).

### 🟡 M1 — NGUYỄN TRƯƠNG HẬU (Hạ Tầng, NiFi, Firebase & Billing Platform)

#### 1. `SCRUM-362` (`W5-M1-01` · P0 · 8h · Epic E1 `SCRUM-261`)
**Cấu hình scaling Worker RabbitMQ và Quota tài nguyên hạ tầng**
* **Phải làm sao để hoàn thành task**:
  * Tạo file `compose.workers.yaml` cấu hình mở rộng 4 workers: `docker compose -f compose.yaml -f compose.workers.yaml up -d --scale analysis-worker=4`.
  * Cấu hình giới hạn CPU/RAM trong Compose: `cpus: 1.5`, `memory: 1G` cho mỗi worker.
  * Thiết lập `BasicQos(0, 1, false)` (Prefetch Count = 1) trong `Aura.Worker.Analysis/Messaging/RabbitMqConsumer.cs` để RabbitMQ phân phối đều đặn từng ảnh cho 4 worker xử lý song song, tránh tình trạng 1 worker ôm trọn cả batch.
  * Cấu hình YARP API Gateway (`appsettings.json`): Tăng `MaxRequestBodySize` lên `100MB` phục vụ đợt upload batch lớn.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Chạy lệnh scale 4 workers, cả 4 container đều kết nối vào Quorum Queue `aura.s3.analysis.jobs`.
  * **AC2**: Gửi batch 100 ảnh, đồ thị Grafana hiển thị tốc độ xử lý tăng gấp 3.5 lần so với 1 worker, không có worker nào bị crash OOM.

#### 2. `SCRUM-363` (`W5-M1-02` · P0 · 10h · Epic E9 `SCRUM-269`)
**Hoàn thiện luồng Apache NiFi: Dedup SHA-256, Quarantine cách ly ảnh lỗi và Bounded Retry**
* **Phải làm sao để hoàn thành task**:
  * Trong Process Group `AURA-S3-Reporting-Delivery-v1`, cấu hình processor `Record-Camera-Receipt-Safe-v2` (`ExecuteGroovyScript`):
    - Đọc `image_hash_sha256`, bắt lỗi `SQLSTATE 23505` (Duplicate Key) $\rightarrow$ Gán `reporting.status = HOLD` $\rightarrow$ Đẩy sang cổng `Reporting-Reconcile`.
    - Bắt lỗi mất kết nối DB $\rightarrow$ Gán `reporting.status = RETRY` $\rightarrow$ Đẩy sang `Bounded-Reporting-Retry-Delivery-v1` áp dụng Backoff (5s, 10s, 20s...).
    - Phát hiện tệp hỏng cấu trúc nhị phân $\rightarrow$ Đẩy ngay vào thư mục `/quarantine/` trong $< 1$s.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Thực hiện thành công cả 4 kịch bản theo đúng tài liệu [QUY_TRINH_DEMO_SPRINT3.md](file:///D:/XD-PMHDT/QUY_TRINH_DEMO_SPRINT3.md).
  * **AC2**: Không một FlowFile nào bị biến mất khi restart container NiFi hoặc PostgreSQL.

#### 3. `SCRUM-364` (`W5-M1-03` · P1 · 6h · Epic E7 `SCRUM-267`)
**Triển khai Dịch vụ Thông báo Đẩy Firebase Cloud Messaging (FCM Backend Service)**
* **Phải làm sao để hoàn thành task**:
  * Tích hợp `FirebaseAdmin` SDK vào `AURA.Notification.Api` bằng credentials service account `aura-dev-service-account.json`.
  * Lắng nghe sự kiện `AnalysisCompletedEvent` từ RabbitMQ exchange `aura.events`.
  * Truy vấn bảng `notification.device_registrations` lấy danh sách thiết bị của bệnh nhân/bác sĩ và gọi `FirebaseMessaging.DefaultInstance.SendMulticastAsync()`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Khi Worker hoàn tất ca phân tích, thông điệp push được chuyển giao sang Firebase FCM trong $< 200\text{ms}$.
  * **AC2**: Tự động dọn dẹp các FCM registration token không còn hợp lệ (`UNREGISTERED`) khỏi `notification.device_registrations`.

#### 4. `SCRUM-368` (`W5-M1-04` · P0 · 8h · Epic E8 `SCRUM-268`)
**Triển khai Billing cấp tổ chức (Clinic Packages & Entitlements)**
*(M1 tiếp nhận bàn giao từ M3 — hoàn thiện trên nền tảng SCRUM-348)*
* **Phải làm sao để hoàn thành task**:
  * Mở rộng bảng `billing.credit_wallets` và `billing.package_price_versions` hỗ trợ các gói cước khám lưu động (Gói Cơ bản 500 ảnh, Gói Chiến dịch 5.000 ảnh).
  * Kiểm soát chặt chẽ phân quyền: Chỉ tài khoản có vai trò `ClinicAdmin` mới có quyền xem số dư và mua gói nạp credit cho tổ chức.
  * Đảm bảo tính cô lập: Bác sĩ thuộc phòng khám A tuyệt đối không được phép trừ tiền ví của phòng khám B (`HTTP 403 Forbidden`).
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: ClinicAdmin mua gói khám tổ chức thành công, số dư ví phòng khám được cộng đúng hạn mức ngay khi Sandbox webhook xác nhận.
  * **AC2**: Ngăn chặn hoàn toàn việc trừ credit chéo giữa các Clinic.

#### 5. `SCRUM-369` (`W5-M1-05` · P0 · 10h · Epic E8 `SCRUM-268`)
**Triển khai Cơ chế Đối soát & Tự Động Hoàn Credit Batch (Idempotent Batch Refund)**
*(M1 tiếp nhận bàn giao từ M3 — hoàn thiện trên nền tảng SCRUM-348)*
* **Phải làm sao để hoàn thành task**:
  * Hoàn thiện quy trình giao dịch 3 bước an toàn cho Batch lớn $\ge 100$ ảnh:
    1. *Reserve*: Đặt trước số credit tương ứng khi tiếp nhận batch.
    2. *Capture*: Khấu trừ dần credit từng ảnh khi Worker phân tích thành công.
    3. *Release*: Tự động giải phóng và hoàn trả credit của các ảnh bị lỗi cục bộ (Partial Failure) hoặc ảnh chất lượng kém (`LOW_QUALITY`).
  * Đảm bảo tính Idempotent: API Batch Partial Refund `POST /internal/v1/billing/reservations/batch-refund` dù gọi lặp lại cũng chỉ hoàn tiền đúng 1 lần duy nhất.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Batch 100 ảnh có 5 ảnh lỗi, hệ thống chỉ capture 95 credits và tự động hoàn trả đúng 5 credits về ví Clinic.
  * **AC2**: Sổ cái `billing.credit_ledger_entries` ghi nhận đủ các dòng `RESERVE`, `CAPTURE`, `RELEASE` khớp tuyệt đối với số dư.

---

### 🟡 M2 — VÕ HỮU DUY (Batch Processing Engine, Báo Cáo Y Khoa & Đối Soát Batch)

#### 1. `SCRUM-365` (`W5-M2-01` · P0 · 8h · Epic E4 `SCRUM-264`)
**Triển khai Batch Processing Engine ($\ge 100$ ảnh) & Cơ chế Resume khi ngắt quãng**
* **Phải làm sao để hoàn thành task**:
  * Tạo endpoint `POST /api/v1/analyses/batches` trong `AURA.Analysis.Api` tiếp nhận danh sách metadata của $\ge 100$ ảnh.
  * Tạo bảng `analysis.batches` (`id`, `clinic_id`, `campaign_id`, `total_items`, `completed_items`, `status`).
  * Lưu trạng thái từng ảnh vào Redis Hash: `HSET batch:{batchId}:status {imageId} "COMPLETED"`.
  * Xử lý lỗi cục bộ (Partial Failure): Một ảnh lỗi không làm dừng batch; các ảnh còn lại vẫn tiếp tục phân tích bình thường.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Tiếp nhận batch 100 ảnh, trả về mã tiếp nhận `batch_id` kèm `HTTP 202 Accepted` trong $< 1.5\text{s}$.
  * **AC2**: API `GET /api/v1/analyses/batches/{id}` phản ánh chính xác số ảnh đã xử lý xong theo thời gian thực (đọc từ Redis cache với TTL 1 giờ).

#### 2. `SCRUM-366` (`W5-M2-02` · P0 · 10h · Epic E4 `SCRUM-264`)
**Triển khai Động cơ Kết xuất Báo cáo Y khoa PDF chuẩn A4 (QuestPDF) & CSV**
* **Phải làm sao để hoàn thành task**:
  * Sử dụng thư viện `QuestPDF` trên .NET 10 render báo cáo bệnh án chuẩn y tế:
    - Trang 1: Thông tin Clinic, Bác sĩ, Bệnh nhân, Ngày khám.
    - Ảnh màu đáy mắt gốc đặt song song với Ảnh mặt nạ mạch máu, bảng chỉ số định lượng (`vessel_density`, `tortuosity_index`, `av_ratio`).
    - Kết luận chẩn đoán lâm sàng của bác sĩ (Doctor Revision note) và cấp độ ICDR.
  * Xuất báo cáo tổng kết Campaign dạng CSV: Chống lỗ hổng CSV Formula Injection (escape các ký tự `=`, `+`, `-`, `@`).
  * Lưu PDF vào Azurite/Azure Blob Storage; sinh link tải Presigned URL có thời hạn 15 phút.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Tệp PDF xuất ra chuẩn y tế A4, dung lượng nén $< 1.2\text{MB}$, hình ảnh mạch máu rõ nét.
  * **AC2**: Link tải hết hạn sau 15 phút, truy cập trực tiếp blob bị chặn `HTTP 403`.

#### 3. `SCRUM-367` (`W5-M2-03` · P1 · 6h · Epic E13 `SCRUM-273`)
**Kiểm thử Batch/Report, liên kết Campaign và cập nhật Hợp đồng API**
* **Phải làm sao để hoàn thành task**:
  * Viết integration test `BatchProcessingTests.cs` kiểm tra luồng gửi batch 100 ảnh.
  * Phối hợp với M6 để kiểm tra việc liên kết batch ảnh vào đúng `campaign_id` của Clinic.
  * Cập nhật tài liệu OpenAPI `contracts/analysis-openapi.yaml` bổ sung schema `BatchResponse`, `MedicalReportExportRequest`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: 100% test cases của phân hệ Batch và Report pass.
  * **AC2**: Hợp đồng OpenAPI đồng bộ tuyệt đối với mã nguồn Controller.

#### 4. `SCRUM-370` (`W5-M2-04` · P1 · 6h · Epic E13 `SCRUM-273`)
**Kiểm thử Tích hợp Batch-Credit và Hoàn thiện Tài liệu Đối Soát Tài Chính**
*(M2 tiếp nhận bàn giao từ M3 — phối hợp chặt chẽ với M1)*
* **Phải làm sao để hoàn thành task**:
  * Viết integration test kiểm tra luồng kết hợp giữa Batch Processing của M2 và Billing Reservation của M1: Khi batch 100 ảnh được gửi, Worker gọi reserve, capture và batch-refund tự động.
  * Xác nhận khóa bi quan PostgreSQL `SELECT ... FOR UPDATE` trên ví clinic ngăn chặn hoàn toàn tranh chấp số dư khi 4 workers cùng cập nhật.
  * Hoàn thiện tài liệu `docs/billing/clinic_batch_billing_reconciliation.md`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Không xảy ra trường hợp số dư ví bị âm khi chạy batch lớn có partial failure.
  * **AC2**: Đối soát khớp 100% giữa số lượng ảnh phân tích thành công và số credit bị trừ.

---

### 🟡 M4 — ĐÀO DUY QUÂN (AI Core: Phân Vùng Mạch, Đái Tháo Đường HRF & $AVR$ INSPIRE)

#### 1. `SCRUM-371` (`W5-M4-01` · P0 · 8h · Epic E5 `SCRUM-265`)
**Tối ưu hóa Phân vùng Mạch máu Frangi 2D v0.2 & Đo kiểm Benchmark trên Tập HRF**
* **Phải làm sao để hoàn thành task**:
  * Tối ưu hóa pipeline tiền xử lý ảnh trong `src/ai-core/app/services/image_preprocessor.py`:
    - Áp dụng thuật toán cân bằng độ tương phản cục bộ thích ứng CLAHE (`clipLimit=2.0`, `tileGridSize=(8,8)`).
    - Đo kiểm độc lập độ chính xác thuật toán Frangi 2D vector hóa trên tập ảnh siêu phân giải **HRF ($3504 \times 2336$ pixel)** tại `datasets/HRF/images` so sánh với mask chuẩn `datasets/HRF/manual1`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Ảnh đáy mắt kích thước $3504 \times 2336$ được phân vùng rõ nét, phân biệt rõ mạch máu lớn và vi mạch nhỏ.
  * **AC2**: Thời gian phân vùng tối ưu hóa đạt $< 1.5\text{s}$/ảnh trên CPU nhờ kỹ thuật tích chập 6-pass tách chiều.

#### 2. `SCRUM-372` (`W5-M4-02` · P0 · 10h · Epic E5 `SCRUM-265`)
**Huấn luyện Mô hình Phân loại Bệnh Võng Mạc Đái Tháo Đường (DR Grading) trên Tập HRF**
*(Phát triển tính năng nhận diện bệnh lý theo đúng mong đợi đề tài)*
* **Phải làm sao để hoàn thành task**:
  * Khai thác tập dữ liệu `datasets/HRF`: 15 ca Bệnh võng mạc đái tháo đường (`dr`) vs 15 ca Mắt khỏe mạnh (`h`) vs 15 ca Glôcôm (`g`).
  * Áp dụng Transfer Learning (ResNet-34 / EfficientNet tiền huấn luyện trên ảnh y khoa) kết hợp Data Augmentation để phân loại nguy cơ Đái tháo đường.
  * Tích hợp trích xuất bản đồ nhiệt giải thích **Grad-CAM (Explainable AI — NFR-22)** để làm nổi bật các tổn thương vi phình mạch và xuất huyết.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Mô hình phân loại chính xác giữa ca chụp đái tháo đường (`dr`) và ca chụp bình thường (`h`) với AUC-ROC $\ge 0.88$.
  * **AC2**: Sinh ra ảnh bản đồ nhiệt Grad-CAM khớp tọa độ tổn thương phục vụ hiển thị trên Doctor Workspace.

#### 3. `SCRUM-373` (`W5-M4-03` · P1 · 6h · Epic E5 `SCRUM-265`)
**Xây dựng Pipeline Phân đoạn Động mạch / Tĩnh mạch & Đo Tỷ Số $AVR$ trên Tập INSPIRE-AVR**
*(Tầm soát Tăng Huyết Áp & Nguy cơ Tim Mạch / Đột Quỵ)*
* **Phải làm sao để hoàn thành task**:
  * Khai thác 100 ca chụp chuẩn y khoa tại `datasets/INSPIRE_AVR`: sử dụng cặp mặt nạ Động mạch (`--arteries.jpg`) và Tĩnh mạch (`--veins.jpg`).
  * Xây dựng thuật toán phân loại pixel Động mạch vs Tĩnh mạch (A/V Classifier) và đo khẩu kính mạch máu quanh đĩa thị.
  * Hiện thực hóa công thức tính **Tỷ số Động/Tĩnh mạch ($AVR$)**: $AVR = \text{CRAE} / \text{CRVE}$. Tự động kích hoạt cảnh báo nguy cơ Tăng huyết áp & Tim mạch khi $AVR < 0.60$.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Thuật toán trích xuất bản đồ phân màu Động mạch (Đỏ) và Tĩnh mạch (Xanh) trên ảnh test của INSPIRE-AVR.
  * **AC2**: Giá trị $AVR$ tính toán nằm trong khoảng sinh học hợp lý $[0.40, 0.85]$, chấm dứt hoàn toàn việc trả về giá trị `null` hoặc mock.

---

### 🟡 M5 — LÂM TRẦN NGUYỄN HOÀNG (Frontend Lead / Web UI)

#### 1. `SCRUM-374` (`W5-M5-01` · P0 · 8h · Epic E12 `SCRUM-272`)
**Xây dựng Giao diện Tải ảnh Hàng loạt (Bulk Upload UI $\ge 100$ ảnh)**
* **Phải làm sao để hoàn thành task**:
  * Nâng cấp trang Upload hỗ trợ kéo thả (Drag & Drop) cùng lúc lên đến 100 ảnh võng mạc.
  * Kiểm tra hợp lệ phía Client: Định dạng `.jpg`/`.png`, dung lượng mỗi ảnh $\le 10\text{MB}$, kích thước tối đa 16 triệu pixel.
  * Hiển thị danh sách thumbnail ảnh kèm lựa chọn mắt chụp (`Mắt trái`, `Mắt phải`) cho từng ảnh hoặc áp dụng hàng loạt.
  * Chống gửi lặp: Vô hiệu hóa nút bấm và hiển thị thanh tiến trình tải lên tổng thể.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Kéo thả 50 ảnh cùng lúc, giao diện xử lý mượt mà, hiển thị đầy đủ danh sách xem trước không bị lag.
  * **AC2**: Bấm gửi batch, gọi đúng endpoint `POST /api/v1/analyses/batches` và điều hướng sang trang theo dõi tiến độ.

#### 2. `SCRUM-375` (`W5-M5-02` · P0 · 10h · Epic E12 `SCRUM-272`)
**Xây dựng Giao diện Theo dõi Tiến độ Batch & Nút Tải Báo cáo PDF/CSV**
* **Phải làm sao để hoàn thành task**:
  * Tạo trang `BatchDetailPage.tsx`: Hiển thị thanh tiến trình tổng (ví dụ: `Đã hoàn thành 68/100 ảnh`), danh sách chi tiết trạng thái từng ảnh (`Đang chờ`, `Đang phân tích`, `Thành công`, `Lỗi`).
  * Tích hợp nút bấm "Tải Báo Cáo PDF" cho từng ca bệnh và nút "Tải Tất Cả (ZIP)" cho toàn bộ batch.
  * Hỗ trợ nút "Thử lại các ảnh lỗi" cho phép gửi lại riêng các ca bị partial failure.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Tiến trình cập nhật tự động mượt mà khi nhận được sự kiện SignalR/WebSocket từ Backend.
  * **AC2**: Bấm nút tải PDF tải được đúng file báo cáo chuẩn của bệnh nhân tương ứng.

#### 3. `SCRUM-376` (`W5-M5-03` · P1 · 6h · Epic E13 `SCRUM-273`)
**Kiểm thử Giao diện Batch Frontend và Xử lý Mất Mạng Ngoại Lệ**
* **Phải làm sao để hoàn thành task**:
  * Viết component test với React Testing Library cho form Bulk Upload và thanh tiến trình Batch.
  * Kiểm tra các trường hợp ngoại lệ: Mất mạng giữa chừng khi đang upload, người dùng F5 tải lại trang khi batch đang chạy.
  * Cập nhật tài liệu hướng dẫn sử dụng giao diện trong `docs/ux/user_guide_batch_upload.md`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Khi F5 lại trang, giao diện vẫn khôi phục đúng tiến độ batch từ URL `batchId` nhờ API `GET`.
  * **AC2**: 100% component tests của module Upload pass.

---

### 🟡 M6 — DUNG HOANG (Identity, Clinic & Phân Hệ Campaign)

#### 1. `SCRUM-377` (`W5-M6-01` · P0 · 8h · Epic E12 `SCRUM-272`)
**Xây dựng Giao diện Quản lý Thành viên Bác sĩ & Bệnh nhân Clinic**
* **Phải làm sao để hoàn thành task**:
  * Xây dựng màn hình `ClinicMemberPage.tsx` và `ClinicPatientPage.tsx`.
  * Cho phép ClinicAdmin mời bác sĩ vào tổ chức, gán vai trò (`Doctor`, `Staff`), kích hoạt hoặc thu hồi quyền truy cập.
  * Quản lý danh bạ bệnh nhân thuộc sở hữu của Clinic, liên kết lịch sử các đợt khám.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Thêm mới bác sĩ thành công, bác sĩ đăng nhập bằng Google nhận đúng ngữ cảnh Clinic.
  * **AC2**: Bác sĩ chỉ nhìn thấy danh sách bệnh nhân thuộc phòng khám của mình, tuyệt đối không thấy bệnh nhân của clinic khác.

#### 2. `SCRUM-378` (`W5-M6-02` · P0 · 10h · Epic E3 `SCRUM-263`)
**Triển khai Phân hệ Chiến dịch Tầm soát (Campaign API & UI trong Analysis)**
* **Phải làm sao để hoàn thành task**:
  * Hiện thực module Campaign:
    - Bảng `analysis.campaigns` (`id`, `clinic_id`, `name`, `target_patient_count`, `start_date`, `end_date`, `status`).
    - API: `POST /api/v1/campaigns`, `GET /api/v1/campaigns/{id}`, `POST /api/v1/campaigns/{id}/batches`.
  * Xây dựng giao diện tạo và quản lý chiến dịch tầm soát lưu động cho Clinic.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Quản trị viên Clinic tạo chiến dịch khám mắt cộng đồng, gán các batch chụp ảnh vào chiến dịch đó thành công.
  * **AC2**: Dữ liệu chiến dịch hoàn toàn cô lập theo `clinic_id`, không tạo database riêng làm phân mảnh hệ thống.

#### 3. `SCRUM-379` (`W5-M6-03` · P1 · 6h · Epic E13 `SCRUM-273`)
**Kiểm thử Campaign Đa Tenant và Cập nhật Tài liệu Nghiệp vụ**
* **Phải làm sao để hoàn thành task**:
  * Viết test kiểm tra phân quyền đa tenant: Clinic B cố tình truy cập hoặc thêm batch vào Campaign của Clinic A sẽ nhận ngay lỗi `HTTP 403 Forbidden`.
  * Phối hợp với M2 kiểm tra tính năng xuất báo cáo tổng kết theo Campaign.
  * Cập nhật tài liệu use case và sequence diagram trong `docs/design/campaign_management_sequence.md`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Kiểm thử 10 ca gọi chéo tenant đều bị chặn an toàn.
  * **AC2**: Tài liệu thiết kế mô tả rõ ràng mối quan hệ giữa Clinic, Doctor, Campaign, Batch và Patient.

---

## 3. SPRINT 6 — Admin Portal, Consultation/Chat, Cảnh Báo & Camera Ingestion
> **Thời gian**: 09/11/2026 – 15/11/2026  
> **Dải mã Jira**: **`SCRUM-380` đến `SCRUM-397`** (18 Tasks)  
> **Mục tiêu**: Admin quản trị toàn hệ thống; Bệnh nhân và Bác sĩ trò chuyện/tư vấn lâm sàng trực tuyến qua SignalR; Clinic có dashboard cảnh báo; Camera ingestion vận hành theo chuẩn định danh bảo mật; Tích hợp trọn vẹn mô hình DR & $AVR$ vào Doctor Workspace. **Tiến hành Code Freeze vào cuối tuần**.

### 🟡 M1 — NGUYỄN TRƯƠNG HẬU (Audit, Camera NiFi & Quản Trị FCM/Billing)

#### 1. `SCRUM-380` (`W6-M1-01` · P0 · 8h · Epic E11 `SCRUM-271`)
**Triển khai Nền tảng Audit Log Tập trung & Bảo vệ Dữ liệu Riêng tư (Privacy)**
* **Phải làm sao để hoàn thành task**:
  * Thiết lập bảng lưu vết kiểm toán tập trung `profile.audit_events` (Append-Only) ghi nhận mọi hành vi truy cập dữ liệu nhạy cảm.
  * Cấu hình cơ chế Redaction trong Serilog: Tự động che dấu (Masking) số CCCD/BHYT, số điện thoại và token (ví dụ: `090****123`).
  * Thiết lập chính sách lưu trữ (Data Retention Policy): Tự động lưu trữ lịch sử kiểm toán trong 180 ngày.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Bác sĩ mở xem ảnh bệnh nhân hoặc tải PDF, hệ thống ghi nhận ngay 1 bản ghi audit log kèm IP, UserID và Correlation ID.
  * **AC2**: File log hệ thống tuyệt đối không chứa token JWT hay dữ liệu thô nhạy cảm.

#### 2. `SCRUM-381` (`W6-M1-02` · P0 · 10h · Epic E9 `SCRUM-269`)
**Hoàn thiện Cấu hình Camera Flow & Service Principal Authentication (NiFi Ingestion)**
* **Phải làm sao để hoàn thành task**:
  * Cấu hình Service Principal có thời hạn (Client Credentials) cho từng thiết bị Camera hoặc trạm thu thập NiFi.
  * Processor `InvokeHTTP` trên NiFi tự động lấy token xác thực từ Identity API trước khi đẩy gói tin tiếp nhận sang Gateway.
  * Truyền đầy đủ ngữ cảnh `X-Clinic-ID` và `X-Camera-ID` trong header HTTP để hệ thống nhận diện nguồn tiếp nhận.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: NiFi gửi ảnh qua Gateway vào Analysis API thành công với mã `HTTP 202 Accepted` có kèm Service Token hợp lệ.
  * **AC2**: Khi thu hồi Service Principal của một camera bị mất cắp, NiFi lập tức bị chặn không thể gửi ảnh vào hệ thống (`HTTP 401`).

#### 3. `SCRUM-382` (`W6-M1-03` · P1 · 6h · Epic E11 `SCRUM-271`)
**Kiểm thử Quản trị Hạ tầng, TLS và Chốt Tài liệu ADR-005 Camera Principal**
* **Phải làm sao để hoàn thành task**:
  * Kiểm tra toàn bộ chứng chỉ SSL/TLS cục bộ trên các cổng HTTPS (Gateway 8443, NiFi 8543).
  * Viết bài thử nghiệm ngắt kết nối Camera và kiểm tra khả năng lưu đệm tại chỗ của NiFi trước khi đẩy bù khi có mạng lại.
  * Cập nhật quyết định kiến trúc `docs/adr/ADR-005-camera-service-principal.md`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Báo cáo kiểm thử hạ tầng xác nhận tính toàn vẹn và bảo mật của luồng camera.
  * **AC2**: Tài liệu ADR giải trình đầy đủ lý do kiến trúc và phương án quản lý khóa bí mật.

#### 4. `SCRUM-387` (`W6-M1-04` · P0 · 10h · Epic E8 `SCRUM-268`)
**Triển khai Cổng Quản trị Gói Cước & Chính Sách Thông Báo Firebase FCM (Admin Billing & FCM)**
*(M1 tiếp nhận bàn giao từ M3 — quản lý trọn vẹn Firebase & Billing)*
* **Phải làm sao để hoàn thành task**:
  * Cung cấp API cho SuperAdmin quản lý danh mục gói cước: Thêm mới gói, điều chỉnh giá, tạm ngừng gói cũ với versioning giá an toàn.
  * Quản trị Template thông báo Firebase FCM: Soạn thảo mẫu thông báo cho từng sự kiện (`ANALYSIS_READY`, `HIGH_RISK_WARNING`, `CREDIT_LOW`).
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Đổi giá gói cước mới không làm ảnh hưởng đến các giao dịch đã thanh toán trước đó trong lịch sử.
  * **AC2**: Template thông báo FCM hỗ trợ chèn biến động (`{{patient_name}}`, `{{analysis_id}}`) chính xác.

---

### 🟡 M2 — VÕ HỮU DUY (Thống Kê Y Tế, Alert Rules & Backend Chat SignalR)

#### 1. `SCRUM-383` (`W6-M2-01` · P0 · 8h · Epic E10 `SCRUM-270`)
**Triển khai API Thống kê Y tế & Hoạt động Clinic/Admin**
* **Phải làm sao để hoàn thành task**:
  * Xây dựng controller `AnalyticsController.cs` cung cấp số liệu thống kê chuẩn:
    - Tổng số ảnh đã chụp, tỷ lệ xử lý thành công vs lỗi.
    - Phân bố mức độ nguy cơ tổn thương mạch máu võng mạc (Tỷ lệ Low, Moderate, High) và phân bố cấp độ đái tháo đường ICDR.
    - Năng suất duyệt ca của từng bác sĩ trong Clinic.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: API trả về dữ liệu tổng hợp chính xác theo `clinic_id`, thời gian phản hồi $< 200\text{ms}$ nhờ tận dụng Redis Cache.
  * **AC2**: Quản trị viên SuperAdmin có endpoint riêng để xem thống kê tổng thể toàn hệ thống.

#### 2. `SCRUM-384` (`W6-M2-02` · P0 · 10h · Epic E10 `SCRUM-270`)
**Triển khai Động cơ Cảnh báo & Xuất Dữ liệu Nghiên cứu Ẩn danh (Research Export)**
* **Phải làm sao để hoàn thành task**:
  * Thiết lập Alert Rules nghiệp vụ: Tự động phát cảnh báo khi một ca chụp có chỉ số $AVR < 0.50$ hoặc cấp độ đái tháo đường mức Severe/Proliferative.
  * Xây dựng tính năng Research Export: Xuất tập dữ liệu ẩn danh (Anonymized Dataset) gồm ảnh mask và các thông số đo đạc phục vụ nghiên cứu khoa học; tự động loại bỏ toàn bộ PHI.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Ca chụp nguy cơ cao được gắn nhãn `URGENT` và phát cảnh báo tức thời.
  * **AC2**: Tệp dữ liệu xuất ra đạt chuẩn ẩn danh HIPAA/GDPR.

#### 3. `SCRUM-385` (`W6-M2-03` · P1 · 6h · Epic E13 `SCRUM-273`)
**Kiểm thử Thống kê, Đối soát Dữ liệu và Cập nhật Tài liệu Công thức Dịch tễ**
* **Phải làm sao để hoàn thành task**:
  * Viết test kiểm tra thuật toán thống kê: Đảm bảo các ca chụp trùng lặp hoặc ảnh lỗi không bị đếm vào tổng số ca khám thành công.
  * Cập nhật tài liệu công thức tính toán dịch tễ trong `docs/analytics/epidemiological_metrics_spec.md`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: 100% ca kiểm thử số liệu thống kê đạt kết quả chính xác tuyệt đối.
  * **AC2**: Tài liệu công thức trình bày rõ ràng cơ sở tính toán tỷ lệ bệnh học.

#### 4. `SCRUM-386` (`W6-M2-04` · P0 · 8h · Epic E7 `SCRUM-267`)
**Triển khai Backend Tư vấn Trực tuyến (Consultation/Chat Service qua SignalR Hub)**
*(M2 tiếp nhận bàn giao từ M3 — triển khai Hub thời gian thực)*
* **Phải làm sao để hoàn thành task**:
  * Xây dựng SignalR Hub `ConsultationChatHub.cs`:
    - Bảng `notification.conversations` (`id`, `patient_id`, `doctor_id`, `analysis_id`, `status`).
    - Bảng `notification.messages` (`id`, `conversation_id`, `sender_id`, `content`, `sent_at`, `is_read`).
  * Quản lý phòng chat theo `conversation_id`: Chỉ Bệnh nhân và Bác sĩ được phân công mới được kết nối vào phòng.
  * Lưu trữ tin nhắn bền vững trong PostgreSQL, hỗ trợ phân trang (20 tin gần nhất).
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Bác sĩ và Bệnh nhân trao đổi tin nhắn thời gian thực qua WebSocket với độ trễ $< 50\text{ms}$.
  * **AC2**: Người ngoài cố tình kết nối trái phép bị chặn an toàn với `HTTP 403 Forbidden`.

---

### 🟡 M4 — ĐÀO DUY QUÂN (Tích Hợp AI Core REST API & Đóng Gói Model Card v2.0)

#### 1. `SCRUM-389` (`W6-M4-01` · P0 · 8h · Epic E5 `SCRUM-265`)
**Tích hợp Mô hình Đái Tháo Đường (HRF) & Đo Chỉ Số $AVR$ (INSPIRE-AVR) vào AI Core API**
* **Phải làm sao để hoàn thành task**:
  * Nâng cấp endpoint `POST /api/v1/analyze` trong FastAPI trả về đầy đủ các trường y sinh học thực nghiệm:
    - `vessel_segmentation`: Mask và Overlay mạch máu Frangi.
    - `diabetic_retinopathy`: Nhãn dự đoán nguy cơ Đái tháo đường, điểm số xác suất và đường dẫn ảnh nhiệt Grad-CAM.
    - `artery_vein_analysis`: Tỷ số đường kính Động/Tĩnh mạch `av_ratio` ($AVR$) và bản đồ phân màu Động mạch (Đỏ) - Tĩnh mạch (Xanh).
    - `risk_assessment`: Tích hợp đa yếu tố (Tortuosity + Density + $AVR$) xếp tầng `LOW / MODERATE / HIGH`.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Endpoint tiếp nhận ảnh Fundus, xử lý đồng thời cả phân vùng mạch, đánh giá DR và đo $AVR$ trong thời gian $< 2.0\text{s}$ trên CPU.
  * **AC2**: Phản hồi JSON tuân thủ chuẩn xác hợp đồng OpenAPI `contracts/ai-openapi-1.3.yaml`.

#### 2. `SCRUM-390` (`W6-M4-02` · P0 · 10h · Epic E5 `SCRUM-265`)
**Triển khai Cơ chế Tải lại Cấu hình AI Động (Dynamic Config & Model Reload)**
* **Phải làm sao để hoàn thành task**:
  * Cung cấp endpoint quản trị `/api/v1/admin/config/reload` cho phép cập nhật ngưỡng nhị phân hóa, trọng số rủi ro hoặc thay thế file weights `.onnx` mà không cần restart container.
  * Cơ chế Graceful Reload: Các job đang suy luận dở dang không bị ngắt quãng.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Cập nhật ngưỡng lọc thành công qua API, các ảnh tiếp theo được áp dụng ngay ngưỡng mới.
  * **AC2**: Không phát sinh lỗi 5xx trong lúc reload cấu hình.

#### 3. `SCRUM-391` (`W6-M4-03` · P1 · 6h · Epic E13 `SCRUM-273`)
**Đóng Gói Model Card v2.0 Toàn Diện (Benchmark CHASE_DB1, HRF & INSPIRE-AVR)**
* **Phải làm sao để hoàn thành task**:
  * Biên soạn tài liệu `docs/data_cards/model_card_v2.0.md` theo chuẩn Google Model Card Toolkit.
  * Trình bày chi tiết bảng kết quả thực nghiệm trên cả 3 tập dữ liệu:
    - `CHASE_DB1`: Độ chính xác phân vùng vi mạch (Dice, IoU, Sensitivity, Specificity).
    - `HRF`: Độ chính xác phân loại Đái tháo đường (AUC-ROC, Accuracy).
    - `INSPIRE_AVR`: Sai số đo tỷ số $AVR$ so với nhãn chuyên gia Iowa.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Bảng số liệu khoa học đầy đủ, có thể tái lập 100% bằng script benchmark.
  * **AC2**: Nêu rõ ranh giới y đức: AI là công cụ hỗ trợ sàng lọc (CDS), không thay thế bác sĩ.

---

### 🟡 M5 — LÂM TRẦN NGUYỄN HOÀNG (Frontend Lead / Web UI)

#### 1. `SCRUM-392` (`W6-M5-01` · P0 · 8h · Epic E12 `SCRUM-272`)
**Xây dựng Giao diện Quản trị Hệ thống (Admin Portal UI)**
* **Phải làm sao để hoàn thành task**:
  * Xây dựng layout Admin chuyên biệt cho SuperAdmin:
    - Quản lý danh mục Gói cước (Package Management): Form tạo/sửa giá và trạng thái gói.
    - Quản lý Cấu hình AI (AI Threshold Tuner): Thanh trượt trực quan điều chỉnh tham số Frangi.
    - Quản lý Mẫu thông báo (Notification Templates): Trình soạn thảo mẫu thông báo FCM.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Chỉ tài khoản có vai trò `SuperAdmin` mới truy cập được các trang quản trị này.
  * **AC2**: Thao tác cập nhật giá gói cước và tham số AI phản hồi thành công và cập nhật ngay lên giao diện.

#### 2. `SCRUM-393` (`W6-M5-02` · P0 · 10h · Epic E12 `SCRUM-272`)
**Xây dựng Giao diện Chat Tư Vấn Bệnh Nhân – Bác Sĩ (SignalR Client)**
* **Phải làm sao để hoàn thành task**:
  * Tạo component `ConsultationChatBox.tsx`:
    - Khung chat trực quan: Danh sách tin nhắn, bong bóng chat phân biệt Bác sĩ và Bệnh nhân, trạng thái Đã gửi / Đã xem.
    - Tự động cuộn xuống tin nhắn mới nhất, hỗ trợ đính kèm liên kết nhanh đến Bệnh án phân tích tương ứng.
    - Kết nối SignalR Client tự động kết nối lại khi mất mạng (Auto-reconnect).
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Trò chuyện mượt mà giữa 2 cửa sổ trình duyệt (Bác sĩ và Bệnh nhân), tin nhắn hiển thị tức thì không cần reload.
  * **AC2**: Hiển thị thông báo badge số tin nhắn chưa đọc trên thanh menu điều hướng.

#### 3. `SCRUM-394` (`W6-M5-03` · P1 · 6h · Epic E13 `SCRUM-273`)
**Nâng Cấp Doctor Workspace UI: Hiển Thị Layer Động/Tĩnh Mạch A/V & Gợi Ý Cấp Độ ICDR**
*(Cải tiến giao diện Bác sĩ hiển thị đúng kết quả y khoa mong đợi)*
* **Phải làm sao để hoàn thành task**:
  * Trong `DoctorWorkspace.tsx`:
    - Bổ sung công tắc chuyển đổi lớp phủ: Xem ảnh gốc $\leftrightarrow$ Lớp phủ vi mạch Frangi $\leftrightarrow$ Lớp phủ Động mạch/Tĩnh mạch (A/V Map) $\leftrightarrow$ Bản đồ nhiệt Grad-CAM.
    - Hiển thị bảng chỉ số sinh học: $AVR$, Mật độ vi mạch, Độ cong queo mạch.
    - Form chẩn đoán tự động điền gợi ý cấp độ ICDR do AI đề xuất; Bác sĩ có thể bấm xác nhận hoặc điều chỉnh lại.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Bác sĩ chuyển đổi mượt mà giữa các lớp hiển thị ảnh y khoa với tốc độ 60fps.
  * **AC2**: Bác sĩ duyệt ca bệnh thành công, lưu nhận định vào cơ sở dữ liệu.

---

### 🟡 M6 — DUNG HOANG (Admin Identity, Phê Duyệt Clinic & Code Freeze)

#### 1. `SCRUM-395` (`W6-M6-01` · P0 · 8h · Epic E10 `SCRUM-270`)
**Triển khai API Quản trị Tài khoản & Phê duyệt Clinic (Admin API)**
* **Phải làm sao để hoàn thành task**:
  * Cung cấp các endpoint quản trị tối cao trong `AURA.Identity.Api` & `AURA.Profile.Api`:
    - Phê duyệt / Từ chối hồ sơ đăng ký mở phòng khám (`POST /api/v1/admin/clinics/{id}/approve`).
    - Khóa / Mở khóa tài khoản người dùng vi phạm chính sách (`POST /api/v1/admin/users/{id}/suspend`).
    - Phân bổ vai trò đặc quyền (Role Management) có ghi nhật ký kiểm toán bắt buộc.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Khóa tài khoản thành công, người dùng bị văng khỏi phiên làm việc ngay trong lần gọi API tiếp theo.
  * **AC2**: Chống việc SuperAdmin tự thu hồi quyền của chính mình hoặc tự xóa tài khoản gốc.

#### 2. `SCRUM-396` (`W6-M6-02` · P0 · 10h · Epic E12 `SCRUM-272`)
**Xây dựng Giao diện Phê duyệt Clinic & Quản trị Danh tính**
* **Phải làm sao để hoàn thành task**:
  * Xây dựng trang `AdminClinicApprovalPage.tsx` và `AdminUserManagementPage.tsx`:
    - Bảng danh sách các Clinic đang chờ phê duyệt (kèm giấy phép kinh doanh, thông tin người đại diện).
    - Nút bấm "Phê duyệt" hoặc "Từ chối" (yêu cầu nhập lý do từ chối gửi email tự động).
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Clinic được phê duyệt chuyển trạng thái `ACTIVE` ngay lập tức và bắt đầu được mua credit/khám bệnh.
  * **AC2**: Danh sách người dùng hỗ trợ tìm kiếm theo Email, UID Firebase hoặc Tên hiển thị.

#### 3. `SCRUM-388` (`W6-M6-04` · P1 · 6h · Epic E13 `SCRUM-273`)
**Kiểm thử Tích hợp Chat, Alert & Quản trị Thanh toán**
*(M6 tiếp nhận bàn giao từ M3 — kiểm thử tích hợp chéo)*
* **Phải làm sao để hoàn thành task**:
  * Viết integration test `ChatAuthorizationTests.cs` kiểm tra việc thu hồi quyền: Khi bác sĩ bị xóa khỏi Clinic, bác sĩ đó tự động mất quyền gửi tin nhắn trong các cuộc chat cũ.
  * Kiểm tra fallback: Khi bệnh nhân offline, gửi tin nhắn mới kích hoạt FCM Web Push qua dịch vụ của M1.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Tin nhắn gửi khi offline kích hoạt thông báo FCM thành công.
  * **AC2**: Bác sĩ bị thu hồi quyền lập tức bị ngắt kết nối WebSocket và không thể đọc tin nhắn mới.

#### 4. `SCRUM-397` (`W6-M6-03` · P1 · 6h · Epic E11 `SCRUM-271`)
**Rà soát Phân quyền An toàn & Tiến hành CODE FREEZE Toàn Hệ Thống**
* **Phải làm sao để hoàn thành task**:
  * Rà soát toàn bộ hệ thống API Gateway, đảm bảo 100% endpoint nghiệp vụ đều được kiểm soát bởi phân quyền chặt chẽ.
  * **TIẾN HÀNH CODE FREEZE (KHÓA MÃ NGUỒN TÍNH NĂNG)** vào ngày cuối cùng của Sprint 6:
    - Chốt nhánh `develop`, tạo nhánh `release/v1.0`.
    - Thông báo toàn nhóm: Kể từ thời điểm này, **tuyệt đối không viết thêm bất kỳ tính năng mới nào**, toàn bộ thời gian của Sprint 7 và 8 dành riêng cho việc Kiểm thử, Tối ưu hóa, Đo đạc và Hoàn thiện Hồ sơ Đồ án.
* **Tiêu chí nghiệm thu (AC)**:
  * **AC1**: Toàn bộ các PR tính năng mới được merge hoàn tất trước thời hạn Code Freeze.
  * **AC2**: Lập biên bản kiểm toán bảo mật và chốt checklist tính năng hoàn tất gửi toàn nhóm.

---

## 4. SPRINT 7 — Hiệu Năng Cao, Bảo Mật Toàn Diện, Đo Đạc NFR & Kiểm Thử E2E
> **Thời gian**: 16/11/2026 – 22/11/2026  
> **Dải mã Jira**: **`SCRUM-398` đến `SCRUM-415`** (18 Tasks)  
> **Mục tiêu**: Toàn bộ các chức năng chính hoàn thành; 100% các chỉ số phi chức năng (NFR) được đo đạc thực tế; triệt tiêu hoàn toàn các lỗi mất dữ liệu, sai quyền, tranh chấp credit trước ngày đóng gói bàn giao.

### 🟡 M1 — NGUYỄN TRƯƠNG HẬU (Bảo Mật, Diễn Tập DR & Đo Đạc NFR Hạ Tầng)
* **`SCRUM-398` (`W7-M1-01` · P0 · 8h · Epic E11 `SCRUM-271`)**: **Kiểm toán Bảo mật & Quét Lỗ hổng Toàn Stack (Full-Stack Security Audit)**:  
  * *Phải làm sao*: Quét dependencies bằng Trivy, cấu hình TLS/HTTPS trên Gateway và cấu hình mã hóa tĩnh AES-256 trên PostgreSQL/Azurite. Bảo vệ file khóa `aura-dev-service-account.json`. (AC: Không còn lỗi High/Critical, Secret không lọt vào Git).
* **`SCRUM-399` (`W7-M1-02` · P0 · 10h · Epic E1 `SCRUM-261`)**: **Kiểm tra Backup, Restore & Diễn tập Phục hồi Thảm họa (Disaster Recovery Drill)**:  
  * *Phải làm sao*: Chạy `Backup-AuraDatabase.ps1`, xóa sạch container và chạy `Restore-AuraDatabase.ps1` phục hồi từ zero. Đo đạc RTO và RPO. (AC: RTO $< 12$ phút đáp ứng NFR-4, RPO $= 0$).
* **`SCRUM-400` (`W7-M1-03` · P1 · 6h · Epic E1 `SCRUM-261`)**: **Sửa Lỗi Hạ tầng, Tinh chỉnh Connection Pool & Chốt Runbook v2.0**:  
  * *Phải làm sao*: Tinh chỉnh tham số Connection Pool trên PostgreSQL và channels RabbitMQ. Hoàn thiện sổ tay hướng dẫn cài đặt và vận hành [docs/development/installation_and_operations_manual.md](file:///D:/XD-PMHDT/docs/development/installation_and_operations_manual.md).
* **`SCRUM-405` (`W7-M1-04` · P0 · 10h · Epic E13 `SCRUM-273`)**: **Đo đạc NFR & Kiểm tra Độ Ổn Định Dịch Vụ Hạ Tầng (Resilience & NFR Verification)**:  
  *(M1 tiếp nhận bàn giao từ M3 — thực hiện trên nền tảng đo đạc SCRUM-349)*  
  * *Phải làm sao*: Tiến hành đo đạc độc lập: NFR-1 (AI Core $< 20$s), NFR-2 (Batch $\ge 100$ ảnh), NFR-4 (Uptime $\ge 99\%$). Kiểm tra khả năng tự phục hồi của Billing, RabbitMQ DLQ và FCM khi đứt mạng. (AC: 100% chỉ số đo đạc đạt chuẩn cam kết).

### 🟡 M2 — VÕ HỮU DUY (Tối Ưu Truy Vấn, Kiểm Tra Nhất Quán & Báo Cáo Backend)
* **`SCRUM-401` (`W7-M2-01` · P0 · 8h · Epic E4 `SCRUM-264`)**: **Tối ưu hóa Analysis & Dashboard Queries (Chống nghẽn NFR-3)**:  
  * *Phải làm sao*: Sử dụng `EXPLAIN ANALYZE` rà soát truy vấn, loại bỏ triệt để lỗi N+1 Query trong EF Core bằng `AsNoTracking()`, `Include()` hợp lý. (AC: Thời gian tải Dashboard $< 1.2\text{s}$ trên 10.000 bản ghi, đáp ứng NFR-3 $< 3$s).
* **`SCRUM-402` (`W7-M2-02` · P0 · 10h · Epic E4 `SCRUM-264`)**: **Kiểm tra Nhất quán Dữ liệu, Concurrency & Xử lý Tranh chấp**:  
  * *Phải làm sao*: Kiểm thử kịch bản hai bác sĩ cùng mở một bệnh án và sửa kết luận cùng lúc $\rightarrow$ Xác nhận Optimistic Concurrency chặn bác sĩ thứ hai an toàn (`HTTP 409 Conflict`). (AC: Không xảy ra ghi đè mất dữ liệu).
* **`SCRUM-403` (`W7-M2-03` · P1 · 6h · Epic E4 `SCRUM-264`)**: **Sửa Lỗi Analysis và Chốt Hồ sơ Thiết kế Kỹ thuật Backend**:  
  * *Phải làm sao*: Sửa toàn bộ các lỗi tồn đọng phát hiện trong kiểm thử E2E. Đóng gói bộ sơ đồ thiết kế: ERD, Class Diagram, State Machine Diagram đưa vào báo cáo đồ án.

### 🟡 M4 — ĐÀO DUY QUÂN (Đánh Giá Cuối Mô Hình AI, Tối Ưu Hóa & Báo Cáo Khoa Học)
* **`SCRUM-407` (`W7-M4-01` · P0 · 8h · Epic E5 `SCRUM-265`)**: **Đánh giá Nghiệm thu Cuối Mô hình AI: Phân vùng Frangi, Đái tháo đường & Đo chỉ số $AVR$**:  
  * *Phải làm sao*: Chạy đánh giá độc lập mô hình AI trên cả 3 tập dữ liệu độc lập (`CHASE_DB1`, `HRF`, `INSPIRE_AVR`). Tính toán Sensitivity, Specificity, Accuracy, F1-Score, Dice Coefficient và chỉ số $AVR$. (AC: Accuracy $\ge 93\%$, Dice Coefficient $\ge 0.82$, sai số đo $AVR < 5\%$).
* **`SCRUM-408` (`W7-M4-02` · P0 · 10h · Epic E5 `SCRUM-265`)**: **Tối ưu hóa Bộ nhớ AI Core & Xuất ONNX Runtime FP16**:  
  * *Phải làm sao*: Xuất trọng số mô hình sang ONNX Runtime FP16/INT8 để suy luận cực nhanh trên CPU mà không cần GPU rời. Khởi động cơ chế Warm-up mô hình khi container khởi động. (AC: Độ trễ suy luận ổn định $< 1.5\text{s}$/ảnh trên CPU).
* **`SCRUM-409` (`W7-M4-03` · P1 · 6h · Epic E5 `SCRUM-265`)**: **Sửa Lỗi AI và Chốt Báo cáo Chuyên đề Khoa Học AI Y Tế**:  
  * *Phải làm sao*: Xử lý triệt để các trường hợp biên của ảnh đáy mắt (ảnh quá sáng, ảnh quá tối). Hoàn thiện chương Báo cáo Đồ án chuyên đề AI y tế.

### 🟡 M5 — LÂM TRẦN NGUYỄN HOÀNG (Tối Ưu Giao Diện, Accessibility & Kiểm Thử UI E2E)
* **`SCRUM-410` (`W7-M5-01` · P0 · 8h · Epic E12 `SCRUM-272`)**: **Hoàn thiện Khả năng Tiếp cận (Accessibility WCAG 2.1) & Responsive Toàn Web**:  
  * *Phải làm sao*: Rà soát chuẩn WCAG 2.1 AA (điều hướng bàn phím, tương phản màu sắc). Tinh chỉnh giao diện hoàn hảo trên Desktop, Tablet 10 inch và Mobile. (AC: Điểm Google Lighthouse Accessibility đạt $\ge 92/100$).
* **`SCRUM-411` (`W7-M5-02` · P0 · 10h · Epic E12 `SCRUM-272`)**: **Tối ưu hóa Trải nghiệm Người dùng & Tốc độ Tải trang (Web Vitals)**:  
  * *Phải làm sao*: Code-Splitting theo route, nén ảnh WebP. Bảo đảm nguyên tắc **NFR-14: Hành trình người dùng $\le 3$ click**. (AC: LCP $< 1.8\text{s}$, CLS $\approx 0$).
* **`SCRUM-412` (`W7-M5-03` · P1 · 6h · Epic E13 `SCRUM-273`)**: **Kiểm thử Giao diện Hồi quy Playwright Tự động & Chốt Design Documentation**:  
  * *Phải làm sao*: Viết bộ test Playwright kiểm tra các tương tác: Đăng nhập $\rightarrow$ Upload $\rightarrow$ Kết quả $\rightarrow$ Review $\rightarrow$ Chat. Đóng gói Design System Documentation. (AC: 100% tests pass trên Chrome, Edge, Firefox).

### 🟡 M6 — DUNG HOANG (Kiểm Thử Đa Tenant, Điều Phối QA & Cập Nhật Final RTM)
* **`SCRUM-404` (`W7-M6-04` · P0 · 8h · Epic E13 `SCRUM-273`)**: **Điều phối Kiểm thử E2E & Hồi quy Toàn diện 4 Vai Trò (Regression Testing)**:  
  *(M6 tiếp nhận vai trò QA Lead từ M3)*  
  * *Phải làm sao*: Điều phối chạy kiểm thử tự động toàn diện trên cả 4 vai trò (Bệnh nhân, Bác sĩ, Clinic Admin, SuperAdmin). Ghi nhận Defect Log và phân công khắc phục. (AC: 100% kịch bản User Journeys thực thi trơn tru).
* **`SCRUM-406` (`W7-M6-05` · P1 · 6h · Epic E13 `SCRUM-273`)**: **Cập nhật Ma Trận Đối Soát Yêu Cầu Đề Tài Cuối Cùng (Final RTM Matrix)**:  
  *(M6 tiếp nhận bàn giao từ M3 — hoàn thiện trên nền tảng RTM Sprint 4)*  
  * *Phải làm sao*: Hoàn thiện bảng RTM ánh xạ chi tiết toàn bộ 39 FRs và 23 NFRs sang mã nguồn, endpoint API và mã test case chứng minh. (AC: 100% yêu cầu đạt kết quả `PASS` / `VERIFIED`).
* **`SCRUM-413` (`W7-M6-01` · P0 · 8h · Epic E13 `SCRUM-273`)**: **Kiểm thử Bảo mật Đa Tenant & Cô Lập Dữ Liệu Tuyệt Đối (Multi-Tenancy Audit)**:  
  * *Phải làm sao*: Tạo dữ liệu cho 5 phòng khám khác nhau. Viết script kiểm tra cô lập dữ liệu: Gọi API chéo tenant nhận ngay `HTTP 403 Forbidden`. (AC: 100% các cuộc gọi trái tenant bị chặn đứng).
* **`SCRUM-414` (`W7-M6-02` · P0 · 10h · Epic E3 `SCRUM-263`)**: **Tối ưu hóa Profile, Quản trị Quyền & Xử lý Trạng thái Tài khoản**:  
  * *Phải làm sao*: Cache quyền người dùng trong Redis token cache để giảm tải DB. Khi tài khoản bị khóa, xóa cache Redis tức thời. (AC: Thời gian xác thực quyền tại Gateway $< 5\text{ms}$).
* **`SCRUM-415` (`W7-M6-03` · P1 · 6h · Epic E11 `SCRUM-271`)**: **Hoàn thiện Chính sách Quyền Riêng Tư (Privacy) & Tài Liệu Module**:  
  * *Phải làm sao*: Hiện thực quy trình Xuất dữ liệu cá nhân (Right to Access) và Xóa/Ẩn danh dữ liệu (Right to be Forgotten). Hoàn thiện tài liệu thuyết minh phân hệ Identity/Profile.

---

## 5. SPRINT 8 — Release, Hồ Sơ Đồ Án & Kịch Bản Bảo Vệ Tốt Nghiệp
> **Thời gian**: 23/11/2026 – 29/11/2026  
> **Dải mã Jira**: **`SCRUM-416` đến `SCRUM-433`** (18 Tasks)  
> **Mục tiêu**: Đóng gói Release Candidate; Báo cáo Đồ án Toàn văn; Slide Thuyết trình 25 trang; Video Demo 1080p 60fps; Diễn tập Dry Run bảo vệ đồ án.

* **`SCRUM-416` (`W8-M1-01` · P0 · 8h · E1)**: Đóng gói Docker Release Candidate `v1.0.0-release` độc lập và kịch bản rollback trong 5 phút (M1).
* **`SCRUM-417` (`W8-M1-02` · P0 · 10h · E1)**: Bàn giao môi trường sản xuất chuẩn, kiểm chứng cài đặt trên máy trắng (Fresh Machine) $< 15$ phút (M1).
* **`SCRUM-418` (`W8-M1-03` · P1 · 6h · E14)**: Hoàn thiện cuốn Software Architecture Document (SAD v1.0) 4+1 View Model và Release Notes (M1).
* **`SCRUM-422` (`W8-M1-04` · P0 · 8h · E13)**: *(M1 tiếp nhận)* Chạy Smoke Test nghiệm thu bản Release trên toàn bộ 6 microservices và lập biên bản kỹ thuật.
* **`SCRUM-424` (`W8-M1-05` · P1 · 6h · E14)**: *(M1 tiếp nhận)* Chuẩn bị kịch bản Demo Màn Thanh toán Sandbox, Live Chat và FCM Push trên 2 thiết bị (phối hợp M5).
* **`SCRUM-419` (`W8-M2-01` · P0 · 8h · E4)**: Sửa lỗi phát hành cuối cùng và đóng băng mã nguồn Backend, kiểm tra migration sạch (M2).
* **`SCRUM-420` (`W8-M2-02` · P0 · 10h · E14)**: Hoàn thiện cuốn Thuyết minh Kỹ thuật phân hệ Phân tích, Hàng đợi RabbitMQ và Báo cáo Y khoa (M2).
* **`SCRUM-421` (`W8-M2-03` · P1 · 6h · E6)**: Chuẩn bị kịch bản & dữ liệu mẫu 100 ảnh đáy mắt đa dạng cho Màn Demo Xử lý Hàng loạt (M2).
* **`SCRUM-423` (`W8-M2-04` · P0 · 10h · E14)**: *(M2 tiếp nhận)* Hoàn thiện chương Báo cáo Phân hệ Tài chính, Thông báo Thời gian thực và Consultation Chat.
* **`SCRUM-425` (`W8-M4-01` · P0 · 8h · E5)**: Đóng gói Artifacts mô hình AI (ONNX weights, HRF, INSPIRE-AVR) và tệp hướng dẫn tái lập kết quả khoa học (M4).
* **`SCRUM-426` (`W8-M4-02` · P0 · 10h · E14)**: Hoàn thiện Báo cáo Nghiệm thu Thuật toán và bản in Model Card v2.0 đưa vào cuốn báo cáo (M4).
* **`SCRUM-427` (`W8-M4-03` · P1 · 6h · E5)**: Chuẩn bị bài thuyết trình 3 phút giải thích giải thuật Frangi, phân loại DR và đo chỉ số $AVR$ (M4).
* **`SCRUM-428` (`W8-M5-01` · P0 · 8h · E12)**: Đóng gói bản build Production Web UI tối ưu hóa (Nginx serve thư mục `dist/`) (M5).
* **`SCRUM-429` (`W8-M5-02` · P0 · 10h · E14)**: Thiết kế Bộ Slide Thuyết trình Báo cáo Tốt nghiệp (20–25 slide Canva/PowerPoint hiện đại chuẩn y tế) (M5).
* **`SCRUM-430` (`W8-M5-03` · P1 · 6h · E12)**: Dựng Video Demo Dự phòng 1080p 60fps chất lượng cao có lồng tiếng thuyết minh rõ ràng (M5).
* **`SCRUM-431` (`W8-M6-01` · P0 · 10h · E14)**: Tổng hợp, biên tập và in ấn Cuốn Báo cáo Đồ án Toàn văn Tốt nghiệp theo chuẩn quy định của Nhà trường (M6).
* **`SCRUM-432` (`W8-M6-02` · P0 · 8h · E14)**: Lập Biên bản nghiệm thu sản phẩm phần mềm và bảng điểm đánh giá đóng góp cá nhân của từng thành viên (M6).
* **`SCRUM-433` (`W8-M6-03` · P1 · 6h · E3)**: Chuẩn bị kịch bản Demo Phân quyền Đa Tenant, Phê duyệt Clinic và Quản trị Danh tính (M6).

---

## 6. KỊCH BẢN 5 MÀN DEMO ĐỈNH CAO TRƯỚC HỘI ĐỒNG (15 PHÚT)

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              5 MÀN TRÌNH DIỄN KỸ THUẬT AURA                             │
├─────────┬───────────────────────────────────┬──────────────────────────────────────────┤
│ Màn 1   │ Tiếp nhận Camera qua Apache NiFi  │ • Tiếp nhận ảnh camera công nghiệp tự động│
│ (3 phút)│ (Enterprise Ingestion & Chaos HA) │ • Demo chống trùng SHA-256 (Deduplication)│
│ (M1 phụ │                                   │ • Cách ly ảnh hỏng cấu trúc (Quarantine) │
│  trách) │                                   │ • Đóng băng DB (docker pause) & Phục hồi │
├─────────┼───────────────────────────────────┼──────────────────────────────────────────┤
│ Màn 2   │ Xử lý Hàng loạt & AI Lâm sàng     │ • Gửi Batch 100 ảnh từ giao diện Web.    │
│ (3 phút)│ (Distributed Batch & Clinical AI) │ • 4 Workers RabbitMQ cào xử lý song song.│
│ (M2, M4 │                                   │ • Thuật toán Frangi trích xuất mạch máu  │
│  phụ tr)│                                   │ • Phân loại Đái Tháo Đường & Đo $AVR$     │
├─────────┼───────────────────────────────────┼──────────────────────────────────────────┤
│ Màn 3   │ Không gian Bác sĩ & Báo cáo PDF   │ • So sánh ảnh gốc, mask A/V & Grad-CAM.  │
│ (3 phút)│ (Doctor Review Workspace)         │ • Bác sĩ điều chỉnh cấp độ ICDR          │
│ (M2, M5 │                                   │ • Kết xuất Bệnh án y khoa PDF chuẩn trang│
│  phụ tr)│                                   │   A4 và xuất file ZIP hàng loạt.         │
├─────────┼───────────────────────────────────┼──────────────────────────────────────────┤
│ Màn 4   │ Thanh toán Ví & Tư vấn Trực tuyến │ • Nạp tiền Sandbox có verify chữ ký HMAC │
│ (3 phút)│ (Sandbox Billing & Live Chat)     │ • Trừ cước an toàn Idempotent Ledger.    │
│ (M1, M2,│                                   │ • Chat thời gian thực Bác sĩ - Bệnh nhân │
│  M5 p.tr│                                   │ • Nhận thông báo đẩy FCM Web Push tức thì│
├─────────┼───────────────────────────────────┼──────────────────────────────────────────┤
│ Màn 5   │ Giám sát Toàn diện & Quản trị     │ • Trực quan hóa toàn bộ chỉ số RED       │
│ (3 phút)│ (Full-Stack Observability & Admin)│ • Giám sát hàng đợi RabbitMQ & Grafana   │
│ (M1, M6 │                                   │ • Quản trị duyệt Clinic & Audit Log.     │
│  phụ tr)│                                   │ • Kích hoạt cảnh báo tự động khi quá tải │
└─────────┴───────────────────────────────────┴──────────────────────────────────────────┘
```

---
*Bản kế hoạch hành động chi tiết tiếp nối 4 Sprint Jira — Đề tài Nghiên cứu Khoa học & Đồ án Tốt nghiệp SP26SE025.*
