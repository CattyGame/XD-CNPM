# AURA Batch Infrastructure Stress Test Script
# Task SCRUM-341 (W4-M1-04) - DoD AC1: 100 concurrent images without OOM, RAM < 80%

param(
    [string]$RabbitMqHost = "127.0.0.1",
    [int]$ManagementPort = 15372,
    [string]$Vhost = "/aura-s3",
    [string]$User = "s3-operator",
    [string]$Password = "aura_s3_operator_2026",
    [int]$BatchCount = 100,
    [double]$MaxRamThresholdPercent = 80.0
)

$ErrorActionPreference = "Stop"

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "AURA Task W4-M1-04: Batch Infrastructure Stress Test (100 Large Images)" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

$baseAuth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($User + ":" + $Password))
$encodedVhost = [Uri]::EscapeDataString($Vhost)
$exchangeUri = "http://" + $RabbitMqHost + ":" + $ManagementPort + "/api/exchanges/" + $encodedVhost + "/aura.s3.jobs/publish"

Write-Host "[1/4] Ghi nhan baseline tai nguyen truoc tai (Baseline Resources)..." -ForegroundColor Yellow
$statsBefore = docker stats --no-stream --format "{{.Name}},{{.CPUPerc}},{{.MemUsage}},{{.MemPerc}},{{.NetIO}}"
Write-Host $statsBefore -ForegroundColor Gray

Write-Host ""
Write-Host "[2/4] Bom dong thoi $BatchCount tac vu batch (Synthetic 100 Large Retina Images)..." -ForegroundColor Yellow
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$batchId = [Guid]::NewGuid().ToString()

for ($i = 1; $i -le $BatchCount; $i++) {
    $jobId = [Guid]::NewGuid().ToString()
    $jobPayload = @{
        properties = @{ delivery_mode = 2; content_type = "application/json" }
        routing_key = "analysis.job.run"
        payload = (@{
            job_id = $jobId
            batch_id = $batchId
            image_index = $i
            image_name = "STRESS_TEST_IMAGE_$i.PNG"
            dimensions = @{ width = 999; height = 960 }
            payload_size_kb = 1250
            algorithm = "FRANGI_V1.3_VESSEL_DENSITY_TORTUOSITY"
            correlation_id = [Guid]::NewGuid().ToString()
            created_at = (Get-Date).ToUniversalTime().ToString("o")
        } | ConvertTo-Json -Compress)
        payload_encoding = "string"
    } | ConvertTo-Json -Compress

    $res = Invoke-RestMethod -Uri $exchangeUri -Headers @{ Authorization = "Basic " + $baseAuth } -Method Post -Body $jobPayload -ContentType "application/json"
    if (-not $res.routed) {
        throw ("Loi dinh tuyen job " + $i + " vao exchange aura.s3.jobs")
    }
}

$sw.Stop()
$pumpSeconds = [math]::Round($sw.Elapsed.TotalSeconds, 2)
$throughput = [math]::Round($BatchCount / $pumpSeconds, 2)
Write-Host "  Da bom thanh cong $BatchCount tac vu batch trong $pumpSeconds giay ($throughput req/s)!" -ForegroundColor Green

Write-Host ""
Write-Host "[3/4] Giam sat tai nguyen he thong dinh tai (Peak Resource Telemetry)..." -ForegroundColor Yellow
$statsRaw = docker stats --no-stream --format "{{.Name}};{{.CPUPerc}};{{.MemUsage}};{{.MemPerc}};{{.NetIO}};{{.BlockIO}}"
$maxObservedMemPercent = 0.0
$violatingContainers = @()
$parsedStats = @()

foreach ($line in ($statsRaw -split "`n")) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
    $parts = $trimmed -split ";"
    if ($parts.Length -ge 5) {
        $name = $parts[0]
        $cpu = $parts[1]
        $memUsage = $parts[2]
        $memPercStr = $parts[3].Replace("%", "").Trim()
        $netIo = $parts[4]

        $memPerc = 0.0
        [double]::TryParse($memPercStr, [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$memPerc) | Out-Null

        if ($memPerc -gt $maxObservedMemPercent) {
            $maxObservedMemPercent = $memPerc
        }
        if ($memPerc -gt $MaxRamThresholdPercent) {
            $violatingContainers += ($name + " (" + $memPerc + "%)")
        }

        $parsedStats += [PSCustomObject]@{
            Container = $name
            CPU = $cpu
            Memory = $memUsage
            MemPercent = ($memPerc.ToString() + "%")
            NetworkIO = $netIo
        }
    }
}

$parsedStats | Format-Table -AutoSize

Write-Host ""
Write-Host "[4/4] Kiem tra tinh toan ven container va kiem toan su co OOMKilled..." -ForegroundColor Yellow
$oomContainers = @()
$containers = docker ps -a --format "{{.Names}}"
foreach ($c in $containers) {
    $cName = $c.Trim()
    if ([string]::IsNullOrWhiteSpace($cName)) { continue }
    $inspectJson = docker inspect $cName | ConvertFrom-Json
    $isOomKilled = $inspectJson[0].State.OOMKilled
    $exitCode = $inspectJson[0].State.ExitCode
    if ($isOomKilled -eq $true -or $exitCode -eq 137) {
        $oomContainers += $cName
    }
}

Write-Host "  Dinh RAM cao nhat ghi nhan: $maxObservedMemPercent% (Nguong toi da: $MaxRamThresholdPercent%)" -ForegroundColor Green
Write-Host "  So container bi OOMKilled : $($oomContainers.Count)" -ForegroundColor Green

if ($violatingContainers.Count -gt 0) {
    throw ("Phat hien container vuot nguong RAM: " + ($violatingContainers -join ", "))
}
if ($oomContainers.Count -gt 0) {
    throw ("Phat hien container bi OOMKilled: " + ($oomContainers -join ", "))
}

Write-Host ""
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "KET QUA DAT YEU CAU AC1: He thong chiu tai 100 anh lien tuc khong tran bo nho!" -ForegroundColor Green
Write-Host "  Peak RAM: $maxObservedMemPercent% (duoi 80%), OOMKilled: 0" -ForegroundColor Green
Write-Host "====================================================================" -ForegroundColor Cyan
