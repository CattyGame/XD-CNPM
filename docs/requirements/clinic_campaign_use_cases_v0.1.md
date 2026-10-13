# AURA — Clinic và Campaign Use Cases v0.1

## 1. Thông tin tài liệu

| Thuộc tính | Nội dung |
| --- | --- |
| Dự án | AURA — SP26SE025 |
| Task Jira | SCRUM-55 |
| Người tiếp nhận biên soạn | Nguyễn Trương Hậu |
| Ngày biên soạn | 2026-10-11 |
| Phiên bản | 0.1 |
| Trạng thái | Dự thảo — chờ review |
| Reviewer đề xuất | M2: nghiệp vụ; M5: UI; M6: thiết kế triển khai Clinic |
| Yêu cầu liên quan | FR-22 đến FR-30 |
| SRS tham chiếu | srs_v0.1.md |
| RTM tham chiếu | requirements_traceability_matrix_v0.1.md |

Tài liệu mô tả yêu cầu và thiết kế nghiệp vụ.
Không xác nhận các chức năng Clinic đã được triển khai.

Các endpoint hoặc schema mới phải được chốt trong contract riêng;
tài liệu này không tự bổ sung API chính thức.

## 2. Mục tiêu và phạm vi

Đặc tả các chức năng Clinic:

- Đăng ký và xác minh tổ chức.
- Quản lý thành viên Doctor/Patient.
- Gửi ảnh theo batch.
- Theo dõi báo cáo và dữ liệu tổng hợp.
- Quản lý chiến dịch sàng lọc.
- Theo dõi sử dụng gói dịch vụ.
- Mua hoặc gia hạn gói.
- Nhận cảnh báo.
- Xuất thống kê.

Clinic là phạm vi tổ chức.
Tài khoản thao tác là người dùng có quan hệ thành viên và quyền
trong Clinic, không phải bản ghi tổ chức tự đăng nhập.

## 3. Actors và quyền

| Actor | Trách nhiệm |
| --- | --- |
| Clinic operator | Thực hiện nghiệp vụ trong Clinic theo quyền được cấp. |
| Doctor | Xem/review bệnh nhân được phân công còn hiệu lực. |
| Patient | Xem dữ liệu cá nhân được liên kết và được phép. |
| Admin | Xác minh/phê duyệt hoặc tạm ngừng Clinic theo quy trình. |
| Analysis Worker | Xử lý job và cập nhật kết quả theo contract. |
| Billing service | Quản lý package, giao dịch và credit. |
| Notification service | Gửi thông báo theo sự kiện và chính sách. |

Nguyên tắc:

- Backend xác định Clinic/tenant từ danh tính và membership hợp lệ.
- Không tin clinicId/tenantId do client tự gửi để cấp quyền.
- Role không thay kiểm tra quyền trên tài nguyên cụ thể.
- Doctor không tự có quyền trên mọi bệnh nhân trong Clinic.
- Người dùng có nhiều membership phải chọn ngữ cảnh hợp lệ;
  cơ chế này cần được chốt trước triển khai.
- Admin truy cập dữ liệu phải theo chính sách và audit.
- Membership và doctor assignment là hai quan hệ khác nhau.

## 4. Ma trận truy vết use case

| Use case | Nội dung | FR |
| --- | --- | --- |
| UC-C01 | Đăng ký và xác minh Clinic | FR-22 |
| UC-C02 | Quản lý thành viên và phân công | FR-23 |
| UC-C03 | Bulk upload và theo dõi batch | FR-24 |
| UC-C04 | Theo dõi báo cáo và dữ liệu tổng hợp | FR-25 |
| UC-C05 | Quản lý chiến dịch và tạo báo cáo | FR-26 |
| UC-C06 | Theo dõi số ảnh và sử dụng gói | FR-27 |
| UC-C07 | Mua hoặc gia hạn Clinic package | FR-28 |
| UC-C08 | Nhận cảnh báo nguy cơ/xu hướng | FR-29 |
| UC-C09 | Xuất thống kê nghiên cứu/quản lý | FR-30 |

## 5. Use cases

### UC-C01 — Đăng ký và xác minh Clinic

Actor chính: Người đại diện đăng ký Clinic.
Actor phối hợp: Admin.

Tiền điều kiện:

- Người đăng ký có danh tính hợp lệ theo cơ chế Identity.
- Dữ liệu và hồ sơ xác minh cần cung cấp đã được thống nhất.

Luồng chính:

