[CmdletBinding()]
param(
    [string]$DockerNetwork = "aura_network",
    [string]$FirebaseProjectId = "aura-dev-97716",
    [string]$CredentialPath = "",
    [string]$NotificationKeyPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DockerNetwork)) {
    throw "DockerNetwork không được để trống."
}

if ([string]::IsNullOrWhiteSpace($FirebaseProjectId)) {
    throw "FirebaseProjectId không được để trống."
}

if ([string]::IsNullOrWhiteSpace($CredentialPath)) {
    $CredentialPath = Join-Path $env:USERPROFILE `
        ".aura\credentials\aura-dev-service-account.json"
}

if ([string]::IsNullOrWhiteSpace($NotificationKeyPath)) {
    $NotificationKeyPath = Join-Path $env:USERPROFILE `
        ".aura\credentials\notification-token-key-v1.txt"
}

foreach ($path in @($CredentialPath, $NotificationKeyPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Không tìm thấy file: $path"
    }
}

$CredentialPath = (Resolve-Path -LiteralPath $CredentialPath).Path
$NotificationKeyPath = (
    Resolve-Path -LiteralPath $NotificationKeyPath
).Path

# Validate project without printing credential contents.
$credential = Get-Content -LiteralPath $CredentialPath -Raw |
    ConvertFrom-Json

if (
    $credential.type -ne "service_account" -or
    $credential.project_id -ne $FirebaseProjectId
) {
    throw "Service account không khớp Firebase project đã chọn."
}

Remove-Variable credential

$keyText = (
    Get-Content -LiteralPath $NotificationKeyPath -Raw
).Trim()

try {
    $keyBytes = [Convert]::FromBase64String($keyText)
}
catch {
    throw "Khóa mã hóa Notification không phải Base64 hợp lệ."
}

try {
    if ($keyBytes.Length -ne 32) {
        throw "Khóa mã hóa Notification phải có đúng 32 byte."
    }
}
finally {
    [Array]::Clear($keyBytes, 0, $keyBytes.Length)
}

function Read-AuraPassword {
    param([string]$Prompt)

    $secret = Read-Host $Prompt -AsSecureString

    try {
        if ($secret.Length -eq 0) {
            throw "Mật khẩu không được để trống."
        }

        return (
            New-Object System.Net.NetworkCredential("", $secret)
        ).Password
    }
    finally {
        $secret.Dispose()
    }
}

try {
    $billingPassword = Read-AuraPassword `
        "Mật khẩu aura_billing_app"

    $notificationPassword = Read-AuraPassword `
        "Mật khẩu aura_notification_app"

    $values = @{
        AURA_DOCKER_NETWORK = $DockerNetwork
        FIREBASE_PROJECT_ID = $FirebaseProjectId
        FIREBASE_SERVICE_ACCOUNT_PATH = $CredentialPath.Replace("\", "/")
        NOTIFICATION_ENCRYPTION_KEY_ID = "local-v1"
        NOTIFICATION_ENCRYPTION_KEY = $keyText
        NOTIFICATION_DB_PASSWORD = $notificationPassword

        # Billing currently interpolates a quoted Npgsql password value.
        BILLING_DB_PASSWORD = $billingPassword.Replace('"', '""')
    }

    foreach ($name in $values.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $values[$name],
            "Process"
        )
    }

    Write-Host "PASS: Đã nạp cấu hình AURA vào cửa sổ PowerShell hiện tại."
    Write-Host "Không in hoặc ghi mật khẩu/khóa vào repository."
}
finally {
    $billingPassword = $null
    $notificationPassword = $null
    $keyText = $null
    $values = $null
}
