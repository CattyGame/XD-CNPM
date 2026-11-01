# AURA Phân công Sprint 4–5 Kết hợp (Sprint Tích hợp Tăng tốc)
**Mở rộng Batch Processing, Doctor Review, Thanh toán Sandbox/FCM, Clinic Đa Tenant và Giám sát với Prometheus & Grafana**

> **Ngày cập nhật**: 31/10/2026 | **5 thành viên** (Tái cấu trúc: M3 out nhóm, dồn toàn bộ task M3 & Task 1 M4 về cho M1) | **24 task tích hợp** | **290 giờ dự kiến** (2 tuần gộp)
> **Phạm vi gộp**: Hợp nhất toàn bộ khối lượng công việc của Sprint 4 và Sprint 5 để đẩy nhanh tiến độ về đích.
> **Điều chỉnh nhân sự theo chỉ đạo**: Thành viên M3 rút khỏi nhóm. Toàn bộ **4 task của M3** (Cổng Thanh toán Sandbox, Firebase FCM Web Push, Clinic Packages, Kiểm thử Phục hồi Tài chính) và **Task 1 của M4** (Apache NiFi Ingestion & Quarantine) được **chuyển giao toàn bộ cho M1 (Lead Architect)** đảm nhận. **M4** tập trung toàn lực 100% vào phân hệ chuyên sâu **AI Core: Thuật toán Frangi v0.2, Mô hình Đánh giá Rủi ro Heuristic, Khảo sát Ảnh Cắt lớp OCT và Stress Test AI**.
> **Nguyên tắc kỹ thuật**: Đảm bảo 100% các công nghệ theo yêu cầu đề tài đều được tích hợp đầy đủ và chạy thực tế: **OpenAPI/Swagger, Cache Redis, Firebase Cloud Messaging (FCM), Apache NiFi, Microservices - API Gateway, RabbitMQ, và Monitoring với Prometheus & Grafana**.

---

## 1. Mục tiêu bàn giao

1. **Từ 1 ảnh lên Xử lý Hàng loạt (Batch Processing $\ge 100$ ảnh)**: Web Bulk Upload hoặc Camera NiFi $\rightarrow$ Gateway $\rightarrow$ Analysis tiếp nhận Batch $\rightarrow$ RabbitMQ điều phối song song $\rightarrow$ Cụm Worker phân tích qua AI Frangi $\rightarrow$ Cập nhật tiến độ từng ảnh qua Redis $\rightarrow$ Lưu kết quả an toàn.
2. **Không gian làm việc của Bác sĩ (Doctor Workspace & Review)**: Bác sĩ xem danh sách bệnh nhân được phân công, so sánh ảnh gốc và ảnh phân vùng, chỉnh sửa kết luận y khoa (Optimistic Concurrency) và kết xuất báo cáo y tế định dạng PDF/CSV.
3. **Thanh toán Sandbox, Gói cước & Thông báo đẩy Firebase (FCM)**: Người dùng mua gói khám, thanh toán Sandbox có xác minh chữ ký Webhook; tự động cộng credit và gửi Web Push Notification tức thời qua Firebase Cloud Messaging (FCM) khi phân tích xong hoặc giao dịch thành công.
4. **Quản lý Phòng khám (Clinic Lifecycle) & Đa Tenant**: Đăng ký, xét duyệt Clinic, phân công Bác sĩ (Doctor Assignment), quản lý bệnh nhân tổ chức và khởi tạo Chiến dịch tầm soát (Campaign) với cơ chế cô lập dữ liệu tuyệt đối.
5. **Hạ tầng Giám sát Hoàn chỉnh (Monitoring with Prometheus & Grafana)**: Thu thập metrics toàn diện từ API Gateway, Analysis API, Billing API, AI Core, RabbitMQ Exporter và Postgres Exporter; hiển thị trực quan trên Grafana Dashboards thời gian thực và cấu hình cảnh báo sự cố.

---

## 2. Phân công và Ngân sách Công việc (Cơ cấu 5 Thành viên)

| Mã | Thành viên | Phụ trách Sprint 4–5 Kết hợp | Task | Giờ P0 | Giờ P1 | Tổng giờ |
|:---:|---|---|:---:|:---:|:---:|:---:|
| **M1** | Nguyễn Trương Hậu | **Hạ tầng, Prometheus/Grafana, Redis, NiFi Ingestion, Cổng Sandbox, FCM & Billing** *(Nhận cả 4 task M3 + Task 1 M4)* | **8** | 88 | 10 | **98** |
| **M2** | Võ Hữu Duy | **Analysis Pipeline, Worker Scaling, Doctor Review, Billing Automation & Batch PDF** | **4** | 38 | 10 | **48** |
| **M4** | Đào Duy Quân | **AI Core: Tối ưu Frangi v0.2, Heuristic Risk Model, AI Contract 1.3, Khảo sát OCT & Stress Test AI** | **4** | 38 | 10 | **48** |
| **M5** | Lâm Trần Nguyễn Hoàng | **Frontend Doctor Workspace, Bulk Upload Batch UI & FCM Web Push Client** | **4** | 38 | 10 | **48** |
| **M6** | Thành viên mới | **Identity Profile, Clinic Lifecycle, Phân công Bác sĩ & Chiến dịch Tầm soát** | **4** | 38 | 10 | **48** |
| **TỔNG** | **Cả nhóm 5 người** | **Hệ thống Toàn diện Sprint 4–5** | **24** | **240** | **50** | **290** |

*Ghi chú phân bổ*:
* **M1 (Lead Architect & Platform Lead)** giữ vai trò hạt nhân gánh vác toàn bộ mảng Hạ tầng Nền tảng + Cổng Dữ liệu Ingestion + Phân hệ Thanh toán & Thông báo tập trung:
  * Tiếp nhận **Task 1 của M4** (`W45-M1-04`): Nâng cấp luồng Apache NiFi camera, chống trùng ảnh SHA-256 (Dedup), phân luồng cách ly lỗi (Quarantine) và Bounded Auto-Retry.
  * Tiếp nhận **toàn bộ 4 task của M3**:
    * `W45-M1-05`: Xây dựng Cổng Thanh toán Sandbox, Gói cước và Cấp Credit Idempotent.
    * `W45-M1-06`: Dịch vụ Thông báo Đẩy Firebase Cloud Messaging (FCM) Backend.
    * `W45-M1-07`: Billing Cấp Tổ chức (Clinic Packages) và Tự động Hoàn Credit.
    * `W45-M1-08`: Kiểm thử Khả năng Phục hồi Hạ tầng & Billing/FCM, Đối soát RTM và Bàn giao Runbook.
* **M4 (AI Core Specialist)** giải phóng hoàn toàn khỏi các tác vụ NiFi và Billing ngoài luồng, tập trung 100% chuyên môn vào việc tối ưu hóa thuật toán thị giác máy tính Frangi v0.2, xây dựng Heuristic Risk Model y khoa, đóng gói hợp đồng AI v1.3 và khảo sát định dạng ảnh cắt lớp võng mạc OCT.
* **M2, M5, M6** giữ vững đúng định mức chuẩn 4 task (48 giờ làm việc trong 2 tuần), đảm bảo hoàn thành xuất sắc các phân hệ Doctor Workflow, Frontend Web UI và Clinic Multi-Tenancy.

---

## 3. Kết quả Kế thừa và Phạm vi Phải Bảo toàn

* Kế thừa toàn bộ kết quả đã nghiệm thu của Sprint 3: Container AI Core Frangi v0.2 thật (`aura-ai77-539ab735c32d401c`), RabbitMQ vhost `/aura-s3` với 11 quorum queues, flow NiFi nhận ảnh camera qua HTTP 202 (`Record-Camera-Receipt-Safe-v2`), và PostgreSQL Sandbox (`PASS87S_PG_SANDBOX_READY`).
* Bảo toàn toàn bộ dữ liệu lịch sử của fixture 08; các bài thử nghiệm Batch lớn và nạp tiền Sandbox phải dùng fixture mới có `MinOccurredAtUtc = "2026-10-28T00:00:00Z"`.
* Tuyệt đối không xóa volumes, không sửa trực tiếp database của service khác ngoài quyền sở hữu, không lưu trữ khóa bí mật hoặc thông tin định danh bệnh nhân trong log và Git.

---

## 4. Thứ tự Triển khai (Lịch trình 2 tuần gộp: Ngày 1 – Ngày 14)

