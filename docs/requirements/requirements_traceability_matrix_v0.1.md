# AURA — Requirements Traceability Matrix v0.1

## 1. Thông tin tài liệu

| Thuộc tính | Nội dung |
| --- | --- |
| Dự án | AURA — SP26SE025 |
| Task | SCRUM-54 |
| Người tiếp nhận biên soạn | Nguyễn Trương Hậu |
| Ngày biên soạn | 2026-10-11 |
| Phiên bản | 0.1 |
| SRS tham chiếu | srs_v0.1.md |
| Snapshot mã nguồn tham chiếu | develop tại 3ca294d |
| Trạng thái | Dự thảo — chờ review |
| Reviewer đề xuất | M2: nghiệp vụ; M4: AI; M5: UI |

RTM truy vết yêu cầu toàn dự án.
Không xác nhận mọi yêu cầu đã được triển khai.

Các task Jira trong bảng là nguồn thiết kế hoặc bằng chứng liên quan.
Không mặc nhiên là ticket triển khai đầy đủ yêu cầu.

Các mã TC-* là mã thiết kế kiểm thử dự kiến.
Chưa được tính là kết quả thực thi nếu thiếu test và evidence.

## 2. Quy ước trạng thái

| Ký hiệu | Ý nghĩa |
| --- | --- |
| DESIGN | Có tài liệu thiết kế/contract liên quan; chưa xác nhận triển khai hoàn chỉnh. |
| PARTIAL | Có một phần triển khai hoặc kiểm chứng; phạm vi còn thiếu được ghi rõ. |
| PENDING | Cần hoàn thiện đặc tả, quyết định hoặc triển khai. |
| VERIFIED | Chỉ dùng khi có bằng chứng thực thi phù hợp toàn bộ phạm vi nghiệm thu đã chốt. |

Bảng dưới không sử dụng VERIFIED cho yêu cầu sản phẩm
chưa có bằng chứng nghiệm thu đầy đủ.

## 3. Nguồn tài liệu và bằng chứng

| Mã | Nguồn |
| --- | --- |
| REF-01 | Đề tài SP26SE025: 39 FR và 23 NFR. |
| REF-02 | srs_v0.1.md — dự thảo SRS đi kèm. |
| REF-03 | Glossary và Business Rules v0.2; SCRUM-49. |
| REF-04 | Patient Use Cases v0.2; SCRUM-50. |
| REF-05 | Doctor Use Cases v0.2; SCRUM-51. |
| REF-06 | State Machine và Error Handling v0.3; SCRUM-52. |
| REF-07 | Analysis ERD v0.4; SCRUM-53. |
| REF-08 | contracts/analysis-openapi.yaml; SCRUM-280. |
| REF-09 | Ma trận quyền và Negative Test Cases v0.3; SCRUM-281. |
| REF-10 | Contract Baseline W1 v0.2; SCRUM-282. |
| REF-11 | Architecture Decision Records; SCRUM-45. |
| REF-12 | Patient/Doctor/Clinic/Admin wireframes và design system; SCRUM-65–69. |
| REF-13 | AI contract 1.1.0; SCRUM-61; docs/api/ai_service_contract.md. |
| REF-14 | AI FastAPI mock; SCRUM-62; Python tests và mock smoke evidence. |
| REF-15 | CHASE_DB1 data card và Frangi offline baseline; SCRUM-59/60/64/290. |
| REF-16 | NiFi runbook và camera ingestion PoC; SCRUM-63/290. |
| REF-17 | Review AI và kế hoạch rủi ro; SCRUM-291. |
| REF-18 | Frontend README, PR #24 và bình luận SCRUM-294/295/296/298 ngày 2026-10-11. |
| REF-19 | Firebase bàn giao và checklist; SCRUM-286/296. |
| REF-20 | Compose, Gateway, CI và Redis PoC; SCRUM-46/47/48/276/277. |
| REF-21 | Tài liệu cần hoàn thiện: Clinic, Admin, Profile/Billing/Notification và test plan; SCRUM-55/56/57/58. |

