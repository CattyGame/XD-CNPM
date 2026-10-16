\# AURA — Gateway Sprint 2



\## 1. Phạm vi



\- Task: SCRUM-299 / W2-M1-01.

\- Epic: SCRUM-262 — Identity \& Gateway.

\- Người thực hiện: M1 — Nguyễn Trương Hậu.

\- Baseline: develop tại 875da39.

\- Gateway: .NET 10, YARP 2.3.0.

\- Địa chỉ local: http://127.0.0.1:5000.



\## 2. Route và service đích



| Prefix public | Service | Cổng local |

| --- | --- | --- |

| /api/v1/identity | Identity | 5004 |

| /api/v1/profiles | Profile | 5005 |

| /api/v1/clinics | Profile | 5005 |

| /api/v1/patients | Profile | 5005 |

| /api/v1/analyses | Analysis | 5001 |

| /api/v1/batches | Analysis | 5001 |

| /api/v1/platform | Analysis | 5001 |

| /api/v1/billing/packages | Billing | 5002 |

| /api/v1/notifications | Notification | 5003 |



Các route giữ nguyên path và query khi chuyển tiếp.



GET /services/analysis/health chuyển tới /health của Analysis.

GET /health kiểm tra Gateway, không xác nhận mọi service đích sẵn sàng.



Các prefix Identity/Profile/Clinic/Patient là cấu hình bàn giao

cho M6; cần đối chiếu với contract và API thật khi tích hợp.

Không mở route tổng quát cho API nội bộ của các service.



\## 3. Frontend



Vite chuyển toàn bộ /api đến http://127.0.0.1:5000.

Đã bỏ proxy Notification đi trực tiếp tới cổng 5003.



Cấu hình frontend khi gọi API thật:



\- VITE\_ENABLE\_MOCK=false

\- VITE\_API\_BASE\_URL để trống



Khởi động lại Vite sau khi thay cấu hình.



\## 4. CORS



Origin local được phép:



\- http://localhost:5173

\- http://127.0.0.1:5173



Method: GET, POST, PUT, PATCH, DELETE, OPTIONS.



Request header:

Authorization, Content-Type, Idempotency-Key,

X-Correlation-Id, If-Match, If-None-Match.



Response header được expose:

X-Correlation-Id, Location, Retry-After, ETag.



Không bật AllowCredentials.

Origin ngoài danh sách không được cấp Allow-Origin.

CORS không thay thế xác thực hoặc phân quyền backend.



\## 5. Correlation ID



Gateway nhận X-Correlation-Id có độ dài 1–64 ký tự,

chỉ gồm chữ ASCII, số, dấu gạch ngang, gạch dưới và dấu chấm.



Nếu thiếu, có nhiều giá trị hoặc không hợp lệ:

Gateway sinh ID mới bằng GUID dạng N.



Gateway:

\- Gán ID vào TraceIdentifier.

\- Chuyển tiếp X-Correlation-Id xuống service.

\- Trả X-Correlation-Id trong response.

\- Thêm CorrelationId vào logging scope.



Service phải tiếp nhận header để đồng bộ trace nội bộ.

Kiểm thử header không chứng minh correlationId trong mọi

response body của service đã đồng nhất.



\## 6. Giới hạn request



| Cấu hình | Giá trị |

| --- | ---: |

| Gateway:MaxRequestBodyBytes | 1048576 |

| Gateway:MaxUploadBodyBytes | 1101004800 |



Request thông thường tối đa 1 MiB.

POST /api/v1/analyses được phép tối đa 1050 MiB.

Đường dẫn con và các method khác dùng giới hạn thông thường.



Request có Content-Length vượt giới hạn được trả:

413 ERR\_PAYLOAD\_TOO\_LARGE, kèm correlationId và retryable=false.



Middleware cũng đặt giới hạn đọc body qua server feature.

Chưa kiểm chứng riêng request chunked vượt giới hạn.



Analysis vẫn phải kiểm tra multipart, số ảnh, dung lượng từng ảnh,

định dạng, kích thước ảnh, quyền truy cập và idempotency.

Giới hạn Gateway không chứng minh hệ thống xử lý được batch 100 ảnh.



