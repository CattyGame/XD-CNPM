[CmdletBinding()]
param(
    [string]$EnvFile = ".env",
    [string]$ComposeProject = "aura"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$envPath = if ([System.IO.Path]::IsPathRooted($EnvFile)) {
    $EnvFile
} else {
    Join-Path $repositoryRoot $EnvFile
}

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
    throw "Environment file not found: $envPath"
}

$testSql = Join-Path $repositoryRoot "infra/postgres/migrations/tests/test_004_billing_reservations.sql"
if (-not (Test-Path -LiteralPath $testSql -PathType Leaf)) {
    throw "Billing reservation SQL test was not found: $testSql"
}

$composeArgs = @(
    "compose", "-p", $ComposeProject, "--env-file", $envPath,
    "-f", (Join-Path $repositoryRoot "compose.yaml"),
    "exec", "-T", "postgres",
    "psql", "-X", "-v", "ON_ERROR_STOP=1", "-U", "aura_dev", "-d", "aura_dev"
)

Get-Content -LiteralPath $testSql -Raw -Encoding UTF8 | & docker @composeArgs
if ($LASTEXITCODE -ne 0) {
    throw "Billing reservation schema smoke test failed. The SQL test is transactional and ends with ROLLBACK."
}

Write-Host "PASS: Billing reservation schema smoke test completed. Test rows were rolled back."