1. Người đại diện nhập thông tin tổ chức.
2. Hệ thống kiểm tra dữ liệu bắt buộc và trùng lặp.
3. Người đại diện gửi hồ sơ xác minh theo chính sách.
4. Hệ thống ghi nhận hồ sơ đang chờ xử lý.
5. Admin kiểm tra hồ sơ.
6. Admin phê duyệt hoặc yêu cầu bổ sung/từ chối, có lý do.
7. Hệ thống lưu kết quả và audit.
8. Người đại diện nhận thông báo phù hợp.

Ngoại lệ:

- Thiếu hoặc sai dữ liệu: trả validation errors.
- Tổ chức có dấu hiệu trùng: xử lý theo quy tắc cần chốt.
- Không đủ quyền xác minh: từ chối.
- Hồ sơ bị từ chối: hiển thị lý do được phép và hướng bổ sung.
- Lỗi lưu: không thông báo phê duyệt thành công.

Hậu điều kiện:

- Có hồ sơ tổ chức và trạng thái xử lý được truy vết.
- Clinic chỉ được sử dụng các chức năng theo trạng thái và quyền.

Cần chốt:

- Các giấy tờ/trường xác minh.
- Cách nhận diện tổ chức trùng.
- Quyền sử dụng trong thời gian chờ duyệt.
- Quy trình gửi lại hồ sơ và tạm ngừng Clinic.

### UC-C02 — Quản lý thành viên và phân công

Actor chính: Clinic operator có quyền quản lý.

Tiền điều kiện:

- Tài khoản và membership hợp lệ.
- Clinic được phép thực hiện thao tác.
- Actor có quyền quản lý thành viên/phân công tương ứng.

Luồng chính:

1. Actor xem danh sách thành viên trong Clinic.
2. Actor mời hoặc liên kết tài khoản theo quy trình đã chốt.
3. Hệ thống kiểm tra danh tính, membership và quyền.
4. Hệ thống tạo/cập nhật quan hệ thành viên.
5. Actor phân công Doctor cho Patient nếu có quyền.
6. Hệ thống kiểm tra Doctor/Patient thuộc phạm vi hợp lệ.
7. Hệ thống lưu quan hệ, hiệu lực và audit.
8. Danh sách và quyền truy cập được cập nhật.

Ngoại lệ:

- Tài khoản không tồn tại hoặc chưa đủ điều kiện.
- Liên kết trùng.
- Actor tự nâng quyền đặc quyền.
- Doctor/Patient ngoài tenant hoặc không còn hợp lệ.
- Xung đột thay đổi.
- Không đủ quyền: từ chối và không thay đổi dữ liệu.

Thu hồi:

- Thu hồi membership và assignment theo chính sách riêng.
- Không xóa lịch sử kết quả/review chỉ vì thu hồi quan hệ.
- Assignment bị thu hồi phải mất quyền truy cập tương ứng.
- Không chuyển tenant bằng cách sửa hồ sơ tùy ý.

Cần chốt:

- Ai được mời/gỡ thành viên và phân công.
- Một Patient có thể có bao nhiêu Doctor.
- Quan hệ Patient với nhiều Clinic.
- Chính sách khi Doctor/Patient rời Clinic.

### UC-C03 — Bulk upload và theo dõi batch

Actor chính: Clinic operator có quyền upload.

Tiền điều kiện:

- Actor được xác thực và có quyền trong tenant.
- Patient hoặc ngữ cảnh liên kết ảnh hợp lệ.
- Định dạng, modality và giới hạn upload được hỗ trợ.
- Điều kiện package/credit được kiểm tra theo chính sách.

Luồng chính:

1. Actor chọn ảnh và thông tin liên kết.
2. UI hiển thị danh sách ảnh để kiểm tra.
3. Actor gửi yêu cầu qua Gateway.
4. Backend xác thực và kiểm tra quyền.
5. Backend kiểm tra dữ liệu theo contract.
6. Hệ thống lưu bền vững dữ liệu cần thiết.
7. Hệ thống tiếp nhận yêu cầu theo public contract.
8. Job được xử lý qua luồng Analysis.
9. UI hiển thị trạng thái từng ảnh và trạng thái batch.
10. Actor xem kết quả hoặc lỗi và hành động phục hồi.

Ngoại lệ:

