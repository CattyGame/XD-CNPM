param(
    [int]$Limit = 5,
    [string]$BatchId = "",
    [string]$RequestId = ""
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [AURA] KIEM TRA DONG BO DATABASE REPORTING POSTGRESQL" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$whereClause = ""
if (-not [string]::IsNullOrWhiteSpace($BatchId)) {
    $whereClause = "WHERE batch_id = '$BatchId'::uuid"
} elseif (-not [string]::IsNullOrWhiteSpace($RequestId)) {
    $whereClause = "WHERE request_id = '$RequestId'::uuid"
}

$countCmd = "SELECT COUNT(*) AS total_receipts FROM reporting.camera_ingestion_receipts;"
$queryCmd = "SELECT request_id, batch_id, recorded_at, device_id FROM reporting.camera_ingestion_receipts $whereClause ORDER BY recorded_at DESC LIMIT $Limit;"

Write-Host "Tong so ban ghi hien tai trong database:" -ForegroundColor Yellow
docker exec -e PGPASSWORD="VgoMrxHYz3jxj3SABUh4gb1HdjC2UBIijNNtlNP00drjxB77" aura-s3-reporting-postgres-1 psql -U aura_s3_admin -d aura_reporting -c "$countCmd"

Write-Host "`nDanh sach $Limit ban ghi moi nhat:" -ForegroundColor Yellow
docker exec -e PGPASSWORD="VgoMrxHYz3jxj3SABUh4gb1HdjC2UBIijNNtlNP00drjxB77" aura-s3-reporting-postgres-1 psql -U aura_s3_admin -d aura_reporting -c "$queryCmd"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Ghi chu: So sanh 'request_id' va 'batch_id' voi tren Web & NiFi!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
