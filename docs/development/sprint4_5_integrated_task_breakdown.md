# AURA Phân công Sprint 4–5 Kết hợp (Sprint Tích hợp Tăng tốc)
**Mở rộng Batch Processing, Doctor Review, Thanh toán Sandbox/FCM, Clinic Đa Tenant và Giám sát với Prometheus & Grafana**

> **Ngày lập**: 30/10/2026 | **6 thành viên** | **24 task tích hợp** | **288 giờ dự kiến** (2 tuần gộp)  
> **Phạm vi gộp**: Hợp nhất toàn bộ khối lượng công việc của Sprint 4 và Sprint 5 để đẩy nhanh tiến độ về đích.  
> **Nguyên tắc kỹ thuật**: Đảm bảo 100% các công nghệ theo yêu cầu đề tài đều được tích hợp đầy đủ và chạy thực tế: **OpenAPI/Swagger, Cache Redis, Firebase Cloud Messaging (FCM), Apache NiFi, Microservices - API Gateway, RabbitMQ, và Monitoring với Prometheus & Grafana**.

---

## 1. Mục tiêu bàn giao

1. **Từ 1 ảnh lên Xử lý Hàng loạt (Batch Processing $\ge 100$ ảnh)**: Web Bulk Upload hoặc Camera NiFi $\rightarrow$ Gateway $\rightarrow$ Analysis tiếp nhận Batch $\rightarrow$ RabbitMQ điều phối song song $\rightarrow$ Cụm Worker phân tích qua AI Frangi $\rightarrow$ Cập nhật tiến độ từng ảnh qua Redis $\rightarrow$ Lưu kết quả an toàn.
2. **Không gian làm việc của Bác sĩ (Doctor Workspace & Review)**: Bác sĩ xem danh sách bệnh nhân được phân công, so sánh ảnh gốc và ảnh phân vùng, chỉnh sửa kết luận y khoa (Optimistic Concurrency) và kết xuất báo cáo y tế định dạng PDF/CSV.
3. **Thanh toán Sandbox, Gói cước & Thông báo đẩy Firebase (FCM)**: Người dùng mua gói khám, thanh toán Sandbox có xác minh chữ ký Webhook; tự động cộng credit và gửi Web Push Notification tức thời qua Firebase Cloud Messaging (FCM) khi phân tích xong hoặc giao dịch thành công.
4. **Quản lý Phòng khám (Clinic Lifecycle) & Đa Tenant**: Đăng ký, xét duyệt Clinic, phân công Bác sĩ (Doctor Assignment), quản lý bệnh nhân tổ chức và khởi tạo Chiến dịch tầm soát (Campaign) với cơ chế cô lập dữ liệu tuyệt đối.
5. **Hạ tầng Giám sát Hoàn chỉnh (Monitoring with Prometheus & Grafana)**: Thu thập metrics toàn diện từ API Gateway, Analysis API, Billing API, AI Core, RabbitMQ Exporter và Postgres Exporter; hiển thị trực quan trên Grafana Dashboards thời gian thực và cấu hình cảnh báo sự cố.

---

## 2. Phân công và Ngân sách Công việc

| Mã | Thành viên | Phụ trách Sprint 4–5 Kết hợp | Task | Giờ P0 | Giờ P1 | Tổng giờ |
|:---:|---|---|:---:|:---:|:---:|:---:|
| **M1** | Nguyễn Trương Hậu | Hạ tầng, Monitoring Prometheus/Grafana, Redis Cache & DevOps | 4 | 38 | 10 | 48 |
| **M2** | Võ Hữu Duy | Analysis Pipeline, Worker Scaling, Doctor Review & Batch PDF | 4 | 38 | 10 | 48 |
| **M3** | Thanh Dat | Billing Sandbox, Credit Ledger, Clinic Package & Firebase FCM | 4 | 38 | 10 | 48 |
| **M4** | Đào Duy Quân | Tối ưu Frangi, Heuristic Risk Model, NiFi Dedup/Quarantine & AI 1.3 | 4 | 38 | 10 | 48 |
| **M5** | Lâm Trần Nguyễn Hoàng | Frontend Doctor Workspace, Bulk Upload Batch UI & FCM Web Push | 4 | 38 | 10 | 48 |
| **M6** | Thành viên mới | Identity Profile, Clinic Lifecycle, Doctor Assignment & Campaign | 4 | 38 | 10 | 48 |
| **TỔNG** | **Cả nhóm 6 người** | **Hệ thống Toàn diện Sprint 4–5** | **24** | **228** | **60** | **288** |

*Ghi chú*: Mỗi thành viên đảm nhiệm **4 task tích hợp lớn** tương đương 48 giờ làm việc trong 2 tuần (3 task P0 bắt buộc để nghiệm thu luồng nghiệp vụ và 1 task P1 về kiểm thử tải, bảo mật chuyên sâu hoặc tối ưu hóa).

---

## 3. Kết quả Kế thừa và Phạm vi Phải Bảo toàn

* Kế thừa toàn bộ kết quả đã nghiệm thu của Sprint 3: Container AI Core Frangi v0.2 thật (`aura-ai77-539ab735c32d401c`), RabbitMQ vhost `/aura-s3` với 11 quorum queues, flow NiFi nhận ảnh camera qua HTTP 202, và PostgreSQL Sandbox (`PASS87S_PG_SANDBOX_READY`).
* Bảo toàn toàn bộ dữ liệu lịch sử của fixture 08; các bài thử nghiệm Batch lớn và nạp tiền Sandbox phải dùng fixture mới có `MinOccurredAtUtc = "2026-10-28T00:00:00Z"`.
* Tuyệt đối không xóa volumes, không sửa trực tiếp database của service khác ngoài quyền sở hữu, không lưu trữ khóa bí mật hoặc thông tin định danh bệnh nhân trong log và Git.

---

## 4. Thứ tự Triển khai (Lịch trình 2 tuần gộp: Ngày 1 – Ngày 14)

| Mốc Thời gian | Nội dung công việc phối hợp cần bàn giao | Owner điều phối |
|---|---|---|
| **Ngày 1–2** | • Dựng stack Prometheus & Grafana, cào metrics cơ bản.<br>• Chốt OpenAPI 0.8 cho Doctor Review, Batch, Payment và Clinic.<br>• M1 cấp Redis Cache tập trung và cấu hình RabbitMQ Worker Scaling. | M1, M2, M3, M6 |
| **Ngày 3–5** | • M2 dựng Doctor Workspace API & Batch Processing Engine.<br>• M3 tích hợp Firebase Admin SDK phát FCM Web Push & Cổng thanh toán Sandbox.<br>• M4 nâng cấp NiFi InvokeHTTP, Dedup và tối ưu tốc độ Frangi.<br>• M6 hoàn thành API phân quyền Clinic và Doctor Assignment. | M2, M3, M4, M6 |
| **Ngày 6–8** | • M5 kết nối giao diện Doctor Workspace với API thật của M2.<br>• M5 tích hợp FCM Service Worker trên Web nhận thông báo tức thời.<br>• M1 hoàn thành Dashboards Grafana chuyên sâu (System, Services, RabbitMQ). | M1, M2, M3, M5 |
| **Ngày 9–11** | • Thử nghiệm chạy Batch lớn $\ge 100$ ảnh từ Web Bulk Upload và NiFi.<br>• Kiểm tra cơ chế tự động Reserve $\rightarrow$ Capture $\rightarrow$ Release Credit và hoàn tiền khi ảnh lỗi.<br>• M6 triển khai module Campaign tầm soát gắn kết quả Batch. | Cả nhóm |
| **Ngày 12–13** | • Diễn tập sự cố: Worker crash giữa batch, broker downtime, phục hồi backup DB.<br>• Kiểm thử đa tenant: Bác sĩ Clinic A không thể xem bệnh nhân Clinic B.<br>• Đo đạc NFR: Thời gian xử lý AI, độ trễ hiển thị Dashboard Grafana, độ trễ FCM. | M1, M3, M6 |
| **Ngày 14** | • Tổng hợp báo cáo kiểm thử, chốt RTM, hoàn thiện Runbook và nghiệm thu toàn bộ 24 task. | Cả nhóm |

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

