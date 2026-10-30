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
builder.Services.AddSingleton<UserIdentityResolver>();

builder.Services.AddHttpClient("IdentityClient", (sp, client) =>
{
    var baseUrl = builder.Configuration["Services:Identity:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
            || File.Exists("/.dockerenv")
            ? "http://identity-api:8080"
            : "http://127.0.0.1:5004";
    }
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
.RedactLoggedHeaders(new[] { "Authorization", "X-User-Authorization" });

builder.Services.AddSingleton<ServiceTokenValidator>();

var app = builder.Build();

// Validate provisioned service-auth material before health or business routes start.
_ = app.Services.GetRequiredService<ServiceTokenValidator>();

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
    var (valid, _, errCode, errMsg) = await tokenValidator.ValidateTokenAsync(token, "aura-profile", "profile:provision");
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
    UserIdentityResolver identityResolver,
    CancellationToken ct) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var identity = await identityResolver.ResolveAsync(request, ct);
    if (!identity.Succeeded)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(identity.Code, identity.Message, correlationId, identity.Retryable)),
            statusCode: identity.StatusCode);
    }
    var accountId = identity.AccountId;

    try
    {
        var profile = await repository.GetProfileByAccountIdAsync(accountId, ct);
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
    UserIdentityResolver identityResolver,
    CancellationToken ct) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var identity = await identityResolver.ResolveAsync(request, ct);
    if (!identity.Succeeded)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(identity.Code, identity.Message, correlationId, identity.Retryable)),
            statusCode: identity.StatusCode);
    }
    var accountId = identity.AccountId;

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
        var currentProfile = await repository.GetProfileByAccountIdAsync(accountId, ct);
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
            accountId, targetDisplayName, targetPhone, expectedVersion, ct);

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
    var (valid, _, errCode, errMsg) = await tokenValidator.ValidateTokenAsync(token, "aura-profile", "profile:resolve");
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


// 5. GET /api/v1/clinics/{clinicId:guid}/memberships
app.MapGet("/api/v1/clinics/{clinicId:guid}/memberships", async (
    Guid clinicId,
    HttpRequest request,
    ProfileRepository repository,
    UserIdentityResolver identityResolver,
    CancellationToken ct,
    string? status = null) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var identity = await identityResolver.ResolveAsync(request, ct);
    if (!identity.Succeeded)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(identity.Code, identity.Message, correlationId, identity.Retryable)),
            statusCode: identity.StatusCode);
    }

    var (allowed, failureReason) = await repository.CheckClinicManagementAuthorityAsync(
        identity.AccountId, clinicId, identity.Roles, ct);

    if (!allowed)
    {
        if (failureReason == "CLINIC_NOT_FOUND" || failureReason == "NOT_A_MEMBER")
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_CLINIC_NOT_FOUND", "Kh�ng t�m th?y ph�ng kh�m.", correlationId, false)),
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_FORBIDDEN", "B?n kh�ng c� quy?n qu?n l� th�nh vi�n ph�ng kh�m n�y.", correlationId, false)),
            statusCode: StatusCodes.Status403Forbidden);
    }

    var items = await repository.GetClinicMembersAsync(clinicId, status, ct);
    return Results.Ok(new ClinicMemberListResponse(clinicId, items));
})
.WithName("GetClinicMemberships")
.WithTags("Clinics");

