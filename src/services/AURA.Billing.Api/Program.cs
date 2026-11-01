using AURA.Billing.Api;
using System.Text.RegularExpressions;
using AURA.Billing.Api.Auth;
using AURA.Billing.Api.Data;
using AURA.Billing.Api.Models;
using AURA.Billing.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Billing currently runs only in Development in this repository.
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Billing is restricted to Development until the production security configuration is approved.");
}

var billingEnvironment = (builder.Configuration["Billing:Environment"] ?? "TEST")
    .Trim().ToUpperInvariant();
if (billingEnvironment is not ("LIVE" or "TEST"))
{
    throw new InvalidOperationException("Billing:Environment must be LIVE or TEST.");
}

var isMock = builder.Configuration.GetValue<bool>("Billing:IsMock");
if (isMock && billingEnvironment != "TEST")
{
    throw new InvalidOperationException("Mock Billing mode must use the TEST wallet environment.");
}

builder.Services.AddSingleton(new BillingRuntimeOptions(billingEnvironment, isMock));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing PostgreSQL connection string.");
    return NpgsqlDataSource.Create(connectionString);
});

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing Redis connection string.");

    var options = ConfigurationOptions.Parse(connectionString);
    options.AbortOnConnectFail = false;
    options.ConnectTimeout = 1000;
    options.ConnectRetry = 1;
    options.SyncTimeout = 1000;
    options.AsyncTimeout = 1000;
    return ConnectionMultiplexer.Connect(options);
});

var identityBaseUrl = builder.Configuration["Services:Identity:BaseUrl"]
    ?? "http://127.0.0.1:5004";