### M1 — NGUYỄN TRƯƠNG HẬU (Kiến trúc, Hạ tầng, Monitoring & DevOps)

#### Task W45-M1-01 | P0 | 14 giờ | Epic E1 Platform/DevOps (SCRUM-261)
**Triển khai Cụm Giám sát Prometheus & Dashboards Grafana Toàn diện**
* **Phối hợp kiểm tra**: M2, M3.
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
* **Đầu ra bàn giao**: File cấu hình `redis.conf`, Compose scaling template, script kiểm thử tải hàng đợi `Test-RabbitMqScaling.ps1`, tài liệu thông số cache.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Redis khởi động an toàn, có đặt password, client từ Gateway và Billing kết nối đọc/ghi cache thành công với độ trễ $< 2ms$.
  * **AC2**: Khi đẩy 100 jobs đồng thời vào queue `aura.s3.analysis.jobs`, cụm 2–4 Worker chia tải đồng đều (tải xử lý phân bổ trên cả 4 container Worker), không có job nào bị treo vô hạn.
  * **AC3**: Khởi động lại Redis (restart container) không làm mất dữ liệu cache quan trọng nhờ cấu hình AOF persistence.

#### Task W45-M1-03 | P0 | 12 giờ | Epic E11 Security & Compliance (SCRUM-271)
**Tự động hóa Sao lưu/Phục hồi Dữ liệu & Hoàn thiện Bảo mật Liên dịch vụ**
* **Phối hợp kiểm tra**: M3, M6.
* **Phụ thuộc**: Không có task phụ thuộc.
* **Cần thực hiện**:
  * Viết script tự động hóa backup hàng ngày (Automated Scheduled Backup) cho toàn bộ cơ sở dữ liệu PostgreSQL (`identity`, `analysis`, `billing`, `reporting`) bằng `pg_dump` dạng custom format nén (`.dump`).
  * Tự động sao lưu metadata của Private Storage (MinIO / Azure Blob) và mã hóa file backup bằng AES-256 trước khi lưu vào vùng lưu trữ cách ly.
  * Diễn tập kịch bản phục hồi thảm họa (Disaster Recovery Drill): Giả lập xóa trắng database, chạy script khôi phục toàn bộ dữ liệu từ bản backup gần nhất, xác minh RPO $\le 1$ giờ và RTO $\le 30$ phút.
  * Hoàn thiện cơ chế xoay vòng khóa Service Auth JWT (Key Rotation): Hỗ trợ nạp song song Public Key mới và Public Key cũ trong giai đoạn chuyển giao để không làm gián đoạn các request đang xử lý.
* **Đầu ra bàn giao**: Thư mục script `scripts/backup/Backup-AuraDatabase.ps1`, `Restore-AuraDatabase.ps1`, biên bản diễn tập phục hồi thảm họa, tài liệu chính sách bảo mật RPO/RTO.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Chạy script backup tạo ra file nén `.dump` hợp lệ, checksum SHA-256 nhất quán và tự động xóa bản sao lưu cũ vượt quá thời gian retention (7 ngày).
  * **AC2**: Quy trình phục hồi dữ liệu từ bản backup diễn ra thành công trong vòng $< 15$ phút, toàn bộ dữ liệu receipts, analysis và billing được khôi phục nguyên vẹn 100%.
  * **AC3**: Xoay vòng khóa JWT thành công mà không gây lỗi HTTP 401 cho các Worker và dịch vụ đang kết nối.

#### Task W45-M1-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Tải Hạ tầng Batch, Diễn tập Sự cố và Bàn giao Runbook Sprint 4–5**
* **Phối hợp kiểm tra**: M2, M4.
* **Phụ thuộc**: W45-M1-01, W45-M1-02, W45-M1-03.
* **Cần thực hiện**:
  * Thiết lập kịch bản kiểm thử tải hạ tầng: Bơm đồng thời 100 ảnh dung lượng lớn qua NiFi và Web API; theo dõi tài nguyên RAM, CPU, Swap và Network I/O trên Grafana.
  * Diễn tập kịch bản sự cố mất kết nối mạng giữa Worker và RabbitMQ Broker giữa lúc đang xử lý batch; kiểm tra cơ chế NACK và đưa message về DLQ an toàn.
  * Cập nhật toàn diện Sổ tay Vận hành (Operations Runbook): Hướng dẫn khởi động cụm Monitoring, xử lý cảnh báo Alertmanager, tra cứu cache Redis và quy trình khôi phục sự cố.
* **Đầu ra bàn giao**: Báo cáo kiểm thử tải hạ tầng `docs/testing/m1_batch_infrastructure_stress_report.md`, Runbook vận hành cập nhật `docs/development/operations_runbook_v1.2.md`.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Hệ thống chịu tải 100 ảnh liên tục không bị tràn bộ nhớ (RAM không vượt quá ngưỡng trần 80%), không có container nào bị OOMKilled.
  * **AC2**: Khi ngắt broker đột ngột, Worker ghi nhận lỗi có kiểm soát, không phát sinh duplicate billing và tự động tái kết nối thành công khi broker online trở lại.
  * **AC3**: Một kỹ sư khác có thể đọc Runbook và tự khởi động, vận hành cũng như đọc hiểu toàn bộ thông số trên Grafana Dashboards.

---

### M2 — VÕ HỮU DUY (Analysis Pipeline, Worker, Doctor Review & Batch)

#### Task W45-M2-01 | P0 | 14 giờ | Epic E6 Doctor Workflow (SCRUM-266)
**Triển khai API Không gian làm việc Bác sĩ (Doctor Review & Revision)**
* **Phối hợp kiểm tra**: M5 (Frontend), M6 (Quyền Doctor).
* **Phụ thuộc**: M6 bàn giao hợp đồng phân quyền Doctor Assignment.
* **Cần thực hiện**:
  * Xây dựng nhóm API phục vụ Bác sĩ:
    * `GET /api/v1/doctor/worklist`: Lấy danh sách bệnh nhân và các ca chụp cần review được phân công cho bác sĩ hiện tại.
    * `GET /api/v1/doctor/analyses/{id}`: Xem chi tiết ca phân tích (ảnh gốc, kết quả phân vùng AI, chỉ số mạch máu, khuyến nghị sơ bộ).
    * `POST /api/v1/doctor/analyses/{id}/reviews`: Gửi kết quả đánh giá chuyên môn của Bác sĩ (xác nhận kết quả, chỉnh sửa độ nặng, ghi chú lâm sàng).
  * Thực thi nguyên tắc bảo toàn dữ liệu y tế: Kết quả AI gốc (`AI Result`) được đóng băng bất biến; kết quả của Bác sĩ được lưu thành một bản ghi sửa đổi riêng (`Doctor Revision`) có versioning rõ ràng.
  * Cơ chế Optimistic Concurrency Control: Kiểm tra `row_version` khi lưu review; nếu có hai bác sĩ cùng mở một ca và nộp đè lên nhau, request sau sẽ bị từ chối với lỗi HTTP `409 Conflict`.
