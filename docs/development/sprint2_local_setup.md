\# AURA — Môi trường phát triển Sprint 2



Owner: M1. Task: SCRUM-301. Cập nhật: 2026-10-16.



\## Thành phần và cổng



| Thành phần | Cổng host local | Địa chỉ Docker |

| --- | --- | --- |

| Vite | 5173 | Chạy trên Windows |

| Gateway | 5000 | api-gateway:8080 |

| Analysis | 5001 | analysis-api:8080 |

| Billing | 5002 | billing-api:8080 |

| Notification | 5003 | notification-api:8080 |

| Identity | 5004 | identity-api:8080, chờ bản nguồn M6 tích hợp |

| Profile | 5005 | profile-api:8080, chờ bản nguồn M6 tích hợp |

| AI Core | 8000 | ai-core:8000 |

| PostgreSQL | 5432 | postgres:5432 |

| Redis | 6379 | redis:6379 |

| RabbitMQ | 5672, UI 15672 | rabbitmq:5672 |

| Azurite | 10000 | azurite:10000 |



Chỉ bind các cổng local trên loopback.

Không dùng localhost từ container để gọi container khác.



\## Sơ đồ triển khai



Đường liền thể hiện kết nối của cấu hình/runtime đã có.

Đường đứt thể hiện phần tích hợp còn cần hoàn thiện.



```mermaid

flowchart TD

&#x20;   WEB\["Web/Vite trên Windows"]



&#x20;   subgraph DOCKER\["Docker network AURA"]

&#x20;       GW\["Gateway"]

&#x20;       AN\["Analysis API"]

&#x20;       BI\["Billing API"]

&#x20;       NO\["Notification API"]

&#x20;       PG\["PostgreSQL"]

&#x20;       RE\["Redis"]

&#x20;       AZ\["Azurite"]

&#x20;       AI\["AI Core"]

&#x20;       MQ\["RabbitMQ"]

&#x20;   end



&#x20;   M6\["Identity/Profile: chờ tích hợp mã M6"]



&#x20;   WEB --> GW

&#x20;   GW --> AN

&#x20;   GW --> BI

&#x20;   GW --> NO

&#x20;   GW -.-> M6

&#x20;   BI --> PG

&#x20;   BI --> RE

&#x20;   NO --> PG

&#x20;   AN -.-> PG

&#x20;   AN -.-> AZ

&#x20;   AN -.-> AI

&#x20;   AN -.-> MQ

```



Analysis hiện có schema và mã xử lý upload, nhưng endpoint/persistence

và các adapter thực tế cần được M2 hoàn thiện.

Worker/publisher đầy đủ thuộc Sprint 3.



Container Identity/Profile còn lại từ checkout cũ không chứng minh

mã tương ứng đã được tích hợp vào repository hiện tại.



\## Hai cấu hình Compose



\- compose.yaml: nền tảng và các ứng dụng hiện có; CI sử dụng file này.

\- compose.dev.yaml: bổ sung Firebase mount/config cho Gateway

&#x20; và Notification Docker.

\- Dùng cả hai file khi chạy local có Firebase.



Không đưa credentials thật vào hai file Compose.



\## Chuẩn bị trên máy mới



1\. Cài Git, Docker Desktop Linux containers, .NET SDK theo global.json,

&#x20;  Node.js theo src/web/aura-web/.nvmrc.

2\. Nếu chưa có .env, copy từ .env.example.

3\. Điền mật khẩu bootstrap PostgreSQL và RabbitMQ vào .env local.

4\. Chuẩn bị Firebase service account cho đúng project dev tại:

&#x20;  %USERPROFILE%\\.aura\\credentials\\aura-dev-service-account.json.

5\. Chuẩn bị khóa AES Base64 32 byte tại:

&#x20;  %USERPROFILE%\\.aura\\credentials\\notification-token-key-v1.txt.

6\. Nếu dùng database có dữ liệu cũ, phải dùng lại đúng khóa và key ID.

&#x20;  Không tự sinh khóa mới để thay thế.

7\. Với database hoàn toàn mới, quản trị viên tạo khóa mới và quản lý

&#x20;  cùng cấu hình môi trường; script hiện dùng key ID local-v1.

8\. Tạo external volume Azurite:



```powershell

docker volume create aura\_azurite\_data

```



Không chạy docker compose down -v để xử lý lỗi kết nối.



\## Nạp cấu hình local



Tại thư mục gốc repository:



```powershell

\& ".\\scripts\\dev\\Set-AuraDevEnvironment.ps1"

```



Script mặc định network aura\_network cho máy mới.



Trên máy đang dùng network aura\_aura\_network:



```powershell

\& ".\\scripts\\dev\\Set-AuraDevEnvironment.ps1" `

&#x20;   -DockerNetwork "aura\_aura\_network"

```



Script kiểm tra file credentials/project và độ dài khóa mã hóa,

hỏi mật khẩu runtime Billing/Notification, rồi đặt biến trong

cửa sổ PowerShell hiện tại.



Giữ cửa sổ này để chạy các lệnh Compose.

Mở cửa sổ mới hoặc đóng phiên PowerShell thì cần nạp lại.

Không gửi output docker compose config đầy đủ vì có thể chứa secret.



Kiểm tra cấu hình:



```powershell

docker compose -p aura --env-file .env `

&#x20;   -f compose.yaml -f compose.dev.yaml config -q

```



\## Bootstrap database mới



