[CmdletBinding()]
param(
    [string]$EnvFile = '.env',
    [string]$ComposeProject = 'aura'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

$envPath = if ([System.IO.Path]::IsPathRooted($EnvFile)) {
    $EnvFile
} else {
    Join-Path $repositoryRoot $EnvFile
}

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
    throw "Environment file not found: $envPath"
}

$composeFile = Join-Path $repositoryRoot 'compose.yaml'

if (-not (Test-Path -LiteralPath $composeFile -PathType Leaf)) {
    throw "Docker Compose file not found: $composeFile"
}

$testFiles = @(
    'infra/postgres/migrations/tests/test_007_billing_settlement_inbox.sql',
    'infra/postgres/migrations/tests/test_008_billing_settlement_conflict_audit.sql'
)

$composeArgs = @(
    'compose', '-p', $ComposeProject, '--env-file', $envPath,
    '-f', $composeFile,
    'exec', '-T', 'postgres',
    'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', 'aura_dev', '-d', 'aura_dev'
)

foreach ($relativePath in $testFiles) {
    $testSql = Join-Path $repositoryRoot $relativePath

    if (-not (Test-Path -LiteralPath $testSql -PathType Leaf)) {
        throw "SQL test was not found: $testSql"
    }

    Write-Host "`n===== RUNNING $relativePath =====" -ForegroundColor Cyan

    $sql = Get-Content -LiteralPath $testSql -Raw -Encoding UTF8
    $sql | & docker @composeArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Billing settlement inbox SQL test failed: $relativePath. Check the PostgreSQL output."
    }

    Write-Host "PASS: $relativePath" -ForegroundColor Green
}

Write-Host "`nPASS: All Billing settlement inbox SQL tests completed. Test rows were rolled back." -ForegroundColor Green