| Mốc Thời gian | Nội dung công việc phối hợp cần bàn giao | Owner điều phối |
|---|---|---|
| **Ngày 1–2** | • M1 dựng stack Prometheus & Grafana, cào metrics cơ bản.<br>• M1 cấp Redis Cache tập trung, cấu hình RabbitMQ Worker Scaling.<br>• Chốt OpenAPI 0.8 cho Doctor Review, Batch, Payment và Clinic.<br>• M1 khởi động nâng cấp luồng NiFi InvokeHTTP và chuẩn bị Firebase Admin SDK. | M1, M2, M4, M6 |
| **Ngày 3–5** | • M2 dựng Doctor Workspace API & Batch Processing Engine.<br>• M1 hoàn thiện luồng NiFi Dedup/Quarantine, tích hợp FCM Web Push và dựng Cổng Thanh toán Sandbox.<br>• M4 tối ưu hóa thuật toán phân vùng Frangi v0.2 và xây dựng Heuristic Risk Model.<br>• M6 hoàn thành API phân quyền Clinic và Doctor Assignment. | M1, M2, M4, M6 |
| **Ngày 6–8** | • M5 kết nối giao diện Doctor Workspace với API thật của M2.<br>• M5 tích hợp FCM Service Worker trên Web nhận thông báo tức thời từ backend của M1.<br>• M5 kết nối giao diện nạp Credit với API Sandbox của M1.<br>• M1 hoàn thành Dashboards Grafana chuyên sâu (System, Services, RabbitMQ) và hoàn thiện Clinic Packages. | M1, M2, M5 |
| **Ngày 9–11** | • Thử nghiệm chạy Batch lớn $\ge 100$ ảnh từ Web Bulk Upload và NiFi Ingestion.<br>• M1 và M2 kiểm tra cơ chế tự động Reserve $\rightarrow$ Capture $\rightarrow$ Release Credit và hoàn tiền khi ảnh lỗi.<br>• M4 đóng gói AI Contract 1.3 và khảo sát định dạng ảnh cắt lớp võng mạc OCT.<br>• M6 triển khai module Campaign tầm soát gắn kết quả Batch. | Cả nhóm |
| **Ngày 12–13** | • Diễn tập sự cố: Worker crash giữa batch, broker downtime, phục hồi backup DB (M1).<br>• M1 kiểm thử khả năng phục hồi Billing/FCM, ma trận đối soát số dư.<br>• M4 chạy Stress test AI Core container Frangi v0.2 (50 requests liên tục).<br>• M6 kiểm thử bảo mật phân quyền đa tenant phòng khám. | M1, M4, M6 |
| **Ngày 14** | • Cả nhóm tổng hợp báo cáo kiểm thử, chốt RTM, hoàn thiện Runbook và nghiệm thu toàn bộ 24 task. | Cả nhóm |

---

## 5. Điều kiện Hoàn thành Chung cho Mọi Task (Definition of Done - DoD)

Một task chỉ được chuyển sang trạng thái **`Done`** khi thỏa mãn đầy đủ các điều kiện sau:
1. **Mã nguồn & Cấu hình**: Code hoàn chỉnh, tuân thủ Clean Code, không có lỗi biên dịch/linter, commit gắn mã Jira tương ứng (ví dụ: `feat(SCRUM-xxx): ...`).
2. **Kiểm thử tự động**: Có unit tests và integration tests tương ứng. Tất cả bài test phải pass 100%, không skip, không giả mạo kết quả.
3. **Tuân thủ Hợp đồng API (OpenAPI First)**: Mọi thay đổi về endpoint, request, response hoặc mã lỗi phải được cập nhật đồng bộ trong file OpenAPI tương ứng trước khi merge code.
4. **Bằng chứng xác thực (Evidence)**: Có log thực thi, kết quả test, ảnh chụp màn hình hoặc file JSON/Trx ghi nhận ngày giờ, môi trường và version.
5. **Tài liệu bàn giao**: Cập nhật tài liệu hướng dẫn vận hành (Runbook), README hoặc sơ đồ tuần tự (Sequence Diagram) liên quan.

---

## 6. CHI TIẾT TÁC VỤ CHO TỪNG THÀNH VIÊN

---

### M1 — NGUYỄN TRƯƠNG HẬU (Kiến trúc, Hạ tầng, Monitoring, Apache NiFi, Cổng Sandbox & FCM)
*(Đảm nhiệm toàn bộ 4 task của M3 và Task 1 của M4 theo chỉ đạo tái cấu trúc)*

#### Task W45-M1-01 | P0 | 14 giờ | Epic E1 Platform/DevOps (SCRUM-261)
**Triển khai Cụm Giám sát Prometheus & Dashboards Grafana Toàn diện**
* **Phối hợp kiểm tra**: M2, M4.
* **Phụ thuộc**: Không có task phụ thuộc; sử dụng môi trường Docker Compose Sprint 3 hiện hữu.
* **Cần thực hiện**:
  * Dựng container `prometheus` (v2.51+) và `grafana` (v10.4+) trong `compose.monitoring.yaml`.
  * Cấu hình Prometheus cào định kỳ (`scrape_interval: 5s`) từ endpoint `/metrics` của: API Gateway, Analysis API, Billing API, AI Core, PostgreSQL Exporter và RabbitMQ Prometheus Plugin (port 15692).
  * Xây dựng 3 Dashboards Grafana chuẩn trực quan:
    1. *System Overview Dashboard*: CPU, RAM, Disk I/O, Network traffic của từng container.
    2. *Microservices RED Dashboard*: Rate (Throughput), Errors (Tỷ lệ lỗi HTTP 4xx, 5xx), Duration (Độ trễ p50, p95, p99).
    3. *RabbitMQ & Worker Health Dashboard*: Độ sâu hàng đợi (Queue Depth), tốc độ Publish/Consume, tin nhắn tồn đọng (Backlog), Unacked messages và DLQ count.
  * Cấu hình Alertmanager với các quy tắc cảnh báo: Gửi alert khi tỷ lệ lỗi HTTP $5xx > 2\%$ trong 2 phút, hoặc hàng đợi `aura.s3.analysis.jobs` tồn đọng $> 50$ messages quá 5 phút.
* **Đầu ra bàn giao**: `compose.monitoring.yaml`, `prometheus.yml`, file JSON export của 3 Grafana Dashboards, runbook hướng dẫn truy cập và cấu hình cảnh báo.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Truy cập `http://localhost:3000` (Grafana) xem được biểu đồ thời gian thực đầy đủ các metrics từ Gateway, Analysis, RabbitMQ và Postgres.
  * **AC2**: Khi mô phỏng tải hoặc tạo lỗi HTTP 500 nhân tạo, biểu đồ Grafana lập tức nhảy vọt và Alertmanager kích hoạt trạng thái `FIRING`.
  * **AC3**: Prometheus cào metrics liên tục không làm tăng quá 3% CPU của các microservices; không ghi lộ secret hoặc token người dùng trong labels.

#### Task W45-M1-02 | P0 | 12 giờ | Epic E1 Platform/DevOps (SCRUM-261)
**Cấu hình Cụm Cache Redis Tập trung & Tối ưu Scaling RabbitMQ Worker**
* **Phối hợp kiểm tra**: M2.
* **Phụ thuộc**: W45-M1-01.
* **Cần thực hiện**:
  * Chuẩn hóa service `redis` (v7.2+) với cấu hình bền vững dữ liệu (AOF & RDB snapshot), cấp quyền kết nối bằng password bảo mật qua biến môi trường.
  * Thiết lập mạng lưới phân vùng cache: Prefix namespace riêng biệt cho API Gateway (`gw:ratelimit:`, `gw:session:`), Billing (`billing:package:`, `billing:idempotency:`) và Analysis (`analysis:batch:`).
  * Cấu hình scaling cụm Worker trong Docker Compose (chạy đồng thời 2 đến 4 Worker replicas) kết nối vào vhost `/aura-s3`.
  * Điều chỉnh thông số RabbitMQ prefetch count (`prefetch=10`), channel pooling và heartbeat timeout để tối ưu xử lý hàng loạt $\ge 100$ jobs mà không gây nghẽn kết nối.
* **Đầu ra bàn giao**: File Compose cập nhật cấu hình Redis & Worker Replicas, script kiểm tra kết nối `Test-RedisCluster.ps1`, tài liệu cấu hình cache conventions.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Redis khởi động mượt mà, lưu trữ và đọc key-value với độ trễ $< 2$ms; dữ liệu không bị mất sau khi restart container Redis.
  * **AC2**: Khởi chạy 3 Worker replicas đồng thời, phân phối đều tải khi tiêu thụ hàng đợi RabbitMQ (round-robin hoặc fair-dispatch).
  * **AC3**: Các khóa cache phân vùng đúng namespace, có TTL hợp lý (không để key vô hạn gây tràn RAM).

#### Task W45-M1-03 | P0 | 10 giờ | Epic E11 Security & Compliance (SCRUM-271)
**Tự động hóa Sao lưu/Phục hồi Dữ liệu & Hoàn thiện Bảo mật Liên dịch vụ**
* **Phối hợp kiểm tra**: M2, M6.
* **Phụ thuộc**: Sử dụng cơ sở dữ liệu PostgreSQL hiện hữu.
* **Cần thực hiện**:
  * Xây dựng script sao lưu tự động (Automated Backup Script) bằng PowerShell/Bash: Chụp snapshot database PostgreSQL (`aura_dev`, `aura_reporting`), mã hóa bằng AES-256 và lưu trữ vào thư mục an toàn có xoay vòng (Retention policy 7 ngày).
  * Diễn tập khôi phục thảm họa (Disaster Recovery Simulation): Giả lập mất dữ liệu và chạy script phục hồi tự động, xác minh toàn vẹn dữ liệu trước và sau phục hồi.
  * Thiết lập chính sách bảo mật mạng liên container (Docker Network Isolation): Tách biệt mạng `aura-public-net` (Gateway, Web) và `aura-internal-net` (Postgres, RabbitMQ, Redis, AI Core, NiFi Internal).