- Ảnh hỏng/sai định dạng/quá giới hạn.
- Modality chưa hỗ trợ.
- Không đủ credit hoặc không đủ quyền.
- Request trùng.
- Một số ảnh xử lý thất bại.
- Timeout/mất kết nối.
- AI lỗi hoặc quality rejection.

Quy tắc:

- Không tự thay đổi HTTP statuses hoặc cơ chế partial acceptance
  ngoài contract đã chốt.
- Lỗi validation trước tiếp nhận và lỗi xử lý sau tiếp nhận
  phải được phân biệt.
- Một ảnh lỗi sau tiếp nhận không mặc nhiên làm mọi ảnh thất bại.
- Retry không tạo kết quả hoặc trừ credit hai lần.
- Ảnh đã tiếp nhận được bảo toàn khi AI lỗi.
- Processing/failed không được hiển thị nguy cơ thấp suy diễn.

Giới hạn hiện tại:

- JPEG/PNG và Fundus là baseline hiện hành.
- OCT chưa hỗ trợ.
- Mục tiêu >=100 ảnh/batch cần test riêng.
- NiFi/MSW PoC không chứng minh bulk backend đã chạy.

Cần chốt:

- Cách ánh xạ nhiều ảnh với từng Patient.
- Quy trình một batch chứa nhiều Patient.
- Giới hạn mỗi request và chính sách partial acceptance.
- Cách tổng hợp trạng thái batch.

### UC-C04 — Theo dõi báo cáo và dữ liệu tổng hợp

Actor chính: Clinic operator có quyền xem báo cáo.

Luồng chính:

1. Actor chọn phạm vi thời gian và bộ lọc được phép.
2. Backend xác định tenant và quyền.
3. Hệ thống trả danh sách phân tích/báo cáo được phép.
4. Actor mở chi tiết.
5. Hệ thống kiểm tra quyền file và dữ liệu chi tiết.
6. Hệ thống hiển thị AI result và doctor review riêng.
7. Actor xem thống kê tổng hợp với định nghĩa phép tính.

Ngoại lệ:

- Không có dữ liệu.
- Dữ liệu chưa hoàn tất.
- Quyền bị thu hồi.
- File không truy cập được.
- Lỗi truy vấn hoặc dữ liệu chưa đồng bộ.

Quy tắc:

- Không trả báo cáo ngoài tenant/phạm vi được phép.
- Mock data phải được nhận diện và tách khỏi thống kê thật.
- Phân bố nguy cơ chỉ dùng dữ liệu có kết quả hợp lệ.
- Phải nêu rõ đang tính theo ảnh, phân tích hay Patient.
- Không đánh đồng AI finding với kết luận Doctor.

### UC-C05 — Quản lý chiến dịch và tạo báo cáo

Actor chính: Clinic operator có quyền chiến dịch.

Đây là thiết kế đề xuất để đáp ứng FR-26.
Contract và schema campaign chưa được chốt.

Luồng chính:

1. Actor tạo thông tin chiến dịch.
2. Hệ thống kiểm tra quyền, dữ liệu và phạm vi Clinic.
3. Actor liên kết các Patient/batch/analysis được phép.
4. Hệ thống ghi nhận quan hệ có truy vết.
5. Actor chọn tiêu chí báo cáo.
6. Hệ thống tổng hợp dữ liệu theo định nghĩa đã chốt.
7. Hệ thống tạo báo cáo và ghi thời điểm/phạm vi dữ liệu.
8. Actor xem hoặc tải báo cáo theo quyền.

Ngoại lệ:

- Trùng hoặc thiếu dữ liệu.
- Liên kết tài nguyên ngoài phạm vi.
- Phân tích chưa hoàn tất.
- Một Patient có nhiều lần phân tích.
- Lỗi tạo báo cáo.

Quy tắc:

- Không tính trùng Patient/ảnh mà không có định nghĩa rõ.
- Nêu thời điểm snapshot, tiêu chí lựa chọn và dữ liệu thiếu.
- Báo cáo không sửa kết quả AI hoặc review gốc.
- Hủy/đóng chiến dịch không tự xóa hồ sơ bệnh nhân.

Cần chốt:

- Campaign fields và lifecycle.
- Tiêu chí liên kết Patient/batch/analysis.
- Cách tính người được sàng lọc, ảnh và lần phân tích.
- Quyền tạo/đóng/sửa chiến dịch.
- Định dạng và nội dung báo cáo.

### UC-C06 — Theo dõi số ảnh và sử dụng gói

Actor chính: Clinic operator có quyền xem usage.

Luồng chính:

