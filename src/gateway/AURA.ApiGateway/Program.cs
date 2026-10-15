using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicyName = "AURA.Web";
const string correlationHeader = "X-Correlation-Id";

var allowedOrigins = builder.Configuration
    .GetSection("Gateway:AllowedOrigins")
    .Get<string[]>()
    ?? [];

var maxRequestBodyBytes = builder.Configuration
    .GetValue<long>("Gateway:MaxRequestBodyBytes");

var maxUploadBodyBytes = builder.Configuration
    .GetValue<long>("Gateway:MaxUploadBodyBytes");

if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Gateway:AllowedOrigins must contain at least one origin.");
}

foreach (var origin in allowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp &&
         uri.Scheme != Uri.UriSchemeHttps) ||
        origin != uri.GetLeftPart(UriPartial.Authority))
    {
        throw new InvalidOperationException(
            "Each Gateway:AllowedOrigins entry must be an HTTP/HTTPS " +
            "origin without a path or trailing slash.");
    }
}

if (maxRequestBodyBytes <= 0 ||
    maxUploadBodyBytes < maxRequestBodyBytes)
{
    throw new InvalidOperationException(
        "Gateway request body limits are invalid.");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadBodyBytes;
});

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .WithMethods(
                "GET",
                "POST",
                "PUT",
                "PATCH",
                "DELETE",
                "OPTIONS")
            .WithHeaders(
                "Authorization",
                "Content-Type",
                "Idempotency-Key",
                correlationHeader,
                "If-Match",
                "If-None-Match")
            .WithExposedHeaders(
                correlationHeader,
                "Location",
                "Retry-After",
                "ETag");
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.Use(async (context, next) =>
{
    var suppliedValues = context.Request.Headers[correlationHeader];
    var suppliedId = suppliedValues.Count == 1
        ? suppliedValues[0]
        : null;

    var correlationId = IsValidCorrelationId(suppliedId)
        ? suppliedId!
        : Guid.NewGuid().ToString("N");

    context.TraceIdentifier = correlationId;

    context.Request.Headers[correlationHeader] = correlationId;

    context.Response.OnStarting(() =>
    {
        context.Response.Headers[correlationHeader] = correlationId;
        return Task.CompletedTask;
    });

    using var scope = app.Logger.BeginScope(
        new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

    await next(context);
});

app.UseExceptionHandler();

app.UseRouting();
app.UseCors(corsPolicyName);

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    var isAnalysisUpload =
        HttpMethods.IsPost(context.Request.Method) &&
        string.Equals(
            path.TrimEnd('/'),
            "/api/v1/analyses",
            StringComparison.OrdinalIgnoreCase);

    var limit = isAnalysisUpload
        ? maxUploadBodyBytes
        : maxRequestBodyBytes;

    if (context.Request.ContentLength is long contentLength &&
        contentLength > limit)
    {
        await Results.Json(
            new
            {
                error = new
                {
                    code = "ERR_PAYLOAD_TOO_LARGE",
                    message = "Dung lượng yêu cầu vượt giới hạn cho phép.",
                    correlationId = context.TraceIdentifier,
                    retryable = false
                }
            },
            statusCode: StatusCodes.Status413PayloadTooLarge)
            .ExecuteAsync(context);

        return;
    }

    var bodySizeFeature = context.Features
        .Get<IHttpMaxRequestBodySizeFeature>();

    if (bodySizeFeature is { IsReadOnly: false })
    {
        bodySizeFeature.MaxRequestBodySize = limit;
    }

    await next(context);
});

app.MapHealthChecks("/health");

app.MapReverseProxy()
    .RequireCors(corsPolicyName);

app.Run();

static bool IsValidCorrelationId(string? value)
{
    if (string.IsNullOrEmpty(value) || value.Length > 64)
    {
        return false;
    }

    foreach (var character in value)
    {
        var allowed =
            character is >= 'a' and <= 'z' ||
            character is >= 'A' and <= 'Z' ||
            character is >= '0' and <= '9' ||
            character is '-' or '_' or '.';

        if (!allowed)
        {
            return false;
        }
    }

    return true;
}