REF-21 là phạm vi task cần bàn giao.
Không được ghi là tài liệu đã được review hoàn chỉnh.

## 4. Functional Requirements

### 4.1. Patient/User

| ID | Yêu cầu | Thiết kế/nguồn | Jira liên quan | Kiểm thử dự kiến | Trạng thái và giới hạn |
| --- | --- | --- | --- | --- | --- |
| FR-1 | Đăng ký/đăng nhập email, Google/social | REF-04, REF-12, REF-19 | SCRUM-50, SCRUM-66, SCRUM-286 | TC-FR-01: đăng ký/đăng nhập hợp lệ; lỗi tài khoản/provider; phiên hết hạn | DESIGN — login chưa được nghiệm thu với Identity backend thật; social provider cần chốt. |
| FR-2 | Upload một/nhiều ảnh Fundus/OCT | REF-04, REF-06, REF-08, REF-16 | SCRUM-50, SCRUM-52, SCRUM-63, SCRUM-280 | TC-FR-02: ảnh hợp lệ/hỏng/quá giới hạn; nhiều ảnh; modality chưa hỗ trợ | PARTIAL — có chọn JPEG/PNG demo, contract và NiFi PoC; chưa có private upload toàn luồng; OCT chưa hỗ trợ. |
| FR-3 | Xem AI result và risk | REF-04, REF-08, REF-13, REF-17, REF-18 | SCRUM-50, SCRUM-61, SCRUM-291, SCRUM-295 | TC-FR-03: completed hiển thị đúng nguồn/version; processing/failed không tạo risk | PARTIAL — MSW/UI có kiểm chứng; chưa có kết quả AI lâm sàng và API nghiệp vụ thật. |
| FR-4 | Xem ảnh chú thích | REF-08, REF-13, REF-15, REF-18 | SCRUM-60, SCRUM-61, SCRUM-66, SCRUM-295 | TC-FR-04: annotation đúng ảnh, quyền truy cập file và trường hợp file lỗi | PARTIAL — mock annotation và Frangi offline; chưa tích hợp private file access toàn luồng. |
| FR-5 | Khuyến nghị/cảnh báo tự động | REF-04, REF-17, REF-21 | SCRUM-50, SCRUM-57, SCRUM-291 | TC-FR-05: nội dung có căn cứ, disclaimer, không cảnh báo từ failed/mock như kết quả thật | DESIGN — chính sách nội dung và căn cứ chuyên môn cần chốt. |
| FR-6 | Lịch sử phân tích cá nhân | REF-04, REF-08, REF-12 | SCRUM-50, SCRUM-66, SCRUM-280 | TC-FR-06: history, pagination, empty state và quyền sở hữu | DESIGN — chưa có API/database history thật được nghiệm thu. |
| FR-7 | Báo cáo PDF/CSV | REF-04, REF-08 | SCRUM-50, SCRUM-280 | TC-FR-07: nội dung report, định dạng, version và download authorization | DESIGN — chưa có export/report hoàn chỉnh. |
| FR-8 | Hồ sơ cá nhân/y tế | REF-04, REF-12, REF-21 | SCRUM-50, SCRUM-57, SCRUM-66 | TC-FR-08: xem/sửa own profile, validation, cấm sửa role/tenant | DESIGN — Profile backend và persistence chưa nghiệm thu. |
| FR-9 | Thông báo kết quả sẵn sàng | REF-19, REF-18, REF-21 | SCRUM-57, SCRUM-286, SCRUM-296 | TC-FR-09: event-to-notification, permission, token binding và nhận push thật | PARTIAL — notification UI/MSW scaffold có test; token/backend/push thật chưa nghiệm thu. |
| FR-10 | Nhắn tin bác sĩ được phân công | REF-04, REF-05, REF-21 | SCRUM-50, SCRUM-51, SCRUM-57 | TC-FR-10: gửi/nhận đúng quan hệ, unauthorized, revoke assignment | DESIGN — chat backend và delivery chưa nghiệm thu. |
| FR-11 | Mua/gia hạn gói | REF-04, REF-06, REF-21 | SCRUM-50, SCRUM-52, SCRUM-57 | TC-FR-11: purchase/renew, payment failure, duplicate callback | PARTIAL — Billing package catalog PoC; chưa có giao dịch mua/gia hạn thật. |
| FR-12 | Payment history và credit | REF-04, REF-06, REF-21 | SCRUM-50, SCRUM-52, SCRUM-57 | TC-FR-12: lịch sử, số dư, reserve/capture/release và retry không trừ hai lần | DESIGN — credit ledger/payment history chưa nghiệm thu. |

