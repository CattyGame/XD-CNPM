# AURA — Admin và Configuration Use Cases v0.1

## 1. Thông tin tài liệu

- Dự án: AURA — SP26SE025.
- Task: SCRUM-56.
- Người biên soạn: Nguyễn Trương Hậu, tiếp nhận công việc M3.
- Ngày cập nhật: 2026-10-11.
- Trạng thái: DRAFT — chưa được review và phê duyệt.
- Reviewer đề xuất: M2 về dữ liệu và quyền; M4 về AI;
  M1/M6 về vận hành; M5 về UI.
- Phạm vi: FR-31 đến FR-39 và quy trình thay đổi cấu hình AI.
- Tài liệu liên quan: SRS, RTM, permissions matrix, ADR
  và các contract hiện có của dự án.

Đây là đặc tả hành vi cần triển khai. Tài liệu không xác nhận
các chức năng Admin đã chạy trên backend hoặc môi trường staging.

Endpoint, schema và tên trạng thái trong tài liệu này chỉ trở thành
contract chính thức sau khi được thống nhất và cập nhật trong contracts.

## 2. Actor và nguyên tắc quyền

| Actor | Trách nhiệm |
| --- | --- |
| Admin | Quản lý tài khoản, tổ chức, cấu hình và chính sách trong phạm vi quyền được cấp |
| Người phụ trách AI | Đề xuất, kiểm chứng model và ngưỡng trước khi kích hoạt |
| Người vận hành hệ thống | Triển khai, giám sát, khôi phục cấu hình và dịch vụ |
| Clinic/Doctor/User | Sử dụng chức năng được cấp; không tự thay đổi quyền Admin |
| Backend/Worker | Kiểm tra quyền, áp dụng phiên bản cấu hình và ghi audit |

Nguyên tắc:

- Backend kiểm tra danh tính, vai trò và quyền tài nguyên cho mỗi thao tác.
- Không tin role, clinicId hoặc quyền do frontend tự khai báo.
- Quyền Admin không mặc nhiên cho phép xem mọi dữ liệu sức khỏe.
- Tách quyền quản trị tài khoản khỏi quyền đọc dữ liệu bệnh nhân.
- Thao tác nhạy cảm phải có audit và lý do thay đổi.
- Tài khoản bị vô hiệu hóa phải bị chặn theo cơ chế thu hồi quyền
  được thống nhất với thiết kế authentication.
- Dữ liệu demo và dữ liệu thật phải được phân biệt trong dashboard.
- Không ghi token, mật khẩu, ảnh hoặc nội dung y tế vào log thông thường.

## 3. Danh sách use case

| ID | Use case | Yêu cầu |
| --- | --- | --- |
| UC-A01 | Quản lý tài khoản | FR-31 |
| UC-A02 | Quản lý vai trò và quyền | FR-32 |
| UC-A03 | Quản lý cấu hình và phiên bản AI | FR-33 |
| UC-A04 | Quản lý gói dịch vụ và giá | FR-34 |
| UC-A05 | Xem dashboard toàn hệ thống | FR-35 |
| UC-A06 | Xem thống kê vận hành | FR-36 |
| UC-A07 | Quản lý audit và chính sách dữ liệu | FR-37 |
| UC-A08 | Phê duyệt hoặc đình chỉ Clinic | FR-38 |
| UC-A09 | Quản lý mẫu và chính sách thông báo | FR-39 |

## 4. Đặc tả use case

### UC-A01 — Quản lý tài khoản

**Actor:** Admin có quyền quản lý tài khoản.

**Tiền điều kiện:** Đã xác thực và được backend cấp quyền tương ứng.

**Luồng chính:**

1. Tìm kiếm tài khoản theo tiêu chí được phép.
2. Xem thông tin quản trị cần thiết.
3. Chọn chỉnh sửa, kích hoạt hoặc vô hiệu hóa.
4. Nhập lý do cho thao tác nhạy cảm và xác nhận.
5. Backend kiểm tra quyền, dữ liệu và phiên bản hiện tại.
6. Lưu thay đổi, ghi audit và trả trạng thái thực tế.

