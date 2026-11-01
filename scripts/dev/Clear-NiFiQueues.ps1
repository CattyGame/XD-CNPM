[CmdletBinding()]
param(
    [string]$NifiUrl = "https://localhost:8543/nifi-api",
    [string]$Username = "aura-s3-admin",
    [string]$Password = "C0JbotAsPE-jQySsqgHY-xK1hefZXa5SM1bq0auqAeKpEwz-"
)

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "Logging into Apache NiFi API ($NifiUrl)..." -ForegroundColor Cyan
$token = Invoke-RestMethod -Uri "$NifiUrl/access/token" -Method Post -Body "username=$Username&password=$Password" -ContentType "application/x-www-form-urlencoded"
$headers = @{ "Authorization" = "Bearer $token" }

function Clear-GroupQueues($groupId, $groupName) {
    Write-Host "Checking queues in Process Group: $groupName ($groupId)..." -ForegroundColor Yellow
    $pgFlow = Invoke-RestMethod -Uri "$NifiUrl/flow/process-groups/$groupId" -Headers $headers
    
    foreach ($conn in $pgFlow.processGroupFlow.flow.connections) {
        $count = $conn.status.aggregateSnapshot.flowFilesQueued
        if ($count -gt 0) {
            $connId = $conn.id
            $src = $conn.component.source.name
            $dst = $conn.component.destination.name
            Write-Host "  -> Dropping $count FlowFiles from queue: [$src -> $dst] ($connId)" -ForegroundColor Magenta
            try {
                $drop = Invoke-RestMethod -Uri "$NifiUrl/flowfile-queues/$connId/drop-requests" -Method Post -Headers $headers
            } catch {
                Write-Warning "Failed to drop queue $connId : $_"
            }
        }
    }

    # Recurse sub-process groups
    foreach ($sub in $pgFlow.processGroupFlow.flow.processGroups) {
        Clear-GroupQueues $sub.id $sub.component.name
    }
}

$root = Invoke-RestMethod -Uri "$NifiUrl/flow/process-groups/root" -Headers $headers
Clear-GroupQueues "root" "NiFi Flow Root"

Write-Host "All NiFi queues cleared successfully!" -ForegroundColor Green