Khởi động hạ tầng trước:



```powershell

docker compose -p aura --env-file .env `

&#x20;   -f compose.yaml -f compose.dev.yaml `

&#x20;   up -d postgres redis rabbitmq azurite

```



Chờ PostgreSQL healthy. Dùng tài khoản bootstrap aura\_dev để

chạy tuần tự các migration sau; kiểm tra exit code sau mỗi file:



1\. infra/postgres/migrations/001\_notification\_devices.sql.

2\. infra/postgres/migrations/002\_service\_database\_roles.sql.

3\. infra/postgres/migrations/003\_billing\_poc\_packages.sql.

4\. infra/postgres/migrations/analysis/001\_analysis\_core.sql.

5\. Chạy lại 002\_service\_database\_roles.sql để chuyển ownership

&#x20;  bảng Analysis và áp dụng chính sách quyền.



Ví dụ chạy một file:



```powershell

Get-Content "infra/postgres/migrations/002\_service\_database\_roles.sql" -Raw |

&#x20;   docker exec -i aura-postgres-1 `

&#x20;       psql -X -v ON\_ERROR\_STOP=1 -U aura\_dev -d aura\_dev



if ($LASTEXITCODE -ne 0) {

&#x20;   throw "Migration chưa đạt."

}

```



Sau migration, runtime role mới vẫn NOLOGIN.

Trong psql tương tác, dùng \\password để đặt mật khẩu riêng cho

aura\_billing\_app và aura\_notification\_app, khớp mật khẩu đã nhập

khi nạp môi trường. Sau đó bật LOGIN cho hai role này.



```powershell

docker exec -it aura-postgres-1 psql -X -U aura\_dev -d aura\_dev

```



Thực hiện từng lệnh trong psql:



```text

\\password aura\_billing\_app

\\password aura\_notification\_app

```



Sau khi đặt mật khẩu thành công:



```sql

ALTER ROLE aura\_billing\_app LOGIN;

ALTER ROLE aura\_notification\_app LOGIN;

```



Thoát bằng \\q.



Các runtime role khác được đặt mật khẩu/bật LOGIN khi cấu hình

service tương ứng. Migrator giữ NOLOGIN.

Không reset mật khẩu trên máy đã cấu hình nếu không cần thiết.



Xem infra/postgres/README.md để biết ma trận quyền và ngoại lệ

các bảng Analysis append-only.



\## Khởi tạo storage



Thực hiện scripts/storage/initialize\_local\_storage.py theo

infra/storage/README.md.



Container công cụ phải tham gia cùng network với Azurite.

Script giữ nguyên dữ liệu và kiểm tra ba container private.

Dùng verify\_local\_storage.py để kiểm tra SAS và cleanup.



\## Chạy các ứng dụng hiện có



Sau bootstrap/migration và cấu hình runtime role:



```powershell

docker compose -p aura --env-file .env `

&#x20;   -f compose.yaml -f compose.dev.yaml `

&#x20;   up -d --build

```



Không dùng --remove-orphans khi chưa xác định các container cũ.

Không ghi nhận Identity/Profile đã chạy từ mã nguồn hiện tại

chỉ vì Docker đang có container cùng tên.



\## Kiểm tra sau khởi động



```powershell

Invoke-RestMethod "http://127.0.0.1:5000/health"

Invoke-RestMethod "http://127.0.0.1:5001/health"

Invoke-RestMethod "http://127.0.0.1:5002/health"

Invoke-RestMethod "http://127.0.0.1:5003/health"

Invoke-RestMethod "http://127.0.0.1:5003/health/ready"

Invoke-RestMethod "http://127.0.0.1:8000/health"

```



Health không thay thế kiểm thử xác thực hoặc toàn luồng nghiệp vụ.



Đã kiểm chứng local:

\- Firebase token thật qua Gateway Docker tới Notification Docker.

\- Response đúng Firebase user, correlation ID và CORS.

\- Notification kết nối PostgreSQL bằng aura\_notification\_app.

\- Billing đọc PostgreSQL bằng aura\_billing\_app.

\- Gateway timeout trả 504 với phép thử rút ngắn 2 giây.

\- Private storage và phân quyền database theo tài liệu liên quan.



\## Chạy frontend



Trong cửa sổ khác:



```powershell

Set-Location "src/web/aura-web"

npm ci

npm run dev -- --host localhost --port 5173 --strictPort

```



Dùng .env.local của frontend với Firebase web config phù hợp.

VITE\_ENABLE\_MOCK=false khi kiểm thử tích hợp.

VITE\_API\_BASE\_URL để trống khi sử dụng Vite proxy.

Mọi public API đi qua Gateway cổng 5000.

Không đặt backend secret vào VITE\_\*.



\## Giới hạn còn lại



\- Identity/Profile cần tích hợp bản nguồn M6.

\- Service JWT cần hoàn thiện issuance/validation và cấu hình M1/M6.

\- Upload thật cần adapter Profile/Billing/storage và persistence của M2.

\- Billing reservation nghiệp vụ thuộc M3.

\- Không coi health hoặc các PoC là toàn luồng upload đã hoàn tất.

\- Azure staging RBAC, managed identity và TLS cần triển khai riêng.



\## Tài liệu liên quan



\- ../adr/ADR-004-private-storage.md

\- ../api/gateway\_sprint2.md

\- ../../infra/postgres/README.md

\- ../../infra/storage/README.md