* **Đầu ra bàn giao**: Migration database lưu bảng `analysis.doctor_reviews`, C# Controllers & Services, file OpenAPI `contracts/doctor-review-openapi.yaml`, xUnit tests.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Bác sĩ được phân công xem và gửi được review hợp lệ; bác sĩ không có assignment bị từ chối với HTTP 403 Forbidden.
  * **AC2**: Review thành công cập nhật trạng thái phân tích sang `REVIEWED`, phát sinh bản ghi revision có đầy đủ audit log (thời gian, ID bác sĩ, nội dung sửa); kết quả AI gốc không bị thay đổi.
  * **AC3**: Thử nghiệm gửi 2 review đồng thời trên cùng 1 version: đúng 1 request thành công, request thứ hai trả về HTTP 409 kèm thông báo xung đột phiên làm việc.

#### Task W45-M2-02 | P0 | 14 giờ | Epic E4 Analysis Pipeline (SCRUM-264)
**Triển khai Xử lý Ảnh Hàng loạt (Batch Processing $\ge 100$ ảnh) qua RabbitMQ và Redis**
* **Phối hợp kiểm tra**: M1 (RabbitMQ/Redis), M4 (AI Core).
* **Phụ thuộc**: W45-M1-02 (Redis & Worker Scaling).
* **Cần thực hiện**:
  * Xây dựng module tiếp nhận và xử lý Batch:
    * `POST /api/v1/batches`: Tiếp nhận danh sách tối đa 100 ảnh Fundus cùng lúc kèm metadata từng bệnh nhân.
    * Sinh bản ghi `analysis.batches` và các bản ghi con `analysis.images` trong một database transaction duy nhất.
  * Tích hợp Redis lưu trữ trạng thái tiến độ thời gian thực của Batch (`batch:{id}:progress`) gồm: tổng số ảnh, số ảnh đang xử lý, số ảnh hoàn tất, số ảnh lỗi.
  * Worker tiêu thụ jobs từ RabbitMQ song song: Mỗi khi hoàn thành một ảnh, Worker cập nhật Atomic Counter trong Redis và kiểm tra nếu là ảnh cuối cùng của Batch thì phát sự kiện `batch.completed`.
  * Xử lý trường hợp lỗi một phần (Partial Failure): Một vài ảnh bị lỗi chất lượng không làm dừng hoặc hỏng toàn bộ Batch; Batch chuyển trạng thái `PARTIAL_COMPLETED`.
* **Đầu ra bàn giao**: Mã nguồn Batch Engine trong `AURA.Analysis.Api`, Worker Batch Consumer, migration database cho Batch, tài liệu integration tests.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Gửi batch 100 ảnh qua API tạo đúng 1 batch ID và 100 job tương ứng trong RabbitMQ; các Worker xử lý song song và hoàn tất toàn bộ trong thời gian quy định.
  * **AC2**: API `GET /api/v1/batches/{id}` đọc trực tiếp tiến độ từ Redis trả về kết quả ngay lập tức ($< 10ms$), phản ánh chính xác số lượng ảnh đã xong.
  * **AC3**: Thử nghiệm cố tình đưa 5 ảnh hỏng vào batch 100 ảnh: 95 ảnh thành công, 5 ảnh ghi nhận lỗi rõ ràng, batch kết thúc ở trạng thái `PARTIAL_COMPLETED`.

#### Task W45-M2-03 | P0 | 10 giờ | Epic E4 Analysis Pipeline (SCRUM-264)
**Tích hợp Tự động hóa Thanh quyết toán Billing và Kết xuất Báo cáo PDF/CSV**
* **Phối hợp kiểm tra**: M3 (Billing), M5 (Frontend).
* **Phụ thuộc**: M3 bàn giao API Reservation/Capture của Billing.
* **Cần thực hiện**:
  * Tích hợp tự động hóa vòng đời Credit cho Batch: Khi tạo Batch $\ge 100$ ảnh, gọi Billing reserve số credit tương ứng; khi từng ảnh hoàn tất thì capture credit; nếu ảnh bị từ chối chất lượng (422 Low Quality) hoặc lỗi vĩnh viễn thì tự động phát lệnh release hoàn trả credit cho ảnh đó.
  * Triển khai module sinh Báo cáo Y tế (Medical Report Generator):
    * Báo cáo ca đơn (Single Analysis PDF): Chứa thông tin bệnh nhân, ảnh chụp đáy mắt gốc, ảnh phân vùng mạch máu overlay, biểu đồ phân bố mạch, chẩn đoán của Bác sĩ và mã QR đối soát.
    * Báo cáo tổng hợp Batch (Batch Summary CSV/PDF): Danh sách toàn bộ ca chụp trong đợt, tỷ lệ nghi ngờ bất thường, xuất file CSV bảo đảm an toàn dữ liệu cá nhân (đã khử định danh nhạy cảm).
  * Lưu trữ file PDF/CSV xuất ra vào vùng Private Storage và cấp URL tải về có chữ ký kèm thời hạn (Presigned URL 15 phút).
* **Đầu ra bàn giao**: Service tích hợp Billing Client, module kết xuất PDF (sử dụng QuestPDF hoặc iTextSharp), template báo cáo chuẩn y tế, integration test suite.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Luồng thanh quyết toán credit tự động hoạt động chính xác: Batch 10 ảnh thành công 8, lỗi 2 $\rightarrow$ Quyết toán chính xác 8 credit, tự động hoàn trả 2 credit, không thất thoát số dư.
  * **AC2**: File PDF sinh ra hiển thị sắc nét hình ảnh mạch máu, đúng định dạng trang in y tế tiêu chuẩn; file CSV tải về mở được trên Excel không bị lỗi font UTF-8.
  * **AC3**: Link tải file báo cáo chỉ người có quyền mới truy cập được; hết hạn 15 phút link tự vô hiệu hóa.

#### Task W45-M2-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Tích hợp Batch/Doctor Review, Concurrency và Cập nhật OpenAPI 0.8**
* **Phối hợp kiểm tra**: M3, M6.
* **Phụ thuộc**: W45-M2-01, W45-M2-02, W45-M2-03.
* **Cần thực hiện**:
  * Xây dựng bộ test tích hợp toàn diện cho các ca cạnh tranh (Race Conditions): Nhiều Worker cùng commit kết quả cho 1 batch, nhiều request cùng polling trạng thái.
  * Kiểm thử khả năng chịu lỗi (Resilience): Giả lập kill tiến trình Worker giữa lúc đang ghi đè kết quả; kiểm tra tính toàn vẹn dữ liệu transaction trong PostgreSQL.
  * Cập nhật và đồng bộ file hợp đồng `contracts/analysis-openapi-0.8.yaml` phản ánh đầy đủ các endpoint Batch, Doctor Review, Report Download và các mã lỗi mới.
* **Đầu ra bàn giao**: Suite test xUnit mới (`BatchIntegrationTests.cs`, `DoctorReviewTests.cs`), báo cáo kiểm thử tự động, OpenAPI 0.8.0 đồng bộ.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Toàn bộ các test cases mới pass 100% trên CI; không có lỗi dead-lock database khi chạy đồng thời 20 luồng.
  * **AC2**: Script `validate_openapi.py` kiểm tra schema khớp 100% với mã nguồn thực tế của controller.
  * **AC3**: Báo cáo kiểm thử thể hiện rõ các kịch bản biên (Edge cases) đã được bao phủ đầy đủ.

---

### M3 — THANH DAT (Billing, Credit, Firebase FCM & Notification)

#### Task W45-M3-01 | P0 | 14 giờ | Epic E8 Billing (SCRUM-268)
**Xây dựng Cổng Thanh toán Sandbox, Gói cước và Cấp Credit Idempotent**
* **Phối hợp kiểm tra**: M5 (Frontend UI).
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

