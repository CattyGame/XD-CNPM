using System.Net;
using System.Text;
using AURA.Profile.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

var passed = 0;
var accountId = Guid.Parse("11111111-1111-4111-8111-111111111111");
var otherId = Guid.Parse("22222222-2222-4222-8222-222222222222");
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ServiceAuth:ClientId"] = "profile-api", ["ServiceAuth:ClientSecret"] = "fake-unit-test-secret"
}).Build();

async Task Check(string name, string? authorization, int status, string? code = null,
    string? contextJson = null, int contextStatus = 200, int tokenStatus = 200,
    string? tokenJson = null, bool timeout = false, bool spoof = false,
    bool missingConfig = false, bool multipleAuth = false, string[]? expectedRoles = null)
{
    var handler = new FakeIdentityHandler(accountId)
    {
        ContextJson = contextJson, ContextStatus = contextStatus, TokenStatus = tokenStatus,
        TokenJson = tokenJson, Timeout = timeout
    };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("http://identity.test") };
    var factory = new FakeFactory(http);
    var resolver = new UserIdentityResolver(factory, missingConfig ? new ConfigurationBuilder().Build() : config);
    var context = new DefaultHttpContext();
    context.Items["CorrelationId"] = "profile-unit-1";
    if (authorization is not null) context.Request.Headers.Authorization = authorization;
    if (multipleAuth) context.Request.Headers.Authorization = new[] { "Bearer user-A", "Bearer user-B" };
    if (spoof)
    {
        context.Request.Headers["X-Account-Id"] = otherId.ToString();
        context.Request.Headers["X-User-Authorization"] = "Bearer user-B";
        context.Request.Headers["X-Role"] = "SYSTEM_ADMIN";
    }
    var result = await resolver.ResolveAsync(context.Request, CancellationToken.None);
    if (result.StatusCode != status || (code is not null && result.Code != code)
        || (status == 200 && (!result.Succeeded || result.AccountId != accountId))
        || (status != 200 && (result.Succeeded || result.AccountId != Guid.Empty))
        || (status == 503 && !result.Retryable))
        throw new Exception("FAIL: " + name);
    if (status == 200)
    {
        if (expectedRoles != null && (result.Roles == null || !expectedRoles.SequenceEqual(result.Roles)))
        throw new Exception("FAIL: roles mismatch: " + name);
        if (handler.UserAuthorization != authorization || handler.ServiceAuthorization != "Bearer fake-service-token"
            || handler.LastCorrelation != "profile-unit-1" || handler.CallCount != 2)
            throw new Exception("FAIL: header isolation: " + name);
    }
    if ((authorization is null || multipleAuth || missingConfig) && handler.CallCount != 0)
        throw new Exception("FAIL: unnecessary network call: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

try
{
    await Check("Missing bearer", null, 401, "ERR_UNAUTHORIZED");
    await Check("Basic auth rejected", "Basic abc", 401, "ERR_UNAUTHORIZED");
    await Check("Bare GUID rejected", accountId.ToString(), 401, "ERR_UNAUTHORIZED");
    await Check("Multiple Authorization values rejected", "Bearer user-A", 401, "ERR_UNAUTHORIZED", multipleAuth: true);
    await Check("Missing service configuration", "Bearer user-A", 503, "ERR_IDENTITY_DEPENDENCY_UNAVAILABLE", missingConfig: true);
    await Check("Valid identity resolves authoritative account", "Bearer user-A", 200);
    await Check("Forged account and user headers ignored", "Bearer user-A", 200, spoof: true);
    await Check("Rejected user token", "Bearer invalid-user", 401, "ERR_UNAUTHORIZED", contextStatus: 401);
    await Check("Uninitialized account", "Bearer user-A", 409, "ERR_PROFILE_NOT_READY", contextStatus: 404);
    await Check("Disabled account", "Bearer user-A", 403, "ERR_ACCOUNT_DISABLED",
        contextJson: $$"""{"accountId":"{{accountId}}","accountStatus":"DISABLED"}""");
    await Check("Pending account denied", "Bearer user-A", 403, "ERR_ACCOUNT_DISABLED",
        contextJson: $$"""{"accountId":"{{accountId}}","accountStatus":"PENDING"}""");
    await Check("Empty account rejected", "Bearer user-A", 503, "ERR_IDENTITY_DEPENDENCY_UNAVAILABLE",
        contextJson: "{\"accountId\":\"00000000-0000-0000-0000-000000000000\",\"accountStatus\":\"ACTIVE\"}");
    await Check("Missing account status rejected", "Bearer user-A", 503, "ERR_IDENTITY_DEPENDENCY_UNAVAILABLE",
        contextJson: $$"""{"accountId":"{{accountId}}"}""");
    await Check("Malformed context rejected", "Bearer user-A", 503, "ERR_IDENTITY_DEPENDENCY_UNAVAILABLE", contextJson: "not-json");
    await Check("Bad service credentials are dependency error", "Bearer user-A", 503, "ERR_IDENTITY_DEPENDENCY_UNAVAILABLE", tokenStatus: 401);
    await Check("Malformed issuance response", "Bearer user-A", 503, "ERR_IDENTITY_DEPENDENCY_UNAVAILABLE", tokenJson: "{}");
    await Check("Wrong service scope is dependency error", "Bearer user-A", 503, "ERR_IDENTITY_DEPENDENCY_UNAVAILABLE", contextStatus: 403);
    await Check("Identity timeout is retryable", "Bearer user-A", 503, "ERR_IDENTITY_DEPENDENCY_UNAVAILABLE", timeout: true);
    await Check("Development mock is delegated to Identity", "Bearer mock-dev-token-test-user", 200);
    await Check("Resolves account with roles", "Bearer user-admin", 200,
        contextJson: $$"""{"accountId":"{{accountId}}","accountStatus":"ACTIVE","roles":["SYSTEM_ADMIN","CLINIC_ADMIN"]}""",
        expectedRoles: new[] { "SYSTEM_ADMIN", "CLINIC_ADMIN" });

    var cancelledHandler = new FakeIdentityHandler(accountId) { Cancel = true };
    using var cancelledHttp = new HttpClient(cancelledHandler) { BaseAddress = new Uri("http://identity.test") };
    var cancelledContext = new DefaultHttpContext();
    cancelledContext.Request.Headers.Authorization = "Bearer user-A";
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await new UserIdentityResolver(new FakeFactory(cancelledHttp), config)
            .ResolveAsync(cancelledContext.Request, cancellation.Token);
        throw new Exception("FAIL: caller cancellation must propagate");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("PASS: Caller cancellation propagates");
        passed++;
    }
    Console.WriteLine($"RESULT: {passed}/21 Profile identity checks passed (fake HTTP; not Firebase integration).");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

sealed class FakeFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => name == "IdentityClient" ? client : throw new Exception("Unexpected client");
}