### 4.2. Doctor

| ID | Yêu cầu | Thiết kế/nguồn | Jira liên quan | Kiểm thử dự kiến | Trạng thái và giới hạn |
| --- | --- | --- | --- | --- | --- |
| FR-13 | Login và bệnh nhân được phân công | REF-05, REF-09, REF-12 | SCRUM-51, SCRUM-67, SCRUM-281 | TC-FR-13: login, assigned patients, hết hiệu lực phân công, cross-tenant | DESIGN — Identity/assignment/resource authorization chưa nghiệm thu thực thi. |
| FR-14 | Xem AI result và annotation | REF-05, REF-08, REF-12 | SCRUM-51, SCRUM-67, SCRUM-280 | TC-FR-14: dữ liệu đúng bệnh nhân, quyền file, AI/review status tách biệt | DESIGN — chưa có Doctor flow với API thật. |
| FR-15 | Xác nhận/sửa nhận định AI | REF-05, REF-06, REF-07, REF-08 | SCRUM-51, SCRUM-52, SCRUM-53, SCRUM-280 | TC-FR-15: approve/correct, giữ AI gốc, revision và concurrent edit | DESIGN — review contract có; backend revision chưa nghiệm thu. |
| FR-16 | Ghi chú/chẩn đoán/khuyến nghị | REF-05, REF-08 | SCRUM-51, SCRUM-280 | TC-FR-16: lưu nội dung hợp lệ, quyền sửa, audit và lỗi lưu | DESIGN — chưa có persistence/review flow đầy đủ. |
| FR-17 | Lịch sử và xu hướng bệnh nhân | REF-05, REF-07, REF-12 | SCRUM-51, SCRUM-53, SCRUM-67 | TC-FR-17: lịch sử đúng thứ tự/quyền; dữ liệu thiếu; tính trend đã chốt | DESIGN — chưa có trend implementation; cách tính cần chốt. |
| FR-18 | Tìm/lọc ID, tên, risk | REF-05, REF-09, REF-12 | SCRUM-51, SCRUM-67, SCRUM-281 | TC-FR-18: filter/search/pagination trong tập được phép; risk chưa có | PARTIAL — frontend có tìm/lọc dữ liệu mẫu; chưa có truy vấn backend phân quyền thật. |
| FR-19 | Phản hồi cải thiện AI | REF-05, REF-17 | SCRUM-51, SCRUM-291 | TC-FR-19: feedback gắn kết quả/version; quyền; dữ liệu retraining được xử lý đúng | DESIGN — feedback/retraining chưa được nghiệm thu. |
| FR-20 | Chat tư vấn Patient | REF-05, REF-12, REF-21 | SCRUM-51, SCRUM-57, SCRUM-67 | TC-FR-20: Doctor chỉ chat theo quan hệ hợp lệ; revoke và delivery errors | DESIGN — chat chưa triển khai đầy đủ. |
| FR-21 | Thống kê hoạt động Doctor | REF-05, REF-12 | SCRUM-51, SCRUM-67 | TC-FR-21: phạm vi dữ liệu, khoảng thời gian và đối chiếu số liệu nguồn | DESIGN — chưa có thống kê thật được nghiệm thu. |