**Ngoại lệ và ràng buộc:**

- Không cho sửa thuộc tính định danh do nhà cung cấp xác thực quản lý
  bằng cách cập nhật tùy ý trong database ứng dụng.
- Không hiển thị mật khẩu hoặc token.
- Không xóa lịch sử phân tích khi vô hiệu hóa tài khoản.
- Khi có chỉnh sửa đồng thời, báo xung đột theo contract.
- Cần cơ chế bảo vệ để không làm mất toàn bộ tài khoản quản trị hợp lệ.

**Cần chốt:** Trường được chỉnh sửa, cơ chế thu hồi phiên và chính sách
tự vô hiệu hóa tài khoản Admin.

### UC-A02 — Quản lý vai trò và quyền

**Actor:** Admin có quyền quản lý phân quyền.

**Luồng chính:**

1. Chọn tài khoản hoặc vai trò.
2. Xem quyền hiện tại và phạm vi áp dụng.
3. Đề xuất thay đổi quyền.
4. Backend kiểm tra quyền của người thực hiện và giới hạn cấp quyền.
5. Lưu thay đổi và ghi audit.
6. Áp dụng thay đổi theo cơ chế cập nhật hoặc thu hồi quyền đã thống nhất.

**Ngoại lệ và ràng buộc:**

- Không cho người dùng tự nâng quyền.
- Không chỉ ẩn nút trên UI để bảo vệ API.
- Role không thay thế kiểm tra tenant, ownership và Doctor assignment.
- Thu hồi membership hoặc assignment phải làm mất quyền truy cập tương ứng.
- Không mặc nhiên xóa dữ liệu lịch sử khi thu hồi quyền.

**Cần chốt:** Role cố định hay tùy biến, cách kết hợp nhiều role,
thời điểm quyền mới có hiệu lực và bảo vệ tài khoản quản trị cuối cùng.

### UC-A03 — Quản lý cấu hình và phiên bản AI

**Actor:** Người có quyền đề xuất, kiểm chứng hoặc kích hoạt cấu hình AI.

**Luồng chính:**

1. Xem phiên bản đang hoạt động và các bản dự thảo.
2. Tạo bản cấu hình mới, không sửa trực tiếp bản đã dùng cho kết quả.
3. Kiểm tra schema, phạm vi giá trị và tương thích model.
4. Chạy kiểm chứng offline hoặc staging với dữ liệu được phép.
5. Ghi kết quả kiểm chứng, giới hạn và người review.
6. Người có quyền kích hoạt chọn phiên bản đã được chấp thuận.
7. Hệ thống áp dụng cấu hình cho job mới theo chính sách đã thống nhất.
8. Giám sát và rollback nếu phát hiện lỗi.

**Ngoại lệ và ràng buộc:**

- Không kích hoạt cấu hình không hợp lệ hoặc chưa có bằng chứng kiểm chứng.
- Không dùng ngưỡng phân đoạn mạch máu như ngưỡng nguy cơ bệnh.
- Job phải gắn với phiên bản model và cấu hình xác định.
- Retry cùng một job không tự chuyển sang cấu hình khác.
- Không tự tính lại hoặc ghi đè kết quả lịch sử.
- Rollback không xóa các phiên bản đã được sử dụng.
- Nếu không tìm được phiên bản đã gắn với job, trả lỗi rõ ràng;
  không âm thầm dùng phiên bản hiện tại.

**Cần chốt:** Quy trình phê duyệt, metadata chính thức, cơ chế kích hoạt,
khả năng giữ phiên bản cũ và chính sách phân tích lại.

### UC-A04 — Quản lý gói dịch vụ và giá

**Actor:** Admin có quyền quản lý danh mục dịch vụ.