* **Đầu ra bàn giao**: Scripts `Backup-Database.ps1`, `Restore-Database.ps1`, file cấu hình mạng `docker-compose.security.yaml`, báo cáo diễn tập phục hồi thảm họa.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Script backup thực thi tự động, tạo file nén `.sql.gz` có mã hóa đúng quy cách; dung lượng và thời gian thực hiện hiển thị trên log.
  * **AC2**: Khôi phục thành công 100% dữ liệu thử nghiệm trong vòng $< 15$ phút; không có bảng nào bị sai lệch khóa ngoại (Foreign Key) hay mất chỉ mục (Index).
  * **AC3**: Kiểm tra bảo mật: Các container nội bộ (Postgres, Redis) không thể bị truy cập trực tiếp từ mạng bên ngoài host nếu không qua Gateway hoặc cổng được chỉ định an toàn.

#### Task W45-M1-04 | P0 | 14 giờ | Epic E9 NiFi Integration (SCRUM-269)
**Nâng cấp Luồng Apache NiFi: InvokeHTTP, Chống trùng (Dedup), Quarantine & Auto-Retry**
*(Nhiệm vụ chuyển giao từ Task 1 của M4 sang M1)*
* **Phối hợp kiểm tra**: M2, M5.
* **Phụ thuộc**: Kế thừa trực tiếp Canvas NiFi Sprint 3 (`Record-Camera-Receipt-Safe-v2`), PostgreSQL Reporting DB và API Gateway.
* **Cần thực hiện**:
  * Nâng cấp flow Apache NiFi xử lý camera:
    * Hoàn thiện processor `InvokeHTTP` đẩy ảnh camera chính thức vào API Gateway qua route `/api/v1/analyses/camera` kèm Service Token và Header định danh thiết bị.
    * Tích hợp bộ lọc chống gửi trùng ảnh (Deduplication): Tính hash SHA-256 của file ảnh camera ngay tại NiFi processor; kiểm tra trong cache/state hoặc qua API, nếu phát hiện trùng lặp hash với ca đã tiếp nhận thì drop an toàn và ghi log cảnh báo trùng lặp.
  * Xây dựng phân luồng cách ly lỗi (Quarantine Routing):
    * Ảnh đúng chuẩn $\rightarrow$ Chuyển tiếp vào hệ thống AURA tiếp nhận (HTTP 202) và ghi nhận biên lai vào database `camera_receipts`.
    * Ảnh lỗi format, hỏng header hoặc Gateway từ chối $\rightarrow$ Định tuyến sang thư mục/queue `camera.quarantine` kèm thuộc tính ghi rõ mã lỗi và lý do cách ly để kỹ thuật viên kiểm tra.
  * Cấu hình Bounded Retry & Backpressure threshold hợp lý (tăng lên 1000 FlowFiles) trên các kết nối giữa `Record-Camera-Receipt-Safe-v2`, `Route-Reporting-Result-v2` và `Bounded-Reporting-Retry-Delivery-v1` để giải phóng hoàn toàn các FlowFiles tồn đọng.
* **Đầu ra bàn giao**: File template NiFi XML v2.0 (`aura-nifi-camera-v2.xml`), script nạp flow tự động `Deploy-NiFiFlow.ps1`, tài liệu cấu hình Quarantine và Dedup.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: NiFi tự động lấy ảnh từ camera/folder mô phỏng, gọi `InvokeHTTP` đến Gateway và nhận về HTTP 202 Accepted; mã biên lai và mã batch được ghi nhận vào database `camera_receipts`.
  * **AC2**: Gửi liên tiếp 2 file ảnh giống hệt nhau qua camera folder: NiFi chỉ đẩy ảnh đầu tiên vào Gateway, ảnh thứ hai bị phát hiện trùng SHA-256 và chuyển sang luồng drop/duplicate có log chứng thực.
  * **AC3**: Gửi 1 file hỏng (corrupted bytes): NiFi tự động định tuyến vào queue Quarantine, không làm gián đoạn luồng xử lý của các ảnh bình thường khác; Data Provenance hiển thị đầy đủ nguồn gốc sự kiện.

#### Task W45-M1-05 | P0 | 14 giờ | Epic E8 Billing (SCRUM-268)
**Xây dựng Cổng Thanh toán Sandbox, Gói cước và Cấp Credit Idempotent**
*(Nhiệm vụ chuyển giao từ Task 1 của M3 sang M1)*
* **Phối hợp kiểm tra**: M5 (Frontend UI), M2 (Analysis Pipeline).
* **Phụ thuộc**: Không có task phụ thuộc; sử dụng module Billing hiện hữu.
* **Cần thực hiện**:
  * Xây dựng catalog quản lý các gói khám (Packages): Gói cá nhân lẻ (1 ảnh), gói gia đình (5 ảnh), gói tầm soát tổ chức (100 ảnh) kèm giá tiền và số credit tương ứng. Lưu cache bảng giá vào Redis.
  * Triển khai endpoint tạo yêu cầu thanh toán (`POST /api/v1/billing/orders`): Sinh mã đơn hàng, tạo URL chuyển hướng sang cổng thanh toán mô phỏng Sandbox (VNPAY Sandbox / MoMo Sandbox / Stripe Test Mode).
  * Xây dựng endpoint tiếp nhận Webhook Callback từ cổng thanh toán (`POST /api/v1/billing/payments/callback`):
    * Xác minh tính toàn vẹn và chữ ký điện tử an toàn (HMAC SHA-256 Signature Verification).
    * Áp dụng khóa phân tán Redis (Redis Distributed Lock) trên mã đơn hàng để chống tấn công Replay Attack.
    * Ghi nhận giao dịch thành công và nạp đúng số credit vào ví người dùng trong một transaction duy nhất.
* **Đầu ra bàn giao**: Billing Controller & Services, Webhook Verification Handler, migration database bảng `billing.orders` và `billing.payments`, Swagger tài liệu thanh toán.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Tạo đơn hàng và thanh toán trên cổng Sandbox trả về callback hợp lệ $\rightarrow$ số dư credit trong ví tăng đúng bằng gói đã mua.
  * **AC2**: Giả lập hacker gửi lặp cùng 1 Webhook callback 5 lần liên tiếp: chỉ có đúng lần đầu tiên được xử lý và cộng tiền, 4 lần sau bị chặn bởi cơ chế Idempotency (trả về mã giao dịch đã xử lý, không cộng credit lần 2).
  * **AC3**: Webhook có chữ ký sai hoặc bị can thiệp payload lập tức bị từ chối với HTTP 400 Bad Request và ghi audit log cảnh báo an ninh.

#### Task W45-M1-06 | P0 | 14 giờ | Epic E7 Notification & Chat (SCRUM-267)
**Triển khai Dịch vụ Đẩy Thông báo Tức thời qua Firebase Cloud Messaging (FCM) Backend**
*(Nhiệm vụ chuyển giao từ Task 2 của M3 sang M1)*
* **Phối hợp kiểm tra**: M5 (Frontend Web Push Client).
* **Phụ thuộc**: File credential Firebase Service Account JSON (`aura-dev-service-account.json`).
* **Cần thực hiện**:
  * Tích hợp Firebase Admin .NET SDK vào dịch vụ Notification Service (`AURA.Notification.Service` hoặc tích hợp vào API Gateway / Event Dispatcher).
  * Xây dựng API quản lý danh bạ thiết bị người dùng:
    * `POST /api/v1/notifications/devices`: Đăng ký FCM Device Token của trình duyệt web người dùng sau khi được cấp quyền.
    * Cơ chế dọn dẹp token: Tự động vô hiệu hóa các token hết hạn hoặc báo lỗi `UNREGISTERED` từ Firebase.
  * Lắng nghe các sự kiện từ RabbitMQ exchange `aura.s3.events`:
    * Sự kiện `analysis.completed`: Tự động đẩy Web Push Notification: *"Kết quả phân tích mắt của bạn đã có! Bấm để xem chi tiết."* kèm deep-link dẫn tới trang kết quả.
    * Sự kiện `billing.payment.success`: Đẩy Web Push xác nhận nạp tiền/credit thành công.
  * Vận hành đa kênh: Mọi thông báo đều được lưu đồng thời vào cơ sở dữ liệu `notification.inbox` (để hiển thị chuông thông báo) và bắn qua FCM (để hiển thị popup trên hệ điều hành).
* **Đầu ra bàn giao**: Notification Service hoàn chỉnh có Firebase Admin SDK, Event Consumer lắng nghe RabbitMQ, API đăng ký device token, tài liệu hướng dẫn cấu hình FCM.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Khi một ca phân tích hoàn tất, trong vòng $< 1.5$ giây, trình duyệt của người dùng nhận được pop-up Web Push Notification từ Firebase ngay cả khi đang chuyển sang tab khác.
  * **AC2**: Click vào thông báo push điều hướng chính xác đến màn hình xem kết quả của đúng ca chụp đó.
  * **AC3**: Gửi thông báo đến token không tồn tại được xử lý an toàn (không crash consumer), token lỗi được tự động đánh dấu ngừng kích hoạt trong database.