#### Task W45-M3-02 | P0 | 14 giờ | Epic E7 Notification & Chat (SCRUM-267)
**Triển khai Dịch vụ Đẩy Thông báo Tức thời qua Firebase Cloud Messaging (FCM)**
* **Phối hợp kiểm tra**: M5 (Frontend Web Push).
* **Phụ thuộc**: M1 cấp Secret Firebase Service Account JSON.
* **Cần thực hiện**:
  * Tích hợp Firebase Admin .NET SDK vào dịch vụ Notification Service.
  * Xây dựng API quản lý danh bạ thiết bị người dùng:
    * `POST /api/v1/notifications/devices`: Đăng ký FCM Device Token của trình duyệt web người dùng sau khi được cấp quyền.
    * Cơ chế dọn dẹp token: Tự động vô hiệu hóa các token hết hạn hoặc báo lỗi `UNREGISTERED` từ Firebase.
  * Lắng nghe các sự kiện từ RabbitMQ exchange `aura.s3.events`:
    * Sự kiện `analysis.completed`: Tự động đẩy Web Push Notification: *"Kết quả phân tích mắt của bạn đã có! Bấm để xem chi tiết."* kèm deep-link dẫn tới trang kết quả.
    * Sự kiện `billing.payment.success`: Đẩy Web Push xác nhận nạp tiền thành công.
  * Vận hành đa kênh: Mọi thông báo đều được lưu đồng thời vào cơ sở dữ liệu `notification.inbox` (để hiển thị chuông thông báo) và bắn qua FCM (để hiển thị popup trên hệ điều hành).
* **Đầu ra bàn giao**: Notification Service hoàn chỉnh có Firebase Admin SDK, Event Consumer lắng nghe RabbitMQ, API đăng ký device token, tài liệu hướng dẫn cấu hình FCM.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Khi một ca phân tích hoàn tất, trong vòng $< 1.5$ giây, trình duyệt của người dùng nhận được pop-up Web Push Notification từ Firebase ngay cả khi đang chuyển tab khác.
  * **AC2**: Click vào thông báo push điều hướng chính xác đến màn hình xem kết quả của đúng ca chụp đó.
  * **AC3**: Gửi thông báo đến token không tồn tại được xử lý an toàn (không crash consumer), token lỗi được tự động đánh dấu ngừng kích hoạt trong database.

#### Task W45-M3-03 | P0 | 10 giờ | Epic E8 Billing (SCRUM-268)
**Triển khai Billing Cấp Tổ chức (Clinic Packages) và Tự động Hoàn Credit**
* **Phối hợp kiểm tra**: M2, M6.
* **Phụ thuộc**: W45-M3-01.
* **Cần thực hiện**:
  * Mở rộng mô hình ví Credit: Hỗ trợ ví cấp Tổ chức (Clinic Wallet) có hạn mức lớn, cho phép phân quyền người sở hữu thanh toán (Billing Owner) và cấp quyền sử dụng credit cho các Bác sĩ trong phòng khám.
  * Xây dựng Background Job đối soát tự động (Reconciliation Engine): Quét các reservation bị treo quá 30 phút do sự cố hệ thống để tự động giải phóng (Auto-release) trả lại credit cho phòng khám.
  * Triển khai API hoàn credit tự động phục vụ Batch Processing: Hỗ trợ lệnh hoàn trả credit theo danh sách ảnh lỗi (Batch Partial Refund) có kiểm soát idempotency.
* **Đầu ra bàn giao**: Module Clinic Billing, Background Service đối soát, migration ví tổ chức, báo cáo kiểm thử hoàn tiền.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Phòng khám mua gói 500 credit thành công; các bác sĩ thuộc phòng khám đó sử dụng credit để chụp ảnh và số dư ví tổ chức bị trừ chính xác.
  * **AC2**: Giả lập lỗi Worker crash khiến 3 reservation bị treo: Background Job quét sau 30 phút tự động release và hoàn 3 credit về ví, ghi log lý do `STALE_RESERVATION_AUTO_REFUND`.
  * **AC3**: Bác sĩ thuộc phòng khám A không thể sử dụng credit thuộc ví phòng khám B.

#### Task W45-M3-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Khả năng Phục hồi Billing/FCM, Ma trận Đối soát và Cập nhật RTM**
* **Phối hợp kiểm tra**: M1.
* **Phụ thuộc**: W45-M3-01, W45-M3-02, W45-M3-03.
* **Cần thực hiện**:
  * Lập ma trận kiểm thử đối soát tài chính toàn diện: Kiểm thử các trường hợp tranh chấp số dư (Concurrency Race Condition khi 10 luồng cùng gọi trừ 1 credit cuối cùng).
  * Kiểm thử ngắt kết nối mạng giữa Notification Service và Google Firebase API; đảm bảo tin nhắn không bị mất mà được lưu trong Dead Letter Queue để retry.
  * Cập nhật Ma trận Truy vết Yêu cầu (RTM): Đối soát toàn bộ các yêu cầu chức năng FR-10, FR-11, FR-12 và NFR liên quan đến tài chính và thông báo.
* **Đầu ra bàn giao**: Báo cáo kiểm thử tài chính `docs/testing/m3_billing_resilience_report.md`, RTM cập nhật, bộ test tự động xUnit cho Billing.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: 10 request đồng thời tranh chấp 1 credit cuối cùng: đúng 1 request thành công, 9 request còn lại bị từ chối do không đủ số dư (`INSUFFICIENT_CREDITS`), ví không bao giờ bị âm.
  * **AC2**: Đối soát sau 100 giao dịch nạp và 500 ca phân tích: tổng số dư ví + tổng credit đã capture luôn cân bằng chính xác tuyệt đối với tổng credit đã nạp.
  * **AC3**: Toàn bộ các tiêu chí kiểm thử tài chính đạt chứng nhận an toàn dữ liệu.

---

### M4 — ĐÀO DUY QUÂN (AI Core, Nâng cấp Frangi & Apache NiFi)

#### Task W45-M4-01 | P0 | 14 giờ | Epic E9 NiFi Integration (SCRUM-269)
**Nâng cấp Luồng Apache NiFi: InvokeHTTP, Chống trùng (Dedup), Quarantine & Auto-Retry**
* **Phối hợp kiểm tra**: M1 (Hạ tầng NiFi/Gateway).
* **Phụ thuộc**: M1 cung cấp endpoint Gateway và credential cho NiFi.
* **Cần thực hiện**:
  * Nâng cấp flow Apache NiFi xử lý camera:
    * Thay thế luồng mô phỏng cũ bằng processor `InvokeHTTP` chính thức đẩy ảnh vào API Gateway qua route `/api/v1/analyses/camera` kèm mTLS hoặc Service Token.
    * Tích hợp bộ lọc chống gửi trùng ảnh (Deduplication): Tính hash SHA-256 của file ảnh camera ngay tại NiFi; nếu trùng với ảnh đang xử lý thì drop và ghi nhận log cảnh báo.
  * Xây dựng phân luồng cách ly lỗi (Quarantine Routing):
    * Ảnh đúng chuẩn $\rightarrow$ Chuyển tiếp vào hệ thống AURA tiếp nhận (HTTP 202).
    * Ảnh lỗi format, hỏng header hoặc Gateway từ chối $\rightarrow$ Định tuyến sang thư mục/queue `camera.quarantine` kèm thuộc tính ghi rõ mã lỗi và lý do cách ly để kỹ thuật viên kiểm tra.
  * Cơ chế tự động thử lại (Limited Exponential Backoff Retry): Khi Gateway hoặc mạng bận (HTTP 429, 503), tự động thử lại tối đa 10 lần với thời gian chờ tăng dần; hết 10 lần tự động chuyển vào `camera.exhausted`.