**Luồng chính:**

1. Xem hoặc tạo gói dịch vụ.
2. Nhập giá, đơn vị tiền tệ, quyền lợi và thời hạn.
3. Kiểm tra dữ liệu.
4. Kích hoạt hoặc ngừng bán theo chính sách.
5. Ghi audit và trả kết quả.

**Ngoại lệ và ràng buộc:**

- Không sửa giá đã ghi nhận trong giao dịch lịch sử.
- Ngừng bán không tự hủy quyền lợi đã mua.
- Không dùng cập nhật danh mục để chỉnh trực tiếp số dư credit.
- Credit phải được quản lý qua ledger và quy tắc giao dịch.
- Callback thanh toán cần xác thực và xử lý idempotent.

**Cần chốt:** Cấu trúc quyền lợi, hiệu lực giá mới, hoàn tiền,
hết hạn và chính sách credit khi phân tích thất bại.

### UC-A05 — Xem dashboard toàn hệ thống

**Actor:** Admin có quyền xem thống kê tổng hợp.

**Luồng chính:**

1. Chọn khoảng thời gian và bộ lọc được phép.
2. Backend tổng hợp số liệu từ nguồn dữ liệu xác định.
3. Hiển thị usage, doanh thu và chỉ số AI có dữ liệu hợp lệ.
4. Hiển thị thời điểm cập nhật và định nghĩa chỉ số.

**Ngoại lệ và ràng buộc:**

- Dữ liệu demo không được trình bày như số liệu vận hành thật.
- Thiếu dữ liệu phải hiển thị chưa có dữ liệu.
- Phân biệt đơn hàng, thanh toán thành công, hoàn tiền và doanh thu.
- Chất lượng phân đoạn offline không chứng minh độ chính xác
  dự đoán nguy cơ bệnh trong môi trường lâm sàng.
- Thống kê tổng hợp không mặc nhiên cấp quyền xem hồ sơ bệnh nhân.

**Cần chốt:** Công thức chỉ số, nguồn dữ liệu, timezone,
độ trễ cập nhật và quyền drill-down.

### UC-A06 — Xem thống kê vận hành

**Actor:** Admin hoặc người vận hành có quyền phù hợp.

**Luồng chính:**

1. Chọn dịch vụ, khoảng thời gian hoặc phiên bản.
2. Xem số ảnh, trạng thái job, thời gian xử lý và tỷ lệ lỗi.
3. Phân tích lỗi theo mã lỗi và correlationId.
4. Chuyển đến thông tin kỹ thuật được phép để xử lý sự cố.

**Ngoại lệ và ràng buộc:**

- Phân biệt upload, accepted, completed, failed và quality-reject.
- Không gán mức nguy cơ cho job chưa có kết quả hợp lệ.
- Xác định mẫu số khi tính tỷ lệ lỗi hoặc phân bố nguy cơ.
- Phân biệt thời gian chờ queue, inference và tổng thời gian xử lý.
- Không hiển thị payload y tế hoặc token trong dashboard kỹ thuật.

**Cần chốt:** Hệ thống thu thập metrics, thời gian lưu,
mức cảnh báo và bộ lọc.

### UC-A07 — Quản lý audit và chính sách dữ liệu

**Actor:** Người được cấp quyền audit hoặc quản trị chính sách dữ liệu.

**Luồng chính:**

1. Tìm audit theo actor, hành động, tài nguyên và thời gian.
2. Xem thông tin trong phạm vi quyền.
3. Đề xuất cập nhật chính sách lưu giữ hoặc truy cập dữ liệu.
4. Kiểm tra tác động và ghi nhận phê duyệt.
5. Áp dụng chính sách và ghi audit.

**Ngoại lệ và ràng buộc:**