builder.Services.AddHttpClient("IdentityApi", client =>
{
    client.BaseAddress = new Uri(identityBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var analysisBaseUrl = builder.Configuration["Services:Analysis:BaseUrl"]
    ?? "http://127.0.0.1:5001";
builder.Services.AddHttpClient("AnalysisApi", client =>
{
    client.BaseAddress = new Uri(analysisBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
});
var profileBaseUrl = builder.Configuration["Services:Profile:BaseUrl"]
    ?? "http://127.0.0.1:5005";
builder.Services.AddHttpClient("ProfileApi", client =>
{
    client.BaseAddress = new Uri(profileBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<PackageRepository>();
builder.Services.AddSingleton<PackageCatalogService>();
builder.Services.AddSingleton<CreditReservationRepository>();
builder.Services.AddSingleton<ServiceTokenValidator>();
builder.Services.AddSingleton<SystemAdminAuthorizer>();
builder.Services.AddSingleton<ProfileAccessClient>();
builder.Services.AddSingleton<AnalysisSettlementResolverClient>();
builder.Services.AddSingleton<SettlementInboxRepository>();
builder.Services.Configure<BillingSettlementOptions>(
    builder.Configuration.GetSection(BillingSettlementOptions.Section));
builder.Services.AddHostedService<AnalysisSettlementWorker>();

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("AURA.Billing.Api.UnhandledException");
    logger.LogError(exception, "Unhandled Billing API error.");

    var correlationId = context.Items["CorrelationId"] as string ?? Guid.NewGuid().ToString("D");
    var databaseUnavailable = exception is NpgsqlException or TimeoutException;
    context.Response.StatusCode = databaseUnavailable
        ? StatusCodes.Status503ServiceUnavailable
        : StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new BillingErrorEnvelope(
        new BillingErrorDetails(
            databaseUnavailable ? "ERR_BILLING_DEPENDENCY_UNAVAILABLE" : "ERR_INTERNAL",
            databaseUnavailable ? "Billing storage is temporarily unavailable." : "Đã xảy ra lỗi nội bộ.",
            correlationId,
            databaseUnavailable)));
}));

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)
        || correlationId.Length > 64
        || !Regex.IsMatch(correlationId, "^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant))
    {
        correlationId = Guid.NewGuid().ToString("D");
    }

    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

static IResult BillingError(
    HttpContext context,
    int statusCode,
    string code,
    string message,
    bool retryable = false) =>
    Results.Json(new BillingErrorEnvelope(new BillingErrorDetails(
        code,
        message,
        context.Items["CorrelationId"] as string ?? Guid.NewGuid().ToString("D"),
        retryable)), statusCode: statusCode);

static bool TryReadIdempotencyKey(HttpContext context, out Guid key)
{
    key = Guid.Empty;
    var value = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
    return Guid.TryParse(value, out key) && key != Guid.Empty;
}

static IResult ReservationConflict(HttpContext context, ReservationOperationResult result) =>
    result.Status == ReservationOperationStatus.IdempotencyConflict
        ? BillingError(context, StatusCodes.Status409Conflict, "ERR_IDEMPOTENCY_CONFLICT",
            "Idempotency-Key đã được dùng cho một yêu cầu khác.")
        : BillingError(context, StatusCodes.Status409Conflict, "ERR_RESERVATION_STATE_CONFLICT",
            "Trạng thái reservation không cho phép thao tác này.");

await app.Services.GetRequiredService<PackageRepository>()
    .VerifyDatabaseAsync(CancellationToken.None);
await app.Services.GetRequiredService<CreditReservationRepository>()
    .VerifyDatabaseAsync(CancellationToken.None);

app.MapHealthChecks("/health");

// Public package catalog remains anonymous and continues to read the legacy PoC catalog.
app.MapGet("/api/v1/billing/packages", async (
    PackageCatalogService service,
    CancellationToken cancellationToken) =>
{
    var response = await service.GetAllAsync(cancellationToken);
    return Results.Ok(response);
})
.WithName("GetPackageCatalog")
.WithTags("Package Catalog")
.Produces<PackageCatalogResponse>();

// Legacy catalog update is retained for compatibility, but now requires SYSTEM_ADMIN.
app.MapPut("/api/v1/billing/packages/{id:int}", async Task<IResult> (
    int id,
    UpdatePackageRequest request,
    HttpContext context,
    PackageCatalogService service,
    SystemAdminAuthorizer adminAuthorizer,
    CancellationToken cancellationToken) =>
{
    var authorization = await adminAuthorizer.AuthorizeAsync(context.Request, cancellationToken);
    if (!authorization.Allowed)
    {
        return BillingError(context, authorization.StatusCode, authorization.Code,
            authorization.Message, authorization.Retryable);
    }

    if (string.IsNullOrWhiteSpace(request.Name)
        || request.Name.Length > 100
        || request.Price < 0
        || request.Credits <= 0)
    {
        return BillingError(context, StatusCodes.Status400BadRequest, "ERR_VALIDATION",
            "Name phải có từ 1 đến 100 ký tự; price không âm và credits phải lớn hơn 0.");
    }

    var response = await service.UpdateAsync(id, request, cancellationToken);
    return response is null
        ? BillingError(context, StatusCodes.Status404NotFound, "ERR_NOT_FOUND", "Không tìm thấy package.")
        : Results.Ok(response);
})
.WithName("UpdatePackage")
.WithTags("Package Catalog")
.Produces<UpdatePackageResponse>()
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/internal/v1/billing/reservations", async Task<IResult> (
    ReserveCreditRequest request,
    HttpContext context,
    ServiceTokenValidator tokenValidator,
    ProfileAccessClient profileAccessClient,
    CreditReservationRepository repository,
    BillingRuntimeOptions runtimeOptions,
    CancellationToken cancellationToken) =>
{
    var tokenResult = await tokenValidator.ValidateAsync(
        context.Request, "billing:reserve", "analysis-api", cancellationToken);
    if (!tokenResult.Succeeded)
    {
        return BillingError(context, tokenResult.StatusCode, tokenResult.Code,
            tokenResult.Message, tokenResult.Retryable);
    }

    if (!TryReadIdempotencyKey(context, out var idempotencyKey))
    {
        return BillingError(context, StatusCodes.Status400BadRequest, "ERR_VALIDATION",
            "Header Idempotency-Key hợp lệ dạng UUID là bắt buộc.");
    }

    if (request.AnalysisId == Guid.Empty
        || request.TenantId == Guid.Empty
        || request.ActorAccountId == Guid.Empty
        || request.PatientId == Guid.Empty
        || request.BillingOwner is null
        || request.BillingOwner.Id == Guid.Empty
        || request.Units != 1
        || request.Generation < 1
        || !(string.Equals(request.BillingOwner.Type, "ACCOUNT", StringComparison.OrdinalIgnoreCase)
             || string.Equals(request.BillingOwner.Type, "CLINIC", StringComparison.OrdinalIgnoreCase)))
    {
        return BillingError(context, StatusCodes.Status400BadRequest, "ERR_VALIDATION",
            "Reserve yêu cầu UUID hợp lệ, billingOwner ACCOUNT/CLINIC, units=1 và generation dương.");
    }

    var normalized = request with
    {
        BillingOwner = request.BillingOwner with { Type = request.BillingOwner.Type.Trim().ToUpperInvariant() }
    };
    var profileDecision = await profileAccessClient.ValidateForAnalysisCreateAsync(
        normalized, cancellationToken);
    if (!profileDecision.Allowed)
    {
        return BillingError(context,
            profileDecision.DependencyUnavailable
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status403Forbidden,
            profileDecision.Code,
            profileDecision.Message,
            retryable: profileDecision.DependencyUnavailable);
    }

    var fingerprint = ReservationFingerprint.ForReserve(normalized, runtimeOptions);
    var result = await repository.ReserveAsync(
        normalized, idempotencyKey, fingerprint, runtimeOptions, cancellationToken);

    if (result.Status == ReservationOperationStatus.Created && result.Reservation is not null)
    {
        context.Response.Headers["Location"] = $"/internal/v1/billing/reservations/{result.Reservation.ReservationId:D}";
        return Results.Created(
            $"/internal/v1/billing/reservations/{result.Reservation.ReservationId:D}",
            result.Reservation);
    }

    if (result.Status == ReservationOperationStatus.Replayed && result.Reservation is not null)
    {
        context.Response.Headers["Idempotent-Replayed"] = "true";
        return Results.Ok(result.Reservation);
    }

    if (result.Status == ReservationOperationStatus.InsufficientCredit)
    {
        return BillingError(context, StatusCodes.Status402PaymentRequired,
            "ERR_INSUFFICIENT_CREDIT", "Không đủ credit để tạo reservation.");
    }

    return ReservationConflict(context, result);
})
.WithName("ReserveAnalysisCredit")
.WithTags("Billing Internal")
.Produces<ReservationResponse>(StatusCodes.Status201Created)
.Produces<ReservationResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status402PaymentRequired)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status409Conflict)
.Produces(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/internal/v1/billing/reservations/{reservationId:guid}", async Task<IResult> (
    Guid reservationId,
    HttpContext context,
    ServiceTokenValidator tokenValidator,
    CreditReservationRepository repository,
    CancellationToken cancellationToken) =>
{
    var tokenResult = await tokenValidator.ValidateAsync(
        context.Request, "billing:read", "analysis-api", cancellationToken);
    if (!tokenResult.Succeeded)
    {
        return BillingError(context, tokenResult.StatusCode, tokenResult.Code,
            tokenResult.Message, tokenResult.Retryable);
    }

    var reservation = await repository.GetAsync(reservationId, cancellationToken);
    return reservation is null
        ? BillingError(context, StatusCodes.Status404NotFound, "ERR_NOT_FOUND", "Không tìm thấy reservation.")
        : Results.Ok(reservation);
})
.WithName("GetBillingReservation")
.WithTags("Billing Internal")
.Produces<ReservationResponse>()
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status503ServiceUnavailable);

