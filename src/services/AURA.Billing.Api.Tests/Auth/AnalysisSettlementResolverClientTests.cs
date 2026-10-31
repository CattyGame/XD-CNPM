using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AURA.Billing.Api.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AURA.Billing.Api.Tests.Auth;

public sealed class AnalysisSettlementResolverClientTests
{
    private static readonly Guid EventId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static readonly Guid AnalysisId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static readonly Guid TenantId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");

    private static readonly Guid ReservationId =
        Guid.Parse("44444444-4444-4444-8444-444444444444");

    private static readonly JsonSerializerOptions WebJson =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ResolveAsync_ValidCapture_SendsExpectedIdentityAndAnalysisRequests()
    {
        using var context = CreateContext(
            analysisResponder: (_, _) =>
                Task.FromResult(JsonResponse(ValidCapture())));

        var result = await context.Client.ResolveAsync(
            EventId,
            AnalysisId,
            TenantId,
            "scrum326-correlation",
            CancellationToken.None);

        Assert.Equal(EventId, result.EventId);
        Assert.Equal(AnalysisId, result.AnalysisId);
        Assert.Equal(TenantId, result.TenantId);
        Assert.True(result.Applicable);
        Assert.Equal("CAPTURE", result.SettlementAction);
        Assert.Equal("VALID_RESULT", result.Reason);
        Assert.Equal(ReservationId, result.ReservationId);
        Assert.Equal(2, result.Generation);
        Assert.Equal(7, result.ResultVersion);
        Assert.False(result.IsMock);

        var identityRequest = Assert.Single(context.IdentityHandler.Requests);
        Assert.Equal("POST", identityRequest.Method);
        Assert.Equal(
            "https://identity.test/internal/v1/identity/service-tokens",
            identityRequest.Uri);
        Assert.Equal("scrum326-correlation", identityRequest.CorrelationId);

        using var tokenBody = JsonDocument.Parse(identityRequest.Body!);
        Assert.Equal(
            "billing-api",
            tokenBody.RootElement.GetProperty("clientId").GetString());
        Assert.Equal(
            "unit-test-secret",
            tokenBody.RootElement.GetProperty("clientSecret").GetString());
        Assert.Equal(
            "aura-analysis",
            tokenBody.RootElement.GetProperty("audience").GetString());
        Assert.Equal(
            "analysis:reconcile",
            tokenBody.RootElement.GetProperty("scope").GetString());

        var analysisRequest = Assert.Single(context.AnalysisHandler.Requests);
        Assert.Equal("POST", analysisRequest.Method);
        Assert.Equal(
            "https://analysis.test/internal/v1/analysis/settlements/resolve",
            analysisRequest.Uri);
        Assert.Equal(
            "Bearer unit-test-service-token",
            analysisRequest.Authorization);
        Assert.Equal("scrum326-correlation", analysisRequest.CorrelationId);

        using var resolverBody = JsonDocument.Parse(analysisRequest.Body!);
        Assert.Equal(
            EventId.ToString(),
            resolverBody.RootElement.GetProperty("eventId").GetString());
        Assert.Equal(
            AnalysisId.ToString(),
            resolverBody.RootElement.GetProperty("analysisId").GetString());
        Assert.Equal(
            TenantId.ToString(),
            resolverBody.RootElement.GetProperty("tenantId").GetString());
    }

    [Theory]
    [InlineData("VALIDATION_ABORT", "FAILED")]
    [InlineData("STORAGE_ABORT", "FAILED")]
    [InlineData("ANALYSIS_ABORT", "FAILED")]
    [InlineData("FINAL_FAILURE", "FAILED")]
    [InlineData("LOW_QUALITY", "LOW_QUALITY")]
    public async Task ResolveAsync_AcceptsContractedReleaseReasons(
        string reason,
        string processingStatus)
    {
        using var context = CreateContext(
            analysisResponder: (_, _) =>
                Task.FromResult(JsonResponse(new
                {
                    eventId = EventId,
                    analysisId = AnalysisId,
                    tenantId = TenantId,
                    applicable = true,
                    settlementAction = "RELEASE",
                    reason,
                    reservationId = ReservationId,
                    generation = 2,
                    processingStatus,
                    resultVersion = (int?)null,
                    isMock = false
                })));

        var result = await context.Client.ResolveAsync(
            EventId,
            AnalysisId,
            TenantId,
            null,
            CancellationToken.None);

        Assert.Equal("RELEASE", result.SettlementAction);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(processingStatus, result.ProcessingStatus);
        Assert.Null(result.ResultVersion);
        Assert.False(result.IsMock);
    }

