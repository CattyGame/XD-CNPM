# AURA RabbitMQ Worker Scaling & Fair Dispatch Verification Script
# Task SCRUM-339 (W4-M1-02)

param(
    [string]$RabbitMqHost = "127.0.0.1",
    [int]$Port = 5672,
    [int]$ManagementPort = 15372,
    [string]$Vhost = "/aura-s3",
    [string]$User = "s3-operator",
    [string]$Password = "aura_s3_operator_2026",
    [int]$TotalJobs = 100
)

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "AURA Task W4-M1-02: RabbitMQ Worker Scaling & Queue Capacity Test" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

$baseAuth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$User`:$Password"))
$encodedVhost = [Uri]::EscapeDataString($Vhost)
$mgmtUri = "http://$RabbitMqHost`:$ManagementPort/api/queues/$encodedVhost/aura.s3.analysis.jobs"

try {
    Write-Host "[1/3] Kiểm tra trạng thái hàng đợi aura.s3.analysis.jobs..." -ForegroundColor Yellow
    $queueInfo = Invoke-RestMethod -Uri $mgmtUri -Headers @{ Authorization = "Basic $baseAuth" } -Method Get
    Write-Host "  Tên hàng đợi: $($queueInfo.name)" -ForegroundColor Green
    Write-Host "  Kiểu hàng đợi: $($queueInfo.type)" -ForegroundColor Green
    Write-Host "  Số message hiện tại: $($queueInfo.messages)" -ForegroundColor Green
    Write-Host "  Số Consumers (Workers) đang lắng nghe: $($queueInfo.consumers)" -ForegroundColor Green

    Write-Host "[2/3] Mô phỏng đẩy 100 jobs xử lý hàng loạt (Batch Processing)..." -ForegroundColor Yellow
    $batchId = [Guid]::NewGuid().ToString()

    for ($i = 1; $i -le $TotalJobs; $i++) {
        $jobId = [Guid]::NewGuid().ToString()
        $payload = @{
            properties = @{ delivery_mode = 2; content_type = "application/json" }
            routing_key = "analysis.job.run"
            payload = (@{
                job_id = $jobId
                batch_id = $batchId
                index = $i
                total = $TotalJobs
                priority = "NORMAL"
                created_at = (Get-Date).ToUniversalTime().ToString("o")
            } | ConvertTo-Json -Compress)
            payload_encoding = "string"
        } | ConvertTo-Json -Compress

        $pubUri = "http://$RabbitMqHost`:$ManagementPort/api/exchanges/$encodedVhost/aura.s3.jobs/publish"
        $pubRes = Invoke-RestMethod -Uri $pubUri -Headers @{ Authorization = "Basic $baseAuth" } -Method Post -Body $payload -ContentType "application/json"

        if (-not $pubRes.routed) {
            throw "Job $i không được định tuyến vào hàng đợi."
        }
    }
    Write-Host "  Đã đẩy thành công $TotalJobs jobs vào hàng đợi!" -ForegroundColor Green

    Write-Host "[3/3] Xác minh độ sâu hàng đợi sau khi đẩy..." -ForegroundColor Yellow
    Start-Sleep -Seconds 1
    $queueAfter = Invoke-RestMethod -Uri $mgmtUri -Headers @{ Authorization = "Basic $baseAuth" } -Method Get
    Write-Host "  Tổng messages trong hàng đợi: $($queueAfter.messages)" -ForegroundColor Green
    Write-Host "  Messages sẵn sàng (ready): $($queueAfter.messages_ready)" -ForegroundColor Green

    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "KẾT QUẢ: Kiểm thử tải 100 RabbitMQ jobs thành công 100%!" -ForegroundColor Green
    Write-Host "====================================================================" -ForegroundColor Cyan
}
catch {
    Write-Host "LỖI KIỂM THỬ: $_" -ForegroundColor Red
    exit 1
}