* **Đầu ra bàn giao**: File template NiFi Flow mới `flow.json.gz`, NiFi Parameter Context không chứa secret, Sổ tay vận hành camera ingestion `docs/nifi/camera_ingestion_runbook_v1.2.md`.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Bơm 10 ảnh từ camera simulator qua NiFi: 10 ảnh được chuyển tiếp thành công sang Gateway, nhận mã HTTP 202 và tạo receipt hợp lệ trong PostgreSQL.
  * **AC2**: Đưa 1 file ảnh hỏng (corrupted bytes) vào camera: NiFi tự động nhận diện và chuyển file sang nhánh Quarantine trong vòng $< 1$ giây, không làm tắc luồng các ảnh khác.
  * **AC3**: Tắt Gateway tạm thời: NiFi tự động thực hiện retry theo đúng backoff (1s, 2s, 4s...); khi Gateway bật lại, ảnh tự động được gửi tiếp thành công mà không bị mất dữ liệu.

#### Task W45-M4-02 | P0 | 14 giờ | Epic E5 AI Core (SCRUM-265)
**Tối ưu hóa Phân vùng Frangi v0.2 & Khảo sát Mô hình Đánh giá Rủi ro (Risk Model)**
* **Phối hợp kiểm tra**: M2 (Worker timeout).
* **Phụ thuộc**: Không có task phụ thuộc; sử dụng module AI Core hiện hữu.
* **Cần thực hiện**:
  * Tối ưu hóa hiệu năng tính toán thuật toán Frangi v0.2 trên CPU:
    * Vector hóa các phép tính ma trận Hessian, tối ưu bộ lọc Gaussian trong thư viện SciPy/NumPy.
    * Đưa thời gian suy luận (Inference Latency) cho ảnh chuẩn $999 \times 960$ từ mức trung bình $2.5$ giây xuống **dưới $1.8$ giây** trên môi trường Docker chuẩn.
  * Nghiên cứu và triển khai mô hình tính điểm nguy cơ mạch máu sơ bộ (Heuristic Risk Model):
    * Trích xuất thêm đặc trưng độ cong queo của mạch máu (Vessel Tortuosity Index) và mật độ mạch (Vessel Density).
    * Tính toán điểm nguy cơ sơ bộ (Risk Score từ 0.0 đến 1.0) và phân loại mức độ rủi ro (LOW, MODERATE, HIGH) kèm cảnh báo giải thích rõ thuật toán heuristic không thay thế chẩn đoán bác sĩ.
  * Đảm bảo tính nhất quán: Nếu ảnh không đạt chất lượng (Quality check fail), lập tức ngắt pipeline và trả HTTP 422 `LOW_QUALITY_IMAGE`, không tính toán rủi ro.
* **Đầu ra bàn giao**: Mã nguồn tối ưu trong `src/ai-core/app/services/frangi_service.py`, thuật toán đo độ cong queo `tortuosity.py`, báo cáo đo đạc hiệu năng và giải thuật rủi ro.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Benchmark đo đạc trên 6 ảnh test của CHASE-DB1 xác nhận thời gian suy luận trung bình giảm xuống $< 1.8$ giây/ảnh trên CPU, hình ảnh Mask và Overlay giữ nguyên 100% độ chính xác so với baseline.
  * **AC2**: Endpoint trả về đầy đủ các chỉ số `vessel_density`, `tortuosity_index` và `risk_assessment` sơ bộ có nhãn disclaimer rõ ràng.
  * **AC3**: Toàn bộ unit tests của AI Core pass, bộ nhớ RAM giải phóng sạch sẽ sau mỗi lượt phân tích (không bị Memory Leak qua các lượt chạy).

#### Task W45-M4-03 | P0 | 10 giờ | Epic E5 AI Core (SCRUM-265)
**Đóng gói AI Contract 1.3, Chuẩn bị Khảo sát Định dạng Ảnh Cắt lớp Võng mạc OCT**
* **Phối hợp kiểm tra**: M2 (Worker integration).
* **Phụ thuộc**: W45-M4-02.
* **Cần thực hiện**:
  * Cập nhật OpenAPI AI Core lên phiên bản `1.3.0`:
    * Tích hợp middleware đọc và echo header `X-Correlation-Id` vào HTTP response để hỗ trợ tracing liên dịch vụ.
    * Bổ sung trường `risk_assessment` (với cấu trúc `risk_score`, `risk_level`, `indicators`, `disclaimer`) vào schema response.
  * Khảo sát kỹ thuật và chuẩn bị nền tảng cho định dạng ảnh OCT (Optical Coherence Tomography):
    * Nghiên cứu cấu trúc ảnh cắt lớp OCT (định dạng DICOM, TIFF đa lớp hoặc nén PNG).
    * Viết tài liệu đánh giá tính khả thi kỹ thuật (Technical Feasibility): Phân tích yêu cầu tài nguyên GPU/VRAM, mô hình AI phù hợp (UNet 3D hoặc ResNet) và lập kế hoạch cho Sprint 6.
* **Đầu ra bàn giao**: File OpenAPI `contracts/ai-openapi-1.3.yaml`, tài liệu khảo sát kỹ thuật OCT `docs/ai/oct_modality_feasibility_study_v1.0.md`, Model Card v1.3.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Header `X-Correlation-Id` truyền vào được trả ngược lại chính xác trong mọi response của AI Core (kể cả response lỗi 400, 422, 500).
  * **AC2**: Schema response khớp 100% với JSON Schema 1.3; Worker của M2 gọi kiểm tra không phát sinh lỗi parse dữ liệu.
  * **AC3**: Tài liệu khảo sát OCT chỉ rõ được phương án tiếp nhận dữ liệu và kết luận rõ ràng về phạm vi triển khai thực tế.

#### Task W45-M4-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Tải AI Core (Stress Test), Đo đạc Benchmark và Hoàn thiện Model Card**
* **Phối hợp kiểm tra**: M1.
* **Phụ thuộc**: W45-M4-01, W45-M4-02, W45-M4-03.
* **Cần thực hiện**:
  * Thực hiện kiểm thử tải (Stress Testing) trên endpoint `/api/v1/analyze`: Chạy đồng thời 5 đến 10 requests suy luận ảnh lớn liên tục trong 15 phút.
  * Giám sát mức tiêu thụ tài nguyên của tiến trình Python (CPU usage, RAM peak, Garbage Collection).
  * Cập nhật báo cáo benchmark runtime chính thức `docs/benchmarks/frangi_runtime_api_v1.3.json` và hoàn thiện Dataset Card/Model Card theo chuẩn học thuật.
* **Đầu ra bàn giao**: Báo cáo Stress test AI Core, file JSON benchmark cập nhật, Model Card hoàn chỉnh.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Endpoint chịu được tải 10 request đồng thời mà không bị crash tiến trình Uvicorn, tỷ lệ lỗi 0%.
  * **AC2**: Dung lượng RAM của container duy trì ổn định quanh mức cho phép, không xảy ra hiện tượng rò rỉ bộ nhớ (Memory Leak) sau 100 lượt suy luận.
  * **AC3**: Báo cáo Model Card phản ánh trung thực toàn bộ các thông số đo lường thực tế.

---

### M5 — LÂM TRẦN NGUYỄN HOÀNG (Frontend Web: Doctor, Batch UI, Payment & FCM)

