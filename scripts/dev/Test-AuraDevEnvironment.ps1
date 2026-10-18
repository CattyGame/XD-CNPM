[CmdletBinding()]
param(
    [string]$GatewayUrl = "http://127.0.0.1:5000",
    [string]$AnalysisUrl = "http://127.0.0.1:5001",
    [string]$BillingUrl = "http://127.0.0.1:5002",
    [string]$NotificationUrl = "http://127.0.0.1:5003",
    [string]$IdentityUrl = "http://127.0.0.1:5004",
    [string]$ProfileUrl = "http://127.0.0.1:5005",
    [string]$AiUrl = "http://127.0.0.1:8000",
    [switch]$SkipNotification,
    [switch]$CheckDevelopmentBootstrap,
    [switch]$CheckFirebase
)

$ErrorActionPreference = "Stop"
$script:checkCount = 0

function Request-Aura {
    param([string]$Uri, [string]$Method = "GET", [hashtable]$Headers = @{})
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -Method $Method `
            -Headers $Headers -TimeoutSec 20
        return @{ Status = [int]$response.StatusCode; Headers = $response.Headers; Body = $response.Content }
    }
    catch {
        if (-not $_.Exception.Response) { throw "Cannot connect to $Uri." }
        return @{ Status = [int]$_.Exception.Response.StatusCode; Headers = @{}; Body = "" }
    }
}

function Assert-Aura {
    param([bool]$Condition, [string]$Name)
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checkCount++
    Write-Host "PASS: $Name"
}

foreach ($target in @(
    @("Gateway", $GatewayUrl), @("Analysis", $AnalysisUrl), @("Billing", $BillingUrl),
    @("Identity", $IdentityUrl), @("Profile", $ProfileUrl), @("AI Core", $AiUrl)
)) {
    $response = Request-Aura ($target[1].TrimEnd('/') + "/health")
    Assert-Aura ($response.Status -eq 200) ($target[0] + " health")
}

if (-not $SkipNotification) {
    foreach ($path in @("/health", "/health/ready")) {
        $response = Request-Aura ($NotificationUrl.TrimEnd('/') + $path)
        Assert-Aura ($response.Status -eq 200) ("Notification " + $path)
    }
}

$ping = Request-Aura ($GatewayUrl.TrimEnd('/') + "/api/v1/platform/ping")
Assert-Aura ($ping.Status -eq 200) "Gateway routes platform ping"
$pingData = $ping.Body | ConvertFrom-Json
Assert-Aura ($pingData.service -eq "AURA.Analysis.Api") "Ping reaches Analysis"
Assert-Aura (-not [string]::IsNullOrWhiteSpace([string]$ping.Headers["X-Correlation-Id"])) "Correlation ID is returned"

$catalog = Request-Aura ($GatewayUrl.TrimEnd('/') + "/api/v1/billing/packages")
Assert-Aura ($catalog.Status -eq 200) "Gateway routes Billing catalog"
$catalogData = $catalog.Body | ConvertFrom-Json
Assert-Aura ($null -ne $catalogData.data) "Billing catalog returns database/cache data"

foreach ($target in @(
    @("/api/v1/identity/bootstrap", "POST"),
    @("/api/v1/profiles/me", "GET"),
    @("/api/v1/analyses", "POST")
)) {
    $response = Request-Aura ($GatewayUrl.TrimEnd('/') + $target[0]) $target[1]
    Assert-Aura ($response.Status -eq 401) ("Missing user token rejected: " + $target[0])
}

foreach ($origin in @("http://localhost:5173", "http://127.0.0.1:5173")) {
    $cors = Request-Aura ($GatewayUrl.TrimEnd('/') + "/api/v1/analyses") "OPTIONS" @{
        Origin = $origin
        "Access-Control-Request-Method" = "POST"
        "Access-Control-Request-Headers" = "authorization,idempotency-key,content-type"
    }
    Assert-Aura (([string]$cors.Headers["Access-Control-Allow-Origin"]) -eq $origin) ("CORS allows " + $origin)
}

$outside = Request-Aura ($GatewayUrl.TrimEnd('/') + "/api/v1/analyses") "OPTIONS" @{
    Origin = "http://outside.invalid"
    "Access-Control-Request-Method" = "POST"
}
Assert-Aura ([string]::IsNullOrEmpty([string]$outside.Headers["Access-Control-Allow-Origin"])) "CORS excludes unconfigured origin"

$internal = Request-Aura ($GatewayUrl.TrimEnd('/') + "/internal/v1/identity/service-tokens") "POST"
Assert-Aura ($internal.Status -eq 404) "Gateway does not expose internal token endpoint"

if ($CheckDevelopmentBootstrap) {
    # This checks local wiring/database provisioning, not real Firebase authentication.
    # The service must be Development. A synthetic account/profile will be created.
    $uid = "m1-check-" + [Guid]::NewGuid().ToString("N")
    $headers = @{ Authorization = "Bearer mock-dev-token-$uid" }
    $uri = $IdentityUrl.TrimEnd('/') + "/api/v1/identity/bootstrap"
    $first = Request-Aura $uri "POST" $headers
    Assert-Aura ($first.Status -eq 200) "Development Identity bootstrap"
    $firstData = $first.Body | ConvertFrom-Json
    Assert-Aura ($firstData.profileProvisioningStatus -eq "READY") "Identity reaches Profile and PostgreSQL"
    $second = Request-Aura $uri "POST" $headers
    Assert-Aura ($second.Status -eq 200) "Repeated development bootstrap"
    $secondData = $second.Body | ConvertFrom-Json
    Assert-Aura ($firstData.accountId -eq $secondData.accountId) "Bootstrap preserves account ID"
}

if ($CheckFirebase) {
    if ($SkipNotification) { throw "CheckFirebase requires Notification for the user-token probe." }
    $secret = Read-Host "Fresh Firebase ID token (not a service account JSON or service JWT)" -AsSecureString
    try {
        $token = (New-Object System.Net.NetworkCredential("", $secret)).Password
        if ([string]::IsNullOrWhiteSpace($token)) { throw "Firebase ID token must not be empty." }
        $headers = @{ Authorization = "Bearer $token" }
        $notification = Request-Aura ($GatewayUrl.TrimEnd('/') + "/api/v1/notifications/me") "GET" $headers
        Assert-Aura ($notification.Status -eq 200) "Firebase user token through Gateway to Notification"
        $identity = Request-Aura ($GatewayUrl.TrimEnd('/') + "/api/v1/identity/me") "GET" $headers
        # A verified user may be uninitialized (409) or an internal account disabled (403).
        Assert-Aura ($identity.Status -in @(200, 403, 409)) "Firebase user token through Gateway to Identity"
    }
    finally {
        $secret.Dispose()
        $token = $null
        $headers = $null
    }
}

Write-Host "PASS: $script:checkCount M1 environment checks."
Write-Host "These checks do not certify Profile authorization, upload/reservations or Worker/AI end-to-end."
