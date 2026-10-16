\# ADR-004 — Private storage và quyền truy cập service



\- Dự án: AURA / SP26SE025.

\- Ngày cập nhật: 2026-10-16.

\- Owner: M1.

\- Task: SCRUM-300, SCRUM-301.

\- Trạng thái: Chấp nhận lựa chọn Azure Blob/Azurite cho Sprint 2.

\- Service identity JWT: cần hoàn thiện cùng M6, chưa ghi nhận đã triển khai đầy đủ.



\## Bối cảnh



Ảnh võng mạc và kết quả phân tích cần được lưu riêng tư.

Backend phải kiểm quyền tài nguyên trước khi cấp quyền đọc.

Các service cần tài khoản PostgreSQL riêng và cấu hình thống nhất

giữa Windows host và Docker.



\## Quyết định storage



\- Dùng Azure Blob Storage SDK trong backend.

\- Local dùng Azurite 3.33.0.

\- Ba container private: aura-originals, aura-annotations, aura-reports.

\- Dữ liệu Azurite nằm trong external volume aura\_azurite\_data.

\- Không bật anonymous public access.

\- Object key do backend sinh, không chứa tên/email bệnh nhân

&#x20; hoặc tên file do client cung cấp.

\- Public API dùng fileId; object key chỉ dùng nội bộ.

\- Signed URL tất yếu chứa đường dẫn blob nhưng không được sử dụng

&#x20; như định danh public ổn định hoặc ghi vào log.



\## Endpoint và URL đọc



\- SDK chạy trên Windows:

&#x20; http://127.0.0.1:10000/devstoreaccount1.

\- SDK chạy trong Docker:

&#x20; http://azurite:10000/devstoreaccount1.

\- URL trả cho trình duyệt local dùng 127.0.0.1.

\- Read SAS giới hạn đúng một blob, chỉ đọc, hết hạn sau 120 giây.

\- Kiểm quyền hiện hành trước mỗi lần cấp SAS.

\- Endpoint cấp URL trả Cache-Control: no-store.

\- Không log SAS.

\- URL đã cấp có thể tiếp tục dùng đến khi hết hạn.



M2 sở hữu adapter IPrivateFileStorage, metadata và tích hợp

quyền Patient/Billing/upload. Cấu hình storage không tự tạo

chức năng upload hoặc chứng minh adapter đã được sử dụng.



\## Quyết định database



\- PostgreSQL 17, database local aura\_dev.

\- Schema riêng: identity, profile, analysis, billing, notification.

\- Giữ billing\_poc phục vụ PoC cho đến khi có migration chuyển đổi.

\- Runtime role tương ứng aura\_<service>\_app.

\- Migration role tương ứng aura\_<service>\_migrator.

\- Runtime không có quyền quản trị, tạo schema hoặc sở hữu bảng.

\- Không truy vấn hoặc tạo FK xuyên schema giữa các service.

\- Các bảng Analysis append-only chỉ cấp SELECT/INSERT cho runtime.

\- Tài khoản aura\_dev chỉ dùng bootstrap/migration/quản trị local.

\- API không tạo schema/bảng hoặc seed khi khởi động.



\## Credentials và mã hóa local



\- Firebase service account nằm ngoài repository.

\- Gateway và Notification Docker nhận file qua bind mount read-only.

\- Khóa mã hóa FCM hiện có được giữ nguyên cùng encryption\_key\_id.

\- Script nạp môi trường không ghi mật khẩu hoặc khóa vào Git.

\- Không đưa backend secret vào VITE\_\*.



Azurite HTTP local không chứng minh TLS hoặc encryption at rest

của Azure cloud. Local chỉ sử dụng dữ liệu thử nghiệm.

Staging cần HTTPS, encryption at rest, tắt public access và

managed identity/RBAC riêng trước khi nghiệm thu.



\## Service identity cần hoàn thiện



Theo contract Sprint 2:

\- JWT RS256 do Identity phát, issuer aura-identity-local.

\- TTL 5 phút, audience theo service đích.

\- Client secret lưu dạng hash và xác thực trước issuance.

\- Scope/audience được giới hạn theo allowlist.

\- Private signing key chỉ Identity giữ.

\- Consumer dùng public key hoặc JWKS tin cậy.

\- Không public route /internal/v1 qua Gateway.



M1 chuẩn bị client identities, secret/key/config và ma trận quyền.

M6 hiện thực issuance/validation/context/provisioning.

Các nội dung này chưa được coi là hoàn tất chỉ vì database role

hoặc Firebase user authentication đã hoạt động.



\## Kết quả kiểm chứng local



\- Private containers, anonymous denial, read-only SAS,

&#x20; expired SAS và cleanup: PASS.

\- Runtime database role và isolation schema: PASS.

\- Bootstrap database mới và migration lặp: PASS.

\- Quyền Analysis append-only trên database tạm: PASS.

\- Billing đọc PostgreSQL bằng aura\_billing\_app: PASS.

\- Notification Docker dùng aura\_notification\_app: PASS.

\- Firebase thật qua Gateway Docker tới Notification Docker: PASS.



\## Hệ quả



\- Có cấu hình local tái sử dụng, giữ dữ liệu hiện có.

\- Chấp nhận phụ thuộc Azure Blob SDK tại adapter.

\- Cần xử lý compensation/reconciliation giữa Billing,

&#x20; storage và transaction Analysis.

\- Cloud deployment và service JWT có các bước nghiệm thu riêng.
