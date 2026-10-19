# ====================================================================
# AURA — Bộ Kiểm thử Toàn diện M6 (Task W2-M6-03 / SCRUM-316)
# Phiên bản 2.0 — Cập nhật đầy đủ theo đánh giá Sprint 2 (M6-01, M6-02, M6-03):
# 1. Xác thực người dùng, Bootstrap idempotent & kiểm role nguồn thẩm quyền
# 2. Service-to-Service JWT (RS256, Issuer, Audience, Scopes & Negative tests)
# 3. Bảo vệ endpoint nội bộ Profile & Phân giải quyền lâm sàng (Access Resolve)
# 4. Chống giả mạo headers, chống sửa Role & Optimistic Concurrency (ETag/If-Match)
# ====================================================================

[CmdletBinding()]
param(
    [string]$IdentityUrl = "http://127.0.0.1:5004",
    [string]$ProfileUrl = "http://127.0.0.1:5005"
)

$ErrorActionPreference = "Continue"

$validUserToken = "Bearer mock-token-hoang_dev_1"
$userBToken = "Bearer mock-token-user_b_dev"
$uninitializedToken = "Bearer mock-token-uninitialized_user_999"
$invalidToken = "Bearer invalid_garbage_token_xyz"
$expiredToken = "Bearer mock-token-expired_token_test"

$global:passCount = 0
$global:failCount = 0

function Report-Result {
    param(
        [string]$TestCode,
        [string]$Description,
        [bool]$Success,
        [string]$Details = ""
    )
    if ($Success) {
        $global:passCount++
        Write-Host " [PASS] $TestCode : $Description" -ForegroundColor Green
        if ($Details) { Write-Host "        -> $Details" -ForegroundColor Gray }
    } else {
        $global:failCount++
        Write-Host " [FAIL] $TestCode : $Description" -ForegroundColor Red
        if ($Details) { Write-Host "        -> $Details" -ForegroundColor Yellow }
    }
}

function Invoke-ApiSafe {
    param(
        [string]$Uri,
        [string]$Method = "Get",
        [hashtable]$Headers = @{},
        [string]$Body = $null
    )
    try {
        $params = @{
            Uri = $Uri
            Method = $Method
            Headers = $Headers
            UseBasicParsing = $true
        }
        if ($Body) {
            $params["Body"] = $Body
            $params["ContentType"] = "application/json"
        }
        $response = Invoke-WebRequest @params
        return @{
            StatusCode = [int]$response.StatusCode
            Headers = $response.Headers
            Content = $response.Content
            Error = $null
        }
    } catch {
        $statusCode = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        $errorBody = ""
        if ($_.Exception.Response) {
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $errorBody = $reader.ReadToEnd()
            } catch {
                $errorBody = $_.Exception.Message
            }
        } else {
            $errorBody = $_.Exception.Message
        }
        return @{
            StatusCode = $statusCode
            Headers = if ($_.Exception.Response) { $_.Exception.Response.Headers } else { @{} }
            Content = $errorBody
            Error = $_.Exception
        }
    }
}

Write-Host "`n====================================================================" -ForegroundColor Cyan
Write-Host " BAT DAU CHAY BO KIEM THU TOAN DIEN M6 (TASK W2-M6-03 - SPRINT 2)" -ForegroundColor Cyan
Write-Host " Identity API: $IdentityUrl | Profile API: $ProfileUrl" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------
# PHẦN 1: XÁC THỰC NGƯỜI DÙNG & BOOTSTRAP (M6-01)
# --------------------------------------------------------------------
Write-Host "`n--- [PHAN 1] XAC THUC NGUOI DUNG, BOOTSTRAP & KIEM ROLE THAM QUYEN ---" -ForegroundColor Yellow

# TC-AUTH-01: Yêu cầu thiếu header Authorization
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/me" -Method "Get"
$pass = ($res.StatusCode -eq 401) -and ($res.Content -like "*ERR_UNAUTHORIZED*")
Report-Result "TC-AUTH-01" "Tu choi request thieu header Authorization (HTTP 401 ERR_UNAUTHORIZED)" $pass "Status: $($res.StatusCode)"