#### Task W45-M1-07 | P0 | 10 giờ | Epic E8 Billing (SCRUM-268)
**Triển khai Billing Cấp Tổ chức (Clinic Packages) và Tự động Hoàn Credit**
*(Nhiệm vụ chuyển giao từ Task 3 của M3 sang M1)*
* **Phối hợp kiểm tra**: M2, M6.
* **Phụ thuộc**: W45-M1-05.
* **Cần thực hiện**:
  * Mở rộng mô hình ví Credit: Hỗ trợ ví cấp Tổ chức (Clinic Wallet) có hạn mức lớn, cho phép phân quyền người sở hữu thanh toán (Billing Owner) và cấp quyền sử dụng credit cho các Bác sĩ trong phòng khám.
  * Xây dựng Background Job đối soát tự động (Reconciliation Engine): Quét các reservation bị treo quá 30 phút do sự cố hệ thống để tự động giải phóng (Auto-release) trả lại credit cho phòng khám.
  * Triển khai API hoàn credit tự động phục vụ Batch Processing: Hỗ trợ lệnh hoàn trả credit theo danh sách ảnh lỗi (Batch Partial Refund) có kiểm soát idempotency.
* **Đầu ra bàn giao**: Module Clinic Billing, Background Service đối soát, migration ví tổ chức, báo cáo kiểm thử hoàn tiền.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Phòng khám mua gói 500 credit thành công; các bác sĩ thuộc phòng khám đó sử dụng credit để chụp ảnh và số dư ví tổ chức bị trừ chính xác.
  * **AC2**: Giả lập lỗi Worker crash khiến 3 reservation bị treo: Background Job quét sau 30 phút tự động release và hoàn 3 credit về ví, ghi log lý do `STALE_RESERVATION_AUTO_REFUND`.
  * **AC3**: Bác sĩ thuộc phòng khám A không thể sử dụng credit thuộc ví phòng khám B.

#### Task W45-M1-08 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Khả năng Phục hồi Hạ tầng & Billing/FCM, Ma trận Đối soát Tài chính và Bàn giao Runbook Sprint 4–5**
*(Hợp nhất Task 4 của M1 và Task 4 của M3)*
* **Phối hợp kiểm tra**: M2, M4, M5, M6.
* **Phụ thuộc**: W45-M1-01, W45-M1-04, W45-M1-05, W45-M1-06, W45-M1-07.
* **Cần thực hiện**:
  * Lập ma trận kiểm thử đối soát tài chính toàn diện:
    * Kiểm thử các trường hợp tranh chấp số dư (Concurrency Race Condition khi 10 luồng cùng gọi trừ 1 credit cuối cùng).
    * Đối soát số dư: Tổng số dư ví + tổng credit đã capture luôn cân bằng chính xác tuyệt đối với tổng credit đã nạp sau 100 giao dịch nạp và 500 ca phân tích.
  * Kiểm thử ngắt kết nối mạng giữa Notification Service và Google Firebase API; đảm bảo tin nhắn không bị mất mà được lưu trong Dead Letter Queue để retry.
  * Diễn tập sự cố hạ tầng Batch: Giả lập Worker crash giữa batch, RabbitMQ broker downtime tạm thời.
  * Cập nhật Ma trận Truy vết Yêu cầu (RTM) cho các yêu cầu hạ tầng, bảo mật và tài chính FR-10, FR-11, FR-12.
* **Đầu ra bàn giao**: Báo cáo kiểm thử tài chính & hạ tầng `docs/testing/m1_infrastructure_billing_resilience_report.md`, RTM cập nhật, Runbook vận hành tổng hợp Sprint 4–5.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: 10 request đồng thời tranh chấp 1 credit cuối cùng: đúng 1 request thành công, 9 request còn lại bị từ chối do không đủ số dư (`INSUFFICIENT_CREDITS`), ví không bao giờ bị âm.
  * **AC2**: Đối soát sau 100 giao dịch nạp và 500 ca phân tích: tổng số dư ví + tổng credit đã capture luôn cân bằng chính xác tuyệt đối với tổng credit đã nạp.
  * **AC3**: Toàn bộ hệ thống hồi phục tự động sau sự cố mà không làm mất mát tin nhắn trong hàng đợi hoặc thất thoát dữ liệu database.

---

### M2 — VÕ HỮU DUY (Analysis Pipeline, Worker Scaling, Doctor Review & Batch)

#### Task W45-M2-01 | P0 | 14 giờ | Epic E6 Doctor Workflow (SCRUM-266)
**Triển khai API Không gian làm việc Bác sĩ (Doctor Review & Revision)**
* **Phối hợp kiểm tra**: M5 (Frontend UI), M6 (Clinic Scope).
* **Phụ thuộc**: Không có task phụ thuộc; sử dụng module Analysis hiện hữu.
* **Cần thực hiện**:
  * Thiết kế và bổ sung các bảng lưu trữ đánh giá y khoa vào PostgreSQL:
    * `analysis_reviews`: Lưu chẩn đoán của bác sĩ, mức độ bệnh (ICDR 0–4), độ nghiêm trọng của mạch máu, khuyến nghị điều trị, thời gian review.
    * `analysis_revisions`: Lưu lịch sử các lần chỉnh sửa kèm thông tin bác sĩ thực hiện (Audit Trail).
  * Xây dựng nhóm API Review:
    * `GET /api/v1/analyses/{id}/workspace`: Lấy chi tiết ca chụp kèm URL ảnh gốc, ảnh mạch máu phân vùng (Overlay Mask) và các đánh giá trước đó.
    * `POST /api/v1/analyses/{id}/reviews`: Gửi kết luận đánh giá ban đầu của Bác sĩ.
    * `PUT /api/v1/analyses/{id}/reviews/{reviewId}`: Chỉnh sửa kết luận của Bác sĩ kèm cơ chế khóa lạc quan (Optimistic Concurrency Control qua `ETag` hoặc trường `version`) để ngăn chặn việc ghi đè chéo khi nhiều bác sĩ cùng mở một ca.
* **Đầu ra bàn giao**: Migration database, C# Entities, Repositories, Controllers trong `AURA.Analysis.Api`, bài test xUnit kiểm tra tính toàn vẹn dữ liệu.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Gọi API tạo và cập nhật review thành công, lưu đúng dữ liệu bác sĩ, mức độ bệnh và khuyến nghị điều trị vào PostgreSQL.
  * **AC2**: Cơ chế khóa lạc quan hoạt động chuẩn xác: Trả về HTTP 409 Conflict nếu gửi request update với `version` cũ hơn version đang có trong database.
  * **AC3**: Kết quả phân tích ban đầu của AI không bao giờ bị sửa đổi hay ghi đè; dữ liệu bác sĩ chỉnh sửa được lưu riêng biệt trong bảng review/revision.

#### Task W45-M2-02 | P0 | 14 giờ | Epic E4 Analysis Pipeline (SCRUM-264)
**Triển khai Xử lý Ảnh Hàng loạt (Batch Processing $\ge 100$ ảnh) qua RabbitMQ và Redis**
* **Phối hợp kiểm tra**: M1 (Redis/RabbitMQ), M5 (Frontend Batch UI).
* **Phụ thuộc**: M1 cấu hình xong Redis và RabbitMQ Scaling.
* **Cần thực hiện**:
  * Thiết kế API tiếp nhận lô ảnh lớn:
    * `POST /api/v1/batches`: Tiếp nhận danh sách metadata của lô ảnh (tối đa 100 ảnh/lượt), sinh `batch_id`, lưu thông tin sơ bộ vào PostgreSQL.
    * `GET /api/v1/batches/{batchId}`: Truy vấn tiến độ xử lý thời gian thực của toàn bộ batch.
  * Tích hợp Redis quản lý trạng thái Batch:
    * Sử dụng Redis Hash (`batch:{id}:progress`) để lưu trữ trạng thái tiến độ thời gian thực: tổng số ảnh, số ảnh đang xử lý, số ảnh hoàn tất, số ảnh lỗi.
    * Worker phân tích sau khi hoàn thành từng ảnh sẽ gọi atomic increment (`HINCRBY`) để cập nhật tiến độ tức thời, độ trễ $< 5$ms.
  * Nâng cấp Analysis Worker:
    * Tiêu thụ jobs từ RabbitMQ song song nhiều thread/container.
    * Cơ chế tự phục hồi: Nếu xử lý một ảnh bị lỗi (timeout hoặc ảnh hỏng), tự động đánh dấu ảnh đó lỗi trong batch, ghi log nguyên nhân và tiếp tục xử lý các ảnh còn lại mà không làm dừng cả batch.
