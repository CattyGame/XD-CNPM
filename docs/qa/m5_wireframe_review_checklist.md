# AURA — Checklist review wireframe M5

Ngày tổng hợp: 2026-10-12.
Task liên quan: SCRUM-65, SCRUM-66, SCRUM-68, SCRUM-69.
Trạng thái: Đã kiểm tra bản xuất; chờ review thiết kế.

## 1. Tài liệu tham chiếu

- [Danh mục wireframe](../ux/wireframes/README.md)
- [Patient v0.2](../ux/wireframes/patient_wireframes_v0.2.pdf)
- [Clinic/Admin v0.1](../ux/wireframes/clinic_admin_wireframes_v0.1.pdf)
- [SRS](../requirements/srs_v0.1.md)
- [RTM](../requirements/requirements_traceability_matrix_v0.1.md)

## 2. Kiểm tra đã thực hiện

| Nội dung | Kết quả | Giới hạn |
| --- | --- | --- |
| Patient: khung không bị chia qua nhiều trang | Đạt kiểm tra bản xuất, 9 trang | Không thay thế review nghiệp vụ |
| Patient: nguồn draw.io nhúng trong PDF | Có, 9 trang thiết kế | Chưa lưu nguồn thành file draw.io riêng |
| Clinic/Admin: bố cục bản xuất | Không thấy lỗi chia khung khi rà soát 8 trang | Không thay thế review thiết kế |
| Clinic/Admin: nguồn chỉnh sửa | Chưa có trong tài liệu đã nhận | Cần bổ sung file nguồn |
| Tiêu đề trang Error States của Patient | Giữ nguyên theo yêu cầu người thao tác | Vẫn ghi “AURA — Packages & Payment” |

## 3. Checklist chờ review

Các mục dưới đây là việc cần kiểm tra, chưa phải kết quả PASS.

| Nội dung review | Task | Trạng thái |
| --- | --- | --- |
| Walkthrough luồng Patient upload đến xem kết quả; ghi số lần nhấp thực tế | SCRUM-65, SCRUM-66 | Chưa xác minh |
| Walkthrough Clinic bulk upload và xử lý thành công một phần | SCRUM-65, SCRUM-68 | Chưa xác minh |
| Đối chiếu điều hướng, nút và trạng thái với Design System | SCRUM-69 | Chờ review |
| Kiểm tra empty, loading, processing, failed, quality rejected và permission denied | SCRUM-66, SCRUM-68, SCRUM-69 | Chờ review |
| Đối chiếu nhãn Fundus/OCT với contract và phạm vi triển khai hiện tại; OCT chưa được hỗ trợ trong API đã kiểm chứng | SCRUM-66 | Chờ quyết định thiết kế |
| Kiểm tra thông điệp AI là sàng lọc, phân biệt kết quả AI và xác nhận bác sĩ | SCRUM-66 | Chờ review |
| Kiểm tra quyền truy cập theo vai trò Clinic và System Admin | SCRUM-68 | Chờ review |
| Kiểm tra nhãn input, lỗi và hướng phục hồi | SCRUM-69 | Chờ review |
| Kiểm tra tương phản màu theo token Design System | SCRUM-69 | Chưa đo |
| Kiểm thử bàn phím, focus và screen reader trên giao diện chạy thực tế | SCRUM-69 | Chưa thực hiện |
| Bổ sung nguồn chỉnh sửa Clinic/Admin | SCRUM-68 | Còn thiếu |

PDF tĩnh không chứng minh hành vi bàn phím, focus, screen reader
hoặc phân quyền backend.

## 4. Ghi nhận review

Reviewer và ngày review: Chưa ghi nhận.

Khi thực hiện review, ghi từng phát hiện, màn hình liên quan,
mức độ ảnh hưởng, người xử lý và bằng chứng kiểm tra lại.
Không đánh dấu checklist đạt chỉ vì tài liệu đã được tạo
hoặc task Jira đã chuyển Done.

## Bản draw.io nhập từ PDF ngày 2026-10-12

Đã bổ sung [Clinic/Admin draw.io](../ux/wireframes/clinic_admin_wireframes_v0.1.drawio)
với 8 tab chứa ảnh màn hình từ PDF.

File hỗ trợ mở và sao chép các màn hình trong draw.io.
Chưa hỗ trợ chỉnh sửa riêng từng chữ, nút hoặc ô nhập.
Không coi file này là nguồn thiết kế gốc.

Phạm vi bàn giao hiện tại gồm PDF và bản draw.io chứa ảnh.
Yêu cầu nguồn chỉnh sửa từng thành phần, nếu còn áp dụng,
vẫn chưa được đáp ứng.
