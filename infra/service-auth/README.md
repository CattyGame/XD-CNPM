\# Service identity — Sprint 2



Owner chuẩn bị cấu hình: M1 / SCRUM-300.

Owner triển khai Identity issuance/validation: M6 / SCRUM-314.

Cập nhật: 2026-10-16.



\## Trạng thái



Đã có:

\- Registry client/principal/audience/scope.

\- Công cụ tạo khóa RS256 và credentials ngoài repository.

\- Kiểm tra cặp khóa RSA bằng ký/xác minh.

\- Kiểm tra secret đúng được chấp nhận, secret sai bị từ chối.



Chưa xác nhận:

\- Identity runtime đọc cấu hình được tạo.

\- Endpoint phát service token xác thực credentials đúng.

\- Consumer kiểm issuer/audience/scope và hạn dùng.

\- Các cuộc gọi nội bộ thực tế qua service JWT.



Firebase user token và service JWT là hai loại token khác nhau.



\## Registry công khai



Nguồn cấu hình: clients.example.json.



| Caller | Audience | Scope |

| --- | --- | --- |

| analysis-api | aura-identity | identity:context |

| analysis-api | aura-profile | profile:resolve |

| analysis-api | aura-billing | billing:reserve, billing:read, billing:release |

| identity-api | aura-profile | profile:provision |

| billing-api | aura-profile | profile:resolve |



Mỗi caller có principalId UUID ổn định.

Token sub dùng principalId; client\_id dùng tên client.



billing:release chỉ phục vụ compensation hợp lệ.

Billing vẫn kiểm tra trạng thái bền vững, không cho caller

tự quyết định request đã abort bằng một boolean.



analysis:reconcile bổ sung khi endpoint đối soát được triển khai.

Quyền Worker được bổ sung theo phạm vi Sprint 3.



\## Tạo credentials trên máy local



Chạy từ thư mục gốc repository:



```powershell

$registryPath = (

&#x20;   Resolve-Path "infra/service-auth/clients.example.json"

).Path



$outputDirectory = Join-Path $env:USERPROFILE `

&#x20;   ".aura\\service-auth\\local-v1"



dotnet run `

&#x20;   --project "tools/AURA.ServiceIdentity.Setup/AURA.ServiceIdentity.Setup.csproj" `

&#x20;   --configuration Release `

&#x20;   --no-launch-profile `

&#x20;   -- `

&#x20;   --registry $registryPath `

&#x20;   --output $outputDirectory

```



Công cụ:

\- Chỉ ghi output ngoài repository.

\- Dừng nếu thư mục output đã tồn tại.

\- Tạo RSA 3072 bit; private key PEM PKCS#8.

\- Tạo public key PEM SubjectPublicKeyInfo.

\- Tạo secret ngẫu nhiên riêng cho mỗi client.

\- Không in secret hoặc private key.

\- Ghi file trong thư mục tạm rồi chuyển thành output khi hoàn tất.



Chạy lại không được xóa thư mục để tự động sinh lại khóa đang dùng.

Thay khóa hoặc credentials phải có kế hoạch rotation riêng.



\## File được tạo và phạm vi cấp quyền đọc



| File tương đối trong output | Bên sử dụng |

| --- | --- |

| issuer/signing-key.pem | Chỉ Identity |

| issuer/identity-clients.json | Chỉ Identity |

| public/signing-public.pem | Consumer xác minh JWT |

| clients/analysis-api.json | Chỉ Analysis |

| clients/identity-api.json | Chỉ Identity khi gọi Profile |

| clients/billing-api.json | Chỉ Billing |

| manifest.json | Metadata keyId/issuer/version |



Không mount toàn bộ thư mục output cho mọi service.



identity-clients.json chứa clientSecretHash và allowlist.

File clients/\*.json chứa clientSecret thực tế.

Caller không được nhận registry hash của các client khác.



privateKeyPath và publicKeyPath trong issuer config được giải

tương đối từ thư mục chứa identity-clients.json.



\## Hợp đồng đọc cấu hình cần M6 triển khai



Identity đọc file được chỉ định bởi ServiceAuth\_\_RegistryPath.

Đây là key bổ sung cần code bind/validate; chưa tự có hiệu lực.



Các key theo contract dùng ở consumer/caller:

\- ServiceAuth\_\_Issuer.

\- ServiceAuth\_\_Audience: audience mà API hiện tại chấp nhận.

\- ServiceAuth\_\_PublicKeyPath.

\- ServiceAuth\_\_ClientId.

\- ServiceAuth\_\_ClientSecret.

\- Services\_\_Identity\_\_BaseUrl.



ServiceAuth\_\_Audience của consumer không được nhầm với audience

đích khi caller xin token cho một service khác.



Identity nạp lại khóa đã lưu khi khởi động.

Không sinh khóa mới mỗi lần chạy.

keyId trong JWT header lấy từ issuer config.



\## Endpoint issuance cần M6 hoàn thiện



POST /internal/v1/identity/service-tokens.



Request chốt để lựa chọn đúng service đích:



```json