sealed class FakeIdentityHandler(Guid accountId) : HttpMessageHandler
{
    public string? ContextJson { get; init; }
    public string? TokenJson { get; init; }
    public int TokenStatus { get; init; } = 200;
    public int ContextStatus { get; init; } = 200;
    public bool Timeout { get; init; }
    public bool Cancel { get; init; }
    public int CallCount { get; private set; }
    public string? UserAuthorization { get; private set; }
    public string? ServiceAuthorization { get; private set; }
    public string? LastCorrelation { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        CallCount++;
        if (Cancel) throw new OperationCanceledException(ct);
        if (Timeout) throw new TaskCanceledException("fake-timeout");
        if (request.RequestUri!.AbsolutePath == "/internal/v1/identity/service-tokens")
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            using var parsed = System.Text.Json.JsonDocument.Parse(body);
            var root = parsed.RootElement;
            if (root.GetProperty("clientId").GetString() != "profile-api"
                || root.GetProperty("audience").GetString() != "aura-identity"
                || root.GetProperty("scope").GetString() != "identity:context")
                throw new Exception("Unexpected service credentials target");
            return Reply(TokenStatus, TokenJson ?? "{\"accessToken\":\"fake-service-token\",\"tokenType\":\"Bearer\"}");
        }
        if (request.RequestUri.AbsolutePath != "/internal/v1/identity/context")
            throw new Exception("Unexpected endpoint");
        UserAuthorization = string.Join(",", request.Headers.GetValues("X-User-Authorization"));
        ServiceAuthorization = request.Headers.Authorization?.ToString();
        LastCorrelation = string.Join(",", request.Headers.GetValues("X-Correlation-Id"));
        return Reply(ContextStatus, ContextJson ?? $$"""{"accountId":"{{accountId}}","accountStatus":"ACTIVE","roles":["PATIENT"]}""");
    }

    private static HttpResponseMessage Reply(int status, string body) => new((HttpStatusCode)status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
}