#### Task W45-M5-01 | P0 | 14 giờ | Epic E12 Frontend (SCRUM-272)
**Xây dựng Giao diện Không gian làm việc Bác sĩ (Doctor Workspace & Review UI)**
* **Phối hợp kiểm tra**: M2 (Doctor Review API).
* **Phụ thuộc**: M2 bàn giao API Doctor Review.
* **Cần thực hiện**:
  * Xây dựng trang làm việc chuyên dụng cho Bác sĩ (`DoctorWorkspacePage`):
    * Bảng danh sách bệnh nhân và ca chụp cần xem xét (Worklist), bộ lọc theo ngày chụp, mức độ nghi ngờ và trạng thái phân tích.
  * Xây dựng Khung xem ảnh tương tác chuyên sâu (`InteractiveImageViewer`):
    * Cho phép zoom in, zoom out, xoay và kéo rê ảnh chụp đáy mắt võng mạc.
    * Nút công tắc bật/tắt lớp phủ mạch máu phân vùng (Overlay Toggle) và thanh trượt điều chỉnh độ trong suốt (Opacity Slider).
    * Chế độ xem song song (Side-by-side View): Đặt ảnh gốc và ảnh mạch máu cạnh nhau để bác sĩ dễ đối chiếu tổn thương.
  * Form nhập kết luận chẩn đoán y khoa (`DoctorReviewForm`):
    * Đánh giá mức độ bất thường, ghi chú khuyến nghị điều trị, xác nhận kết luận.
    * Xử lý trường hợp bị xung đột phiên làm việc (Conflict): Nếu ca chụp đã được bác sĩ khác review trước đó, hiển thị cảnh báo popup hỏi người dùng có muốn tải lại phiên bản mới nhất hay không.
* **Đầu ra bàn giao**: React Components trong `src/web/aura-web/src/pages/doctor/`, CSS Module/Tailwind, unit tests Jest/Testing Library.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Bác sĩ đăng nhập vào xem được danh sách ca chụp, mở chi tiết ảnh và sử dụng mượt mà các công cụ zoom/overlay/so sánh song song.
  * **AC2**: Form gửi review hoạt động trơn tru với API thật của M2, lưu thành công và cập nhật ngay lập tức giao diện sang trạng thái `Đã đánh giá`.
  * **AC3**: Mô phỏng xung đột phiên làm việc hiển thị đúng modal cảnh báo xung đột (Conflict Warning), không cho phép ghi đè mất dữ liệu của bác sĩ khác.

#### Task W45-M5-02 | P0 | 14 giờ | Epic E12 Frontend (SCRUM-272)
**Xây dựng Giao diện Mua Gói cước, Thanh toán Sandbox và Tích hợp FCM Web Push**
* **Phối hợp kiểm tra**: M3 (Billing & FCM).
* **Phụ thuộc**: M3 bàn giao API Sandbox và cấu hình Firebase Web.
* **Cần thực hiện**:
  * Xây dựng giao diện Mua gói khám (`BillingPackagePage`):
    * Hiển thị bảng giá các gói khám cá nhân và tổ chức, hiển thị số dư Credit hiện tại trong ví (`CreditBalanceBadge`).
    * Nút bấm Mua gói $\rightarrow$ Chuyển tiếp sang màn hình thanh toán mô phỏng Sandbox.
  * Tích hợp Firebase Client SDK trên trình duyệt Web:
    * Tạo Service Worker `firebase-messaging-sw.js` để lắng nghe sự kiện push trong background.
    * Popup xin quyền người dùng cấp phép thông báo trình duyệt (Browser Notification Permission Prompt).
    * Gửi FCM Device Token về backend M3 để đăng ký nhận tin.
  * Xử lý hiển thị thông báo đa trạng thái:
    * Khi đang mở web: Hiển thị Toast Notification góc màn hình kèm âm thanh thông báo nhẹ.
    * Khi đang ẩn tab/thu nhỏ trình duyệt: Hiển thị Native System Notification của hệ điều hành, click vào thông báo tự động mở lại tab và điều hướng đúng ca phân tích.
* **Đầu ra bàn giao**: Các trang `BillingPage.tsx`, `PaymentSuccessPage.tsx`, Service Worker script, module `useFcmNotification.ts`, test components.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Thực hiện quy trình chọn gói $\rightarrow$ thanh toán Sandbox $\rightarrow$ quay lại web hiển thị số dư credit được cộng ngay lập tức.
  * **AC2**: Người dùng bấm cho phép thông báo $\rightarrow$ token được gửi về backend thành công; khi có kết quả phân tích, thông báo Web Push hiển thị chính xác tiêu đề và nội dung.
  * **AC3**: Click vào thông báo push mở chính xác trang chi tiết kết quả của ca chụp tương ứng.

#### Task W45-M5-03 | P0 | 10 giờ | Epic E12 Frontend (SCRUM-272)
**Xây dựng Giao diện Tải ảnh Hàng loạt (Bulk Upload) & Bảng Theo dõi Tiến độ Batch**
* **Phối hợp kiểm tra**: M2 (Batch API).
* **Phụ thuộc**: M2 bàn giao Batch Processing API.
* **Cần thực hiện**:
  * Xây dựng giao diện Bulk Upload (`BulkUploadPage`):
    * Vùng kéo thả tiếp nhận cùng lúc tối đa 100 ảnh Fundus (hỗ trợ JPG, PNG).
    * Client-side validation: Kiểm tra kích thước file ($\le 10MB$), kiểm tra định dạng và cảnh báo ngay tại trình duyệt trước khi upload.
    * Form nhập mã bệnh nhân tương ứng hoặc tự động sinh mã theo tên file.
  * Xây dựng Bảng điều khiển tiến độ Batch thời gian thực (`BatchProgressDashboard`):
    * Thanh tiến trình tổng thể (Progress Bar: 0% đến 100%) hiển thị số lượng ảnh đã phân tích xong.
    * Bảng danh sách chi tiết từng ảnh: Thumbnail ảnh, mã ảnh, trạng thái (Đang chờ, Đang phân tích, Hoàn tất, Lỗi chất lượng), thời gian xử lý.
  * Nút tải Báo cáo tổng kết: Nút tải file PDF tổng hợp và xuất file kết quả CSV kích hoạt tải trực tiếp từ presigned URL.
* **Đầu ra bàn giao**: Giao diện Bulk Upload, Dashboard theo dõi tiến độ batch, component tải file báo cáo, video demo luồng tải hàng loạt.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Người dùng kéo thả 50 ảnh vào giao diện $\rightarrow$ hệ thống validate và gửi batch thành công lên backend mà không bị đơ trình duyệt.
  * **AC2**: Thanh tiến trình tự động nhảy số theo thời gian thực (polling nhẹ nhàng không gây giật lag); các ảnh hoàn tất hiển thị dấu tick xanh, ảnh lỗi hiển thị lý do rõ ràng.
  * **AC3**: Bấm nút xuất báo cáo tải về thành công file PDF và CSV có dữ liệu khớp hoàn toàn với bảng tiến độ.

#### Task W45-M5-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Toàn diện UI Doctor/Payment/Batch và Cập nhật Design Documentation**
* **Phối hợp kiểm tra**: M2, M3.
* **Phụ thuộc**: W45-M5-01, W45-M5-02, W45-M5-03.
* **Cần thực hiện**:
  * Kiểm thử giao diện trên nhiều độ phân giải màn hình (Responsive Design): Desktop màn hình lớn, Laptop và Tablet chuyên dụng của bác sĩ.
  * Kiểm thử xử lý lỗi mạng (Network Interruption): Giả lập rớt mạng khi đang upload batch hoặc đang gửi review của bác sĩ; đảm bảo form lưu tạm dữ liệu vào LocalStorage và không làm mất nội dung người dùng đã nhập.
  * Cập nhật tài liệu hướng dẫn sử dụng giao diện cho Bác sĩ và Quản trị viên phòng khám `docs/ux/user_guide_sprint4_5.md`.
* **Đầu ra bàn giao**: Báo cáo kiểm thử giao diện tự động (Cypress/Playwright E2E tests), bộ ảnh chụp màn hình chuẩn, tài liệu hướng dẫn người dùng cập nhật.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: 100% các trang mới hiển thị chuẩn xác, không bị vỡ layout trên các độ phân giải màn hình thông dụng ($\ge 1024px$).
  * **AC2**: Khi mất kết nối mạng và kết nối lại, trang tiến trình tự động reconnect mà không cần người dùng phải F5 tải lại trang.
  * **AC3**: Bộ test E2E mô phỏng hành trình Bác sĩ review và duyệt ảnh chạy đạt (PASS) trên CI.