* **Đầu ra bàn giao**: Batch Service & Controller, Worker Consumer nâng cấp xử lý batch, tài liệu API Batch.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Gửi batch gồm 100 ảnh hợp lệ: Toàn bộ 100 ảnh được xếp hàng vào RabbitMQ, các Worker xử lý song song và hoàn tất 100% trong thời gian quy định ($< 3$ phút trên môi trường chuẩn).
  * **AC2**: API `GET /api/v1/batches/{batchId}` trả về dữ liệu tiến độ chính xác từng giây khớp hoàn toàn với số lượng job đã hoàn thành trong Redis.
  * **AC3**: Gửi batch chứa 5 ảnh lỗi trong tổng số 50 ảnh: Hệ thống hoàn thành 45 ảnh đạt chuẩn, 5 ảnh lỗi được ghi nhận đúng mã lỗi; trạng thái batch chuyển thành `COMPLETED_WITH_ERRORS`.

#### Task W45-M2-03 | P0 | 10 giờ | Epic E4 Analysis Pipeline (SCRUM-264)
**Tích hợp Tự động hóa Thanh quyết toán Billing và Kết xuất Báo cáo PDF/CSV**
* **Phối hợp kiểm tra**: M1 (Billing API), M5 (Download Report UI).
* **Phụ thuộc**: M1 cung cấp API quản lý Credit và Sandbox.
* **Cần thực hiện**:
  * Tích hợp luồng kiểm tra và trừ credit tự động trước khi phân tích ảnh:
    * Khi tiếp nhận yêu cầu phân tích lẻ hoặc batch: Gọi Billing Service của M1 để tạm giữ credit (Reserve Credit).
    * Khi Worker phân tích thành công: Gửi sự kiện thanh quyết toán (Capture Credit).
    * Khi Worker phân tích thất bại do lỗi hệ thống hoặc ảnh không hợp lệ: Gửi sự kiện giải phóng (Release/Refund Credit) để bảo vệ quyền lợi người dùng.
  * Triển khai module kết xuất báo cáo y tế:
    * `GET /api/v1/analyses/{id}/report.pdf`: Sinh file PDF báo cáo kết quả y tế chuẩn (chứa thông tin bệnh nhân, ảnh mạch máu phân vùng, kết luận của AI, kết luận và chữ ký số của Bác sĩ).
    * `GET /api/v1/batches/{batchId}/export.csv`: Xuất bảng tổng hợp kết quả của toàn bộ lô ảnh dưới dạng file CSV phục vụ thống kê lâm sàng.
* **Đầu ra bàn giao**: C# PDF Generator (QuestPDF/SkiaSharp), CSV Exporter, Billing Integration Client, bộ test kiểm tra sinh file báo cáo.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: File PDF sinh ra đầy đủ thông tin, hiển thị rõ ràng ảnh mạch máu, đúng format y tế, dung lượng tối ưu ($< 1.5$ MB).
  * **AC2**: File CSV xuất ra chứa đầy đủ danh sách bệnh nhân, mã ảnh, kết quả phân loại rủi ro và mã bác sĩ đánh giá, mở được trên Excel không bị lỗi font UTF-8.
  * **AC3**: Tự động hoàn trả credit chính xác khi có ảnh bị lỗi phân tích, không có tình trạng trừ tiền mà không trả kết quả.

#### Task W45-M2-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Tích hợp Batch/Doctor Review, Concurrency và Cập nhật OpenAPI 0.8**
* **Phối hợp kiểm tra**: M1, M5.
* **Phụ thuộc**: W45-M2-01, W45-M2-02, W45-M2-03.
* **Cần thực hiện**:
  * Viết bộ integration test toàn diện bằng xUnit và Testcontainers:
    * Test kịch bản 2 Bác sĩ cùng cập nhật đồng thời 1 ca chụp (kiểm tra Concurrency Conflict HTTP 409).
    * Test xử lý batch 100 ảnh trong điều kiện ngắt kết nối RabbitMQ Worker tạm thời.
    * Test tính toàn vẹn dữ liệu transaction trong PostgreSQL.
  * Cập nhật và đồng bộ file hợp đồng `contracts/analysis-openapi-0.8.yaml` phản ánh đầy đủ các endpoint Batch, Doctor Review, Report Download và các mã lỗi mới.
* **Đầu ra bàn giao**: Suite test xUnit mới (`BatchIntegrationTests.cs`, `DoctorReviewTests.cs`), báo cáo kiểm thử tự động, OpenAPI 0.8.0 đồng bộ.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Toàn bộ các test cases mới pass 100% trên CI; không có lỗi dead-lock database khi chạy đồng thời 20 luồng.
  * **AC2**: Script `validate_openapi.py` kiểm tra schema khớp 100% với mã nguồn thực tế của controller.
  * **AC3**: Báo cáo kiểm thử thể hiện rõ các kịch bản biên (Edge cases) đã được bao phủ đầy đủ.

---

### M4 — ĐÀO DUY QUÂN (AI Core Specialist: Frangi v0.2, Risk Model, OCT & Stress Test)

#### Task W45-M4-01 | P0 | 14 giờ | Epic E5 AI Core (SCRUM-265)
**Tối ưu hóa Thuật toán Phân vùng Frangi v0.2 & Vectorization Ma trận Hessian**
* **Phối hợp kiểm tra**: M2 (Analysis Pipeline).
* **Phụ thuộc**: Sử dụng Container AI Core thật (`aura-ai77-539ab735c32d401c`).
* **Cần thực hiện**:
  * Tinh chỉnh thuật toán lọc dải mạch máu Frangi (Frangi Vesselness Filter):
    * Tinh chỉnh dải tham số scale $\sigma \in [1.0, 3.0]$, $\beta = 0.5$, $c = 15$ để tách biệt rõ nét các mao mạch võng mạc siêu nhỏ và loại bỏ nhiễu vùng rìa đồng tử.
    * Tối ưu hóa hiệu năng tính toán ma trận Hessian bằng thư viện vector hóa NumPy/SciPy, rút ngắn thời gian suy luận xuống mức $< 1.8$ giây/ảnh trên CPU thông thường.
  * Tinh chỉnh thuật toán nhị phân hóa cục bộ thích nghi (Adaptive Local Thresholding):
    * Giữ nguyên cấu trúc cây mạch máu liên tục, triệt tiêu đứt gãy tại các nhánh giao thoa mao mạch.
* **Đầu ra bàn giao**: Mã nguồn thuật toán cập nhật trong `AURA.AI`, container AI Core Frangi v0.2.1, báo cáo đánh giá độ chính xác phân vùng.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Thời gian xử lý trung bình một ảnh võng mạc kích thước $512 \times 512$ đạt dưới $1.8$ giây trên CPU thông thường.
  * **AC2**: Ảnh mặt nạ mạch máu nhị phân (Binary Mask) hiển thị rõ cấu trúc cây mạch máu, không bị đứt đoạn vô lý tại các nhánh chính.
  * **AC3**: Không gây rò rỉ bộ nhớ RAM sau khi xử lý liên tục 100 ảnh trong container.

#### Task W45-M4-02 | P0 | 14 giờ | Epic E5 AI Core (SCRUM-265)
**Xây dựng Mô hình Đánh giá Rủi ro Võng mạc Heuristic & Trích xuất Đặc trưng Hình thái học**
* **Phối hợp kiểm tra**: M2.
* **Phụ thuộc**: W45-M4-01.
* **Cần thực hiện**:
  * Xây dựng bộ trích xuất đặc trưng hình thái học mạch máu (Morphological Feature Extraction):
    * Mật độ mạch máu (Vessel Density Ratio): Tỷ lệ diện tích mao mạch trên toàn bộ diện tích đáy mắt.
    * Chỉ số uốn lượn (Vessel Tortuosity Index): Độ cong lượn của các nhánh mạch máu chính.
    * Vùng vô mạch hoàng điểm (Foveal Avascular Zone - FAZ): Phát hiện sự mở rộng bất thường của FAZ.
  * Xây dựng thuật toán phân loại rủi ro (Heuristic Retinal Risk Model):
    * Phân loại mức độ rủi ro thành 3 cấp độ chuẩn: `LOW` (Bình thường), `MODERATE` (Cần theo dõi), `HIGH` (Bất thường nghiêm trọng cần can thiệp y tế).
    * Tạo báo cáo giải thích đặc trưng (Explainable AI - XAI Features) trả về chi tiết tỷ lệ từng chỉ số để Bác sĩ tham khảo.
* **Đầu ra bàn giao**: Module `risk_assessment.py`, bộ test xác thực độ chính xác phân loại với tập dữ liệu mẫu.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Trích xuất chính xác các chỉ số mật độ mạch máu và độ uốn lượn từ mặt nạ phân vùng.
  * **AC2**: Hàm phân loại rủi ro trả về đúng 1 trong 3 nhãn chuẩn (`LOW`, `MODERATE`, `HIGH`) khớp với các chỉ số hình thái học tính toán được.
  * **AC3**: Đạt độ tương đồng chẩn đoán $\ge 90\%$ trên tập 20 ca mẫu kiểm chuẩn y tế.