app.MapPost("/internal/v1/billing/reservations/{reservationId:guid}/capture", async Task<IResult> (
    Guid reservationId,
    HttpContext context,
    ServiceTokenValidator tokenValidator,
    CreditReservationRepository repository,
    CancellationToken cancellationToken) =>
{
    // The event consumer should call the domain service in-process. This route is
    // retained for the internal contract and requires an explicit billing:capture scope.
    var tokenResult = await tokenValidator.ValidateAsync(
        context.Request, "billing:capture", null, cancellationToken);
    if (!tokenResult.Succeeded)
    {
        return BillingError(context, tokenResult.StatusCode, tokenResult.Code,
            tokenResult.Message, tokenResult.Retryable);
    }

    if (!TryReadIdempotencyKey(context, out var idempotencyKey))
    {
        return BillingError(context, StatusCodes.Status400BadRequest, "ERR_VALIDATION",
            "Header Idempotency-Key hợp lệ dạng UUID là bắt buộc.");
    }

    var result = await repository.CaptureAsync(
        reservationId, idempotencyKey, ReservationFingerprint.ForCapture(reservationId), cancellationToken);
    if (result.Status == ReservationOperationStatus.Changed && result.Reservation is not null)
    {
        return Results.Ok(result.Reservation);
    }
    if (result.Status == ReservationOperationStatus.Replayed && result.Reservation is not null)
    {
        context.Response.Headers["Idempotent-Replayed"] = "true";
        return Results.Ok(result.Reservation);
    }
    if (result.Status == ReservationOperationStatus.NotFound)
    {
        return BillingError(context, StatusCodes.Status404NotFound, "ERR_NOT_FOUND", "Không tìm thấy reservation.");
    }
    if (result.Status == ReservationOperationStatus.MockLiveBlocked)
    {
        return BillingError(context, StatusCodes.Status409Conflict, "ERR_MOCK_LIVE_FORBIDDEN",
            "Mock result không được capture vào ví LIVE.");
    }
    return ReservationConflict(context, result);
})
.WithName("CaptureBillingReservation")
.WithTags("Billing Internal")
.Produces<ReservationResponse>()
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status409Conflict)
.Produces(StatusCodes.Status503ServiceUnavailable);

