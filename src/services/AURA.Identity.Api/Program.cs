using System.Net.Http.Headers;
using System.Text.Json;
using AURA.Identity.Api;
using AURA.Identity.Api.Models;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var postgresConnString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=127.0.0.1;Port=5432;Database=aura_dev;Username=aura_identity_app;Password=aura_identity_pass;";

builder.Services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(postgresConnString));
builder.Services.AddSingleton<IdentityRepository>();
builder.Services.AddSingleton<ServiceTokenIssuer>();
builder.Services.AddSingleton<FirebaseAuthProvider>();

builder.Services.AddHttpClient("ProfileClient", (sp, client) =>
{
    var baseUrl = builder.Configuration["Services:Profile:BaseUrl"] ?? "http://127.0.0.1:5005";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Correlation ID & Security Headers Middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
    {
        correlationId = Guid.NewGuid().ToString("D");
    }
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    context.Items["CorrelationId"] = correlationId;

    await next();
});

app.MapHealthChecks("/health");

// Helper to extract Bearer token
static string? ExtractBearerToken(HttpRequest request)
{
    var authHeader = request.Headers.Authorization.FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return authHeader["Bearer ".Length..].Trim();
    }
    return null;
}

// Helper to verify Firebase Token
static async Task<(bool Success, string? Uid, string? Email, string? Name, string? ErrorCode)> VerifyFirebaseTokenAsync(
    string token, FirebaseAuth? auth, IWebHostEnvironment env)
{
    // Support simulated development tokens for testing
    if (env.IsDevelopment() && token.StartsWith("mock-dev-token-", StringComparison.OrdinalIgnoreCase))
    {
        var mockUid = token["mock-dev-token-".Length..];
        return (true, mockUid, $"{mockUid}@example.com", "Mock User", null);
    }
    if (env.IsDevelopment() && token.StartsWith("mock-token-", StringComparison.OrdinalIgnoreCase))
    {
        var mockUid = token["mock-token-".Length..];
        return (true, mockUid, $"{mockUid}@example.com", "Mock User", null);
    }

    if (auth == null)
    {
        return (false, null, null, null, "ERR_UNAUTHORIZED");
    }

    try
    {
        var decoded = await auth.VerifyIdTokenAsync(token, checkRevoked: true);
        var email = decoded.Claims.TryGetValue("email", out var e) ? e?.ToString() : null;
        var name = decoded.Claims.TryGetValue("name", out var n) ? n?.ToString() : null;
        return (true, decoded.Uid, email, name, null);
    }
    catch (FirebaseAuthException)
    {
        return (false, null, null, null, "ERR_UNAUTHORIZED");
    }
    catch
    {
        return (false, null, null, null, "ERR_UNAUTHORIZED");
    }
}

// 1. POST /api/v1/identity/bootstrap
app.MapPost("/api/v1/identity/bootstrap", async (
    HttpRequest request,
    IdentityRepository repository,
    FirebaseAuthProvider authProvider,
    IHttpClientFactory httpClientFactory,
    IWebHostEnvironment env) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var token = ExtractBearerToken(request);
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_UNAUTHORIZED", "Thiếu Firebase ID token.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var (success, uid, email, name, errorCode) = await VerifyFirebaseTokenAsync(token, authProvider.Auth, env);
    if (!success || string.IsNullOrWhiteSpace(uid))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(errorCode ?? "ERR_UNAUTHORIZED", "Firebase token không hợp lệ hoặc đã bị thu hồi.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    // Idempotent account lookup or creation
    var account = await repository.GetAccountByFirebaseUidAsync(uid);
    if (account == null)
    {
        account = await repository.CreateAccountAsync(uid);
    }

    if (account.AccountStatus != "ACTIVE")
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_ACCOUNT_DISABLED", "Tài khoản đã bị khóa.", correlationId, false)),
            statusCode: StatusCodes.Status403Forbidden);
    }

    // Call Profile service to provision profile/personal tenant
    if (account.ProfileProvisioningStatus != "READY")
    {
        try
        {
            var profileClient = httpClientFactory.CreateClient("ProfileClient");
            var provisionPayload = new ProvisionProfileRequest(
                account.AccountId,
                account.FirebaseUid,
                !string.IsNullOrWhiteSpace(name) ? name : "Người dùng AURA",
                email);

            var profileResponse = await profileClient.PostAsJsonAsync("/internal/v1/profile/provision", provisionPayload);
            if (profileResponse.IsSuccessStatusCode)
            {
                await repository.UpdateProvisioningStatusAsync(account.AccountId, "READY");
                account = account with { ProfileProvisioningStatus = "READY" };
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Profile provisioning call failed for account {AccountId}, will keep PENDING for retry.", account.AccountId);
        }
    }

    return Results.Ok(account);
})
.WithName("Bootstrap")
.WithTags("Identity");

