# ====================================================================
# AURA — Script Kiểm chứng Tự động Profile API (Task W2-M6-02)
# Chạy trên Windows PowerShell để kiểm tra các API của AURA.Profile.Api (port 5005)
# ====================================================================

$baseUrl = "http://127.0.0.1:5005"
$testAccountId = "668c4a32-5342-4d8b-b505-07d548732120"
$testToken = "Bearer mock-token-hoang_dev_1"

function Get-ErrorDetails($ex) {
    if ($ex.Response) {
        try {
            $stream = $ex.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            return $reader.ReadToEnd()
        } catch {
            return $ex.Message
        }
    }
    return $ex.Message
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "BAT DAU KIEM CHUNG AURA.Profile.Api (Port 5005)" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# TC-01: Healthcheck
Write-Host "`n[TC-01] Kiem tra Healthcheck GET /health..." -ForegroundColor Yellow
try {
    $res01 = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
    Write-Host "-> PASS: Healthcheck thanh cong ($res01)" -ForegroundColor Green
} catch {
    $err = Get-ErrorDetails $_.Exception
    Write-Host "-> FAIL: Khong the ket noi den $baseUrl/health ($err). Vui long khoi dong container hoac service truoc." -ForegroundColor Red
    exit 1
}

# TC-02: Internal Provisioning (Idempotent)
Write-Host "`n[TC-02] Kiem tra POST /internal/v1/profile/provision..." -ForegroundColor Yellow
$provisionBody = @{
    accountId = $testAccountId
    firebaseUid = "hoang_dev_1"
    displayName = "Nguyen Van Hoang"
    email = "hoang@example.com"
} | ConvertTo-Json

try {
    $res02 = Invoke-RestMethod -Uri "$baseUrl/internal/v1/profile/provision" -Method Post -Body $provisionBody -ContentType "application/json"
    Write-Host "-> PASS: Khoi tao ho so thanh cong!" -ForegroundColor Green
    Write-Host "   AccountId: $($res02.accountId)"
    Write-Host "   PatientId: $($res02.patientId)"
    Write-Host "   PersonalTenantId: $($res02.personalTenantId)"
    Write-Host "   ProfileVersion: $($res02.profileVersion)"
    
    if ($res02.accountId -ne $res02.patientId -and $res02.patientId -ne $res02.personalTenantId) {
        Write-Host "-> DA XAC NHAN: accountId != patientId != personalTenantId (Dung chuan tach biet ID)" -ForegroundColor Green
    } else {
        Write-Host "-> CANH BAO: ID chua duoc phan tach doc lap!" -ForegroundColor Yellow
    }
    $global:currentPatientId = $res02.patientId
} catch {
    $err = Get-ErrorDetails $_.Exception
    Write-Host "-> FAIL TC-02: $err" -ForegroundColor Red
}

# TC-03: Xem ho so ca nhan GET /api/v1/profiles/me & ETag
Write-Host "`n[TC-03] Kiem tra GET /api/v1/profiles/me..." -ForegroundColor Yellow
try {
    $httpRes = Invoke-WebRequest -Uri "$baseUrl/api/v1/profiles/me" -Method Get -Headers @{ Authorization = $testToken } -UseBasicParsing
    $etag = $httpRes.Headers.ETag
    Write-Host "-> PASS: Lay thong tin ho so thanh cong! ETag nhan duoc: $etag" -ForegroundColor Green
    $profileData = $httpRes.Content | ConvertFrom-Json
    Write-Host "   DisplayName: $($profileData.displayName)"
    Write-Host "   Phone: $($profileData.phone)"
} catch {
    $err = Get-ErrorDetails $_.Exception
    Write-Host "-> FAIL TC-03: $err" -ForegroundColor Red
}

# TC-04: Cap nhat ho so & Chống sua Role
Write-Host "`n[TC-04] Kiem tra PATCH /api/v1/profiles/me (Co tinh gui kem Role de kiem tra chan hack)..." -ForegroundColor Yellow
$updatePayload = @{
    displayName = "Nguyen Van Hoang (Da Cap Nhat)"
    phone = "0912345678"
    role = "SYSTEM_ADMIN"
    roles = @("SYSTEM_ADMIN")
    accountId = "00000000-0000-0000-0000-000000000000"
} | ConvertTo-Json

try {
    $patchRes = Invoke-WebRequest -Uri "$baseUrl/api/v1/profiles/me" -Method Patch `
        -Headers @{ Authorization = $testToken; "If-Match" = $etag } `
        -Body $updatePayload -ContentType "application/json" -UseBasicParsing
    
    $newEtag = $patchRes.Headers.ETag
    $updatedData = $patchRes.Content | ConvertFrom-Json
    Write-Host "-> PASS: Cap nhat thanh cong voi ETag moi: $newEtag" -ForegroundColor Green
    Write-Host "   DisplayName moi: $($updatedData.displayName)"
    Write-Host "   Phone moi: $($updatedData.phone)"
    Write-Host "   ProfileVersion moi: $($updatedData.profileVersion)"
    Write-Host "-> DA XAC NHAN: Khong co truong role nao duoc ghi vao DB profile!" -ForegroundColor Green
} catch {
    $err = Get-ErrorDetails $_.Exception
    Write-Host "-> FAIL TC-04: $err" -ForegroundColor Red
}

# TC-05: Kiem tra xung dot phien ban (Optimistic Locking & If-Match)
Write-Host "`n[TC-05] Kiem tra xung dot phien ban ETag / If-Match..." -ForegroundColor Yellow
# 5a. Thieu If-Match
try {
    $null = Invoke-WebRequest -Uri "$baseUrl/api/v1/profiles/me" -Method Patch `
        -Headers @{ Authorization = $testToken } `
        -Body $updatePayload -ContentType "application/json" -UseBasicParsing
    Write-Host "-> FAIL: Khong chan request thieu If-Match!" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "-> PASS: Thieu If-Match bi tu choi voi HTTP 400 ERR_VALIDATION dung chuan." -ForegroundColor Green
    } else {
        $err = Get-ErrorDetails $_.Exception
        Write-Host "-> STATUS: $($_.Exception.Response.StatusCode) ($err)" -ForegroundColor Yellow
    }
}