    [Fact]
    public async Task ResolveAsync_NonApplicableNone_ReturnsWithoutSettlement()
    {
        using var context = CreateContext(
            analysisResponder: (_, _) =>
                Task.FromResult(JsonResponse(new
                {
                    eventId = EventId,
                    analysisId = AnalysisId,
                    tenantId = TenantId,
                    applicable = false,
                    settlementAction = "NONE",
                    reason = "STALE_EVENT",
                    reservationId = (Guid?)null,
                    generation = (int?)null,
                    processingStatus = "PROCESSING",
                    resultVersion = (int?)null,
                    isMock = (bool?)null
                })));

        var result = await context.Client.ResolveAsync(
            EventId,
            AnalysisId,
            TenantId,
            null,
            CancellationToken.None);

        Assert.False(result.Applicable);
        Assert.Equal("NONE", result.SettlementAction);
        Assert.Equal("STALE_EVENT", result.Reason);
    }

    [Fact]
    public async Task ResolveAsync_EmptyEventId_RejectsBeforeHttpRequests()
    {
        using var context = CreateContext();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => context.Client.ResolveAsync(
                Guid.Empty,
                AnalysisId,
                TenantId,
                null,
                CancellationToken.None));

        Assert.Contains("eventId", exception.Message);
        Assert.Empty(context.IdentityHandler.Requests);
        Assert.Empty(context.AnalysisHandler.Requests);
    }

    [Fact]
    public async Task ResolveAsync_InvalidServiceIdentity_FailsBeforeHttpRequest()
    {
        using var context = CreateContext(clientId: "wrong-client");

        var exception = await Assert.ThrowsAsync<SettlementResolverCallException>(
            () => context.Client.ResolveAsync(
                EventId,
                AnalysisId,
                TenantId,
                null,
                CancellationToken.None));

        Assert.Equal(
            "ERR_BILLING_SERVICE_IDENTITY_NOT_CONFIGURED",
            exception.Code);
        Assert.False(exception.Retryable);
        Assert.Empty(context.IdentityHandler.Requests);
        Assert.Empty(context.AnalysisHandler.Requests);
    }

    [Fact]
    public async Task ResolveAsync_MismatchedEventId_RejectsResponse()
    {
        var wrongEventId =
            Guid.Parse("55555555-5555-4555-8555-555555555555");

        using var context = CreateContext(
            analysisResponder: (_, _) =>
                Task.FromResult(JsonResponse(
                    ValidCapture(eventId: wrongEventId))));

        var exception = await Assert.ThrowsAsync<SettlementResolverCallException>(
            () => context.Client.ResolveAsync(
                EventId,
                AnalysisId,
                TenantId,
                null,
                CancellationToken.None));

        Assert.Equal("ERR_ANALYSIS_RESOLVER_ID_MISMATCH", exception.Code);
        Assert.False(exception.Retryable);
    }

    [Fact]
    public async Task ResolveAsync_MockCapture_RejectsDecision()
    {
        using var context = CreateContext(
            analysisResponder: (_, _) =>
                Task.FromResult(JsonResponse(ValidCapture(isMock: true))));

        var exception = await Assert.ThrowsAsync<SettlementResolverCallException>(
            () => context.Client.ResolveAsync(
                EventId,
                AnalysisId,
                TenantId,
                null,
                CancellationToken.None));

        Assert.Equal("ERR_ANALYSIS_RESOLVER_INVALID_CAPTURE", exception.Code);
        Assert.False(exception.Retryable);
    }

    [Fact]
    public async Task ResolveAsync_AnalysisUnavailable_IsRetryable()
    {
        using var context = CreateContext(
            analysisResponder: (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var exception = await Assert.ThrowsAsync<SettlementResolverCallException>(
            () => context.Client.ResolveAsync(
                EventId,
                AnalysisId,
                TenantId,
                null,
                CancellationToken.None));

        Assert.Equal("ERR_ANALYSIS_RESOLVER_HTTP_503", exception.Code);
        Assert.True(exception.Retryable);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public async Task ResolveAsync_AnalysisForbidden_IsNotRetryable()
    {
        using var context = CreateContext(
            analysisResponder: (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.Forbidden)));

        var exception = await Assert.ThrowsAsync<SettlementResolverCallException>(
            () => context.Client.ResolveAsync(
                EventId,
                AnalysisId,
                TenantId,
                null,
                CancellationToken.None));

        Assert.Equal("ERR_ANALYSIS_RESOLVER_HTTP_403", exception.Code);
        Assert.False(exception.Retryable);
        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
    }

    [Fact]
    public async Task ResolveAsync_MalformedAnalysisJson_RejectsResponse()
    {
        using var context = CreateContext(
            analysisResponder: (_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{",
                        Encoding.UTF8,
                        "application/json")
                }));

        var exception = await Assert.ThrowsAsync<SettlementResolverCallException>(
            () => context.Client.ResolveAsync(
                EventId,
                AnalysisId,
                TenantId,
                null,
                CancellationToken.None));

        Assert.Equal(
            "ERR_ANALYSIS_RESOLVER_INVALID_RESPONSE",
            exception.Code);
        Assert.False(exception.Retryable);
    }

    private static object ValidCapture(
        Guid? eventId = null,
        bool isMock = false)
    {
        return new
        {
            eventId = eventId ?? EventId,
            analysisId = AnalysisId,
            tenantId = TenantId,
            applicable = true,
            settlementAction = "CAPTURE",
            reason = "VALID_RESULT",
            reservationId = ReservationId,
            generation = 2,
            processingStatus = "COMPLETED",
            resultVersion = 7,
            isMock
        };
    }

    private static HttpResponseMessage JsonResponse(
        object body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(body, WebJson);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };
    }

    private static TestContext CreateContext(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>?
            identityResponder = null,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>?
            analysisResponder = null,
        string clientId = "billing-api",
        string? clientSecret = "unit-test-secret")
    {
        var identityHandler = new RecordingHandler(
            identityResponder ??
            ((_, _) => Task.FromResult(JsonResponse(new
            {
                accessToken = "unit-test-service-token",
                tokenType = "Bearer",
                expiresIn = 300
            }))));

        var analysisHandler = new RecordingHandler(
            analysisResponder ??
            ((_, _) => Task.FromResult(JsonResponse(ValidCapture()))));

        var factory = new TestHttpClientFactory(
            identityHandler,
            analysisHandler);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAuth:ClientId"] = clientId,
                ["ServiceAuth:ClientSecret"] = clientSecret
            })
            .Build();

        var client = new AnalysisSettlementResolverClient(
            factory,
            configuration,
            NullLogger<AnalysisSettlementResolverClient>.Instance);

        return new TestContext(
            client,
            factory,
            identityHandler,
            analysisHandler);
    }

    private sealed class TestContext(
        AnalysisSettlementResolverClient client,
        TestHttpClientFactory factory,
        RecordingHandler identityHandler,
        RecordingHandler analysisHandler) : IDisposable
    {
        public AnalysisSettlementResolverClient Client { get; } = client;

        public RecordingHandler IdentityHandler { get; } = identityHandler;

        public RecordingHandler AnalysisHandler { get; } = analysisHandler;

        public void Dispose()
        {
            factory.Dispose();
        }
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _identityClient;
        private readonly HttpClient _analysisClient;

        public TestHttpClientFactory(
            RecordingHandler identityHandler,
            RecordingHandler analysisHandler)
        {
            _identityClient = new HttpClient(identityHandler)
            {
                BaseAddress = new Uri("https://identity.test")
            };

            _analysisClient = new HttpClient(analysisHandler)
            {
                BaseAddress = new Uri("https://analysis.test")
            };
        }

        public HttpClient CreateClient(string name)
        {
            return name switch
            {
                "IdentityApi" => _identityClient,
                "AnalysisApi" => _analysisClient,
                _ => throw new InvalidOperationException(
                    $"Unexpected HttpClient name: {name}")
            };
        }

        public void Dispose()
        {
            _identityClient.Dispose();
            _analysisClient.Dispose();
        }
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
            responder) : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var correlationId =
                request.Headers.TryGetValues("X-Correlation-Id", out var values)
                    ? values.SingleOrDefault()
                    : null;

            Requests.Add(new RequestSnapshot(
                request.Method.Method,
                request.RequestUri?.AbsoluteUri,
                request.Headers.Authorization?.ToString(),
                correlationId,
                body));

            return await responder(request, cancellationToken);
        }
    }

    private sealed record RequestSnapshot(
        string Method,
        string? Uri,
        string? Authorization,
        string? CorrelationId,
        string? Body);
}