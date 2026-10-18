[CmdletBinding()]
param(
    [string]$DockerNetwork = "aura_network",
    [string]$FirebaseProjectId = "aura-dev-97716",
    [string]$CredentialPath = "",
    [string]$NotificationKeyPath = "",
    [string]$NotificationKeyId = "local-v1"
)

$ErrorActionPreference = "Stop"

if ($NotificationKeyId -notmatch '^[A-Za-z0-9._-]{1,64}$') {
    throw "NotificationKeyId must contain 1-64 letters, digits, dots, underscores or hyphens."
}

if ([string]::IsNullOrWhiteSpace($DockerNetwork)) {
    throw "DockerNetwork must not be empty."
}

if ([string]::IsNullOrWhiteSpace($FirebaseProjectId)) {
    throw "FirebaseProjectId must not be empty."
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
        throw "File not found: $path"
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
    throw "Service account does not match the selected Firebase project."
}

Remove-Variable credential

$keyText = (
    Get-Content -LiteralPath $NotificationKeyPath -Raw
).Trim()

try {
    $keyBytes = [Convert]::FromBase64String($keyText)
}
catch {
    throw "Notification encryption key is not valid Base64."
}

try {
    if ($keyBytes.Length -ne 32) {
        throw "Notification encryption key must contain exactly 32 bytes."
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
            throw "Password must not be empty."
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
    $identityPassword = Read-AuraPassword "Mat khau aura_identity_app"
    $profilePassword = Read-AuraPassword "Mat khau aura_profile_app"
    $analysisPassword = Read-AuraPassword "Mat khau aura_analysis_app"

    $billingPassword = Read-AuraPassword `
        "Mat khau aura_billing_app"

    $notificationPassword = Read-AuraPassword `
        "Mat khau aura_notification_app"

    $values = @{
        AURA_DOCKER_NETWORK = $DockerNetwork
        FIREBASE_PROJECT_ID = $FirebaseProjectId
        FIREBASE_SERVICE_ACCOUNT_PATH = $CredentialPath.Replace("\", "/")
        NOTIFICATION_ENCRYPTION_KEY_ID = $NotificationKeyId
        NOTIFICATION_ENCRYPTION_KEY = $keyText
        NOTIFICATION_DB_PASSWORD = $notificationPassword

        # Npgsql connection strings quote these values in compose.yaml.
        IDENTITY_DB_PASSWORD = $identityPassword.Replace('"', '""')
        PROFILE_DB_PASSWORD = $profilePassword.Replace('"', '""')
        ANALYSIS_DB_PASSWORD = $analysisPassword.Replace('"', '""')
        BILLING_DB_PASSWORD = $billingPassword.Replace('"', '""')
    }

    foreach ($name in $values.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $values[$name],
            "Process"
        )
    }

    Write-Host "PASS: AURA configuration loaded into this PowerShell process."
    Write-Host "Database passwords were loaded, not created or verified. Keep this window open."
}
finally {
    $identityPassword = $null
    $profilePassword = $null
    $analysisPassword = $null
    $billingPassword = $null
    $notificationPassword = $null
    $keyText = $null
    $values = $null
}