---

### M6 — THÀNH VIÊN MỚI (Identity, Profile, Clinic & Chiến dịch Tầm soát)

#### Task W45-M6-01 | P0 | 14 giờ | Epic E3 Profile & Clinic (SCRUM-263)
**Triển khai Quản lý Vòng đời Phòng khám (Clinic Lifecycle) và Quản lý Hội viên**
* **Phối hợp kiểm tra**: M1 (Gateway), M5 (Frontend).
* **Phụ thuộc**: Không có task phụ thuộc; sử dụng module Profile/Clinic hiện có.
* **Cần thực hiện**:
  * Xây dựng phân hệ quản lý Tổ chức Y tế (Clinic Management):
    * API Đăng ký thông tin phòng khám (`POST /api/v1/clinics`): Tên cơ sở, mã số thuế y tế, địa chỉ, người đại diện.
    * API Xét duyệt phòng khám dành cho Quản trị viên hệ thống (`PATCH /api/v1/admin/clinics/{id}/status`): Chuyển trạng thái `PENDING` $\rightarrow$ `ACTIVE`, `SUSPENDED`, `REJECTED` kèm lý do và ghi nhận audit log.
  * Quản lý hội viên phòng khám (Clinic Memberships):
    * Mời Bác sĩ/Kỹ thuật viên vào phòng khám bằng email, gán vai trò (`ClinicManager`, `ClinicDoctor`, `ClinicStaff`).
    * Chấp nhận lời mời, kích hoạt tài khoản và gỡ bỏ hội viên khi nghỉ việc.
  * Thực thi nghiêm ngặt cơ chế Đa người thuê (Multi-Tenancy Isolation): Bác sĩ hoặc nhân viên phòng khám A tuyệt đối không thể truy xuất dữ liệu bệnh nhân hay ca chụp của phòng khám B.
* **Đầu ra bàn giao**: Migration cơ sở dữ liệu `profile.clinics` và `profile.clinic_memberships`, API Controllers & Services, ma trận kiểm tra phân quyền đa tenant.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Quy trình đăng ký Clinic $\rightarrow$ Admin duyệt $\rightarrow$ Mời Bác sĩ $\rightarrow$ Bác sĩ kích hoạt hoạt động trơn tru qua API.
  * **AC2**: Thử nghiệm dùng token của Bác sĩ Clinic A để đọc thông tin thành viên Clinic B: hệ thống từ chối ngay lập tức với HTTP 403 Forbidden.
  * **AC3**: Khi Clinic bị chuyển sang trạng thái `SUSPENDED`, toàn bộ tài khoản thuộc Clinic đó tự động bị khóa quyền tạo ca chụp mới.

#### Task W45-M6-02 | P0 | 14 giờ | Epic E3 Profile & Clinic (SCRUM-263)
**Triển khai Quản lý Bệnh nhân Phòng khám (Clinic Patients) và Phân công Bác sĩ**
* **Phối hợp kiểm tra**: M2 (Analysis Authorization).
* **Phụ thuộc**: W45-M6-01.
* **Cần thực hiện**:
  * Quản lý hồ sơ bệnh nhân nội bộ của phòng khám (Clinic Patient Registry):
    * Thêm mới bệnh nhân, cập nhật thông tin cá nhân, mã bảo hiểm y tế, tiền sử bệnh án (đái tháo đường, tăng huyết áp).
    * Phân biệt rõ giữa tài khoản người dùng cá nhân (Individual User) và hồ sơ bệnh nhân do phòng khám quản lý (Clinic Managed Patient).
  * Xây dựng phân hệ Phân công Bác sĩ (Doctor Assignment Engine):
    * Phân công một ca chụp cụ thể hoặc một nhóm bệnh nhân cho Bác sĩ chuyên khoa phụ trách theo dõi và đánh giá.
    * API thu hồi phân công (Revoke Assignment) hoặc chuyển giao ca bệnh cho Bác sĩ khác khi cần hội chẩn.
  * Cung cấp Hợp đồng Kiểm tra Quyền Tài nguyên (Resource Authorization Contract) cho Analysis API: Trả về kết quả quyết định `Allow`/`Deny` khi một Bác sĩ yêu cầu truy xuất ảnh hoặc gửi kết quả đánh giá.
* **Đầu ra bàn giao**: Migration database bảng bệnh nhân phòng khám và phân công bác sĩ, Service kiểm tra quyền nội bộ, tài liệu contract bàn giao cho M2.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Phòng khám tạo và quản lý được danh sách bệnh nhân riêng; gán thành công ca chụp cho Bác sĩ phụ trách.
  * **AC2**: Bác sĩ chỉ xem được các ca chụp của bệnh nhân mà mình được phân công; khi phân công bị thu hồi, quyền truy cập lập tức bị cắt đứt.
  * **AC3**: Analysis API gọi sang API phân quyền của M6 xác thực nhanh chóng ($< 5ms$) và nhận diện đúng quyền truy cập.

#### Task W45-M6-03 | P0 | 10 giờ | Epic E3 Profile & Clinic (SCRUM-263)
**Triển khai Phân hệ Chiến dịch Tầm soát Sức khỏe Mắt (Screening Campaign API & UI)**
* **Phối hợp kiểm tra**: M2 (Batch Analysis), M5 (Frontend).
* **Phụ thuộc**: W45-M6-01, W45-M6-02.
* **Cần thực hiện**:
  * Xây dựng module Chiến dịch Tầm soát (Campaign Module):
    * Khởi tạo chiến dịch tầm soát cộng đồng hoặc khám sức khỏe định kỳ cho doanh nghiệp (`POST /api/v1/campaigns`): Tên đợt khám, thời gian bắt đầu/kết thúc, địa điểm, bác sĩ phụ trách.
    * Gắn kết nối: Cho phép liên kết một Batch gồm nhiều ảnh Fundus vào một Campaign cụ thể.
  * Thống kê và theo dõi chiến dịch:
    * API tổng hợp tiến độ hoàn thành của đợt tầm soát (số lượng người đã chụp, số ca bình thường, số ca nghi ngờ bất thường).
    * Kết xuất dữ liệu tổng kết chiến dịch phục vụ báo cáo y tế cộng đồng.
  * Giao diện quản lý Campaign cơ bản: Cho phép điều phối viên phòng khám tạo đợt khám và theo dõi biểu đồ tiến độ.
* **Đầu ra bàn giao**: Campaign API Controller, database migration `analysis.campaigns`, giao diện quản lý chiến dịch cơ bản, tài liệu tích hợp với M2.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: Tạo thành công một chiến dịch tầm soát, liên kết thành công một batch 50 ảnh vào chiến dịch đó.
  * **AC2**: API thống kê trả về chính xác tỷ lệ hoàn thành và số ca có phát hiện tổn thương mạch máu trong chiến dịch.
  * **AC3**: Chiến dịch đã kết thúc (trạng thái `CLOSED`) không cho phép nạp thêm ảnh chụp mới.