// 2. GET /api/v1/identity/me
app.MapGet("/api/v1/identity/me", async (
    HttpRequest request,
    IdentityRepository repository,
    FirebaseAuthProvider authProvider,
    IWebHostEnvironment env) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var token = ExtractBearerToken(request);
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_UNAUTHORIZED", "Thiếu Firebase ID token.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var (success, uid, _, _, errorCode) = await VerifyFirebaseTokenAsync(token, authProvider.Auth, env);
    if (!success || string.IsNullOrWhiteSpace(uid))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(errorCode ?? "ERR_UNAUTHORIZED", "Firebase token không hợp lệ hoặc đã bị thu hồi.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var account = await repository.GetAccountByFirebaseUidAsync(uid);
    if (account == null)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_ACCOUNT_NOT_INITIALIZED", "Tài khoản chưa được khởi tạo. Hãy gọi bootstrap trước.", correlationId, true)),
            statusCode: StatusCodes.Status409Conflict);
    }

    if (account.AccountStatus != "ACTIVE")
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_ACCOUNT_DISABLED", "Tài khoản đã bị khóa.", correlationId, false)),
            statusCode: StatusCodes.Status403Forbidden);
    }

    return Results.Ok(account);
})
.WithName("GetIdentityMe")
.WithTags("Identity");

// 3. POST /internal/v1/identity/service-tokens
app.MapPost("/internal/v1/identity/service-tokens", (
    ServiceTokenRequest request,
    ServiceTokenIssuer issuer) =>
{
    if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_VALIDATION", "Thiếu clientId hoặc clientSecret.", Guid.NewGuid().ToString("D"), false)),
            statusCode: StatusCodes.Status400BadRequest);
    }

    // In local dev/Sprint 2, issue token with requested scope
    var scopes = (request.Scope ?? "aura:internal").Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var token = issuer.IssueToken(request.ClientId, "aura-internal-services", scopes);

    return Results.Ok(new ServiceTokenResponse(token, "Bearer", 300));
})
.WithName("IssueServiceToken")
.WithTags("Internal");

// 4. POST /internal/v1/identity/context
app.MapPost("/internal/v1/identity/context", async (
    HttpRequest request,
    IdentityRepository repository,
    FirebaseAuthProvider authProvider,
    IWebHostEnvironment env) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var userAuth = request.Headers["X-User-Authorization"].FirstOrDefault()
                   ?? request.Headers.Authorization.FirstOrDefault();

    if (string.IsNullOrWhiteSpace(userAuth))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_UNAUTHORIZED", "Thiếu User token trong X-User-Authorization.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var token = userAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? userAuth["Bearer ".Length..].Trim()
        : userAuth.Trim();

    var (success, uid, _, _, _) = await VerifyFirebaseTokenAsync(token, authProvider.Auth, env);
    if (!success || string.IsNullOrWhiteSpace(uid))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_UNAUTHORIZED", "Token người dùng không hợp lệ.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var account = await repository.GetAccountByFirebaseUidAsync(uid);
    if (account == null)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_NOT_FOUND", "Không tìm thấy tài khoản tương ứng.", correlationId, false)),
            statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Ok(new IdentityContextResponse(account.AccountId, account.AccountStatus, account.Roles));
})
.WithName("ResolveIdentityContext")
.WithTags("Internal");

app.Run();

public class FirebaseAuthProvider
{
    public FirebaseAuth? Auth { get; }

    public FirebaseAuthProvider(IConfiguration config)
    {
        var projectId = config["Firebase:ProjectId"] ?? "aura-dev-project";
        try
        {
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                ProjectId = projectId,
                Credential = GoogleCredential.GetApplicationDefault()
            }, "aura-identity");
            Auth = FirebaseAuth.GetAuth(app);
        }
        catch
        {
            Auth = null;
        }
    }
}