### 4.3. Clinic

| ID | Yêu cầu | Thiết kế/nguồn | Jira liên quan | Kiểm thử dự kiến | Trạng thái và giới hạn |
| --- | --- | --- | --- | --- | --- |
| FR-22 | Đăng ký/xác minh Clinic | REF-01, REF-12, REF-21 | SCRUM-55, SCRUM-68 | TC-FR-22: đăng ký, thiếu hồ sơ, approval/rejection và trạng thái tổ chức | PENDING — đặc tả xác minh và backend cần hoàn thiện. |
| FR-23 | Quản lý Doctor/Patient members | REF-09, REF-12, REF-21 | SCRUM-55, SCRUM-57, SCRUM-68, SCRUM-281 | TC-FR-23: add/remove members, role constraints và cross-tenant | DESIGN — có định hướng quyền/UI; membership backend chưa nghiệm thu. |
| FR-24 | Bulk upload và batch | REF-06, REF-08, REF-12, REF-16 | SCRUM-52, SCRUM-55, SCRUM-63, SCRUM-68, SCRUM-280 | TC-FR-24: nhiều ảnh, lỗi riêng từng ảnh, partial success và batch state | DESIGN — contract/PoC có; bulk processing toàn luồng chưa nghiệm thu. |
| FR-25 | Theo dõi báo cáo và aggregate risk | REF-09, REF-12, REF-21 | SCRUM-55, SCRUM-68, SCRUM-281 | TC-FR-25: report scope, aggregate theo clinic, loại dữ liệu chưa hợp lệ | DESIGN — chưa có dữ liệu và tổng hợp backend thật. |
| FR-26 | Báo cáo chiến dịch | REF-01, REF-21 | SCRUM-55 | TC-FR-26: campaign scope, inclusion criteria và số liệu báo cáo | PENDING — đặc tả campaign/report cần hoàn thiện. |
| FR-27 | Image count và package usage | REF-06, REF-21 | SCRUM-55, SCRUM-57 | TC-FR-27: đối chiếu count/ledger, retry và các ảnh failed | PENDING — định nghĩa cách tính và implementation cần chốt. |
| FR-28 | Mua/gia hạn Clinic package | REF-12, REF-21 | SCRUM-55, SCRUM-57, SCRUM-68 | TC-FR-28: quyền mua, payment, renewal và duplicate callback | DESIGN — chưa có payment/renewal thật. |
| FR-29 | High-risk/trend alerts | REF-01, REF-17, REF-21 | SCRUM-55, SCRUM-57, SCRUM-291 | TC-FR-29: ngưỡng/căn cứ hợp lệ, recipients và data minimization | PENDING — clinical rule, aggregation và delivery cần chốt. |
| FR-30 | Export thống kê nghiên cứu/quản lý | REF-01, REF-21 | SCRUM-55, SCRUM-58 | TC-FR-30: export permission, aggregate, ẩn danh và định dạng | PENDING — phạm vi export/ẩn danh chưa nghiệm thu. |

### 4.4. Admin