\## 7. Cấu hình môi trường



Ví dụ override địa chỉ Notification trong PowerShell:



&#x20;   \[Environment]::SetEnvironmentVariable(

&#x20;       "ReverseProxy\_\_Clusters\_\_notification-cluster\_\_Destinations\_\_notification-api\_\_Address",

&#x20;       "http://127.0.0.1:5003/",

&#x20;       "Process"

&#x20;   )



CORS có thể cấu hình bằng Gateway\_\_AllowedOrigins\_\_0,

Gateway\_\_AllowedOrigins\_\_1 và các phần tử tiếp theo.

Khi triển khai phải rà soát toàn bộ danh sách origin có hiệu lực.



Giới hạn body:

\- Gateway\_\_MaxRequestBodyBytes

\- Gateway\_\_MaxUploadBodyBytes



Khởi động lại Gateway sau khi đổi các cấu hình Gateway trên.



Trong container, loopback là chính container đó.

Compose hiện chỉ override địa chỉ Analysis/Billing.

Identity/Profile/Notification cần cấu hình đích phù hợp

khi bổ sung môi trường Docker trong SCRUM-301.



Không lưu Firebase credentials, mật khẩu hoặc khóa mã hóa vào Git.



\## 8. Chạy local



Tại thư mục gốc repository:



&#x20;   $env:DOTNET\_ENVIRONMENT = "Development"

&#x20;   $env:ASPNETCORE\_ENVIRONMENT = "Development"



&#x20;   dotnet run --project src/gateway/AURA.ApiGateway/AURA.ApiGateway.csproj --no-launch-profile --urls http://127.0.0.1:5000



Chạy các service cần sử dụng ở cổng tương ứng trong bảng route.



Frontend:



&#x20;   cd src/web/aura-web

&#x20;   npm ci

&#x20;   npm run dev -- --host localhost --port 5173 --strictPort



\## 9. Kết quả kiểm tra ngày 2026-10-15



Nguồn: log PowerShell người thao tác cung cấp.



\- Gateway build PASS.

\- Health, correlation ID hợp lệ/tự sinh/thay ID quá dài: PASS.

\- CORS preflight, method/header và origin ngoài danh sách: PASS.

\- Request thông thường vượt 1 MiB: 413 đúng envelope và CORS.

\- Analysis thật: platform/ping và health transform PASS.

\- Backend giả lập: chín route chuyển tới đúng năm cluster.

\- Chuyển tiếp path/query/bearer/correlation ID: PASS.

\- Chuyển tiếp POST body và Idempotency-Key: PASS.

\- Giới hạn upload kiểm tra bằng cấu hình giảm:

&#x20; request thường 1024 byte, upload 4096 byte.

\- Upload 2048 và 4096 byte: PASS; 4097 byte: 413.

\- POST đường dẫn con và PUT vượt giới hạn thường: 413.

\- Lần chạy liên tiếp có lỗi kết nối sau ca 413;

&#x20; hai ca còn lại PASS khi dùng kết nối mới.

&#x20; Chưa xác định nguyên nhân lỗi kết nối ban đầu.

\- Frontend: 82/82 tests PASS, lint 0 lỗi/cảnh báo.

\- Sau npm ci đồng bộ react-router-dom 7.18.4:

&#x20; TypeScript/Vite build PASS, 73 modules.

\- Notification thật: thiếu bearer trả 401 ERR\_UNAUTHORIZED

&#x20; khi gọi trực tiếp, qua Gateway và qua Vite.

\- Các response qua Gateway giữ correlation header đúng.



\## 10. Giới hạn bàn giao



\- Gateway chưa bổ sung cơ chế xác thực/phân quyền riêng.

\- Các service phải xác minh token và quyền nghiệp vụ.

\- Bearer giả trong kiểm thử chỉ dùng để kiểm tra chuyển tiếp.

\- Chưa nghiệm thu Identity/Profile thật hoặc upload nghiệp vụ.

\- Chưa kiểm chứng đăng nhập/FCM delivery qua Gateway.

\- Chưa kiểm chứng tải lớn, rate limiting hoặc môi trường production.