- Không cho sửa hoặc xóa audit tùy ý qua UI quản trị.
- Việc đọc hoặc export dữ liệu nhạy cảm cần được kiểm soát.
- Không ghi bí mật và dữ liệu sức khỏe không cần thiết vào audit.
- Mã hóa, backup và xóa dữ liệu cần có thiết kế và bằng chứng riêng.
- Không tuyên bố tuân thủ pháp lý chỉ dựa trên tài liệu hoặc một checklist.

**Cần chốt:** Thời hạn lưu, quyền export, yêu cầu xóa dữ liệu,
bảo vệ audit và quy trình xử lý sự cố.

### UC-A08 — Phê duyệt hoặc đình chỉ Clinic

**Actor:** Admin có quyền quản lý tổ chức.

**Luồng chính:**

1. Xem hồ sơ Clinic đang chờ xử lý.
2. Kiểm tra thông tin và tài liệu xác minh.
3. Phê duyệt, yêu cầu bổ sung hoặc từ chối kèm lý do.
4. Với Clinic đang hoạt động, có thể đình chỉ theo chính sách.
5. Ghi audit và gửi thông báo phù hợp.

**Ngoại lệ và ràng buộc:**

- Clinic chưa được phê duyệt không tự nhận quyền vận hành đầy đủ.
- Đình chỉ không xóa hồ sơ bệnh nhân hoặc kết quả lịch sử.
- Không mặc nhiên vô hiệu hóa mọi tài khoản cá nhân thuộc Clinic.
- Cần xác định cách xử lý job đang chạy khi Clinic bị đình chỉ.
- Kiểm tra quyền ở backend cho trạng thái tổ chức tương ứng.

**Cần chốt:** Tiêu chí xác minh, trạng thái chính thức,
quyền trước phê duyệt và cách xử lý job hoặc gói dịch vụ khi đình chỉ.

### UC-A09 — Quản lý mẫu và chính sách thông báo

**Actor:** Admin có quyền quản lý thông báo.

**Luồng chính:**

1. Chọn loại sự kiện và kênh thông báo.
2. Tạo hoặc chỉnh sửa bản dự thảo của mẫu.
3. Kiểm tra nội dung, biến được phép và người nhận.
4. Preview bằng dữ liệu giả lập.
5. Phê duyệt và kích hoạt phiên bản.
6. Ghi audit và giám sát trạng thái gửi.

**Ngoại lệ và ràng buộc:**

- Push dùng nội dung chung; không chứa dữ liệu sức khỏe cá nhân.
- Không cho mẫu tùy ý chèn dữ liệu bệnh nhân vào push.
- Deep link phải kiểm tra quyền khi người dùng mở.
- Không coi gửi thành công là người nhận đã đọc.
- Thông báo hoàn tất phân tích không tự khẳng định có nguy cơ cao.
- Cảnh báo nguy cơ cần dữ liệu và quy tắc đã được kiểm chứng.
- Demo MSW không chứng minh gửi FCM thật.

**Cần chốt:** Kênh hỗ trợ, ngôn ngữ, biến mẫu, retry,
chống gửi trùng và cơ chế thu hồi token khi đăng xuất.

## 5. Quy trình thay đổi cấu hình AI

### 5.1. Vòng đời đề xuất

Draft -> Validated -> Approved -> Active -> Retired.

Rejected và Rolled back là các kết quả xử lý cần được thiết kế rõ.

Đây là vòng đời đề xuất, chưa phải enum hoặc API contract chính thức.

### 5.2. Metadata cần thống nhất

- Định danh và phiên bản cấu hình.
- Phiên bản model hoặc thuật toán.
- Bộ tham số và ngưỡng có ý nghĩa rõ ràng.
- Schema hoặc contract version tương thích.
- Nguồn dataset và phiên bản bộ đánh giá.
- Kết quả kiểm chứng, giới hạn và đường dẫn bằng chứng.
- Người đề xuất, người review và người kích hoạt.
- Thời điểm tạo, phê duyệt và kích hoạt.
- Lý do thay đổi và phiên bản có thể rollback.

