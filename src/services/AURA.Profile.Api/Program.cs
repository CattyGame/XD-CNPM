using System.Security.Cryptography;
using System.Text;
using AURA.Profile.Api;
using AURA.Profile.Api.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var postgresConnString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=127.0.0.1;Port=5432;Database=aura_dev;Username=aura_profile_app;Password=aura_profile_pass;";

builder.Services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(postgresConnString));
builder.Services.AddSingleton<ProfileRepository>();

builder.Services.AddHttpClient("IdentityClient", (sp, client) =>
{
    var baseUrl = builder.Configuration["Services:Identity:BaseUrl"] ?? "http://127.0.0.1:5004";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<ServiceTokenValidator>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Correlation ID Middleware
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

// Helper: Phân giải Account ID từ request (Tuyệt đối không tin cậy X-Account-Id hay chuỗi GUID không xác thực)
static Guid? ResolveAccountId(HttpRequest request, IWebHostEnvironment env)
{
    var authHeader = request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var token = authHeader["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
    {
        return null;
    }

    // Chỉ cho phép mock tokens khi đang ở môi trường Development
    if (env.IsDevelopment() && (token.StartsWith("mock-token-", StringComparison.OrdinalIgnoreCase) ||
                                token.StartsWith("mock-dev-token-", StringComparison.OrdinalIgnoreCase)))
    {
        var raw = token.StartsWith("mock-token-", StringComparison.OrdinalIgnoreCase)
            ? token["mock-token-".Length..]
            : token["mock-dev-token-".Length..];

        if (raw.Equals("hoang_dev_1", StringComparison.OrdinalIgnoreCase))
        {
            return Guid.Parse("668c4a32-5342-4d8b-b505-07d548732120");
        }

        if (raw.Equals("uninitialized_user_999", StringComparison.OrdinalIgnoreCase))
        {
            return null; // Chưa bootstrap
        }

        // Sinh deterministic UUID từ chuỗi UID đã đăng nhập
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("aura-profile-dev:" + raw));
        return new Guid(hash);
    }

    return null;
}

// 1. POST /internal/v1/profile/provision (Bảo vệ bằng Service JWT: scope profile:provision)
app.MapPost("/internal/v1/profile/provision", async (
    HttpRequest request,
    ProvisionProfileRequest body,
    ProfileRepository repository,
    ServiceTokenValidator tokenValidator,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;

    // 1.1. Xác thực Service JWT
    var authHeader = request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_UNAUTHORIZED", "Thiếu service token xác thực.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var token = authHeader["Bearer ".Length..].Trim();
    var (valid, _, errCode, errMsg) = tokenValidator.ValidateToken(token, "aura-profile", "profile:provision");
    if (!valid)
    {
        var statusCode = errCode == "ERR_FORBIDDEN" ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(errCode ?? "ERR_UNAUTHORIZED", errMsg ?? "Service token không hợp lệ.", correlationId, false)),
            statusCode: statusCode);
    }

    try
    {
        if (body.AccountId == Guid.Empty)
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_VALIDATION", "Thiếu accountId hợp lệ.", correlationId, false)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var displayName = !string.IsNullOrWhiteSpace(body.DisplayName) ? body.DisplayName : "Người dùng AURA";
        var profile = await repository.ProvisionProfileAsync(body.AccountId, displayName, ct);

        return Results.Ok(profile);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi provision profile cho account {AccountId}", body.AccountId);
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_INTERNAL_DB", "Lỗi xử lý cơ sở dữ liệu khi khởi tạo hồ sơ.", correlationId, false)),
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("ProvisionProfile")
.WithTags("Internal");

// 2. GET /api/v1/profiles/me (Xem thông tin hồ sơ của chính mình)
app.MapGet("/api/v1/profiles/me", async (
    HttpRequest request,
    HttpResponse response,
    ProfileRepository repository,
    IWebHostEnvironment env,
    CancellationToken ct) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var accountId = ResolveAccountId(request, env);

    if (!accountId.HasValue)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_UNAUTHORIZED", "Cần đăng nhập để xem thông tin hồ sơ.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    try
    {
        var profile = await repository.GetProfileByAccountIdAsync(accountId.Value, ct);
        if (profile == null)
        {
            // Theo chuẩn Sprint 2: Hồ sơ chưa provision xong trả 409 ERR_PROFILE_NOT_READY, retryable=true
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_PROFILE_NOT_READY", "Hồ sơ chưa được khởi tạo. Hãy gọi bootstrap trước.", correlationId, true)),
                statusCode: StatusCodes.Status409Conflict);
        }

        // Thiết lập ETag theo phiên bản hồ sơ: "<profileVersion>"
        response.Headers.ETag = $"\"{profile.ProfileVersion}\"";

        return Results.Ok(profile);
    }
    catch (Exception)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_INTERNAL_DB", "Lỗi xử lý cơ sở dữ liệu khi truy vấn hồ sơ.", correlationId, false)),
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("GetMyProfile")
.WithTags("Profiles");

// 3. PATCH /api/v1/profiles/me (Cập nhật displayName, phone - Bắt buộc If-Match, Chống sửa Role)
app.MapPatch("/api/v1/profiles/me", async (
    HttpRequest request,
    HttpResponse response,
    UpdateProfileRequest body,
    ProfileRepository repository,
    IWebHostEnvironment env,
    CancellationToken ct) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var accountId = ResolveAccountId(request, env);

    if (!accountId.HasValue)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_UNAUTHORIZED", "Cần đăng nhập để cập nhật hồ sơ.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    // 3.1. Kiểm tra Header If-Match bắt buộc
    var ifMatchHeader = request.Headers.IfMatch.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(ifMatchHeader))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_VALIDATION", "Thiếu header If-Match bắt buộc để cập nhật hồ sơ.", correlationId, false)),
            statusCode: StatusCodes.Status400BadRequest);
    }

    // Tách chuỗi ETag (loại bỏ dấu ngoặc kép nếu có)
    var rawVersion = ifMatchHeader.Trim('\"', ' ');
    if (!int.TryParse(rawVersion, out var expectedVersion) || expectedVersion <= 0)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_VALIDATION", "Header If-Match không hợp lệ.", correlationId, false)),
            statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
        // 3.2. Validate dữ liệu đầu vào:
        var currentProfile = await repository.GetProfileByAccountIdAsync(accountId.Value, ct);
        if (currentProfile == null)
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_NOT_FOUND", "Không tìm thấy hồ sơ để cập nhật.", correlationId, false)),
                statusCode: StatusCodes.Status404NotFound);
        }

        var targetDisplayName = body.DisplayName != null ? body.DisplayName.Trim() : currentProfile.DisplayName;
        if (string.IsNullOrWhiteSpace(targetDisplayName) || targetDisplayName.Length > 100)
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_VALIDATION", "Tên hiển thị (displayName) phải từ 1 đến 100 ký tự.", correlationId, false)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var targetPhone = body.Phone != null ? body.Phone.Trim() : currentProfile.Phone;
        if (targetPhone != null && targetPhone.Length > 30)
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_VALIDATION", "Số điện thoại (phone) tối đa 30 ký tự.", correlationId, false)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        // 3.3. Thực thi cập nhật kèm kiểm tra phiên bản (Optimistic Concurrency)
        var (success, updatedProfile, conflict) = await repository.UpdateProfileAsync(
            accountId.Value, targetDisplayName, targetPhone, expectedVersion, ct);

        if (conflict)
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_PROFILE_VERSION_CONFLICT", "Phiên bản hồ sơ không khớp (xung đột dữ liệu). Hãy tải lại hồ sơ mới nhất.", correlationId, false)),
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!success || updatedProfile == null)
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_NOT_FOUND", "Không tìm thấy hồ sơ người dùng.", correlationId, false)),
                statusCode: StatusCodes.Status404NotFound);
        }

        // Trả về ETag phiên bản mới
        response.Headers.ETag = $"\"{updatedProfile.ProfileVersion}\"";

        return Results.Ok(updatedProfile);
    }
    catch (Exception)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_INTERNAL_DB", "Lỗi xử lý cơ sở dữ liệu khi cập nhật hồ sơ.", correlationId, false)),
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("UpdateMyProfile")
.WithTags("Profiles");

// 4. POST /internal/v1/profile/access/resolve (Bảo vệ bằng Service JWT: scope profile:resolve)
app.MapPost("/internal/v1/profile/access/resolve", async (
    HttpRequest request,
    AccessResolveRequest body,
    ProfileRepository repository,
    ServiceTokenValidator tokenValidator,
    CancellationToken ct) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;

    // 4.1. Xác thực Service JWT
    var authHeader = request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_UNAUTHORIZED", "Thiếu service token xác thực.", correlationId, false)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var token = authHeader["Bearer ".Length..].Trim();
    var (valid, _, errCode, errMsg) = tokenValidator.ValidateToken(token, "aura-profile", "profile:resolve");
    if (!valid)
    {
        var statusCode = errCode == "ERR_FORBIDDEN" ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(errCode ?? "ERR_UNAUTHORIZED", errMsg ?? "Service token không hợp lệ.", correlationId, false)),
            statusCode: statusCode);
    }

    var response = await repository.ResolveAccessAsync(
        body.ActorAccountId, body.PatientId, body.ClinicContextId, body.Action, ct);

    return Results.Ok(response);
})
.WithName("ResolveAccess")
.WithTags("Internal");

app.Run();