| ID | Yêu cầu | Thiết kế/nguồn | Jira liên quan | Kiểm thử dự kiến | Trạng thái và giới hạn |
| --- | --- | --- | --- | --- | --- |
| FR-31 | Quản lý tài khoản | REF-09, REF-12, REF-21 | SCRUM-56, SCRUM-68, SCRUM-281 | TC-FR-31: enable/disable/edit, authorization và audit | DESIGN — có UI/policy; account management backend chưa nghiệm thu. |
| FR-32 | Quản lý role/permission | REF-09, REF-11, REF-21 | SCRUM-45, SCRUM-56, SCRUM-281 | TC-FR-32: grant/revoke, cấm self-escalation và enforcement backend | DESIGN — permission matrix không thay kiểm thử RBAC thật. |
| FR-33 | AI parameters/threshold/retraining | REF-13, REF-17, REF-21 | SCRUM-56, SCRUM-61, SCRUM-291 | TC-FR-33: validation config, version, audit, activation và rollback | DESIGN — metadata/config có định hướng; Admin configuration flow chưa nghiệm thu. |
| FR-34 | Package/pricing/billing model | REF-20, REF-21 | SCRUM-56, SCRUM-57, SCRUM-276 | TC-FR-34: update đúng quyền, validation giá/credit, invalidation và audit | PARTIAL — catalog GET/PUT cache PoC; chưa có Admin auth và billing model hoàn chỉnh. |
| FR-35 | Global dashboard usage/revenue/AI | REF-12, REF-21 | SCRUM-56, SCRUM-68 | TC-FR-35: nguồn dữ liệu, quyền toàn cục và đối chiếu số liệu | DESIGN — chưa có dashboard số liệu thật. |
| FR-36 | Image/risk/error analytics | REF-12, REF-17, REF-21 | SCRUM-56, SCRUM-58, SCRUM-68 | TC-FR-36: định nghĩa numerator/denominator, window và loại mock data | DESIGN — chưa có analytics pipeline được nghiệm thu. |
| FR-37 | Compliance/audit/privacy | REF-09, REF-11, REF-17, REF-21 | SCRUM-45, SCRUM-56, SCRUM-286, SCRUM-291 | TC-FR-37: audit access, privacy change, retention và dữ liệu log | DESIGN — chính sách/chức năng vận hành cần hoàn thiện; chưa tuyên bố compliance. |
| FR-38 | Approve/suspend Clinic | REF-01, REF-12, REF-21 | SCRUM-55, SCRUM-56, SCRUM-68 | TC-FR-38: approval/suspension, quyền, audit và enforcement sau suspend | PENDING — workflow và backend chưa nghiệm thu. |
| FR-39 | Notification templates/policies | REF-19, REF-21 | SCRUM-56, SCRUM-57, SCRUM-286 | TC-FR-39: template validation, version, quyền và cấm dữ liệu sức khỏe trong push | PENDING — Admin template/policy management chưa nghiệm thu. |

## 5. Non-functional Requirements

