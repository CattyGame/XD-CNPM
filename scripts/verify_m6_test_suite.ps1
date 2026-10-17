# ====================================================================
# AURA — Bộ Kiểm thử Toàn diện M6 (Task W2-M6-03 / SCRUM-316)
# Kiểm tra:
# 1. Token sai / hết hạn / thu hồi / thiếu Authorization
# 2. Hồ sơ trái quyền / Chống sửa Role qua Profile / Access Resolve
# 3. Validation dữ liệu & Optimistic Concurrency (ETag / If-Match)
# ====================================================================

[CmdletBinding()]
param(
    [string]$IdentityUrl = "http://127.0.0.1:5004",
    [string]$ProfileUrl = "http://127.0.0.1:5005"
)

$ErrorActionPreference = "Continue"

$testAccountId = "668c4a32-5342-4d8b-b505-07d548732120"
$validToken = "Bearer mock-token-hoang_dev_1"
$uninitializedToken = "Bearer mock-token-uninitialized_user_999"
$invalidToken = "Bearer invalid_garbage_token_xyz"
$expiredToken = "Bearer expired_mock_token_abc"

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
Write-Host " BAT DAU CHAY BO KIEM THU TOAN DIEN M6 (TASK W2-M6-03)" -ForegroundColor Cyan
Write-Host " Identity API: $IdentityUrl | Profile API: $ProfileUrl" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------
# PHẦN 1: KIỂM THỬ XÁC THỰC & TOKEN (AUTHENTICATION & TOKEN VALIDATION)
# --------------------------------------------------------------------
Write-Host "`n--- [PHAN 1] KIEM THU TOKEN & XAC THUC ---" -ForegroundColor Yellow

# TC-AUTH-01: Yêu cầu thiếu header Authorization
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/me" -Method "Get"
$pass = ($res.StatusCode -eq 401) -and ($res.Content -like "*ERR_UNAUTHORIZED*")
Report-Result "TC-AUTH-01" "Tu choi request thieu header Authorization (HTTP 401 ERR_UNAUTHORIZED)" $pass "Status: $($res.StatusCode)"

# TC-AUTH-02: Token sai định dạng / rác
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/me" -Method "Get" -Headers @{ Authorization = $invalidToken }
$pass = ($res.StatusCode -eq 401) -and ($res.Content -like "*ERR_UNAUTHORIZED*")
Report-Result "TC-AUTH-02" "Tu choi Token rac / khong hop le (HTTP 401 ERR_UNAUTHORIZED)" $pass "Status: $($res.StatusCode)"

# TC-AUTH-03: Truy vấn tài khoản chưa từng Bootstrap
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/me" -Method "Get" -Headers @{ Authorization = $uninitializedToken }
$pass = ($res.StatusCode -eq 409) -and ($res.Content -like "*ERR_ACCOUNT_NOT_INITIALIZED*")
Report-Result "TC-AUTH-03" "Chan tai khoan chua bootstrap qua GET /me (HTTP 409 ERR_ACCOUNT_NOT_INITIALIZED)" $pass "Status: $($res.StatusCode)"

# TC-AUTH-04: Bootstrap tài khoản hợp lệ (Idempotent)
$res = Invoke-ApiSafe -Uri "$IdentityUrl/api/v1/identity/bootstrap" -Method "Post" -Headers @{ Authorization = $validToken }
$pass = ($res.StatusCode -eq 200) -and ($res.Content -like "*ACTIVE*")
Report-Result "TC-AUTH-04" "Bootstrap tai khoan hop le thanh cong (HTTP 200 OK)" $pass "Status: $($res.StatusCode)"

# --------------------------------------------------------------------
# PHẦN 2: KIỂM THỬ HỒ SƠ TRÁI QUYỀN & CHỐNG SỬA ROLE (AUTHORIZATION)
# --------------------------------------------------------------------
Write-Host "`n--- [PHAN 2] KIEM THU HO SO TRAI QUYEN & CHONG SUA ROLE ---" -ForegroundColor Yellow

# Khởi tạo hồ sơ nội bộ để chuẩn bị dữ liệu kiểm thử
$provisionPayload = @{
    accountId = $testAccountId
    firebaseUid = "hoang_dev_1"
    displayName = "Nguyen Van Hoang"
    email = "hoang@example.com"
} | ConvertTo-Json
$provRes = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/provision" -Method "Post" -Body $provisionPayload
$provData = if ($provRes.StatusCode -eq 200) { $provRes.Content | ConvertFrom-Json } else { $null }
$currentPatientId = if ($provData) { $provData.patientId } else { [System.Guid]::NewGuid().ToString() }

# Lấy thông tin hồ sơ hiện tại để nhận ETag
$getProfileRes = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Get" -Headers @{ Authorization = $validToken }
$etag = $getProfileRes.Headers["ETag"]
if (-not $etag) { $etag = '"1"' }

# TC-AUTHZ-01: Cố tình gửi kèm Role trong PATCH me để leo thang đặc quyền
$hackRolePayload = @{
    displayName = "Nguyen Van Hoang"
    phone = "0988888888"
    role = "SYSTEM_ADMIN"
    roles = @("SYSTEM_ADMIN")
    accountId = [System.Guid]::NewGuid().ToString()
} | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validToken; "If-Match" = $etag } -Body $hackRolePayload
$updated = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($null -eq $updated.role) -and ($null -eq $updated.roles)
Report-Result "TC-AUTHZ-01" "Chong sua Role qua PATCH me: Loai bo triet de role khoi DTO va DB" $pass "Status: $($res.StatusCode), ETag moi: $($res.Headers['ETag'])"
$newEtag = $res.Headers["ETag"]
if (-not $newEtag) { $newEtag = '"2"' }