\## Bổ sung SCRUM-299 — Firebase, CORS và timeout (2026-10-16)



\### Xác thực tại Gateway



Gateway xác minh Firebase ID token cho các đường dẫn /api/v1

cần đăng nhập, sử dụng FirebaseAdmin 3.7.0 và checkRevoked=true.



Các ngoại lệ:

\- GET /api/v1/platform/ping.

\- GET /api/v1/billing/packages.

\- OPTIONS phục vụ CORS preflight.

\- Health endpoint nằm ngoài /api/v1.



Thiếu hoặc sai token trả 401 ERR\_UNAUTHORIZED.

Không xác minh được do cấu hình/dependency trả

503 ERR\_AUTH\_UNAVAILABLE; không chuyển tiếp request.



Gateway giữ Authorization để API đích tiếp tục xác minh.

Gateway không suy ra accountId, tenant hoặc role nội bộ từ UID.

API nghiệp vụ vẫn kiểm tra account active, role và quyền resource.



Cấu hình backend khi chạy trên host:

\- Firebase\_\_ProjectId: project Firebase dev.

\- GOOGLE\_APPLICATION\_CREDENTIALS: đường dẫn file service account

&#x20; nằm ngoài repository.



Firebase client được khởi tạo khi có request cần xác minh.

Health/ping không chứng minh cấu hình Firebase đã hoạt động.

Không log ID token, service account hoặc mật khẩu.



\### Header và route



Gateway loại các header do client gửi:

\- X-Account-Id.

\- X-Role.

\- X-Tenant-Id.

\- X-User-Authorization.



Giữ X-Clinic-Id như lựa chọn context; backend phải kiểm membership.

Giữ Authorization, Idempotency-Key và X-Correlation-Id.



CORS cho phép X-Clinic-Id và expose Idempotent-Replayed.

Response lỗi xác thực/timeout vẫn có CORS cho origin được phép.



Route Billing bao phủ /api/v1/billing/{\*\*catch-all}.

Không mở route public tới /internal/v1.



\### Timeout upload



Route analysis-upload:

\- POST /api/v1/analyses.

\- Order = -10.

\- TimeoutPolicy = analysis-upload.



Gateway\_\_UploadTimeoutSeconds mặc định trong appsettings.json

là 120 giây. Analysis cluster ActivityTimeout là 130 giây.



Có middleware nối trạng thái route timeout về Request Timeouts

để tránh trường hợp YARP trả 400 khi request bị hủy do deadline.



Nếu response chưa bắt đầu, timeout trả:

\- HTTP 504.

\- error.code = ERR\_GATEWAY\_TIMEOUT.

\- retryable = true.

\- X-Correlation-Id và Cache-Control: no-store.



Nếu response đã bắt đầu, Gateway không thể thay lại status/body.



Timeout không xác nhận backend đã rollback thao tác.

Khi thử lại upload, client phải giữ nguyên Idempotency-Key

và nội dung request để backend xử lý idempotency.



\### Kiểm thử local đã đạt



\- Gateway build thành công.

\- Health không cần token.

\- CORS preflight trả 204, cho phép X-Clinic-Id.

\- Các prefix Identity/Profile/Analysis/Billing/Notification

&#x20; thiếu token trả 401 với correlation ID và CORS.

\- Token sai trả 401.

\- Firebase token thật được xác minh và chuyển tiếp tới backend thử.

\- Backend nhận Authorization, X-Clinic-Id, Idempotency-Key

&#x20; và correlation ID.

\- Backend không nhận bốn header giả mạo nêu trên.

\- Override timeout 2 giây, backend chờ 5 giây:

&#x20; Gateway trả 504 sau 2,11 giây, có ERR\_GATEWAY\_TIMEOUT,

&#x20; correlation ID và CORS.



Kiểm thử token thật dùng tài khoản Firebase thử nghiệm.

Backend kiểm thử không in hoặc trả nội dung token.



Giá trị production/local baseline vẫn là 120 giây;

không ghi nhận đã chờ đủ 120 giây trong phép thử này.



Cấu hình credentials/mount và địa chỉ các service khi Gateway

chạy trong Docker tiếp tục được hoàn thiện trong SCRUM-301.
