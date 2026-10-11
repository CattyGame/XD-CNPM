# AURA — Software Requirements Specification

## 1. Thông tin tài liệu

| Thuộc tính | Nội dung |
| --- | --- |
| Tên tiếng Anh | System for Retinal Vascular Health Screening |
| Tên tiếng Việt | Hệ Thống Sàng Lọc Sức Khỏe Mạch Máu Võng Mạc |
| Mã đề tài | SP26SE025 |
| Tên hệ thống | AURA |
| Phiên bản tài liệu | 0.1 |
| Ngày biên soạn | 2026-10-11 |
| Người biên soạn | Nguyễn Trương Hậu |
| Phân công tiếp nhận | Công việc tuần 1 của M3 |
| Task Jira | SCRUM-54 |
| Trạng thái |

**Bước tiếp theo — Điền nội dung SRS cho SCRUM-54**

Mở `docs/requirements/srs_v0.1.md`, thay toàn bộ nội dung bằng bản dưới đây và nhấn **Ctrl + S**.

Bản này mô tả **yêu cầu toàn dự án**, đồng thời phân biệt rõ phần đã kiểm chứng trong tuần 1 với chức năng còn phải triển khai.

```markdown
# AURA — Software Requirements Specification v0.1

## 1. Thông tin tài liệu

| Thuộc tính | Nội dung |
| --- | --- |
| Dự án | AURA — System for Retinal Vascular Health Screening |
| Tên tiếng Việt | Hệ Thống Sàng Lọc Sức Khỏe Mạch Máu Võng Mạc |
| Mã đề tài | SP26SE025 |
| Task Jira | SCRUM-54 |
| Phân công ban đầu | M3 — Chuẩn hóa SRS và ma trận truy vết |
| Người tiếp nhận biên soạn | Nguyễn Trương Hậu |
| Phiên bản | 0.1 |
| Ngày biên soạn | 2026-10-11 |
| Trạng thái | Dự thảo — chờ review |
| Reviewer đề xuất | M2: nghiệp vụ; M4: AI; M5: giao diện |
| Tài liệu đi kèm | requirements_traceability_matrix_v0.1.md |
| Snapshot tham chiếu mã nguồn | develop tại 3ca294d |

Tài liệu này xác định yêu cầu sản phẩm, không xác nhận toàn bộ
yêu cầu đã được triển khai hoặc nghiệm thu.

Việc người biên soạn tiếp nhận công việc M3 không thay đổi
trách nhiệm sở hữu dữ liệu của các service trong kiến trúc.

## 2. Mục đích

AURA hỗ trợ sàng lọc sức khỏe mạch máu võng mạc bằng phân tích
ảnh và cung cấp thông tin hỗ trợ quyết định lâm sàng.

Hệ thống hỗ trợ bác sĩ xem kết quả AI, ảnh chú thích và lịch sử,
đồng thời lưu nhận xét hoặc đánh giá của bác sĩ.

AURA là Clinical Decision Support, không thay thế bác sĩ.
Kết quả AI phải được phân biệt với kết luận do bác sĩ xác nhận.

Tài liệu SRS là căn cứ cho thiết kế, triển khai, kiểm thử và
nghiệm thu. Mỗi yêu cầu phải được truy vết trong RTM.

## 3. Phạm vi sản phẩm

### 3.1. Thành phần dự kiến

- Web Application cho Patient, Doctor, Clinic và Admin.
- Backend API và API Gateway.
- Identity/Profile và quản lý quan hệ tổ chức.
- Analysis API, hàng đợi và Analysis Worker.
- AI Core Microservice API.
- Billing và quản lý credit.
- Notification và giao tiếp.
- Tích hợp camera Fundus.
- Database, private file storage và audit.

Danh sách trên là phạm vi sản phẩm.
Không mặc nhiên coi mọi thành phần đã được triển khai.

### 3.2. Phạm vi đầu vào ảnh

Đề tài yêu cầu ảnh Fundus và OCT, một ảnh hoặc nhiều ảnh.

Baseline triển khai hiện tại:

- Public Analysis contract nhận JPEG/PNG.
- Ưu tiên ảnh Fundus.
- OCT chưa được hỗ trợ.
- Bulk processing và mục tiêu tối thiểu 100 ảnh/batch
  chưa được nghiệm thu.

Việc hoãn OCT hoặc batch ở một sprint không xóa các yêu cầu
này khỏi phạm vi đề tài. Thay đổi phạm vi cuối cùng cần
được thống nhất và ghi lại.

### 3.3. Phạm vi AI

Sản phẩm hướng tới phân tích mạch võng mạc, explainability
và thông tin hỗ trợ sàng lọc nguy cơ sức khỏe.

Hiện tại:

- AI API chạy mock.
- Frangi baseline chạy offline trên CHASE_DB1.
- Chưa có bằng chứng mô hình đánh giá nguy cơ bệnh được
  kiểm chứng lâm sàng.

Không suy ra nguy cơ bệnh từ kết quả phân vùng mạch đơn thuần
khi chưa có mô hình, dữ liệu và phương pháp đánh giá phù hợp.

## 4. Vai trò và thuật ngữ

### 4.1. Vai trò

| Vai trò | Trách nhiệm |
| --- | --- |
| Patient/User | Quản lý hồ sơ cá nhân, gửi ảnh, xem kết quả được phép, lịch sử, gói dịch vụ và trao đổi với bác sĩ. |
| Doctor | Truy cập bệnh nhân được phân công, xem và review kết quả, thêm ghi chú và khuyến nghị. |
| Clinic | Quản lý hoạt động và thành viên trong tổ chức, batch và báo cáo theo quyền được cấp. |
| Admin | Quản lý tài khoản, quyền, cấu hình, package, audit và chính sách hệ thống. |
| Service principal | Đại diện dịch vụ như NiFi hoặc Worker; sử dụng scope và tenant được cấp, không giả làm người dùng. |

### 4.2. Thuật ngữ

| Thuật ngữ | Ý nghĩa |
| --- | --- |
| User account | Tài khoản xác thực; không mặc nhiên có ID bằng patientId. |
| Patient profile | Hồ sơ bệnh nhân và liên kết với tài khoản hoặc tổ chức theo nghiệp vụ. |
| Tenant | Phạm vi tổ chức được dùng để cách ly dữ liệu và kiểm tra quyền. |
| AnalysisRequest | Một yêu cầu gửi phân tích; theo baseline có thể đại diện một batch. |
| Image | Một ảnh trong yêu cầu; public analysisId ánh xạ Image.id. |
| Job attempt | Một lần thực thi xử lý ảnh, có số lần thực thi để truy vết retry. |
| AI result | Kết quả do AI tạo, phải giữ nguồn và phiên bản. |
| Doctor review | Đánh giá của bác sĩ, tách khỏi AI result và có revision. |
| Credit | Đơn vị sử dụng dịch vụ theo chính sách Billing. |
| Annotation | Tham chiếu ảnh hoặc vùng chú thích hỗ trợ giải thích kết quả. |
| Mock | Dữ liệu giả lập phục vụ kiểm thử, không dùng đánh giá bệnh nhân. |
| Idempotency key | Khóa chống xử lý lặp theo phạm vi nghiệp vụ đã chốt. |
| Correlation ID | Mã truy vết request; không thay idempotency key. |

## 5. Yêu cầu chức năng

### 5.1. Patient/User

| ID | Yêu cầu |
| --- | --- |
| FR-1 | Cho phép đăng ký và đăng nhập bằng email, Google hoặc phương thức social authentication được lựa chọn. |
| FR-2 | Cho phép gửi một hoặc nhiều ảnh võng mạc Fundus/OCT để phân tích theo modality được hỗ trợ. |
| FR-3 | Cho phép xem kết quả AI và mức nguy cơ khi có kết quả hợp lệ; phân biệt kết quả AI với đánh giá của bác sĩ. |
| FR-4 | Cho phép xem ảnh chú thích thể hiện khu vực mạch cần xem xét và các yếu tố giải thích được hỗ trợ. |
| FR-5 | Cung cấp khuyến nghị hoặc cảnh báo sức khỏe theo chính sách nội dung và kết quả hợp lệ. |
| FR-6 | Cho phép xem lịch sử phân tích cá nhân và các báo cáo trước đó được phép truy cập. |
| FR-7 | Cho phép tải hoặc xuất báo cáo dưới dạng PDF/CSV theo quyền truy cập. |
| FR-8 | Cho phép quản lý và cập nhật các trường hồ sơ cá nhân, thông tin y tế được phép. |
| FR-9 | Thông báo khi kết quả AI sẵn sàng; nội dung thông báo phải tuân thủ chính sách bảo vệ dữ liệu. |
| FR-10 | Cho phép trao đổi với bác sĩ được phân công qua chức năng nhắn tin trong ứng dụng. |
| FR-11 | Cho phép mua hoặc gia hạn gói dịch vụ phân tích. |
| FR-12 | Cho phép xem lịch sử thanh toán và credit còn lại. |

### 5.2. Doctor

| ID | Yêu cầu |
| --- | --- |
| FR-13 | Cho phép Doctor đăng nhập và quản lý hồ sơ bệnh nhân được phân công trong phạm vi được phép. |
| FR-14 | Cho phép Doctor xem kết quả AI và annotation của bệnh nhân được phép truy cập. |
| FR-15 | Cho phép Doctor xác nhận hoặc chỉnh sửa nhận định qua doctor review; không ghi đè AI result gốc. |
| FR-16 | Cho phép Doctor thêm ghi chú y tế, chẩn đoán hoặc khuyến nghị theo quyền được cấp. |
| FR-17 | Cho phép Doctor xem lịch sử, các phân tích trước và dữ liệu xu hướng của bệnh nhân được phép truy cập. |
| FR-18 | Cho phép tìm kiếm/lọc bệnh nhân theo ID, tên hoặc mức nguy cơ hợp lệ trong phạm vi được phân công. |
| FR-19 | Cho phép Doctor gửi phản hồi để hỗ trợ cải thiện AI và quy trình retraining. |
| FR-20 | Cho phép Doctor trao đổi tư vấn với Patient qua chat trong phạm vi quan hệ được phép. |
| FR-21 | Cho phép Doctor xem tổng hợp hoạt động hoặc thống kê phân tích theo quyền. |

### 5.3. Clinic

| ID | Yêu cầu |
| --- | --- |
| FR-22 | Cho phép đăng ký Clinic và thực hiện quy trình xác minh tổ chức. |
| FR-23 | Cho phép Clinic quản lý các tài khoản Doctor/Patient và quan hệ thành viên trong phạm vi tổ chức theo quyền. |
| FR-24 | Cho phép Clinic gửi nhiều ảnh theo batch và theo dõi trạng thái từng ảnh. |
| FR-25 | Cho phép Clinic theo dõi báo cáo phân tích bệnh nhân và dữ liệu nguy cơ tổng hợp được phép. |
| FR-26 | Cho phép tạo báo cáo cho các chiến dịch sàng lọc của Clinic. |
| FR-27 | Cho phép theo dõi số ảnh đã phân tích và mức sử dụng gói dịch vụ. |
| FR-28 | Cho phép Clinic mua hoặc gia hạn gói dịch vụ cấp tổ chức. |
| FR-29 | Cho phép Clinic nhận cảnh báo bệnh nhân có nguy cơ cao hoặc xu hướng bất thường khi có dữ liệu và quy tắc hợp lệ. |
| FR-30 | Cho phép xuất thống kê tổng hợp phục vụ nghiên cứu hoặc quản lý theo quyền và chính sách bảo vệ dữ liệu. |

FR-30 được chuẩn hóa mã từ dòng bị lỗi định dạng trong đề tài;
không bổ sung yêu cầu mới.

### 5.4. Admin

| ID | Yêu cầu |
| --- | --- |
| FR-31 | Cho phép Admin quản lý tài khoản User, Doctor và Clinic, gồm bật, tắt và chỉnh sửa theo quyền. |
| FR-32 | Cho phép Admin định nghĩa hoặc cập nhật vai trò và quyền truy cập theo chính sách. |
| FR-33 | Cho phép quản lý tham số AI, threshold và chính sách retraining có version và audit. |
| FR-34 | Cho phép quản lý package, giá và mô hình tính phí. |
| FR-35 | Cung cấp dashboard tổng hợp sử dụng, doanh thu và hiệu năng AI trong phạm vi dữ liệu được phép. |
| FR-36 | Cung cấp thống kê số ảnh, phân bố nguy cơ và tỷ lệ lỗi với định nghĩa phép đo rõ ràng. |
| FR-37 | Cho phép quản lý audit log, privacy settings và các quy trình liên quan đến bảo vệ dữ liệu. |
| FR-38 | Cho phép Admin phê duyệt hoặc tạm ngừng đăng ký Clinic theo quy trình. |
| FR-39 | Cho phép quản lý mẫu thông báo và chính sách giao tiếp. |

## 6. Yêu cầu phi chức năng

Các chỉ tiêu dưới đây là yêu cầu đề tài.
Kết quả nghiệm thu phải có môi trường, cách đo và bằng chứng.

| ID | Yêu cầu |
| --- | --- |
| NFR-1 | Phân tích AI một ảnh hoàn thành trong 10–20 giây tùy kích thước ảnh và tải model. Cần chốt ranh giới phép đo trong test plan. |
| NFR-2 | Hỗ trợ tối thiểu 100 ảnh trong một batch bằng queue hoặc xử lý song song. |
| NFR-3 | Dashboard và truy xuất kết quả tải trong dưới 3 giây ở điều kiện mạng bình thường được định nghĩa. |
| NFR-4 | Uptime tối thiểu 99%, không tính thời gian bảo trì có lịch; cần xác định cửa sổ đo. |
| NFR-5 | AI lỗi phải trả thông tin rõ ràng và bảo toàn ảnh đã được hệ thống tiếp nhận bền vững. |
| NFR-6 | Backup tự động ít nhất một lần mỗi ngày; cần kiểm chứng khả năng phục hồi. |
| NFR-7 | Kiến trúc cho phép horizontal scaling các AI microservice. |
| NFR-8 | Hỗ trợ nhiều Clinic và hàng nghìn User mà không suy giảm ngoài ngưỡng được thống nhất trong test plan. |
| NFR-9 | Dữ liệu bệnh nhân được mã hóa khi lưu và truyền, với yêu cầu AES-256 và TLS 1.2 trở lên. |
| NFR-10 | Áp dụng thực hành bảo vệ dữ liệu y tế HIPAA-like và yêu cầu pháp luật địa phương phù hợp. Không tự tuyên bố chứng nhận tuân thủ. |
| NFR-11 | Dữ liệu nhạy cảm phải được ẩn danh trước khi sử dụng cho retraining. |
| NFR-12 | Kiểm soát truy cập theo RBAC cho Admin, Clinic, Doctor và User; kết hợp tenant và quyền trên tài nguyên. |
| NFR-13 | Web accessible và responsive trên desktop, tablet và mobile theo tiêu chí được chốt. |
| NFR-14 | Clinician upload ảnh và xem kết quả với tối đa 3 click theo hành trình và điểm đầu/cuối được xác định. |
| NFR-15 | Annotation và giải thích nguy cơ rõ ràng, có khả năng diễn giải lâm sàng; cần người review phù hợp. |
| NFR-16 | Model, threshold và config có thể cập nhật không downtime theo cơ chế triển khai được thiết kế. |
| NFR-17 | Codebase modular, gồm AI Core, Web, các module nghiệp vụ và database layer. |
| NFR-18 | Logging, audit và error tracking được tập trung, có truy vết và kiểm soát dữ liệu nhạy cảm. |
| NFR-19 | Hỗ trợ tích hợp camera Fundus thông dụng qua cloud upload theo giao thức được lựa chọn. |
| NFR-20 | Export có PDF, CSV và định dạng y tế chuẩn khi khả thi, với phạm vi được thống nhất. |
| NFR-21 | AI Engine giao tiếp bằng RESTful API theo contract. |
| NFR-22 | AI output có yếu tố giải thích như heatmap, annotated vessels hoặc highlighted abnormalities phù hợp thuật toán. |
| NFR-23 | Mỗi báo cáo/kết quả truy vết được AI version và threshold/config đã sử dụng. |

## 7. Quy tắc nghiệp vụ bắt buộc

### 7.1. Sở hữu dữ liệu và quyền

- Backend xác định tenant từ thông tin xác thực và quan hệ
  tài khoản hợp lệ; không tin tenant do client tự khai.
- Role không tự cấp quyền trên mọi tài nguyên.
- Patient chỉ xem dữ liệu được liên kết và được phép.
- Doctor chỉ xem bệnh nhân được phân công còn hiệu lực.
- Clinic chỉ truy cập dữ liệu thuộc phạm vi tổ chức và quyền.
- Service principal sử dụng scope và tenant được cấp.
- Thu hồi phân công phải làm mất quyền truy cập tương ứng
  theo chính sách đã chốt.
- Đường dẫn hoặc ID đoán được không được bypass authorization.

### 7.2. Kết quả AI và doctor review

- AI result gốc được giữ nguyên.
- Doctor review lưu revision, người thực hiện, thời gian và lý do.
- Có cơ chế phát hiện xung đột khi nhiều người chỉnh sửa.
- AI status và doctor review status được hiển thị riêng.
- Kết quả mock phải giữ nhãn khi truyền, lưu và hiển thị.
- Failed, timeout hoặc quality rejection không được tạo
  kết luận nguy cơ thấp.
- Chỉ hiển thị mức nguy cơ khi có kết quả hợp lệ và nguồn rõ.
- Khuyến nghị tự động phải được kiểm soát nội dung theo CDS.

### 7.3. Xử lý bất đồng bộ và retry

- Yêu cầu phân tích có trạng thái và lịch sử chuyển trạng thái.
- Public API chỉ trả 202 sau khi dữ liệu cần thiết đã được
  lưu bền vững để tiếp tục xử lý.
- Idempotency key được kiểm tra theo phạm vi nghiệp vụ.
- Job trùng không tạo kết quả hoặc tác dụng phụ trùng.
- Retry hữu hạn, phân biệt lỗi dữ liệu với lỗi tạm thời.
- Retry không trừ credit hai lần.
- Lỗi một ảnh không mặc nhiên làm thất bại toàn batch.
- Ảnh đã tiếp nhận phải được bảo toàn khi AI thất bại.

### 7.4. Billing

- Billing là chủ sở hữu package, payment và credit.
- Redis không là nguồn quyết định số dư hoặc thanh toán.
- Reserve/capture/release và hoàn credit phải có quy tắc
  rõ, idempotency và audit trước khi triển khai.
- Không xác nhận thanh toán chỉ từ thông tin do frontend gửi.
- Quy tắc tính phí cụ thể cần được chốt trong SCRUM-57.

### 7.5. Notification và bảo vệ dữ liệu

- Chỉ xin quyền thông báo sau thao tác chủ động của người dùng.
- Token được liên kết với user/device theo contract xác thực.
- Có thiết kế xử lý token rotation, logout và revoke.
- Push không chứa tên, ảnh, bệnh lý hoặc kết quả bệnh nhân.
- Notification click phải qua auth và authorization.
- Firebase Admin credentials không đặt trong frontend/repo.
- Demo HTTP MSW không chứng minh đã gửi hoặc nhận FCM thật.

## 8. Kiến trúc và ràng buộc kỹ thuật

### 8.1. Luồng tích hợp dự kiến

1. Web hoặc NiFi gửi yêu cầu qua API Gateway.
2. Service nghiệp vụ xác thực, kiểm tra quyền và validation.
3. Analysis lưu metadata, yêu cầu/job và outbox theo thiết kế.
4. Worker nhận job từ queue và gọi AI Core nội bộ.
5. Worker lưu kết quả và file đầu ra, cập nhật trạng thái.
6. Web truy xuất trạng thái/kết quả qua public API.
7. Doctor thực hiện review theo quyền.
8. Notification thông báo nội dung chung khi có sự kiện phù hợp.

Đây là kiến trúc dự kiến; không xác nhận toàn luồng đã chạy.

### 8.2. Quyền sở hữu

| Thành phần | Trách nhiệm |
| --- | --- |
| Identity/Profile | Account, profile, organization, membership, role và assignment theo ADR. |
| Analysis | Image metadata, request/batch, job, AI result, doctor review, report và outbox. |
| Billing | Package, payment và credit ledger. |
| AI Core | Validation ảnh và suy luận; không đọc database nghiệp vụ. |
| Gateway | Định tuyến và chính sách truy cập ở biên hệ thống. |

- Không truy cập hoặc ghi trực tiếp database của service khác.
- Không lưu ảnh nhị phân trong bảng nghiệp vụ.
- Ảnh gốc/mask/overlay dùng private storage.
- Public response không lộ object key nội bộ.
- Việc thêm M6 không tự tạo thêm service hoặc database.

### 8.3. Contract tham chiếu

| Contract | Nguồn | Phiên bản |
| --- | --- | --- |
| Public Analysis REST | contracts/analysis-openapi.yaml | 0.6.0 |
| Event | contracts/analysis-events.schema.json | schemaVersion 0.6 |
| Job | contracts/analysis-job.schema.json | schemaVersion 0.6 |
| AI nội bộ | contracts/ai_service_openapi.yaml | 1.1.0 |
| Baseline Analysis | contracts/BASELINE-W1-v0.2.md | v0.2 |

Quy ước:

- Public endpoints dùng `/api/v1/analyses`.
- AI nội bộ dùng `/api/v1/analyze`.
- Public API và AI API có schema khác nhau.
- Web/NiFi không gọi AI Core trực tiếp theo kiến trúc.
- REST `attemptNo`, database `attempt_no` và job `attempt`
  phải được ánh xạ nhất quán.
- Correlation ID không thay idempotency key.
- Khi thay contract phải cập nhật examples, validator,
  tài liệu và baseline theo quy trình review.

## 9. Hiện trạng tham chiếu và giới hạn

### 9.1. Phạm vi có bằng chứng

| Phần | Phạm vi được ghi nhận |
| --- | --- |
| Development platform | Repository, Compose, Gateway/template và CI. |
| Analysis | Contract, examples, validator và thiết kế; chưa nghiệm thu backend nghiệp vụ toàn luồng. |
| Billing | Package catalog và Redis cache PoC; chưa phải payment/credit ledger đầy đủ. |
| Dataset | CHASE_DB1 đã kiểm tra, chia tập theo đối tượng. |
| Frangi | Benchmark offline, chưa phải thuật toán chạy trong AI endpoint hiện tại. |
| AI API | Mock có nhãn, validation ảnh và test trong phạm vi đã chạy. |
| NiFi | PoC nhận ảnh, metadata/quarantine và chuẩn bị multipart. |
| Web | React demo, MSW Analysis và notification scaffold. |

PR #24 và các bình luận Jira ngày 2026-10-11 ghi nhận frontend
46/46 test PASS, lint/build PASS và audit 0 vulnerabilities.

Các kết quả này chỉ xác nhận phạm vi kiểm thử tương ứng,
không xác nhận toàn bộ FR/NFR đã đạt.

### 9.2. Những phần chưa được nghiệm thu toàn luồng

- Identity, token validation và RBAC trên API nghiệp vụ thật.
- Profile/Clinic/membership/assignment thực thi đầy đủ.
- Private upload và quyền truy cập file.
- Analysis database, outbox, queue và Worker.
- Credit ledger và payment.
- Doctor review, report PDF/CSV và chat.
- FCM token binding, foreground/background push và revoke.
- NiFi gửi qua Gateway đến persisted Analysis.
- Batch tối thiểu 100 ảnh.
- Hiệu năng, uptime, backup và security toàn hệ thống.
- Hiệu quả lâm sàng của đánh giá nguy cơ.

Không đưa các phần trên thành PASS trong RTM khi chưa có test
và evidence phù hợp.

## 10. Các quyết định còn cần chốt

| Mã | Nội dung cần quyết định | Bên phối hợp |
| --- | --- | --- |
| OPEN-01 | Phạm vi và lộ trình hỗ trợ OCT. | M2, M4 |
| OPEN-02 | Social provider ngoài Google và chính sách xác minh email. | Identity owner, M1 |
| OPEN-03 | Quyền phân công, số Doctor trên một Patient và xử lý chuyển Clinic. | M2, M6 |
| OPEN-04 | Định nghĩa risk level, dữ liệu/model và phương pháp kiểm chứng. | M4, người review chuyên môn |
| OPEN-05 | Thời điểm reserve/capture/release credit, đơn vị tính phí và hoàn credit. | Billing owner, M2 |
| OPEN-06 | Token API, payload và phương án kiểm chứng FCM thật. | SCRUM-286, M1, M5 |
| OPEN-07 | Storage provider, retention, consent và quyền truy cập ảnh. | M1, M2 |
| OPEN-08 | Chiến lược backup, restore và tiêu chí RPO/RTO. | M1 |
| OPEN-09 | Điều kiện đo tải, latency, uptime và accessibility. | SCRUM-58, cả nhóm |
| OPEN-10 | Định dạng y tế chuẩn và dữ liệu xuất nghiên cứu. | M2, người review phù hợp |

Không tự ghi các mục OPEN thành quyết định đã được phê duyệt.

## 11. Truy vết và kiểm thử

RTM đi kèm phải có đủ 39 FR và 23 NFR.

Mỗi dòng tối thiểu gồm:

- Requirement ID.
- Nội dung hoặc tên yêu cầu.
- Tài liệu thiết kế/use case liên quan.
- Task Jira liên quan.
- Phương pháp hoặc test case dự kiến.
- Phạm vi bằng chứng hiện có.
- Trạng thái và giới hạn.

Quy tắc:

- Test case dự kiến không được ghi là đã chạy.
- Mock test không thay API integration hoặc E2E.
- Contract validation không chứng minh backend thực thi đúng.
- Benchmark offline không thay latency toàn hệ thống.
- Merge PR/CI PASS không tự thay review nghiệp vụ.
- Yêu cầu chưa triển khai vẫn phải có dòng truy vết.

## 12. Tiêu chí review SCRUM-54

- Có đủ 39 FR và 23 NFR, giữ đúng ID của đề tài.
- Có phạm vi, actors, thuật ngữ và quy tắc nghiệp vụ.
- Phân biệt yêu cầu sản phẩm, thiết kế và triển khai hiện tại.
- Không trình bày mock/benchmark offline thành AI lâm sàng.
- Contract và phiên bản phù hợp baseline tham chiếu.
- Có RTM đi kèm, không bỏ yêu cầu đang hoãn.
- Các quyết định chưa chốt có owner/phối hợp.
- Reviewer ghi nhận nhận xét và các điểm được sửa.
- Có link PR/commit và trang bàn giao theo quy trình nhóm.

Tài liệu hiện là dự thảo.
Chưa ghi nhận review hoặc nghiệm thu khi chưa có bằng chứng.

## 13. Nguồn tham chiếu

1. Đề tài SP26SE025 do người dùng cung cấp:
   39 Functional Requirements và 23 Non-functional Requirements.

2. Jira SCRUM-54:
   https://nguyentruonghau2202.atlassian.net/browse/SCRUM-54

3. Architecture Decision Records:
   https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/786441

4. Glossary và Business Rules v0.2:
   https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/917509

5. Patient Use Cases v0.2:
   https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/524545

6. Doctor Use Cases v0.2:
   https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/917542

7. State Machine và Error Handling v0.3:
   https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/950619

8. Analysis ERD:
   https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/950629

9. Ma trận quyền và Negative Test Cases v0.3:
   https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/1966110

10. Contract Baseline W1 v0.2:
    https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/1736808

11. AI contract:
    contracts/ai_service_openapi.yaml
    docs/api/ai_service_contract.md

12. Frontend evidence:
    https://github.com/CattyGame/XD-PMHDT/pull/24
    src/web/aura-web/README.md

13. Firebase bàn giao:
    https://nguyentruonghau2202.atlassian.net/browse/SCRUM-286

## 14. Lịch sử thay đổi

| Phiên bản | Ngày | Người biên soạn | Nội dung |
| --- | --- | --- | --- |
| 0.1 | 2026-10-11 | Nguyễn Trương Hậu | Dự thảo SRS từ đề tài, baseline contract và tài liệu tuần 1; chờ review. |