{

&#x20; "clientId": "analysis-api",

&#x20; "clientSecret": "<cung cấp riêng>",

&#x20; "audience": "aura-profile",

&#x20; "scope": "profile:resolve"

}

```



Quy tắc:

1\. Tra client trong issuer config.

2\. Xác thực clientSecret với clientSecretHash.

3\. Kiểm audience có trong allowedTargets của chính client đó.

4\. Nếu có scope yêu cầu, toàn bộ scope phải thuộc audience đó.

5\. Nếu thiếu scope, dùng danh sách được server cấu hình cho audience.

6\. Từ chối scope lạ; không tự ghép quyền từ audience khác.

7\. Lấy principalId từ registry, không nhận từ body.

8\. Credentials sai trả 401; audience/scope không được cấp trả 403.



Response thành công:



```json

{

&#x20; "accessToken": "<service JWT>",

&#x20; "tokenType": "Bearer",

&#x20; "expiresIn": 300

}

```



Endpoint chỉ nội bộ, không route qua Gateway public.



\## Định dạng clientSecretHash



```text

pbkdf2-sha256$600000$<Base64 salt>$<Base64 derived key>

```



\- PBKDF2-HMAC-SHA256.

\- 600000 iterations.

\- Salt 16 byte.

\- Derived key 32 byte.

\- So sánh bằng CryptographicOperations.FixedTimeEquals.



Công cụ có hàm HashSecret/VerifySecret làm nguồn tham chiếu.

Không lưu plaintext clientSecret vào bảng/registry của issuer.



\## JWT và consumer validation



\- Algorithm: RS256, chỉ cho phép thuật toán này.

\- Issuer: aura-identity-local.

\- Audience: đúng service đích.

\- TTL: 300 giây.

\- Claims: sub UUID, client\_id, iss, aud, exp, iat, jti,

&#x20; scope phân cách bằng dấu cách.

\- Consumer kiểm chữ ký, issuer, audience, expiry và scope endpoint.

\- Cấu hình clock skew rõ ràng, đề xuất tối đa 30 giây cho local.

\- Chỉ dùng public key/JWKS tin cậy do môi trường cung cấp.

\- Không tin URL khóa do token tự chỉ định.

\- Không log JWT hoặc secret.



Identity context cần service JWT ở Authorization và Firebase

user token riêng ở X-User-Authorization.

Không chấp nhận Firebase token như service JWT hoặc ngược lại.

Gateway public loại X-User-Authorization do browser gửi.



\## Các ca tích hợp bắt buộc sau khi M6 nối runtime



\- Đúng credentials/audience/scope: phát token.

\- Sai secret hoặc client không tồn tại: từ chối.

\- Audience/scope vượt quyền: từ chối.

\- Consumer từ chối token sai chữ ký, hết hạn, issuer/audience sai.

\- Token hợp lệ nhưng thiếu scope: từ chối endpoint.

\- Identity restart vẫn xác minh được token chưa hết hạn

&#x20; bằng public key đã cấp.

\- Không gọi /internal/v1 qua Gateway public.

\- Profile resolve/provision và Identity context dùng service JWT thật.



Kết quả tạo credentials không thay thế các ca tích hợp này.



\## Kết quả local 2026-10-16



\- Tạo credentials analysis-api, identity-api, billing-api: PASS.

\- Kiểm tra hash với secret đúng và sai: PASS.

\- Ký/xác minh bằng cặp RSA xuất ra: PASS.

\- Output nằm ngoài repository: PASS.

\- Không in private key, client secret hoặc token.