#### Task W45-M6-04 | P1 | 10 giờ | Epic E13 Testing (SCRUM-273)
**Kiểm thử Bảo mật Phân quyền Đa Tenant, Kiểm toán Quyền và Bàn giao Thiết kế**
* **Phối hợp kiểm tra**: M1, M3.
* **Phụ thuộc**: W45-M6-01, W45-M6-02, W45-M6-03.
* **Cần thực hiện**:
  * Xây dựng ma trận kiểm thử bảo mật đa người thuê (Multi-Tenancy Security Matrix): Kiểm thử tất cả các lỗ hổng rò rỉ dữ liệu chéo tenant (Cross-tenant Data Leakage), thử nghiệm sửa ID trên URL để xem trộm hồ sơ bệnh nhân.
  * Kiểm thử vòng đời phân quyền: Kiểm tra tính tức thời của việc thu hồi quyền (Revocation immediacy) khi Bác sĩ bị xóa khỏi phòng khám.
  * Cập nhật toàn bộ tài liệu thiết kế hệ thống Identity/Profile/Clinic: Cập nhật sơ đồ ERD, Từ điển dữ liệu (Data Dictionary), Ma trận phân quyền RBAC và sơ đồ tuần tự.
* **Đầu ra bàn giao**: Báo cáo kiểm thử bảo mật đa tenant `docs/testing/m6_multitenancy_security_report.md`, ERD cập nhật, Data Dictionary v1.2.
* **Điều kiện nghiệm thu (DoD)**:
  * **AC1**: 100% các bài test thâm nhập thử nghiệm truy cập chéo phòng khám đều bị chặn tuyệt đối (HTTP 403 Forbidden).
  * **AC2**: Bác sĩ bị gỡ khỏi phòng khám lập tức mất quyền truy cập vào tất cả bệnh nhân và ca chụp của phòng khám đó mà không cần chờ hết hạn token.
  * **AC3**: Toàn bộ tài liệu ERD và ma trận phân quyền được cập nhật đồng bộ với cấu trúc database thực tế.

---

## 7. MA TRẬN ĐỐI CHIẾU NGHIỆM THU TÍCH HỢP CUỐI SPRINT 4–5

Cuối Sprint 4–5, cả nhóm sẽ chạy buổi Diễn tập Nghiệm thu Tích hợp (Integration Acceptance Demo) bao phủ 12 kịch bản liên dịch vụ sau:

| Mã | Tình huống Nghiệm thu Tích hợp | Kết quả Thực tế Cần Chứng minh | Thành viên Phụ trách |
|:---:|---|---|:---:|
| **S45 01** | **Batch Upload $\ge 50$ ảnh qua Web** | Kéo thả 50 ảnh $\rightarrow$ Batch tiếp nhận $\rightarrow$ Worker xử lý song song $\rightarrow$ UI cập nhật thanh tiến trình từng ảnh qua Redis $\rightarrow$ Hoàn tất tải báo cáo PDF/CSV. | **M5, M2, M1** |
| **S45 02** | **Camera NiFi Ingestion & Quarantine** | Bơm ảnh camera qua NiFi $\rightarrow$ Tiếp nhận HTTP 202 $\rightarrow$ Ảnh đúng vào phân tích; ảnh cố tình làm hỏng tự động rẽ nhánh vào thư mục Quarantine có ghi rõ lý do. | **M4, M1, M2** |
| **S45 03** | **Doctor Review & Lớp phủ Mạch máu** | Bác sĩ mở workspace xem ảnh so sánh song song $\rightarrow$ Điều chỉnh lớp phủ Overlay $\rightarrow$ Nhập chẩn đoán $\rightarrow$ Lưu revision thành công mà không ghi đè kết quả AI gốc. | **M5, M2, M6** |
| **S45 04** | **Xung đột Đánh giá Bác sĩ (Conflict)** | Hai tab cùng mở 1 ca phân tích: Tab 1 nộp review thành công; Tab 2 nộp đè lên bị chặn bởi Optimistic Locking (HTTP 409) và hiển thị modal cảnh báo. | **M2, M5** |
| **S45 05** | **Thanh toán Sandbox & Nạp Credit** | Chọn gói khám $\rightarrow$ Chuyển tiếp cổng thanh toán Sandbox $\rightarrow$ Callback webhook xác minh HMAC thành công $\rightarrow$ Ví cộng đúng số credit, chống cộng tiền 2 lần khi gửi lặp callback. | **M3, M5** |
| **S45 06** | **Thông báo Đẩy Firebase (FCM Web Push)** | Sau khi phân tích xong ảnh hoặc nạp credit thành công $\rightarrow$ Trình duyệt web dù đang ở tab khác vẫn nhận được popup Web Push tức thời $(< 1.5s)$. | **M3, M5** |
| **S45 07** | **Quyết toán Credit & Hoàn tiền Tự động** | Batch 10 ảnh có 8 ảnh đạt và 2 ảnh bị từ chối chất lượng (422 Low Quality) $\rightarrow$ Quyết toán chính xác 8 credit, tự động hoàn trả 2 credit về ví, không thất thoát. | **M2, M3** |
| **S45 08** | **Đa Tenant Phòng khám (Multi-Tenancy)** | Đăng nhập tài khoản Bác sĩ Clinic A $\rightarrow$ Thử truy cập ID bệnh nhân hoặc ca chụp của Clinic B $\rightarrow$ Hệ thống chặn đứng với mã lỗi HTTP 403 Forbidden. | **M6, M2, M5** |
| **S45 09** | **Chiến dịch Tầm soát (Campaign)** | Tạo chiến dịch tầm soát cộng đồng $\rightarrow$ Nạp batch ảnh vào chiến dịch $\rightarrow$ Xem bảng tổng kết tỷ lệ bất thường mạch máu theo chiến dịch. | **M6, M2, M5** |
| **S45 10** | **Giám sát Prometheus & Grafana Live** | Mở Grafana xem được biểu đồ thời gian thực: Throughput của Gateway, độ trễ của AI Core ($< 1.8s$), và số lượng jobs đang được xử lý trong các hàng đợi RabbitMQ. | **M1** |
| **S45 11** | **Cảnh báo Tự động (Alertmanager)** | Cố tình tạo tải lỗi HTTP 500 $\rightarrow$ Alertmanager phát hiện tỷ lệ lỗi $> 2\%$ và lập tức bắn cảnh báo `FIRING` lên hệ thống giám sát. | **M1** |
| **S45 12** | **Diễn tập Khôi phục Thảm họa (Backup/Restore)** | Chạy script khôi phục cơ sở dữ liệu từ bản backup gần nhất $\rightarrow$ Dữ liệu receipts, lịch sử khám và ví credit được phục hồi toàn vẹn 100% trong vòng $< 15$ phút. | **M1** |

---

## 8. Tiêu chí Kết thúc Sprint 4–5

Sprint 4–5 được nghiệm thu hoàn thành khi và chỉ khi:
1. Toàn bộ **24 task** (18 task P0 và 6 task P1) đều đạt đầy đủ tiêu chí nghiệm thu (DoD) và có bằng chứng xác thực đính kèm.
2. Cả **12 kịch bản nghiệm thu tích hợp** trong ma trận trên đều chạy thành công trên môi trường tích hợp chung.
3. Toàn bộ **7 công nghệ cốt lõi** đều đang hoạt động ổn định:
   * **OpenAPI**: 100% endpoint có tài liệu Swagger chuẩn hóa.
   * **Cache Redis**: Vận hành lưu cache bảng giá, rate-limit và khóa idempotency.
   * **Firebase FCM**: Bắn được Web Push tức thời tới trình duyệt người dùng.
   * **Apache NiFi**: Luồng camera chạy ổn định có Dedup và Quarantine.
   * **Microservices & API Gateway**: Định tuyến an toàn với Service JWT RS256.
   * **RabbitMQ Broker**: Cụm Worker xử lý mượt mà batch $\ge 100$ ảnh song song.
   * **Prometheus & Grafana**: Giám sát thời gian thực toàn bộ cụm dịch vụ.
4. Toàn bộ mã nguồn được merge sạch sẽ vào nhánh chính `develop`, không có conflict và toàn bộ CI/CD Pipeline chạy xanh.