# TC-AUTHZ-02: Phân giải quyền cho Chính chủ (Personal Context)
$ownAccessPayload = @{
    actorAccountId = $testAccountId
    patientId = $currentPatientId
    clinicContextId = $null
    action = "ANALYSIS_CREATE"
} | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/access/resolve" -Method "Post" -Body $ownAccessPayload
$accessData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($accessData.allowed -eq $true) -and ($accessData.tenantKind -eq "PERSONAL")
Report-Result "TC-AUTHZ-02" "Phan giai quyen chinh chu thanh cong (Allowed: true, TenantKind: PERSONAL)" $pass "Allowed: $($accessData.allowed)"

# TC-AUTHZ-03: Phân giải quyền cho Người lạ (Unauthorized / Stranger)
$strangerPayload = @{
    actorAccountId = [System.Guid]::NewGuid().ToString()
    patientId = $currentPatientId
    clinicContextId = $null
    action = "ANALYSIS_READ"
} | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/access/resolve" -Method "Post" -Body $strangerPayload
$strangerData = if ($res.StatusCode -eq 200) { $res.Content | ConvertFrom-Json } else { $null }
$pass = ($res.StatusCode -eq 200) -and ($strangerData.allowed -eq $false)
Report-Result "TC-AUTHZ-03" "Tu choi quyen truy cap nguoi la (Allowed: false)" $pass "Allowed: $($strangerData.allowed)"

# --------------------------------------------------------------------
# PHẦN 3: KIỂM THỬ VALIDATION & OPTIMISTIC CONCURRENCY (ETAG / IF-MATCH)
# --------------------------------------------------------------------
Write-Host "`n--- [PHAN 3] KIEM THU VALIDATION & OPTIMISTIC CONCURRENCY ---" -ForegroundColor Yellow

$validUpdateBody = @{
    displayName = "Hoang Test Concurrency"
    phone = "0911223344"
} | ConvertTo-Json

# TC-VAL-01: Cập nhật hồ sơ thiếu header If-Match bắt buộc
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validToken } -Body $validUpdateBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-VAL-01" "Tu choi khi thieu header If-Match (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-VAL-02: Header If-Match sai định dạng
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validToken; "If-Match" = '"invalid_etag"' } -Body $validUpdateBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-VAL-02" "Tu choi khi If-Match sai dinh dang (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-VAL-03: Header If-Match phiên bản cũ / không khớp (Version Conflict)
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validToken; "If-Match" = '"999999"' } -Body $validUpdateBody
$pass = ($res.StatusCode -eq 409) -and ($res.Content -like "*ERR_PROFILE_VERSION_CONFLICT*")
Report-Result "TC-VAL-03" "Chan xung dot phien ban ETag cu (HTTP 409 ERR_PROFILE_VERSION_CONFLICT)" $pass "Status: $($res.StatusCode)"

# TC-VAL-04: Cập nhật displayName rỗng
$emptyNameBody = @{ displayName = "   "; phone = "0911223344" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validToken; "If-Match" = $newEtag } -Body $emptyNameBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-VAL-04" "Tu choi displayName rong (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-VAL-05: Cập nhật displayName quá 100 ký tự
$longName = "A" * 105
$longNameBody = @{ displayName = $longName; phone = "0911223344" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validToken; "If-Match" = $newEtag } -Body $longNameBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-VAL-05" "Tu choi displayName vuot qua 100 ky tu (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-VAL-06: Cập nhật phone quá 30 ký tự
$longPhone = "0" * 35
$longPhoneBody = @{ displayName = "Nguyen Van Hoang"; phone = $longPhone } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/api/v1/profiles/me" -Method "Patch" `
    -Headers @{ Authorization = $validToken; "If-Match" = $newEtag } -Body $longPhoneBody
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-VAL-06" "Tu choi so dien thoai vuot qua 30 ky tu (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-VAL-07: Service Token thiếu clientId hoặc clientSecret
$emptyTokenReq = @{ clientId = ""; clientSecret = "" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$IdentityUrl/internal/v1/identity/service-tokens" -Method "Post" -Body $emptyTokenReq
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-VAL-07" "Tu choi cap service token khi thieu clientId/secret (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# TC-VAL-08: Provision thiếu accountId
$emptyProvReq = @{ accountId = [System.Guid]::Empty.ToString(); displayName = "No ID" } | ConvertTo-Json
$res = Invoke-ApiSafe -Uri "$ProfileUrl/internal/v1/profile/provision" -Method "Post" -Body $emptyProvReq
$pass = ($res.StatusCode -eq 400) -and ($res.Content -like "*ERR_VALIDATION*")
Report-Result "TC-VAL-08" "Tu choi provision khi accountId rong (HTTP 400 ERR_VALIDATION)" $pass "Status: $($res.StatusCode)"

# --------------------------------------------------------------------
# TỔNG KẾT BỘ KIỂM THỬ
# --------------------------------------------------------------------
Write-Host "`n====================================================================" -ForegroundColor Cyan
Write-Host " KET QUA TONG THE BO KIEM THU W2-M6-03:" -ForegroundColor Cyan
Write-Host " TONG SO CA KIEM THU : $($global:passCount + $global:failCount)" -ForegroundColor White
Write-Host " SO CA THANH CONG (PASS) : $global:passCount" -ForegroundColor Green
Write-Host " SO CA THAT BAI   (FAIL) : $global:failCount" -ForegroundColor $(if ($global:failCount -eq 0) { "Green" } else { "Red" })
Write-Host "====================================================================" -ForegroundColor Cyan

if ($global:failCount -gt 0) {
    exit 1
} else {
    exit 0
}