# TC-AUTH-02: Token rác / sai định dạng
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/me" -Method "Get" -Headers @{ Authorization = $invalidToken }
$pass = ($res.StatusCode -eq 401) -and ($res.Content -like "*ERR_UNAUTHORIZED*")
Report-Result "TC-AUTH-02" "Tu choi Token rac / khong hop le (HTTP 401 ERR_UNAUTHORIZED)" $pass "Status: $($res.StatusCode)"

# TC-AUTH-03: Thử nghiệm token hết hạn / không hợp lệ
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/me" -Method "Get" -Headers @{ Authorization = "Bearer expired_mock_token_123" }
$pass = ($res.StatusCode -eq 401)
Report-Result "TC-AUTH-03" "Kiem tra tu choi expired/invalid token (HTTP 401)" $pass "Status: $($res.StatusCode)"

# TC-AUTH-04: Truy vấn tài khoản chưa từng Bootstrap qua GET /me
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/me" -Method "Get" -Headers @{ Authorization = $uninitializedToken }
$pass = ($res.StatusCode -eq 409) -and ($res.Content -like "*ERR_ACCOUNT_NOT_INITIALIZED*")
Report-Result "TC-AUTH-04" "Chan tai khoan chua bootstrap qua GET /me (HTTP 409 ERR_ACCOUNT_NOT_INITIALIZED)" $pass "Status: $($res.StatusCode)"

# TC-AUTH-05: Bootstrap tài khoản hợp lệ (Lấy AccountId động)
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/bootstrap" -Method "Post" -Headers @{ Authorization = $validUserToken }
$bootstrapData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$currentAccountId = if ($bootstrapData) { $bootstrapData.accountId } else { "668c4a32-5342-4d8b-b505-07d548732120" }
$pass = ($res.StatusCode -eq 200) -and ($null -ne $bootstrapData.accountId) -and ($bootstrapData.accountStatus -eq "ACTIVE")
Report-Result "TC-AUTH-05" "Bootstrap tai khoan hop le va lay accountId dong (HTTP 200 OK)" $pass "AccountId: $currentAccountId"

# TC-AUTH-06: Bootstrap lặp lại (Idempotency) giữ nguyên AccountId
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/bootstrap" -Method "Post" -Headers @{ Authorization = $validUserToken }
$repeatData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($repeatData.accountId -eq $currentAccountId)
Report-Result "TC-AUTH-06" "Bootstrap lap lai giu nguyen accountId (Idempotent)" $pass "Status: $($res.StatusCode)"

# TC-AUTH-07: Kiểm tra vai trò nguồn thẩm quyền tại Identity (phải là PATIENT mặc định)
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/me" -Method "Get" -Headers @{ Authorization = $validUserToken }
$meData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($meData.roles -contains "PATIENT") -and ($meData.roles -notcontains "SYSTEM_ADMIN")
Report-Result "TC-AUTH-07" "Kiem tra vai tro nguon tham quyen tai Identity (mac dinh PATIENT)" $pass "Roles: $($meData.roles -join ', ')"

# --------------------------------------------------------------------
# PHẦN 2: SERVICE IDENTITY & SERVICE-TO-SERVICE JWT (M6-01)
# --------------------------------------------------------------------
Write-Host "`n--- [PHAN 2] SERVICE IDENTITY & M2M TOKEN ISSUANCE / VALIDATION ---" -ForegroundColor Yellow