1. Actor chọn khoảng thời gian.
2. Hệ thống kiểm tra quyền Clinic.
3. Hệ thống lấy dữ liệu usage và Billing theo contract.
4. Hệ thống hiển thị số ảnh và package usage.
5. Actor xem chi tiết được phép.

Quy tắc:

- Số ảnh đã gửi, đã tiếp nhận, hoàn tất và thất bại
  phải được phân biệt.
- Image count không mặc nhiên bằng credit đã trừ.
- Retry không được làm usage/credit tăng trùng.
- Billing ledger là căn cứ credit; cache không quyết định số dư.
- Dữ liệu thiếu/chưa đồng bộ phải hiển thị rõ.

Cần chốt:

- Định nghĩa từng chỉ số.
- Quy tắc tính phí theo ảnh/request/kết quả.
- Xử lý failed, quality rejection và refund.

### UC-C07 — Mua hoặc gia hạn Clinic package

Actor chính: Clinic operator có quyền mua dịch vụ.

Luồng chính:

1. Actor xem package được phép mua.
2. Actor chọn package/gia hạn.
3. Hệ thống hiển thị giá, credit và điều khoản áp dụng.
4. Actor xác nhận giao dịch.
5. Billing xử lý theo phương thức thanh toán được chốt.
6. Backend xác minh trạng thái từ nguồn thanh toán tin cậy.
7. Hệ thống cập nhật giao dịch và quyền lợi idempotently.
8. Actor xem kết quả và lịch sử.

Ngoại lệ:

- Package không còn hiệu lực.
- Giá/quyền lợi thay đổi trước xác nhận.
- Thanh toán thất bại hoặc chưa xác định.
- Callback trùng hoặc đến không đúng thứ tự.
- Không đủ quyền mua.

Quy tắc:

- Không xác nhận thanh toán từ UI hoặc query parameter.
- Không cấp credit hai lần do callback/retry.
- Package catalog PoC không thay payment implementation.
- Quy trình refund/cancel cần được chốt riêng.

### UC-C08 — Nhận cảnh báo nguy cơ hoặc xu hướng

Actor chính: Clinic operator có quyền nhận cảnh báo.
Actor phối hợp: Notification service.

Luồng chính:

1. Có dữ liệu và điều kiện cảnh báo hợp lệ.
2. Hệ thống đánh giá theo rule/version được chốt.
3. Hệ thống xác định người nhận được phép.
4. Hệ thống ghi nhận cảnh báo và trạng thái gửi.
5. Người nhận mở thông báo.
6. Backend kiểm tra quyền trước khi trả dữ liệu chi tiết.

Quy tắc:

- Không phát cảnh báo lâm sàng thật từ mock result.
- Failed/processing không được suy thành risk.
- Push chỉ có nội dung chung, không chứa PII/kết quả sức khỏe.
- Không mặc nhiên coi notification sent là người nhận đã đọc.
- Có kiểm soát gửi trùng và audit phù hợp.

Cần chốt:

- Clinical rule, ngưỡng và nguồn kết quả.
- AI-only hay Doctor-reviewed là điều kiện cảnh báo.
- Cách tính trend và minimum data.
- Recipients, kênh gửi và escalation.
- Điều kiện đóng hoặc xác nhận cảnh báo.

### UC-C09 — Xuất thống kê nghiên cứu hoặc quản lý

Actor chính: Clinic operator có quyền export.

Luồng chính:

1. Actor chọn phạm vi, mục đích và định dạng được hỗ trợ.
2. Backend kiểm tra quyền và chính sách dữ liệu.
3. Hệ thống tạo dataset tổng hợp theo tiêu chí đã chốt.
4. Hệ thống thực hiện data minimization/ẩn danh phù hợp.
5. Hệ thống ghi nhận export và audit.
6. Actor tải file qua cơ chế truy cập được phép.

Ngoại lệ:

- Không đủ quyền.
- Phạm vi dữ liệu không hợp lệ.
- Export quá lớn.
- Có nguy cơ lộ danh tính.
- Lỗi tạo file hoặc link hết hạn.

Quy tắc:

- Không mặc nhiên xuất tên, ảnh hoặc ID trực tiếp của Patient.
- Không coi thay ID là đủ để ẩn danh.
- Kiểm soát nhóm nhỏ và khả năng tái nhận diện.
- PDF/CSV và chuẩn y tế cần thống nhất phạm vi.
- File export phải có thời hạn/quyền truy cập phù hợp.
- Dữ liệu nghiên cứu cần tuân thủ quyền sử dụng và consent.

