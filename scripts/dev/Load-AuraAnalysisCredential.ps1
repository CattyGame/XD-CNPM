[CmdletBinding()]
param([string]$ServiceAuthDirectory = "")
$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ServiceAuthDirectory)) {
    $ServiceAuthDirectory = $env:AURA_SERVICE_AUTH_HOME
}
if ([string]::IsNullOrWhiteSpace($ServiceAuthDirectory)) {
    $ServiceAuthDirectory = Join-Path $env:USERPROFILE ".aura\service-auth\local-v1"
}
$clientPath = Join-Path $ServiceAuthDirectory "clients\analysis-api.json"
if (-not (Test-Path -LiteralPath $clientPath -PathType Leaf)) {
    throw "Existing Analysis service credential file was not found."
}
try {
    try {
        $analysisCredential = Get-Content -LiteralPath $clientPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "Analysis service credential file is unreadable or malformed."
    }
    if ($analysisCredential.clientId -ne "analysis-api" -or
        [string]::IsNullOrWhiteSpace([string]$analysisCredential.clientSecret)) {
        throw "Invalid Analysis service credential."
    }
    $env:ANALYSIS_SERVICE_CLIENT_SECRET = [string]$analysisCredential.clientSecret
    Write-Host "PASS: Existing analysis-api service credential loaded into this PowerShell process."
}
finally {
    $analysisCredential = $null
}