// 6. POST /api/v1/clinics/{clinicId:guid}/memberships
app.MapPost("/api/v1/clinics/{clinicId:guid}/memberships", async (
    Guid clinicId,
    AddClinicMemberRequest body,
    HttpRequest request,
    ProfileRepository repository,
    UserIdentityResolver identityResolver,
    CancellationToken ct) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var identity = await identityResolver.ResolveAsync(request, ct);
    if (!identity.Succeeded)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(identity.Code, identity.Message, correlationId, identity.Retryable)),
            statusCode: identity.StatusCode);
    }

    var (allowed, failureReason) = await repository.CheckClinicManagementAuthorityAsync(
        identity.AccountId, clinicId, identity.Roles, ct);

    if (!allowed)
    {
        if (failureReason == "CLINIC_NOT_FOUND" || failureReason == "NOT_A_MEMBER")
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_CLINIC_NOT_FOUND", "Kh�ng t�m th?y ph�ng kh�m.", correlationId, false)),
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_FORBIDDEN", "B?n kh�ng c� quy?n qu?n l� th�nh vi�n ph�ng kh�m n�y.", correlationId, false)),
            statusCode: StatusCodes.Status403Forbidden);
    }

    if (body.AccountId == Guid.Empty)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_VALIDATION", "Thi?u accountId h?p l?.", correlationId, false)),
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(body.Role) || !ClinicMemberRoles.AllowedRoles.Contains(body.Role.Trim()))
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_INVALID_ROLE", "Vai tr� th�nh vi�n kh�ng h?p l?. Ch? ch?p nh?n CLINIC_ADMIN, DOCTOR, STAFF.", correlationId, false)),
            statusCode: StatusCodes.Status400BadRequest);
    }

    var targetRole = body.Role.Trim().ToUpperInvariant();
    var (success, member, errCode, errMsg) = await repository.AddOrReactivateClinicMemberAsync(
        clinicId, body.AccountId, targetRole, ct);

    if (!success)
    {
        var statusCode = errCode switch
        {
            "ERR_MEMBERSHIP_ALREADY_EXISTS" => StatusCodes.Status409Conflict,
            "ERR_ACCOUNT_NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(errCode ?? "ERR_INTERNAL_DB", errMsg ?? "Kh�ng th? th�m th�nh vi�n.", correlationId, false)),
            statusCode: statusCode);
    }

    return Results.Json(member, statusCode: StatusCodes.Status201Created);
})
.WithName("AddClinicMembership")
.WithTags("Clinics");

// 7. DELETE /api/v1/clinics/{clinicId:guid}/memberships/{accountId:guid}
app.MapDelete("/api/v1/clinics/{clinicId:guid}/memberships/{accountId:guid}", async (
    Guid clinicId,
    Guid accountId,
    HttpRequest request,
    ProfileRepository repository,
    UserIdentityResolver identityResolver,
    CancellationToken ct) =>
{
    var correlationId = (string)request.HttpContext.Items["CorrelationId"]!;
    var identity = await identityResolver.ResolveAsync(request, ct);
    if (!identity.Succeeded)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(identity.Code, identity.Message, correlationId, identity.Retryable)),
            statusCode: identity.StatusCode);
    }

    var (allowed, failureReason) = await repository.CheckClinicManagementAuthorityAsync(
        identity.AccountId, clinicId, identity.Roles, ct);

    if (!allowed)
    {
        if (failureReason == "CLINIC_NOT_FOUND" || failureReason == "NOT_A_MEMBER")
        {
            return Results.Json(
                new ErrorEnvelope(new ErrorDetails("ERR_CLINIC_NOT_FOUND", "Kh�ng t�m th?y ph�ng kh�m.", correlationId, false)),
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Json(
            new ErrorEnvelope(new ErrorDetails("ERR_FORBIDDEN", "B?n kh�ng c� quy?n qu?n l� th�nh vi�n ph�ng kh�m n�y.", correlationId, false)),
            statusCode: StatusCodes.Status403Forbidden);
    }

    var (success, errCode, errMsg) = await repository.RevokeClinicMemberAsync(clinicId, accountId, ct);
    if (!success)
    {
        var statusCode = errCode switch
        {
            "ERR_CANNOT_REVOKE_SOLE_ADMIN" => StatusCodes.Status400BadRequest,
            "ERR_MEMBERSHIP_NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(errCode ?? "ERR_INTERNAL_DB", errMsg ?? "Kh�ng th? thu h?i th�nh vi�n.", correlationId, false)),
            statusCode: statusCode);
    }

    return Results.NoContent();
})
.WithName("RevokeClinicMembership")
.WithTags("Clinics");

app.Run();