| ID | Yêu cầu | Nguồn/Jira liên quan | Phương pháp kiểm thử dự kiến | Trạng thái và giới hạn |
| --- | --- | --- | --- | --- |
| NFR-1 | AI một ảnh 10–20 giây | REF-13–17; SCRUM-58, SCRUM-60, SCRUM-64 | TC-NFR-01: chốt ranh giới thời gian; đo trên ảnh/tải/hardware xác định; báo cáo phân bố latency | PARTIAL — có Frangi offline latency và mock timing; chưa nghiệm thu latency mục tiêu của hệ thống. |
| NFR-2 | Tối thiểu 100 ảnh/batch | REF-06, REF-08, REF-11; SCRUM-52, SCRUM-58, SCRUM-280 | TC-NFR-02: batch >=100 ảnh, queue/concurrency, partial failure và hoàn tất từng ảnh | DESIGN — chưa có batch/Worker load test toàn luồng. |
| NFR-3 | Dashboard/result dưới 3 giây | REF-12, REF-18; SCRUM-58, SCRUM-294 | TC-NFR-03: đo browser/API theo mạng, tải và dataset được chốt | PENDING — timeout client demo 3 giây không phải bằng chứng đạt latency. |
| NFR-4 | Uptime >=99% | REF-11, REF-20; SCRUM-45, SCRUM-58 | TC-NFR-04: giám sát service trong cửa sổ đo; loại scheduled maintenance theo quy tắc | PENDING — chưa có số liệu uptime đủ thời gian. |
| NFR-5 | Graceful failure, bảo toàn ảnh | REF-06, REF-07, REF-13, REF-14; SCRUM-52, SCRUM-53, SCRUM-62 | TC-NFR-05: gây lỗi AI/timeout/crash; kiểm persisted image và trạng thái lỗi/retry | PARTIAL — có error scenarios/mock tests; chưa kiểm storage/Worker failure toàn luồng. |
| NFR-6 | Backup ít nhất hàng ngày | REF-11; SCRUM-45, SCRUM-58 | TC-NFR-06: lịch backup, artifact, thử restore DB/file và đối chiếu dữ liệu | PENDING — persistence qua restart không thay backup/restore. |
| NFR-7 | Horizontal scaling AI | REF-11, REF-13; SCRUM-45, SCRUM-58, SCRUM-61 | TC-NFR-07: chạy nhiều instance, routing/concurrency, consistency và tài nguyên | DESIGN — kiến trúc có định hướng; chưa có scale test. |
| NFR-8 | Nhiều Clinic/hàng nghìn User | REF-09, REF-11; SCRUM-45, SCRUM-58, SCRUM-281 | TC-NFR-08: định nghĩa registered/concurrent users, workload và ngưỡng suy giảm; load test multi-tenant | PENDING — workload/acceptance threshold chưa chốt và chưa đo. |
| NFR-9 | AES-256 khi lưu, TLS 1.2+ khi truyền | REF-11, REF-19; SCRUM-45, SCRUM-286 | TC-NFR-09: kiểm TLS, provider/storage encryption, DB/backup và quản lý khóa | DESIGN — chưa có bằng chứng mã hóa toàn bộ môi trường mục tiêu. |
| NFR-10 | Bảo vệ dữ liệu y tế | REF-15, REF-17, REF-19; SCRUM-59, SCRUM-286, SCRUM-291 | TC-NFR-10: policy/access/retention/consent review và kiểm chứng kỹ thuật phù hợp | PARTIAL — có dataset/privacy/risk notes; chưa có đánh giá tuân thủ đầy đủ. |
| NFR-11 | Ẩn danh trước retraining | REF-15, REF-17; SCRUM-59, SCRUM-290, SCRUM-291 | TC-NFR-11: kiểm metadata/PII trước-sau pipeline, quyền dataset và khả năng tái nhận diện | DESIGN — chưa có retraining anonymization pipeline được nghiệm thu. |
| NFR-12 | RBAC bốn vai trò | REF-09, REF-11; SCRUM-45, SCRUM-281, SCRUM-286 | TC-NFR-12: 401/403, sai role, cross-tenant, ngoài assignment, revoked/disabled | DESIGN — có ma trận và test design; chưa nghiệm thu API authorization thật. |
| NFR-13 | Responsive/accessibility | REF-12, REF-18; SCRUM-65–69, SCRUM-294 | TC-NFR-13: viewport/browser matrix, keyboard/focus, labels và contrast | PARTIAL — modal keyboard/UI tests có bằng chứng; chưa nghiệm thu mọi màn hình/thiết bị. |
| NFR-14 | Upload/xem kết quả tối đa 3 click | REF-12; SCRUM-65, SCRUM-66, SCRUM-67 | TC-NFR-14: chốt start/end và thao tác được tính; kiểm hành trình thật trên browser | DESIGN — sitemap/wireframe có định hướng; chưa đo luồng upload/results thật. |
| NFR-15 | Annotation/risk explanation rõ | REF-12, REF-13, REF-17; SCRUM-61, SCRUM-67, SCRUM-291 | TC-NFR-15: task-based review với người phù hợp, nguồn/version và giới hạn hiển thị | DESIGN — mock/offline chưa chứng minh tính diễn giải lâm sàng. |
| NFR-16 | Update model/config không downtime | REF-11, REF-13; SCRUM-45, SCRUM-56, SCRUM-61 | TC-NFR-16: thay version khi có request/job; kiểm availability, version và rollback | DESIGN — chưa có deployment/update test không downtime. |
| NFR-17 | Kiến trúc modular | REF-11, REF-20; SCRUM-45, SCRUM-47 | TC-NFR-17: review boundaries/dependencies/data ownership và integration contract | PARTIAL — có ADR/scaffold; chưa review implementation toàn sản phẩm. |
| NFR-18 | Central logging/audit/error tracking | REF-06, REF-07, REF-11; SCRUM-45, SCRUM-52, SCRUM-53 | TC-NFR-18: truy vết correlation ID, tập trung log, quyền truy cập và không lộ PII/token | DESIGN — chưa có telemetry/audit xuyên service được nghiệm thu. |
| NFR-19 | Tích hợp camera Fundus | REF-16; SCRUM-63, SCRUM-290 | TC-NFR-19: simulator/camera theo giao thức chốt -> NiFi -> Gateway -> persisted Analysis | PARTIAL — có simulator/NiFi PoC; chưa có camera thật và backend E2E. |
| NFR-20 | PDF/CSV và chuẩn y tế khi khả thi | REF-04, REF-08, REF-21; SCRUM-50, SCRUM-55, SCRUM-58 | TC-NFR-20: mở/parse export, schema, encoding, authorization và dữ liệu đúng | DESIGN — chưa nghiệm thu export; chuẩn y tế cụ thể cần chốt. |
| NFR-21 | RESTful AI API | REF-13, REF-14; SCRUM-61, SCRUM-62 | TC-NFR-21: runtime OpenAPI, request/response/negative tests và Worker integration | PARTIAL — AI mock REST/contract có test; chưa nghiệm thu Worker–AI. |
| NFR-22 | Explainable AI outputs | REF-13, REF-15, REF-17; SCRUM-60, SCRUM-61, SCRUM-291 | TC-NFR-22: output thực gắn ảnh, mask/overlay đúng và phương pháp giải thích phù hợp | PARTIAL — có mask mock và Frangi offline; chưa có explainability tích hợp thật. |
| NFR-23 | Truy vết AI version/threshold | REF-07, REF-08, REF-13; SCRUM-53, SCRUM-61, SCRUM-280 | TC-NFR-23: version/config lưu bền vững, giữ qua retrieval/report và config change | PARTIAL — fields/contract/mock có; persistence/report chưa nghiệm thu. |