Tên field và cách lưu sẽ được chốt trong thiết kế dữ liệu và contract.

### 5.3. Tính nhất quán khi xử lý job

- Chọn và lưu phiên bản cho job trước khi thực hiện inference.
- Worker sử dụng đúng phiên bản đã gắn với job.
- Kích hoạt bản mới áp dụng cho job mới theo chính sách.
- Job đang chạy và retry giữ nguyên phiên bản.
- Kết quả lưu thông tin truy vết model và ngưỡng đã sử dụng.
- Phân tích lại là một yêu cầu có định danh và lịch sử riêng.
- Không thay đổi kết quả lịch sử để làm khớp cấu hình hiện tại.

### 5.4. Kích hoạt và rollback

- Kiểm tra phiên bản có thể tải và xử lý trước khi kích hoạt.
- Chỉ chuyển phiên bản active sau khi kiểm tra thành công.
- Giám sát lỗi, latency và tính hợp lệ của đầu ra.
- Khi rollback, giữ lịch sử kích hoạt và lý do.
- Nếu chưa hỗ trợ cập nhật không downtime, phải ghi nhận giới hạn
  và kế hoạch đáp ứng NFR-16.
- Không tuyên bố đã đáp ứng NFR-16 khi chưa kiểm thử vận hành.

## 6. Yêu cầu UI và xử lý lỗi

- Có loading, empty, success và error rõ ràng.
- Khóa thao tác lặp khi request đang xử lý.
- Có xác nhận và lý do cho thao tác nhạy cảm.
- Lỗi xác thực hướng dẫn đăng nhập lại.
- Lỗi quyền không tiết lộ tài nguyên ngoài phạm vi.
- Xung đột chỉnh sửa hướng dẫn tải lại dữ liệu.
- Timeout không hiển thị thay đổi đã thành công khi chưa xác nhận.
- Retry thao tác ghi dữ liệu phải tuân thủ quy tắc idempotency.
- Modal hỗ trợ bàn phím và quản lý focus.
- Hiển thị rõ dữ liệu demo và phiên bản đang xem.

## 7. Yêu cầu phi chức năng liên quan

| Yêu cầu | Áp dụng |
| --- | --- |
| NFR-3 | Đo thời gian tải dashboard và truy xuất kết quả |
| NFR-9 | Bảo vệ dữ liệu khi truyền và lưu |
| NFR-10 | Thiết kế và đánh giá chính sách bảo vệ dữ liệu |
| NFR-12 | RBAC và kiểm tra quyền tài nguyên |
| NFR-13 | UI responsive và hỗ trợ truy cập |
| NFR-16 | Cập nhật AI/config không downtime theo thiết kế và kiểm chứng |
| NFR-17 | Phân tách module và trách nhiệm |
| NFR-18 | Logging, auditing và error tracking tập trung |
| NFR-23 | Truy vết phiên bản model và ngưỡng cho từng kết quả |

## 8. Test scenario dự kiến

Các scenario dưới đây là thiết kế kiểm thử, chưa phải kết quả PASS.

| ID | Scenario | Kết quả mong đợi |
| --- | --- | --- |
| TC-A01 | Vô hiệu hóa tài khoản | Chặn truy cập theo chính sách, giữ lịch sử và có audit |
| TC-A02 | User tự cấp role Admin | Backend từ chối, quyền không thay đổi |
| TC-A03 | Kích hoạt cấu hình AI không hợp lệ | Từ chối và giữ phiên bản active hiện tại |
| TC-A04 | Thay đổi giá gói dịch vụ | Giao dịch lịch sử giữ nguyên giá đã ghi nhận |
| TC-A05 | Dashboard chỉ có dữ liệu demo | Có nhãn demo, không trình bày như số liệu thật |
| TC-A06 | Job processing chưa có kết quả | Không đưa vào phân bố nguy cơ hợp lệ |
| TC-A07 | Người không có quyền đọc audit | Backend từ chối và không trả dữ liệu nhạy cảm |
| TC-A08 | Đình chỉ Clinic | Áp dụng quyền theo chính sách, không xóa lịch sử |
| TC-A09 | Mẫu push chứa biến dữ liệu sức khỏe | Không cho kích hoạt mẫu vi phạm chính sách |
| TC-A10 | Job retry sau khi đổi cấu hình active | Giữ model/config đã gắn với job |
| TC-A11 | Rollback cấu hình | Job mới dùng bản được chọn, kết quả cũ giữ nguyên |
| TC-A12 | Hai Admin cùng chỉnh sửa | Phát hiện xung đột theo contract |