#### Task W45-M4-03 | P0 | 10 giờ | Epic E5 AI Core (SCRUM-265)
**Đóng gói Hợp đồng AI Contract 1.3 & Khảo sát Tính khả thi Ảnh Cắt lớp Võng mạc OCT**
* **Phối hợp kiểm tra**: M2.
* **Phụ thuộc**: W45-M4-02.
* **Cần thực hiện**:
  * Đóng gói và nâng cấp Hợp đồng giao tiếp AI (AI Service Contract v1.3):
    * Bổ sung trường `risk_level`, `vessel_density_ratio`, `tortuosity_score` vào schema JSON phản hồi của AI Core.
    * Chuẩn hóa mã lỗi AI: `INVALID_IMAGE_DIMENSIONS`, `LOW_CONTRAST_REJECTED`, `INSUFFICIENT_RETINAL_AREA`.
  * Nghiên cứu khảo sát định dạng ảnh cắt lớp quang học võng mạc (Optical Coherence Tomography - OCT):
    * Khảo sát cấu trúc file DICOM/OCT (.dcm, .oct) dùng trong các dòng máy quét chuyên sâu.
    * Viết tài liệu đánh giá tính khả thi (Feasibility Study) và thiết kế kiến trúc tiền xử lý ảnh OCT cho các phiên bản tiếp theo.
* **Đầu ra bàn giao**: Schema `ai-contract-v1.3.json`, mã nguồn adapter AI Core v1.3, tài liệu khảo sát `docs/ai/oct_imaging_feasibility_study.md`.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Container AI Core trả về đúng schema v1.3; service Analysis phân tích thành công và parse dữ liệu không gặp lỗi validation.
  * **AC2**: Các trường hợp ảnh chụp mờ, sai góc chụp bị AI Core từ chối với đúng mã lỗi chuẩn hóa, không gây Exception 500.
  * **AC3**: Tài liệu khảo sát ảnh OCT hoàn thiện, có phân tích ưu nhược điểm và đề xuất lộ trình tích hợp rõ ràng.

#### Task W45-M4-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Tải AI Core (Stress Test), Đo đạc Benchmark và Hoàn thiện AI Model Card**
* **Phối hợp kiểm tra**: M1, M2.
* **Phụ thuộc**: W45-M4-01, W45-M4-02, W45-M4-03.
* **Cần thực hiện**:
  * Thực hiện bài kiểm thử tải chuyên sâu (AI Core Stress Test):
    * Bơm tải đồng thời 50 request phân tích ảnh võng mạc liên tục vào container AI Core.
    * Đo đạc và ghi nhận: Throughput (ảnh/phút), độ trễ phân vị p50/p95/p99, mức tiêu hao RAM và CPU.
  * Lập bảng so sánh Benchmark: So sánh hiệu năng và độ chính xác giữa Frangi v0.1 và Frangi v0.2.
  * Hoàn thiện Hồ sơ Mô hình Trí tuệ Nhân tạo (AI Model Card): Ghi nhận chi tiết kiến trúc giải thuật, dải tham số, giới hạn kỹ thuật và khuyến cáo sử dụng y tế.
* **Đầu ra bàn giao**: Báo cáo kiểm thử tải AI `docs/testing/M4_Sprint4_5_stress_test_report.md`, AI Model Card chuẩn `docs/ai/model_card_frangi_v0.2.md`.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Container AI Core chịu tải 50 request liên tục không bị crash (OOM), mức chiếm dụng bộ nhớ RAM duy trì $< 1.2$ GB, p95 độ trễ $< 2.2$ giây.
  * **AC2**: AI Model Card hoàn thiện đầy đủ các mục theo chuẩn Google Model Card Toolkit.
  * **AC3**: Tỷ lệ lỗi suy luận dưới tải cao đạt $0\%$.

---

### M5 — LÂM TRẦN NGUYỄN HOÀNG (Frontend Web: Doctor Workspace, Bulk Upload UI, Payment & FCM)

#### Task W45-M5-01 | P0 | 14 giờ | Epic E12 Frontend (SCRUM-272)
**Xây dựng Giao diện Không gian làm việc Bác sĩ (Doctor Workspace & Review UI)**
* **Phối hợp kiểm tra**: M2 (Backend Review API).
* **Phụ thuộc**: M2 cung cấp hợp đồng API Review.
* **Cần thực hiện**:
  * Xây dựng màn hình làm việc chuyên nghiệp cho Bác sĩ nhãn khoa (`/doctor/workspace/:analysisId`):
    * Khung hiển thị ảnh đôi (Side-by-Side Viewer): Cho phép đặt ảnh đáy mắt gốc bên cạnh ảnh đã phân vùng mạch máu.
    * Bộ công cụ tương tác ảnh: Phóng to/thu nhỏ (Zoom), xoay ảnh, kéo rê (Pan), và thanh trượt điều chỉnh độ trong suốt (Opacity Slider) của lớp phủ mạch máu.
    * Form kết luận chẩn đoán: Chọn mức độ bệnh võng mạc tiểu đường theo thang chuẩn ICDR (0: No DR, 1: Mild, 2: Moderate, 3: Severe, 4: Proliferative), nhập ghi chú lâm sàng và khuyến nghị điều trị.
  * Xử lý xung đột sửa đổi (Conflict Handling UI):
    * Nếu nhận mã lỗi HTTP 409 Conflict từ API, tự động hiển thị Modal thông báo: *"Ca chụp này vừa được cập nhật bởi một bác sĩ khác. Bạn có muốn tải lại dữ liệu mới nhất không?"*.
* **Đầu ra bàn giao**: React/TypeScript Components trong `aura-web` (`DoctorWorkspace.tsx`, `ImageCompareViewer.tsx`, `ReviewForm.tsx`), CSS/Tailwind styles.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Bác sĩ thao tác mượt mà các tính năng Zoom, Pan, Opacity Slider với tốc độ phản hồi 60fps trên trình duyệt thông thường.
  * **AC2**: Nộp form đánh giá thành công, hiển thị Toast thông báo và cập nhật trạng thái ca chụp sang `REVIEWED`.
  * **AC3**: Mô phỏng xung đột 409: Modal cảnh báo hiển thị rõ ràng, cho phép bấm nút "Tải lại dữ liệu" để xem nội dung bác sĩ kia vừa sửa.

#### Task W45-M5-02 | P0 | 14 giờ | Epic E12 Frontend (SCRUM-272)
**Xây dựng Giao diện Mua Gói cước, Thanh toán Sandbox và Tích hợp FCM Web Push**
* **Phối hợp kiểm tra**: M1 (Billing API Sandbox & FCM Backend).
* **Phụ thuộc**: M1 cung cấp API Billing, FCM & Config.
* **Cần thực hiện**:
  * Màn hình Bảng giá & Mua gói khám (`/pricing` hoặc `/billing/packages`):
    * Hiển thị danh thiếp các gói cước: Gói Cá nhân (1 lượt), Gói Gia đình (5 lượt), Gói Phòng khám Tầm soát (100 lượt) kèm danh sách quyền lợi và giá tiền.
    * Nút bấm "Thanh toán ngay": Gọi API của M1 tạo đơn và hiển thị giao diện mô phỏng cổng thanh toán Sandbox (với các nút bấm giả lập: Thanh toán thành công / Thanh toán thất bại / Chữ ký không hợp lệ).
  * Tích hợp Firebase Cloud Messaging Web Push Client:
    * Cấu hình Firebase JS SDK trong frontend (`firebase.ts`) và Service Worker (`firebase-messaging-sw.js`).
    * Banner xin quyền thông báo (Notification Permission Banner) thân thiện: *"Bật thông báo để nhận kết quả phân tích mắt ngay lập tức!"*.
    * Khi được cấp quyền: Lấy FCM Token và gửi lên backend qua API của M1 để lưu vào danh bạ thiết bị.
    * Hiển thị Toast thông báo nổi khi người dùng đang mở trang web, và kích hoạt Web Push của trình duyệt khi người dùng đang ở ứng dụng khác.
* **Đầu ra bàn giao**: Trang thanh toán, Modal Sandbox simulation, Service Worker Firebase, Component quản lý thông báo.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Người dùng chọn gói, bấm thanh toán Sandbox thành công $\rightarrow$ số dư credit trên thanh header tự động nhảy số tăng lên mà không cần F5 tải lại trang.
  * **AC2**: Trình duyệt yêu cầu và nhận thành công FCM Device Token, gửi token về backend của M1 thành công.
  * **AC3**: Khi backend bắn thông báo push: Nhận được thông báo cả khi đang duyệt web và khi đã thu nhỏ trình duyệt xuống thanh taskbar.

#### Task W45-M5-03 | P0 | 10 giờ | Epic E12 Frontend (SCRUM-272)
**Xây dựng Giao diện Tải ảnh Hàng loạt (Bulk Upload) & Bảng Theo dõi Tiến độ Batch**
* **Phối hợp kiểm tra**: M2 (Batch Processing API).
* **Phụ thuộc**: M2 cung cấp API Batch.
* **Cần thực hiện**:
  * Màn hình Tải ảnh hàng loạt (`/upload/batch`):
    * Khu vực kéo thả hỗ trợ chọn nhiều file cùng lúc (tối đa 100 ảnh, hỗ trợ PNG, JPG, JPEG, dung lượng mỗi file $\le 10$ MB).
    * Xem trước danh sách thumbnail các ảnh đã chọn, có nút xóa từng ảnh hoặc xóa toàn bộ trước khi bấm nút "Bắt đầu phân tích hàng loạt".
  * Màn hình Theo dõi tiến độ xử lý lô ảnh (`/batches/:batchId`):
    * Thanh tiến trình tổng quan (Overall Progress Bar): Thể hiện tỷ lệ phần trăm % đã hoàn thành.
    * Bảng danh sách chi tiết từng ảnh: Mã ảnh, tên file, trạng thái (Đang chờ / Đang phân tích / Hoàn tất / Lỗi), độ trễ xử lý.
    * Cơ chế tự động cập nhật: Polling định kỳ hoặc kết nối WebSocket/Server-Sent Events để cập nhật tiến độ mà không cần reload.
    * Nút tải kết quả: "Tải báo cáo tổng hợp CSV" và "Tải toàn bộ kết quả PDF (ZIP)".
