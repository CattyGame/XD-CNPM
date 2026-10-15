\## Sprint 2 — Phân quyền PostgreSQL theo service



\### Database và tài khoản



Database local: `aura\_dev`.



| Service | Schema | Runtime role | Migration role |

| --- | --- | --- | --- |

| Identity | identity | aura\_identity\_app | aura\_identity\_migrator |

| Profile | profile | aura\_profile\_app | aura\_profile\_migrator |

| Analysis | analysis | aura\_analysis\_app | aura\_analysis\_migrator |

| Billing | billing, billing\_poc | aura\_billing\_app | aura\_billing\_migrator |

| Notification | notification | aura\_notification\_app | aura\_notification\_migrator |



`billing\_poc` được giữ lại để phục vụ Billing PoC hiện tại.

Schema `billing` dành cho triển khai nghiệp vụ tiếp theo.



\### Quyền và nguyên tắc sử dụng



\- Runtime role có CONNECT database, USAGE schema của service,

&#x20; SELECT/INSERT/UPDATE/DELETE trên bảng và USAGE/SELECT trên sequence.

\- Runtime role không có quyền CREATE trong schema nghiệp vụ,

&#x20; không sở hữu bảng và không có quyền TRUNCATE.

\- Runtime role không được truy cập schema của service khác.

\- Migration role sở hữu schema và các bảng đã chuyển ownership.

\- Migration role giữ NOLOGIN; người chạy migration được ủy quyền

&#x20; sử dụng SET LOCAL ROLE trong transaction.

\- Không cấp SUPERUSER, CREATEDB, CREATEROLE hoặc BYPASSRLS

&#x20; cho runtime/migration role.

\- Các API sử dụng runtime role, không dùng tài khoản quản trị aura\_dev.

\- Không thực hiện CREATE TABLE hoặc seed dữ liệu khi API khởi động.



\### Thứ tự migration liên quan



1\. `001\_notification\_devices.sql`: cấu trúc đăng ký thiết bị Notification.

2\. `002\_service\_database\_roles.sql`: tạo role, schema và cấp quyền.

3\. `003\_billing\_poc\_packages.sql`: tạo bảng và seed Billing PoC

&#x20;  bằng `aura\_billing\_migrator`.



Đây là thứ tự cho nhóm migration nêu trên, không thay thế hướng dẫn

các migration riêng trong thư mục `analysis/`.



Migration 002 tạo các role ở trạng thái NOLOGIN.

Sau đó quản trị viên đặt mật khẩu bằng lệnh psql `\\password`

và bật LOGIN cho các role `\_app`.

Không bật LOGIN cho migrator trong cấu hình local hiện tại.



Migration 003 yêu cầu migration 002 đã chạy.

Dữ liệu gói đã tồn tại được giữ nguyên bằng

`ON CONFLICT (id) DO NOTHING`.



Các migration tạo bảng tiếp theo phải chạy dưới migration role

tương ứng để áp dụng đúng ownership và default privileges.



\### Kết nối local



\- Service chạy trên Windows: PostgreSQL tại `127.0.0.1:5432`

&#x20; theo cấu hình cổng local hiện tại.

\- Service trong Compose: PostgreSQL tại `postgres:5432`.

\- Mỗi service dùng runtime role và mật khẩu riêng.

\- Không commit mật khẩu, connection string có mật khẩu hoặc file backup.



Billing trong Compose dùng username `aura\_billing\_app`.

Biến `BILLING\_DB\_PASSWORD` cần được cung cấp khi Compose đọc

cấu hình hiện tại. Giá trị được chèn trong dấu nháy kép của

connection string Npgsql; dấu nháy kép bên trong mật khẩu

phải được nhân đôi trước khi truyền vào biến này.



Notification chạy trên Windows dùng:

\- `NotificationDatabase\_\_Database=aura\_dev`

\- `NotificationDatabase\_\_Username=aura\_notification\_app`

\- `NotificationDatabase\_\_Password`: cung cấp riêng khi chạy.



Khóa mã hóa token Notification được giữ nguyên.

Thay tài khoản database không yêu cầu tạo lại khóa mã hóa.



\### Kết quả kiểm tra local ngày 2026-10-15



\- Cả 5 runtime role kết nối PostgreSQL qua TCP thành công.

\- Kiểm thử bảng mới dưới migrator: runtime CRUD và sequence đạt.

\- Runtime bị từ chối CREATE, ALTER, TRUNCATE và truy cập chéo

&#x20; schema trong các trường hợp đã kiểm thử.

\- Transaction kiểm thử đã rollback.

\- Billing đọc danh sách gói từ PostgreSQL bằng aura\_billing\_app.

\- Dữ liệu gói Basic Updated được giữ nguyên sau migration.

\- Notification readiness trả True; PostgreSQL ghi nhận kết nối

&#x20; bằng aura\_notification\_app.



Identity, Profile và Analysis đã có tài khoản database được

kiểm thử; việc chuyển cấu hình và kiểm chứng runtime của

các service này chưa được xác nhận trong bước bàn giao này.



Các kiểm tra trên xác nhận quyền database local, không thay thế

kiểm thử phân quyền tài khoản, tenant hoặc tài nguyên tại API.