Test Plan sẽ bổ sung tiền điều kiện, dữ liệu, bước chạy,
môi trường, actual result và bằng chứng.

## 9. Các quyết định còn mở

1. Ma trận quyền Admin chi tiết và phạm vi xem dữ liệu sức khỏe.
2. Cơ chế thu hồi phiên khi tài khoản hoặc quyền bị thay đổi.
3. Quy tắc bảo vệ tài khoản quản trị cuối cùng.
4. Người có quyền đề xuất, phê duyệt và kích hoạt cấu hình AI.
5. Schema cấu hình và tương thích với AI service.
6. Cách giữ phiên bản cũ cho job đang chạy hoặc retry.
7. Quy tắc hoàn tiền, credit và hiệu lực thay đổi giá.
8. Định nghĩa chỉ số dashboard, nguồn dữ liệu và timezone.
9. Chính sách lưu giữ dữ liệu, audit và quyền export.
10. Cách xử lý job khi Clinic bị đình chỉ.
11. Template và contract thông báo chính thức.
12. Bằng chứng cần có để xác nhận cập nhật AI không downtime.

## 10. Phạm vi triển khai trước mắt

Ưu tiên cho sprint tiếp theo:

- Authentication, trạng thái tài khoản và RBAC nền tảng.
- Kiểm tra quyền ở Gateway/backend.
- Thiết kế dữ liệu Clinic membership và Doctor assignment.
- Truy vết phiên bản AI và cấu hình cho kết quả.
- Audit tối thiểu cho thao tác quản trị quan trọng.

Dashboard doanh thu, payment đầy đủ, retraining policy và quản trị
template thông báo được lên kế hoạch theo phụ thuộc thực tế.

## 11. Checklist review

- [ ] Đủ 9 use case và liên kết FR-31 đến FR-39.
- [ ] Quyền Admin và quyền xem dữ liệu sức khỏe được phân biệt.
- [ ] Quy trình thay đổi cấu hình AI được M4 review.
- [ ] Job đang chạy, retry và rollback có quy tắc rõ ràng.
- [ ] Kết quả lịch sử giữ khả năng truy vết.
- [ ] Các quyết định còn mở có người xử lý.
- [ ] RTM được cập nhật sau review.
- [ ] Test scenario được chuyển vào Test Plan.
- [ ] Tài liệu được review trên PR và đồng bộ Confluence.

## 12. Nguồn tham chiếu

- Đề tài AURA — SP26SE025: FR-31 đến FR-39 và các NFR liên quan.
- docs/requirements/srs_v0.1.md.
- docs/requirements/requirements_traceability_matrix_v0.1.md.
- docs/requirements/clinic_campaign_use_cases_v0.1.md.
- Permissions matrix, ADR và ERD hiện có của dự án.
- contracts/analysis-openapi.yaml.
- contracts/ai_service_openapi.yaml.
- contracts/BASELINE-W1-v0.2.md.
- Task SCRUM-56 trên Jira.

## 13. Lịch sử thay đổi

| Phiên bản | Ngày | Nội dung |
| --- | --- | --- |
| 0.1 | 2026-10-11 | Tạo bản dự thảo Admin use cases và quy trình cấu hình AI |