# TC-SVC-01: Cấp service token thiếu clientId hoặc clientSecret
$emptyTokenReq = @{ clientId = ""; clientSecret = "" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $emptyTokenReq
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-SVC-01" "Tu choi cap service token khi thieu clientId/secret (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-SVC-02: Cấp service token với clientId không tồn tại
$unknownClientReq = @{ clientId = "unknown-client"; clientSecret = "any_pass"; audience = "aura-profile" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $unknownClientReq
$pass = ($res.StatusCode -eq 401) -and ($res.Content -like "*ERR_UNAUTHORIZED*")
Report-Result "TC-SVC-02" "Tu choi client la khong co trong registry (HTTP 401 ERR_UNAUTHORIZED)" $pass "Status: $($res.StatusCode)"

# TC-SVC-03: Cấp service token với clientSecret sai
$wrongSecretReq = @{ clientId = "identity-api"; clientSecret = "wrong_secret_pass"; audience = "aura-profile" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $wrongSecretReq
$pass = ($res.StatusCode -eq 401) -and ($res.Content -like "*ERR_UNAUTHORIZED*")
Report-Result "TC-SVC-03" "Tu choi secret khong khop voi clientSecretHash (HTTP 401 ERR_UNAUTHORIZED)" $pass "Status: $($res.StatusCode)"

# TC-SVC-04: Cấp service token với audience không được cấp phép
$wrongAudReq = @{ clientId = "identity-api"; clientSecret = "secret_identity_pass"; audience = "aura-billing" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $wrongAudReq
$pass = ($res.StatusCode -eq 403) -and ($res.Content -like "*ERR_FORBIDDEN*")
Report-Result "TC-SVC-04" "Tu choi audience vuot quyen cua client (HTTP 403 ERR_FORBIDDEN)" $pass "Status: $($res.StatusCode)"

# TC-SVC-05: Cấp service token với scope vượt quyền
$wrongScopeReq = @{ clientId = "identity-api"; clientSecret = "secret_identity_pass"; audience = "aura-profile"; scope = "admin:delete_all" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $wrongScopeReq
$pass = ($res.StatusCode -eq 403) -and ($res.Content -like "*ERR_FORBIDDEN*")
Report-Result "TC-SVC-05" "Tu choi scope vuot quyen cho audience (HTTP 403 ERR_FORBIDDEN)" $pass "Status: $($res.StatusCode)"

# TC-SVC-06: Cấp service token hợp lệ cho identity-api -> aura-profile (profile:provision)
$identityTokenReq = @{ clientId = "identity-api"; clientSecret = "secret_identity_pass"; audience = "aura-profile"; scope = "profile:provision" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $identityTokenReq
$tokenData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$identityServiceToken = if ($tokenData) { $tokenData.accessToken } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($null -ne $identityServiceToken)
Report-Result "TC-SVC-06" "Cap service token thanh cong cho identity-api (profile:provision)" $pass "Token length: $($identityServiceToken.Length)"

# TC-SVC-07: Cấp service token hợp lệ cho analysis-api -> aura-profile (profile:resolve)
$analysisProfileReq = @{ clientId = "analysis-api"; clientSecret = "secret_analysis_pass"; audience = "aura-profile"; scope = "profile:resolve" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $analysisProfileReq
$tokenData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$analysisProfileToken = if ($tokenData) { $tokenData.accessToken } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($null -ne $analysisProfileToken)
Report-Result "TC-SVC-07" "Cap service token thanh cong cho analysis-api (profile:resolve)" $pass "Token length: $($analysisProfileToken.Length)"

# TC-SVC-08: Cấp service token hợp lệ cho analysis-api -> aura-identity (identity:context)
$analysisIdentityReq = @{ clientId = "analysis-api"; clientSecret = "secret_analysis_pass"; audience = "aura-identity"; scope = "identity:context" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $analysisIdentityReq
$tokenData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$analysisIdentityToken = if ($tokenData) { $tokenData.accessToken } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($null -ne $analysisIdentityToken)
Report-Result "TC-SVC-08" "Cap service token thanh cong cho analysis-api (identity:context)" $pass "Token length: $($analysisIdentityToken.Length)"

# TC-SVC-09: Endpoint Identity Context từ chối nếu thiếu Service JWT tại Authorization
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/context" -Method "Post" -Headers @{ "X-User-Authorization" = $validUserToken }
$pass = ($res.StatusCode -eq 401)
Report-Result "TC-SVC-09" "Identity Context tu choi khi thieu Service JWT (HTTP 401)" $pass "Status: $($res.StatusCode)"

# TC-SVC-10: Endpoint Identity Context từ chối nếu thiếu User token tại X-User-Authorization
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/context" -Method "Post" -Headers @{ Authorization = "Bearer $analysisIdentityToken" }
$pass = ($res.StatusCode -eq 400)
Report-Result "TC-SVC-10" "Identity Context tu choi khi thieu X-User-Authorization (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-SVC-11: Endpoint Identity Context thành công khi có đủ Service JWT và User token
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/context" -Method "Post" `
    -Headers @{ Authorization = "Bearer $analysisIdentityToken"; "X-User-Authorization" = $validUserToken }
$ctxData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($ctxData.accountId -eq $currentAccountId) -and ($ctxData.accountStatus -eq "ACTIVE")
Report-Result "TC-SVC-11" "Identity Context phan giai hop le voi Service JWT + User Token" $pass "AccountId: $($ctxData.accountId), Roles: $($ctxData.roles -join ',')"

# --------------------------------------------------------------------
# PHẦN 3: BẢO VỆ ENDPOINT NỘI BỘ PROFILE & PHÂN GIẢI QUYỀN (M6-02)
# --------------------------------------------------------------------
Write-Host "`n--- [PHAN 3] BAO VE ENDPOINT NOI BO PROFILE & PHAN GIAI QUYEN (ACCESS RESOLVE) ---" -ForegroundColor Yellow

# TC-PROV-01: Gọi /internal/v1/profile/provision không có Service JWT -> Bị từ chối
$provReqBody = @{ accountId = $currentAccountId; firebaseUid = "hoang_dev_1"; displayName = "Nguyen Van Hoang" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/provision" -Method "Post" -Body $provReqBody
$pass = ($res.StatusCode -eq 401)
Report-Result "TC-PROV-01" "Profile Provision tu choi khi thieu Service JWT (HTTP 401)" $pass "Status: $($res.StatusCode)"

# TC-PROV-02: Gọi /internal/v1/profile/provision với Service JWT sai scope (dùng token identity:context)
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/provision" -Method "Post" `
    -Headers @{ Authorization = "Bearer $analysisIdentityToken" } -Body $provReqBody
$pass = ($res.StatusCode -eq 403)
Report-Result "TC-PROV-02" "Profile Provision tu choi token sai audience/scope (HTTP 403)" $pass "Status: $($res.StatusCode)"

# TC-PROV-03: Gọi /internal/v1/profile/provision với Service JWT đúng (profile:provision)
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/provision" -Method "Post" `
    -Headers @{ Authorization = "Bearer $identityServiceToken" } -Body $provReqBody
$provData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$currentPatientId = if ($provData -and $provData.patientId) { $provData.patientId } else { [System.Guid]::NewGuid().ToString() }
$personalTenantId = if ($provData -and $provData.personalTenantId) { $provData.personalTenantId } else { [System.Guid]::NewGuid().ToString() }
$pass = ($res.StatusCode -eq 200) -and ($null -ne $provData.patientId) -and ($null -ne $provData.personalTenantId)
Report-Result "TC-PROV-03" "Profile Provision thanh cong voi Service JWT hop le" $pass "PatientId: $currentPatientId, TenantId: $personalTenantId"

# TC-PROV-04: Provision đồng thời / lặp lại không tạo thừa Tenant hay Patient (Idempotency)
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/provision" -Method "Post" `
    -Headers @{ Authorization = "Bearer $identityServiceToken" } -Body $provReqBody
$repeatProv = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($repeatProv.patientId -eq $currentPatientId) -and ($repeatProv.personalTenantId -eq $personalTenantId)
Report-Result "TC-PROV-04" "Provision lap lai giu nguyen dinh danh (Khong tao thua tenant/patient)" $pass "Status: $($res.StatusCode)"

# TC-RES-01: Endpoint Access Resolve từ chối khi thiếu Service JWT
$resolveBody = @{ actorAccountId = $currentAccountId; patientId = $currentPatientId; clinicContextId = $null; action = "ANALYSIS_CREATE" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/access/resolve" -Method "Post" -Body $resolveBody
$pass = ($res.StatusCode -eq 401)
Report-Result "TC-RES-01" "Access Resolve tu choi khi thieu Service JWT (HTTP 401)" $pass "Status: $($res.StatusCode)"

# TC-RES-02: Endpoint Access Resolve từ chối khi Service JWT sai scope (ví dụ dùng token profile:provision)
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/access/resolve" -Method "Post" `
    -Headers @{ Authorization = "Bearer $identityServiceToken" } -Body $resolveBody
$pass = ($res.StatusCode -eq 403)
Report-Result "TC-RES-02" "Access Resolve tu choi token thieu scope profile:resolve (HTTP 403)" $pass "Status: $($res.StatusCode)"

# TC-RES-03: Access Resolve với Action lạ / không hợp lệ -> Allowed: false
$invalidActionBody = @{ actorAccountId = $currentAccountId; patientId = $currentPatientId; clinicContextId = $null; action = "INVALID_HACK_ACTION" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/access/resolve" -Method "Post" `
    -Headers @{ Authorization = "Bearer $analysisProfileToken" } -Body $invalidActionBody
$resData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($resData.allowed -eq $false)
Report-Result "TC-RES-03" "Access Resolve tu choi action khong hop le (Allowed: false)" $pass "Allowed: $($resData.allowed)"

# TC-RES-04: Access Resolve phân giải quyền Chính chủ thành công (Personal Context)
$ownActionBody = @{ actorAccountId = $currentAccountId; patientId = $currentPatientId; clinicContextId = $null; action = "ANALYSIS_CREATE" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/access/resolve" -Method "Post" `
    -Headers @{ Authorization = "Bearer $analysisProfileToken" } -Body $ownActionBody
$resData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($resData.allowed -eq $true) -and ($resData.tenantKind -eq "PERSONAL")
Report-Result "TC-RES-04" "Access Resolve chinh chu hop le (Allowed: true, TenantKind: PERSONAL)" $pass "BillingOwner: $($resData.billingOwner.type)"

# TC-RES-05: Bệnh nhân cá nhân cố thực hiện DOCTOR_REVIEW -> Bị từ chối (Allowed: false)
$docActionBody = @{ actorAccountId = $currentAccountId; patientId = $currentPatientId; clinicContextId = $null; action = "DOCTOR_REVIEW" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/access/resolve" -Method "Post" `
    -Headers @{ Authorization = "Bearer $analysisProfileToken" } -Body $docActionBody
$resData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($resData.allowed -eq $false)
Report-Result "TC-RES-05" "Benh nhan ca nhan khong the thuc hien DOCTOR_REVIEW (Allowed: false)" $pass "Allowed: $($resData.allowed)"

# TC-RES-06: Người lạ cố truy cập phân tích của bệnh nhân khác -> Allowed: false
$strangerAccountId = [System.Guid]::NewGuid().ToString()
$strangerBody = @{ actorAccountId = $strangerAccountId; patientId = $currentPatientId; clinicContextId = $null; action = "ANALYSIS_READ" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/access/resolve" -Method "Post" `
    -Headers @{ Authorization = "Bearer $analysisProfileToken" } -Body $strangerBody
$resData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($resData.allowed -eq $false)
Report-Result "TC-RES-06" "Nguoi la truy cap benh nhan khac bi chan (Allowed: false)" $pass "Allowed: $($resData.allowed)"

# --------------------------------------------------------------------
# PHẦN 4: HỒ SƠ CÁ NHÂN, CHỐNG HACK ROLE & CONCURRENCY (M6-02)
# --------------------------------------------------------------------
Write-Host "`n--- [PHAN 4] HO SO CA NHAN, CHONG HACK ROLE & OPTIMISTIC CONCURRENCY ---" -ForegroundColor Yellow

# TC-PROF-01: Giả mạo header X-Account-Id bị loại bỏ hoàn toàn
$fakeAccountId = [System.Guid]::NewGuid().ToString()
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Get" `
    -Headers @{ Authorization = $validUserToken; "X-Account-Id" = $fakeAccountId }
$meProfile = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($meProfile.accountId -eq $currentAccountId) -and ($meProfile.accountId -ne $fakeAccountId)
Report-Result "TC-PROF-01" "Profile API loai bo trust X-Account-Id gia mao (Chi lay dung token)" $pass "AccountId tra ve: $($meProfile.accountId)"

$etag = $res.Headers["ETag"]
if (-not $etag) { $etag = '"1"' }

# TC-PROF-02: Cố tình gửi kèm Role trong PATCH me để leo thang đặc quyền
$hackRolePayload = @{
    displayName = "Nguyen Van Hoang Hardened"
    phone = "0988888888"
    role = "SYSTEM_ADMIN"
    roles = @("SYSTEM_ADMIN")
    accountId = [System.Guid]::NewGuid().ToString()
} | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validUserToken; "If-Match" = $etag } -Body $hackRolePayload
$updated = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($null -eq $updated.role) -and ($null -eq $updated.roles)
Report-Result "TC-PROF-02" "Chong sua Role qua PATCH me: Loai bo triet de role khoi DTO va DB" $pass "Status: $($res.StatusCode), ETag moi: $($res.Headers['ETag'])"
$newEtag = $res.Headers["ETag"]
if (-not $newEtag) { $newEtag = '"2"' }

# TC-PROF-03: Cập nhật hồ sơ thiếu header If-Match bắt buộc
$updateBody = @{ displayName = "Hoang Test Concurrency"; phone = "0911223344" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validUserToken } -Body $updateBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-PROF-03" "Tu choi khi thieu header If-Match (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-PROF-04: Header If-Match sai định dạng
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validUserToken; "If-Match" = '"invalid_etag"' } -Body $updateBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-PROF-04" "Tu choi khi If-Match sai dinh dang (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-PROF-05: Header If-Match phiên bản cũ / không khớp (Version Conflict)
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validUserToken; "If-Match" = '"999999"' } -Body $updateBody
$pass = ($res.StatusCode -eq 409) -and ($res.Content -like "*ERR_PROFILE_VERSION_CONFLICT*")
Report-Result "TC-PROF-05" "Chan xung dot phien ban ETag cu (HTTP 409 ERR_PROFILE_VERSION_CONFLICT)" $pass "Status: $($res.StatusCode)"

# TC-PROF-06: Cập nhật displayName rỗng
$emptyNameBody = @{ displayName = "   "; phone = "0911223344" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validUserToken; "If-Match" = $newEtag } -Body $emptyNameBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-PROF-06" "Tu choi displayName rong (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-PROF-07: Cập nhật displayName quá 100 ký tự
$longNameBody = @{ displayName = ("A" * 105); phone = "0911223344" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validUserToken; "If-Match" = $newEtag } -Body $longNameBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-PROF-07" "Tu choi displayName vuot qua 100 ky tu (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-PROF-08: Cập nhật phone quá 30 ký tự
$longPhoneBody = @{ displayName = "Nguyen Van Hoang"; phone = ("0" * 35) } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validUserToken; "If-Match" = $newEtag } -Body $longPhoneBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-PROF-08" "Tu choi so dien thoai vuot qua 30 ky tu (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# --------------------------------------------------------------------
# TỔNG KẾT BỘ KIỂM THỬ
# --------------------------------------------------------------------
Write-Host "`n====================================================================" -ForegroundColor Cyan
Write-Host " KET QUA TONG THE BO KIEM THU W2-M6-03 (PHAN BO LAI SPRINT 2):" -ForegroundColor Cyan
Write-Host " TONG SO CA KIEM THU : $($global:passCount + $global:failCount)" -ForegroundColor White
Write-Host " SO CA THANH CONG (PASS) : $global:passCount" -ForegroundColor Green
Write-Host " SO CA THAT BAI   (FAIL) : $global:failCount" -ForegroundColor $(if ($global:failCount -eq 0) { "Green" } else { "Red" })
Write-Host "====================================================================" -ForegroundColor Cyan

if ($global:failCount -gt 0) {
    exit 1
} else {
    exit 0
}