app.MapPost("/internal/v1/billing/reservations/{reservationId:guid}/release", async Task<IResult> (
    Guid reservationId,
    ReleaseCreditRequest request,
    HttpContext context,
    ServiceTokenValidator tokenValidator,
    CreditReservationRepository repository,
    CancellationToken cancellationToken) =>
{
    var tokenResult = await tokenValidator.ValidateAsync(
        context.Request, "billing:release", "analysis-api", cancellationToken);
    if (!tokenResult.Succeeded)
    {
        return BillingError(context, tokenResult.StatusCode, tokenResult.Code,
            tokenResult.Message, tokenResult.Retryable);
    }

    if (!TryReadIdempotencyKey(context, out var idempotencyKey))
    {
        return BillingError(context, StatusCodes.Status400BadRequest, "ERR_VALIDATION",
            "Header Idempotency-Key hợp lệ dạng UUID là bắt buộc.");
    }

    var allowedReasons = new HashSet<string>(StringComparer.Ordinal)
    {
        "VALIDATION_ABORT", "STORAGE_ABORT", "ANALYSIS_ABORT", "FINAL_FAILURE", "LOW_QUALITY"
    };
    var reason = request.Reason?.Trim().ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(reason) || !allowedReasons.Contains(reason))
    {
        return BillingError(context, StatusCodes.Status400BadRequest, "ERR_VALIDATION",
            "reason phải là VALIDATION_ABORT, STORAGE_ABORT, ANALYSIS_ABORT, FINAL_FAILURE hoặc LOW_QUALITY.");
    }

    var result = await repository.ReleaseAsync(
        reservationId, idempotencyKey, ReservationFingerprint.ForRelease(reservationId, reason),
        reason, cancellationToken);
    if (result.Status == ReservationOperationStatus.Changed && result.Reservation is not null)
    {
        return Results.Ok(result.Reservation);
    }
    if (result.Status == ReservationOperationStatus.Replayed && result.Reservation is not null)
    {
        context.Response.Headers["Idempotent-Replayed"] = "true";
        return Results.Ok(result.Reservation);
    }
    if (result.Status == ReservationOperationStatus.NotFound)
    {
        return BillingError(context, StatusCodes.Status404NotFound, "ERR_NOT_FOUND", "Không tìm thấy reservation.");
    }
    return ReservationConflict(context, result);
})
.WithName("ReleaseBillingReservation")
.WithTags("Billing Internal")
.Produces<ReservationResponse>()
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status409Conflict)
.Produces(StatusCodes.Status503ServiceUnavailable);

app.UseAuraPrometheusMetrics();

app.Run();