## 6. Yêu cầu cho bộ test case tiếp theo

SCRUM-58 cần mở rộng mỗi mã TC-* thành các trường:

- Requirement ID.
- Test case ID.
- Mục tiêu.
- Tiền điều kiện.
- Dữ liệu kiểm thử.
- Các bước thực hiện.
- Kết quả kỳ vọng.
- Loại kiểm thử: unit, contract, integration, E2E hoặc NFR.
- Môi trường và commit.
- Kết quả thực tế.
- Evidence.
- Người thực hiện và thời điểm.
- Bug liên quan nếu có.

Một mã TC trong RTM có thể được chia thành nhiều test case.
Ví dụ TC-NFR-12-01, TC-NFR-12-02.

Không dùng dữ liệu bệnh nhân thật để tạo test fixture
khi chưa có quyền và quy trình bảo vệ dữ liệu phù hợp.

## 7. Bằng chứng hiện có và giới hạn

### 7.1. Contract Analysis

Jira SCRUM-280 và baseline ghi nhận:

- Public REST 0.6.0.
- Event/job schemaVersion 0.6.
- 20 REST examples, 4 event examples và 1 job example.
- 54 embedded example checks.
- Negative checks.
- 31 SHA-256 hashes sau chuẩn hóa LF.

Phạm vi: contract/schema/examples.
Không thay thế integration test backend.

### 7.2. AI

Tài liệu M4 ghi nhận:

- AI contract nội bộ 1.1.0.
- Bộ test Python: 29 passed ở mốc đã bàn giao.
- Các mock scenario SUCCESS, WARNING, LOW_QUALITY,
  INFERENCE_ERROR được kiểm tra trong phạm vi ghi nhận.
- Frangi offline trên CHASE_DB1 có benchmark và split.

Phạm vi: AI mock, validation và baseline offline.
Không chứng minh clinical risk, Worker hoặc toàn luồng.