## 6. Yêu cầu UI chung

- Loading, empty, error và session expired có hướng xử lý.
- Permission denied không hiển thị dữ liệu ngoài quyền.
- Batch hiển thị trạng thái riêng từng ảnh.
- AI status, doctor review và payment status tách biệt.
- Mock data có nhãn rõ.
- Không mất lựa chọn/dữ liệu đã nhập khi lỗi nếu có thể phục hồi.
- Thao tác retry/save có kiểm soát gửi trùng.
- Giao diện responsive và thao tác được bằng bàn phím.

## 7. NFR liên quan

| NFR | Liên quan |
| --- | --- |
| NFR-2 | Bulk processing >=100 ảnh/batch. |
| NFR-3 | Truy xuất dashboard/report. |
| NFR-5 | AI lỗi, bảo toàn ảnh. |
| NFR-8 | Multi-clinic và tải hệ thống. |
| NFR-9 | Mã hóa dữ liệu. |
| NFR-10 | Bảo vệ dữ liệu y tế. |
| NFR-11 | Ẩn danh dữ liệu nghiên cứu/retraining. |
| NFR-12 | RBAC, tenant và resource authorization. |
| NFR-13 | Responsive/accessibility. |
| NFR-14 | Hành trình upload/xem kết quả. |
| NFR-18 | Logging/audit. |
| NFR-20 | Export formats. |
| NFR-23 | Version và threshold truy vết. |

## 8. Kiểm thử dự kiến

| Mã | Trường hợp |
| --- | --- |
| TC-C01 | Đăng ký thiếu dữ liệu, duyệt/từ chối và audit. |
| TC-C02 | Membership/assignment, cross-tenant và quyền sau revoke. |
| TC-C03 | Batch hợp lệ, ảnh lỗi, partial failure và duplicate request. |
| TC-C04 | Report scope, empty data và phân biệt AI/Doctor review. |
| TC-C05 | Campaign aggregation và kiểm tra duplicate Patient/analysis. |
| TC-C06 | Usage đối chiếu ledger; retry không tăng trùng. |
| TC-C07 | Payment failure/duplicate callback; credit cấp một lần. |
| TC-C08 | Alert rule/version/recipients và payload không lộ dữ liệu. |
| TC-C09 | Export permission, data minimization và file expiry. |

Các mã trên là test design, chưa phải test đã chạy.
Chi tiết được mở rộng trong SCRUM-58.

## 9. Phạm vi triển khai theo giai đoạn

Đề xuất Sprint 2:

- Clinic/membership/assignment contract.
- Migration và seed giả.
- API Clinic/member/assignment theo phạm vi được chốt.
- Backend tenant isolation và negative tests.

Các phần sau triển khai theo roadmap và phụ thuộc:

- Bulk Analysis toàn luồng.
- Campaign/report/aggregation.
- Package/payment/credit ledger.
- Clinical alerts.
- Research export.

Không xóa yêu cầu khỏi backlog chỉ vì chưa triển khai Sprint 2.

## 10. Tiêu chí review SCRUM-55

- Có đủ UC-C01 đến UC-C09, liên kết FR-22 đến FR-30.
- Có actors, quyền, luồng chính, ngoại lệ và hậu quả dữ liệu.
- Membership và assignment được phân biệt.
- Không tự đặt endpoint/schema trái contract.
- Không đưa mock/PoC thành chức năng thật.
- Campaign, usage, alert và export có các quyết định cần chốt.
- Có test design và NFR liên quan.
- Reviewer ghi nhận nhận xét thực tế.

## 11. Nguồn tham chiếu

- srs_v0.1.md
- requirements_traceability_matrix_v0.1.md
- Glossary và Business Rules v0.2.
- Architecture Decision Records.
- Analysis ERD, State Machine và ma trận quyền.
- contracts/analysis-openapi.yaml
- contracts/BASELINE-W1-v0.2.md
- SCRUM-55:
  https://nguyentruonghau2202.atlassian.net/browse/SCRUM-55
- Wireframe Clinic/Admin:
  https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/2031931

## 12. Lịch sử thay đổi

| Phiên bản | Ngày | Người biên soạn | Nội dung |
| --- | --- | --- | --- |
| 0.1 | 2026-10-11 | Nguyễn Trương Hậu | Dự thảo Clinic/Campaign use cases; chờ review. |