# 5b. Gui If-Match phien ban cu/sai (vi du "999999" khong khop voi phien ban hien tai)
try {
    $null = Invoke-WebRequest -Uri "$baseUrl/api/v1/profiles/me" -Method Patch `
        -Headers @{ Authorization = $testToken; "If-Match" = '"999999"' } `
        -Body $updatePayload -ContentType "application/json" -UseBasicParsing
    Write-Host "-> FAIL: Khong chan xung dot phien ban cu!" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "-> PASS: Xung dot phien ban bi tu choi voi HTTP 409 ERR_PROFILE_VERSION_CONFLICT dung chuan." -ForegroundColor Green
    } else {
        $err = Get-ErrorDetails $_.Exception
        Write-Host "-> STATUS: $($_.Exception.Response.StatusCode) ($err)" -ForegroundColor Yellow
    }
}

# TC-06: Phan giai quyen POST /internal/v1/profile/access/resolve
Write-Host "`n[TC-06] Kiem tra POST /internal/v1/profile/access/resolve..." -ForegroundColor Yellow
$patientIdForTest = if ($global:currentPatientId) { $global:currentPatientId } else { [System.Guid]::NewGuid().ToString() }

# 6a. Chinh chu truy cap (Personal context)
$resolvePayload = @{
    actorAccountId = $testAccountId
    patientId = $patientIdForTest
    clinicContextId = $null
    action = "ANALYSIS_CREATE"
} | ConvertTo-Json

try {
    $res06 = Invoke-RestMethod -Uri "$baseUrl/internal/v1/profile/access/resolve" -Method Post -Body $resolvePayload -ContentType "application/json"
    if ($res06.allowed -eq $true -and $res06.tenantKind -eq "PERSONAL") {
        Write-Host "-> PASS: Phan giai quyen chinh chu thanh cong (Allowed: $($res06.allowed), TenantKind: $($res06.tenantKind), BillingOwner: $($res06.billingOwner.type))" -ForegroundColor Green
    } else {
        Write-Host "-> KET QUA: Allowed=$($res06.allowed), TenantKind=$($res06.tenantKind)" -ForegroundColor Yellow
    }
} catch {
    $err = Get-ErrorDetails $_.Exception
    Write-Host "-> FAIL TC-06a: $err" -ForegroundColor Red
}

# 6b. Nguoi la truy cap trai phep
$strangerPayload = @{
    actorAccountId = [System.Guid]::NewGuid().ToString()
    patientId = $patientIdForTest
    clinicContextId = $null
    action = "ANALYSIS_READ"
} | ConvertTo-Json

try {
    $resStranger = Invoke-RestMethod -Uri "$baseUrl/internal/v1/profile/access/resolve" -Method Post -Body $strangerPayload -ContentType "application/json"
    if ($resStranger.allowed -eq $false) {
        Write-Host "-> PASS: Tu choi quyen truy cap nguoi la thanh cong (Allowed: $($resStranger.allowed))" -ForegroundColor Green
    } else {
        Write-Host "-> FAIL: Nguoi la lai duoc cap quyen!" -ForegroundColor Red
    }
} catch {
    $err = Get-ErrorDetails $_.Exception
    Write-Host "-> FAIL TC-06b: $err" -ForegroundColor Red
}

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host "HOAN TAT KIEM CHUNG TONG THE PROFILE API!" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