* **Đầu ra bàn giao**: `BulkUploadView.tsx`, `BatchProgressView.tsx`, Custom hooks quản lý tiến độ batch.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Kéo thả đồng thời 50 ảnh: Giao diện nhận đủ 50 ảnh, hiển thị preview mượt mà không đơ lag trình duyệt.
  * **AC2**: Thanh tiến trình cập nhật nhảy số theo thời gian thực tương ứng với từng ảnh hoàn thành trong backend.
  * **AC3**: Khi hoàn tất 100%, hiển thị nút tải báo cáo; bấm nút tải về file CSV mở ra có đầy đủ danh sách kết quả.

#### Task W45-M5-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Toàn diện UI Doctor/Payment/Batch và Cập nhật Design Documentation**
* **Phối hợp kiểm tra**: M2, M6.
* **Phụ thuộc**: W45-M5-01, W45-M5-02, W45-M5-03.
* **Cần thực hiện**:
  * Viết bộ kiểm thử giao diện đầu cuối (E2E Tests) sử dụng Cypress hoặc Playwright:
    * Test luồng nạp credit $\rightarrow$ Tải batch 10 ảnh $\rightarrow$ Xem tiến trình hoàn tất.
    * Test luồng Bác sĩ mở workspace $\rightarrow$ So sánh ảnh $\rightarrow$ Lưu kết luận đánh giá.
  * Kiểm thử tương thích đa trình duyệt (Chrome, Firefox, Safari, Edge) và độ phản hồi trên màn hình độ phân giải Retina.
  * Cập nhật tài liệu thiết kế giao diện (UI Design System, Storybook hoặc Figma Tokens nếu có) và cập nhật tài liệu hướng dẫn người dùng.
* **Đầu ra bàn giao**: Bộ kịch bản E2E Test tự động, video ghi lại quá trình chạy test, tài liệu hướng dẫn sử dụng giao diện (`docs/ui/user_guide.md`).
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Toàn bộ kịch bản E2E Test chạy tự động pass 100% không có lỗi flakiness.
  * **AC2**: Giao diện hiển thị sắc nét, không vỡ layout trên các độ phân giải màn hình từ 1366x768 đến 4K.
  * **AC3**: Tài liệu hướng dẫn sử dụng có ảnh chụp minh họa từng bước rõ ràng, dễ hiểu cho Bác sĩ và Kỹ thuật viên.

---

### M6 — THÀNH VIÊN MỚI (Identity, Profile, Clinic & Chiến dịch Tầm soát)

#### Task W45-M6-01 | P0 | 14 giờ | Epic E3 Profile & Clinic (SCRUM-263)
**Triển khai Quản lý Vòng đời Phòng khám (Clinic Lifecycle) và Quản lý Hội viên**
* **Phối hợp kiểm tra**: M1 (Gateway Auth).
* **Phụ thuộc**: Không có task phụ thuộc; sử dụng module Profile/Identity hiện hữu.
* **Cần thực hiện**:
  * Thiết kế schema và migration các bảng phòng khám trong PostgreSQL:
    * `clinics`: ID phòng khám, tên, mã số giấy phép hoạt động, địa chỉ, số điện thoại, trạng thái (`PENDING_APPROVAL`, `ACTIVE`, `SUSPENDED`).
    * `clinic_members`: Liên kết giữa tài khoản người dùng và phòng khám kèm vai trò nội bộ (`CLINIC_ADMIN`, `DOCTOR`, `TECHNICIAN`, `BILLING_STAFF`).
  * Xây dựng nhóm API Quản lý Vòng đời Clinic:
    * `POST /api/v1/clinics`: Đăng ký thành lập phòng khám mới (trạng thái ban đầu chờ Admin duyệt).
    * `PUT /api/v1/admin/clinics/{id}/approve`: System Admin xét duyệt và kích hoạt phòng khám.
    * `POST /api/v1/clinics/{id}/members`: Quản trị viên phòng khám mời Bác sĩ/Nhân viên tham gia.
    * `DELETE /api/v1/clinics/{id}/members/{userId}`: Xóa hoặc vô hiệu hóa quyền thành viên khỏi phòng khám.
* **Đầu ra bàn giao**: Migration database phòng khám, C# Controllers & Services trong `AURA.Profile.Api`, Swagger tài liệu Clinic API.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Đăng ký phòng khám mới thành công; System Admin thực hiện duyệt và kích hoạt phòng khám chuyển sang `ACTIVE`.
  * **AC2**: Mời bác sĩ vào phòng khám thành công; bác sĩ đăng nhập nhận được đúng token chứa claim `clinic_id` và role `DOCTOR`.
  * **AC3**: Khi xóa bác sĩ khỏi phòng khám, bác sĩ đó lập tức mất quyền truy cập vào dữ liệu và bệnh nhân của phòng khám đó.

#### Task W45-M6-02 | P0 | 14 giờ | Epic E3 Profile & Clinic (SCRUM-263)
**Triển khai Quản lý Bệnh nhân Phòng khám (Clinic Patients) và Phân công Bác sĩ**
* **Phối hợp kiểm tra**: M2 (Doctor Workspace), M5 (Frontend).
* **Phụ thuộc**: W45-M6-01.
* **Cần thực hiện**:
  * Thiết kế bảng quản lý hồ sơ bệnh nhân thuộc cơ sở y tế:
    * `clinic_patients`: Mã hồ sơ nội bộ (MRN - Medical Record Number), họ tên, ngày sinh, giới tính, tiền sử bệnh lý tiểu đường/tim mạch, thời gian tiếp nhận.
    * `doctor_assignments`: Bảng phân công ca chụp/bệnh nhân cho một bác sĩ cụ thể chịu trách nhiệm đọc kết quả.
  * Xây dựng nhóm API Quản lý Bệnh nhân & Phân công:
    * `POST /api/v1/clinics/{clinicId}/patients`: Tiếp nhận và tạo hồ sơ bệnh nhân mới vào cơ sở dữ liệu phòng khám.
    * `GET /api/v1/clinics/{clinicId}/patients`: Tra cứu danh sách bệnh nhân có hỗ trợ phân trang, tìm kiếm theo tên hoặc mã MRN.
    * `POST /api/v1/clinics/{clinicId}/assignments`: Trưởng phòng khám phân công danh sách ca chụp cho Bác sĩ phụ trách; tự động bắn sự kiện phân công để thông báo cho bác sĩ.
* **Đầu ra bàn giao**: Clinic Patient Controller, Assignment Engine, migration cơ sở dữ liệu, bộ test xUnit.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Tạo và tra cứu hồ sơ bệnh nhân nhanh chóng; dữ liệu bệnh nhân được cô lập tuyệt đối theo `clinic_id`.
  * **AC2**: Phân công 5 ca chụp cho Bác sĩ X: Bác sĩ X đăng nhập vào màn hình làm việc thấy đúng 5 ca đó trong danh sách việc cần làm (Doctor Worklist).
  * **AC3**: Bác sĩ thuộc phòng khám khác không thể xem hồ sơ bệnh nhân nếu không được phân công hoặc không cùng chi nhánh.

#### Task W45-M6-03 | P0 | 10 giờ | Epic E3 Profile & Clinic (SCRUM-263)
**Triển khai Phân hệ Chiến dịch Tầm soát Sức khỏe Mắt (Screening Campaign API & UI)**
* **Phối hợp kiểm tra**: M2 (Batch Processing), M5 (Frontend).
* **Phụ thuộc**: W45-M6-01, W45-M6-02.
* **Cần thực hiện**:
  * Xây dựng mô hình Chiến dịch tầm soát (`screening_campaigns`): Cho phép các tổ chức y tế phát động các đợt khám mắt cộng đồng hoặc khám định kỳ doanh nghiệp:
    * Tên chiến dịch, đơn vị tổ chức, ngày bắt đầu, ngày kết thúc, số lượng bệnh nhân dự kiến, trạng thái (`PLANNED`, `RUNNING`, `CONCLUDED`).
  * API quản lý và liên kết:
    * `POST /api/v1/clinics/{clinicId}/campaigns`: Khởi tạo chiến dịch tầm soát mới.
    * `POST /api/v1/campaigns/{campaignId}/batches`: Liên kết các lô ảnh chụp (Batch) từ đợt khám thực địa vào chiến dịch tầm soát tương ứng.
    * `GET /api/v1/campaigns/{campaignId}/summary`: Báo cáo thống kê tổng quan của chiến dịch: Tổng số người khám, tỷ lệ phát hiện bất thường mạch máu, số ca chuyển viện khẩn cấp.