### 7.3. Frontend

PR #24 và Jira update ngày 2026-10-11 ghi nhận:

- 8 fixtures khớp contract examples.
- 10 fixtures đạt schema validation.
- App: 7 tests.
- MSW handlers: 10 tests.
- AnalysisDemoPanel: 12 tests.
- FcmDemoPanel: 7 tests.
- NotificationSettings: 10 tests.
- Tổng frontend: 46 tests PASS.
- Lint và production build PASS.
- npm audit: 0 vulnerabilities.
- Repository checks và AURA CI trên PR #24 SUCCESS.

Phạm vi: frontend demo/scaffold, component/API client/MSW
và các kiểm tra tương ứng.

Không chứng minh backend upload, Analysis API thật,
RBAC hoặc FCM push thật.

### 7.4. Hạ tầng

SCRUM-277 ghi nhận chạy Compose trên Ubuntu mới,
health/package API và persistence qua restart.

Phạm vi: stack và PoC ở môi trường được ghi nhận.
Không thay thế production uptime, backup/restore,
security hoặc performance toàn hệ thống.

## 8. Các khoảng trống cần xử lý

| Khoảng trống | Task liên quan |
| --- | --- |
| Clinic/campaign use cases và quy tắc nghiệp vụ | SCRUM-55 |
| Admin/configuration workflows và audit | SCRUM-56 |
| Profile/Billing/Notification design | SCRUM-57 |
| Test plan, test cases và phép đo NFR | SCRUM-58 |
| Firebase config, authenticated token API và privacy | SCRUM-286 |
| Đối chiếu kỹ thuật và kiểm chứng bổ sung | SCRUM-289 |
| Checklist nghiệm thu tuần 1 và bằng chứng review | SCRUM-287 |

RTM phải được cập nhật khi các tài liệu trên được bàn giao.
Không chuyển trạng thái requirement thành VERIFIED
chỉ vì task tài liệu chuyển Done.

## 9. Quy trình cập nhật

1. Xác định requirement và phạm vi thay đổi.
2. Cập nhật SRS hoặc tài liệu thiết kế liên quan.
3. Cập nhật contract/examples khi cần.
4. Cập nhật test case và mã nguồn.
5. Thực thi test trên môi trường/commit rõ ràng.
6. Ghi evidence và giới hạn.
7. Review và cập nhật trạng thái RTM.

Yêu cầu bị hoãn vẫn giữ dòng trong RTM.
Thay đổi phạm vi phải có quyết định được ghi nhận.

## 10. Tiêu chí review RTM

- Đủ 39 FR và 23 NFR, không trùng hoặc thiếu ID.
- Nội dung khớp SRS và đề tài.
- Nguồn thiết kế/task Jira liên quan có thể tra cứu.
- Test dự kiến tách biệt kết quả đã chạy.
- Trạng thái không vượt quá bằng chứng.
- Mock/offline/design không bị ghi thành E2E hoặc clinical PASS.
- Các khoảng trống được ghi rõ.
- Reviewer ghi nhận nhận xét thực tế trước nghiệm thu.

## 11. Liên kết nguồn

- SCRUM-54:
  https://nguyentruonghau2202.atlassian.net/browse/SCRUM-54
- Patient Use Cases:
  https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/524545
- Doctor Use Cases:
  https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/917542
- State Machine:
  https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/950619
- Analysis ERD:
  https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/950629
- Ma trận quyền:
  https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/1966110
- Contract Baseline:
  https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/1736808
- PR #24:
  https://github.com/CattyGame/XD-PMHDT/pull/24

## 12. Lịch sử thay đổi

| Phiên bản | Ngày | Người biên soạn | Nội dung |
| --- | --- | --- | --- |
| 0.1 | 2026-10-11 | Nguyễn Trương Hậu | Dự thảo RTM đủ 39 FR/23 NFR; phân biệt design, partial evidence và khoảng trống; chờ review. |