* **Đầu ra bàn giao**: Campaign Controller, Campaign Summary Service, migration database, tài liệu OpenAPI tương ứng.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Tạo chiến dịch thành công và gắn được các Batch ảnh chụp từ buổi khám thực địa vào chiến dịch.
  * **AC2**: API thống kê trả về chính xác số liệu phân loại bệnh nhân theo các mức độ rủi ro y tế.
  * **AC3**: Chiến dịch đã kết thúc (`CONCLUDED`) thì chuyển sang chế độ chỉ đọc (Read-only), ngăn chặn việc thêm ảnh hoặc sửa đổi kết quả trái phép.

#### Task W45-M6-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Bảo mật Phân quyền Đa Tenant, Kiểm toán Quyền và Bàn giao Thiết kế**
* **Phối hợp kiểm tra**: M1, M2.
* **Phụ thuộc**: W45-M6-01, W45-M6-02, W45-M6-03.
* **Cần thực hiện**:
  * Xây dựng bộ test bảo mật đa tầng (Security Multi-Tenant Matrix Tests):
    * Kiểm thử tấn công vượt quyền ngang (Horizontal Privilege Escalation): Bác sĩ Clinic A tìm cách đọc/ghi hồ sơ bệnh nhân của Clinic B $\rightarrow$ Bắt buộc trả về HTTP 403 Forbidden.
    * Kiểm thử tấn công vượt quyền dọc (Vertical Privilege Escalation): Bác sĩ thường tìm cách gọi API xét duyệt Clinic của Admin $\rightarrow$ Bắt buộc trả về HTTP 403 Forbidden.
  * Kiểm toán bảo mật dữ liệu định danh y tế (HIPAA / GDPR Compliance Review): Đảm bảo thông tin định danh bệnh nhân (họ tên, ngày sinh, số điện thoại) được che mờ (masking) trong nhật ký log của hệ thống.
  * Bàn giao sơ đồ cơ sở dữ liệu quan hệ (ERD) hoàn chỉnh của phân hệ Profile, Clinic và Campaign.
* **Đầu ra bàn giao**: Báo cáo kiểm thử bảo mật `docs/security/multitenancy_audit_report.md`, bộ test tự động xUnit, file sơ đồ cơ sở dữ liệu Mermaid / PlantUML.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: 100% các kịch bản cố tình truy cập trái phép giữa các phòng khám đều bị chặn đứng và ghi nhận vào Security Audit Log.
  * **AC2**: Không có bất kỳ thông tin nhạy cảm (PII) nào của bệnh nhân bị ghi rõ trong Console log hoặc file log ứng dụng.
  * **AC3**: Sơ đồ ERD thể hiện rõ ràng toàn bộ các mối quan hệ 1-N, N-N, các khóa ngoại và ràng buộc toàn vẹn dữ liệu.

---

## 7. MA TRẬN ĐỐI CHIẾU NGHIỆM THU TÍCH HỢP CUỐI SPRINT 4–5

Cuối Sprint 4–5, cả nhóm sẽ chạy buổi Diễn tập Nghiệm thu Tích hợp (Integration Acceptance Demo) bao phủ 12 kịch bản liên dịch vụ sau:

| Mã | Tình huống Nghiệm thu Tích hợp | Kết quả Thực tế Cần Chứng minh | Thành viên Phụ trách |
|:---:|---|---|:---:|
| **S45 01** | **Batch Upload $\ge 50$ ảnh qua Web** | Kéo thả 50 ảnh $\rightarrow$ Batch tiếp nhận $\rightarrow$ Worker xử lý song song $\rightarrow$ UI cập nhật thanh tiến trình từng ảnh qua Redis $\rightarrow$ Hoàn tất tải báo cáo PDF/CSV. | **M5, M2, M1** |
| **S45 02** | **Camera NiFi Ingestion & Quarantine** | Bơm ảnh camera qua NiFi $\rightarrow$ Tiếp nhận HTTP 202 $\rightarrow$ Ảnh đúng vào phân tích; ảnh cố tình làm hỏng tự động rẽ nhánh vào thư mục Quarantine có ghi rõ lý do. | **M1, M2** |
| **S45 03** | **Doctor Review & Lớp phủ Mạch máu** | Bác sĩ mở workspace xem ảnh so sánh song song $\rightarrow$ Điều chỉnh lớp phủ Overlay $\rightarrow$ Nhập chẩn đoán $\rightarrow$ Lưu revision thành công mà không ghi đè kết quả AI gốc. | **M5, M2, M6** |
| **S45 04** | **Xung đột Đánh giá Bác sĩ (Conflict)** | Hai tab cùng mở 1 ca phân tích: Tab 1 nộp review thành công; Tab 2 nộp đè lên bị chặn bởi Optimistic Locking (HTTP 409) và hiển thị modal cảnh báo. | **M2, M5** |
| **S45 05** | **Thanh toán Sandbox & Nạp Credit** | Chọn gói khám $\rightarrow$ Chuyển tiếp cổng thanh toán Sandbox $\rightarrow$ Callback webhook xác minh HMAC thành công $\rightarrow$ Ví cộng đúng số credit, chống cộng tiền 2 lần khi gửi lặp callback. | **M1, M5** |
| **S45 06** | **Thông báo Đẩy Firebase (FCM Web Push)** | Sau khi phân tích xong ảnh hoặc nạp credit thành công $\rightarrow$ Trình duyệt web dù đang ở tab khác vẫn nhận được popup Web Push tức thời $(< 1.5s)$. | **M1, M5** |
| **S45 07** | **Quyết toán Credit & Hoàn tiền Tự động** | Batch 10 ảnh có 8 ảnh đạt và 2 ảnh bị từ chối chất lượng (422 Low Quality) $\rightarrow$ Quyết toán chính xác 8 credit, tự động hoàn trả 2 credit về ví, không thất thoát. | **M1, M2** |
| **S45 08** | **Đa Tenant Phòng khám (Multi-Tenancy)** | Đăng nhập tài khoản Bác sĩ Clinic A $\rightarrow$ Thử truy cập ID bệnh nhân hoặc ca chụp của Clinic B $\rightarrow$ Hệ thống chặn đứng với mã lỗi HTTP 403 Forbidden. | **M6, M2, M5** |
| **S45 09** | **Chiến dịch Tầm soát (Campaign)** | Tạo chiến dịch tầm soát cộng đồng $\rightarrow$ Nạp batch ảnh vào chiến dịch $\rightarrow$ Xem bảng tổng kết tỷ lệ bất thường mạch máu theo chiến dịch. | **M6, M2, M5** |
| **S45 10** | **Giám sát Prometheus & Grafana Live** | Mở Grafana xem được biểu đồ thời gian thực: Throughput của Gateway, độ trễ của AI Core ($< 1.8s$), và số lượng jobs đang được xử lý trong các hàng đợi RabbitMQ. | **M1** |
| **S45 11** | **Cảnh báo Tự động (Alertmanager)** | Cố tình tạo tải lỗi HTTP 500 $\rightarrow$ Alertmanager phát hiện tỷ lệ lỗi $> 2\%$ và lập tức bắn cảnh báo `FIRING` lên hệ thống giám sát. | **M1** |
| **S45 12** | **Diễn tập Khôi phục Thảm họa (Backup/Restore)** | Chạy script khôi phục cơ sở dữ liệu từ bản backup gần nhất $\rightarrow$ Dữ liệu receipts, lịch sử khám và ví credit được phục hồi toàn vẹn 100% trong vòng $< 15$ phút. | **M1** |

---

## 8. Tiêu chí Kết thúc Sprint 4–5

Sprint 4–5 được nghiệm thu hoàn thành khi và chỉ khi:
1. Toàn bộ **24 task tích hợp** (19 task P0 và 5 task P1) đều đạt đầy đủ tiêu chí nghiệm thu (DoD) và có bằng chứng xác thực đính kèm.
2. Cả **12 kịch bản nghiệm thu tích hợp** trong ma trận trên đều chạy thành công trên môi trường tích hợp chung.
3. Toàn bộ **7 công nghệ cốt lõi** đều đang hoạt động ổn định:
   * **OpenAPI**: 100% endpoint có tài liệu Swagger chuẩn hóa.
   * **Cache Redis**: Vận hành lưu cache bảng giá, rate-limit và khóa idempotency do M1 quản lý.
   * **Firebase FCM**: Bắn được Web Push tức thời tới trình duyệt người dùng qua Firebase Admin SDK của M1.
   * **Apache NiFi**: Luồng camera chạy ổn định có Dedup và Quarantine do M1 làm chủ hạ tầng.
   * **Microservices & API Gateway**: Định tuyến an toàn với Service JWT RS256.
   * **RabbitMQ Broker**: Cụm Worker xử lý mượt mà batch $\ge 100$ ảnh song song.
   * **Prometheus & Grafana**: Giám sát thời gian thực toàn bộ cụm dịch vụ.
4. Toàn bộ mã nguồn được merge sạch sẽ vào nhánh chính `develop`, không có conflict và toàn bộ CI/CD Pipeline chạy xanh.
