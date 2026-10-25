using System.Net;
using System.Security.Claims;
using System.Text;
using AURA.Analysis.Api.Auth;
using AURA.Analysis.Api.Upload;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public sealed class AnalysisUserIdentityTests
{
    private static readonly Guid Account = Guid.NewGuid();
    private static DefaultHttpContext Context(string? authorization = "Bearer actual-user-token")
    {
        var context = new DefaultHttpContext();
        if (authorization is not null) context.Request.Headers.Authorization = authorization;
        context.Items["CorrelationId"] = "analysis-user-check";
        return context;
    }
    private static string Reply(string roles = "[\"PATIENT\"]", string status = "ACTIVE") =>
        $$"""{"accountId":"{{Account:D}}","accountStatus":"{{status}}","roles":{{roles}}} """;
    private static (AnalysisUserIdentityResolver Resolver, FakeServiceTokens Tokens, ServiceReplyHandler Handler) Rig(
        Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>>? callback = null)
    {
        var handler = new ServiceReplyHandler { Reply = callback ?? ((_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply()))) };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://identity.test"), Timeout = TimeSpan.FromSeconds(5) };
        var tokens = new FakeServiceTokens();
        return (new(new ServiceHttpFactory(client), tokens), tokens, handler);
    }
    private static async Task Error(Func<Task> action, int status, string code, bool retryable = false)
    {
        var error = await Assert.ThrowsAsync<UploadFailure>(action);
        Assert.Equal((status, code, retryable), (error.Status, error.Code, error.Retryable));
        Assert.DoesNotContain("PRIVATE", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic abc")]
    [InlineData("Bearer")]
    [InlineData("12345678-1234-1234-1234-123456789abc")]
    [InlineData("Bearer two tokens")]
    [InlineData("Bearer first,Bearer second")]
    [InlineData("Bearer invalid+character")]
    public async Task Invalid_public_authorization_stops_before_service_issuance(string? header)
    {
        var rig = Rig(); var context = Context(header);
        await Error(() => rig.Resolver.ResolveAsync(context.Request, default), 401, "ERR_UNAUTHORIZED");
        Assert.Empty(rig.Tokens.Calls);
        Assert.Equal(0, rig.Handler.Calls);
    }

    [Fact]
    public async Task Multiple_values_or_oversized_bearer_are_rejected_without_requests()
    {
        var rig = Rig(); var context = Context();
        context.Request.Headers.Authorization = new StringValues(new[] { "Bearer first", "Bearer second" });
        await Error(() => rig.Resolver.ResolveAsync(context.Request, default), 401, "ERR_UNAUTHORIZED");
        context.Request.Headers.Authorization = "Bearer " + new string('a', 16_385);
        await Error(() => rig.Resolver.ResolveAsync(context.Request, default), 401, "ERR_UNAUTHORIZED");
        Assert.Empty(rig.Tokens.Calls);
        Assert.Equal(0, rig.Handler.Calls);
    }

    [Fact]
    public async Task Only_actual_bearer_is_forwarded_and_context_account_roles_are_authoritative()
    {
        var context = Context("bEaReR actual-user-token");
        context.Request.Headers["X-User-Authorization"] = "Bearer forged-user-token";
        context.Request.Headers["X-Account-Id"] = Guid.NewGuid().ToString();
        context.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
        context.Request.Headers["X-Roles"] = "SYSTEM_ADMIN";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "SYSTEM_ADMIN") }, "forged-principal"));
        var rig = Rig((message, body, _) =>
        {
            Assert.Equal(HttpMethod.Post, message.Method);
            Assert.Equal("/internal/v1/identity/context", message.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer test-service-token", message.Headers.Authorization!.ToString());
            Assert.Equal("Bearer actual-user-token", Assert.Single(message.Headers.GetValues("X-User-Authorization")));
            Assert.Equal("analysis-user-check", Assert.Single(message.Headers.GetValues("X-Correlation-Id")));
            Assert.False(message.Headers.Contains("X-Account-Id"));
            Assert.False(message.Headers.Contains("X-Roles"));
            Assert.Equal("", body);
            return Task.FromResult(ServiceReplyHandler.Json(Reply("[\"DOCTOR\",\"DOCTOR\"]")));
        });
        var user = await rig.Resolver.ResolveAsync(context.Request, default);
        Assert.Equal(Account, user.AccountId);
        Assert.Equal("DOCTOR", Assert.Single(user.Roles));
        Assert.Equal(("aura-identity", "identity:context", "analysis-user-check"), Assert.Single(rig.Tokens.Calls));
        AnalysisUserPolicy.RequireClinicalRole(user);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)user.Roles)[0] = "SYSTEM_ADMIN");
    }

    [Theory]
    [InlineData(401, 401, "ERR_UNAUTHORIZED", false)]
    [InlineData(404, 409, "ERR_ACCOUNT_NOT_INITIALIZED", true)]
    [InlineData(403, 503, "ERR_NETWORK", true)]
    [InlineData(429, 503, "ERR_NETWORK", true)]
    [InlineData(500, 503, "ERR_NETWORK", true)]
    [InlineData(302, 503, "ERR_NETWORK", true)]
    public async Task Context_statuses_have_safe_public_errors(int upstream, int expected, string code, bool retryable)
    {
        var rig = Rig((_, _, _) => Task.FromResult(ServiceReplyHandler.Json("{\"PRIVATE\":\"diagnostic\"}", (HttpStatusCode)upstream)));
        await Error(() => rig.Resolver.ResolveAsync(Context().Request, default), expected, code, retryable);
        Assert.Equal(1, rig.Handler.Calls);
    }

    [Theory]
    [InlineData("DISABLED")]
    [InlineData("PENDING")]
    [InlineData("DELETED")]
    [InlineData("active")]
    public async Task Only_exact_ACTIVE_status_can_produce_a_context(string status)
    {
        var rig = Rig((_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply(status: status))));
        await Error(() => rig.Resolver.ResolveAsync(Context().Request, default), 403, "ERR_ACCOUNT_DISABLED");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"accountId\":\"not-a-guid\",\"accountStatus\":\"ACTIVE\",\"roles\":[\"PATIENT\"]}")]
    [InlineData("{\"accountId\":\"00000000-0000-0000-0000-000000000000\",\"accountStatus\":\"ACTIVE\",\"roles\":[\"PATIENT\"]}")]
    public async Task Malformed_or_empty_context_is_a_dependency_error(string json)
    {
        var rig = Rig((_, _, _) => Task.FromResult(ServiceReplyHandler.Json(json)));
        await Error(() => rig.Resolver.ResolveAsync(Context().Request, default), 503, "ERR_NETWORK", true);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[\"\"]")]
    [InlineData("[\" \"]")]
    [InlineData("[\"PATIENT\\n\"]")]
    public async Task Malformed_roles_do_not_get_a_default_patient_role(string roles)
    {
        var rig = Rig((_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply(roles))));
        await Error(() => rig.Resolver.ResolveAsync(Context().Request, default), 503, "ERR_NETWORK", true);
    }

    [Fact]
    public async Task Response_body_limit_is_enforced_even_without_Content_Length()
    {
        var content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(new string(' ', 32_769))));
        var rig = Rig((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        Assert.Null(content.Headers.ContentLength);
        await Error(() => rig.Resolver.ResolveAsync(Context().Request, default), 503, "ERR_NETWORK", true);
    }

    [Fact]
    public async Task Excessive_role_count_or_role_length_is_a_dependency_error()
    {
        var many = System.Text.Json.JsonSerializer.Serialize(Enumerable.Repeat("PATIENT", 33).ToArray());
        var longRole = System.Text.Json.JsonSerializer.Serialize(new[] { new string('R', 65) });
        foreach (var roles in new[] { many, longRole })
        {
            var rig = Rig((_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply(roles))));
            await Error(() => rig.Resolver.ResolveAsync(Context().Request, default), 503, "ERR_NETWORK", true);
        }
    }

    [Fact]
    public async Task Transport_timeout_is_retryable_but_caller_cancellation_propagates()
    {
        var rig = Rig((_, _, _) => throw new TaskCanceledException("PRIVATE timeout"));
        await Error(() => rig.Resolver.ResolveAsync(Context().Request, default), 503, "ERR_NETWORK", true);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var untouched = Rig();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => untouched.Resolver.ResolveAsync(Context().Request, cancelled.Token));
        Assert.Empty(untouched.Tokens.Calls);
        Assert.Equal(0, untouched.Handler.Calls);
    }

    [Fact]
    public async Task Body_read_also_observes_the_request_deadline()
    {
        var handler = new ServiceReplyHandler { Reply = (_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StreamContent(new WaitingStream()) }) };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://identity.test"), Timeout = TimeSpan.FromSeconds(1) };
        var resolver = new AnalysisUserIdentityResolver(new ServiceHttpFactory(client), new FakeServiceTokens());
        await Error(() => resolver.ResolveAsync(Context().Request, default), 503, "ERR_NETWORK", true);
    }

    [Fact]
    public async Task Bad_service_credentials_are_dependency_errors_and_never_reach_context()
    {
        var handler = new ServiceReplyHandler { Reply = (request, _, _) =>
        {
            Assert.Equal("/internal/v1/identity/service-tokens", request.RequestUri!.AbsolutePath);
            return Task.FromResult(ServiceReplyHandler.Json("{}", HttpStatusCode.Unauthorized));
        } };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://identity.test"), Timeout = TimeSpan.FromSeconds(5) };
        var factory = new ServiceHttpFactory(client);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["ServiceAuth:ClientId"]="analysis-api", ["ServiceAuth:ClientSecret"]="PRIVATE-fixture-secret" }).Build();
        var resolver = new AnalysisUserIdentityResolver(factory, new ServiceTokenProvider(factory, config));
        await Error(() => resolver.ResolveAsync(Context().Request, default), 503, "ERR_NETWORK", true);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Development_fixture_token_is_delegated_to_Identity_without_local_GUID_mapping()
    {
        const string fixture = "mock-dev-token-synthetic-only";
        var rig = Rig((request, _, _) =>
        {
            Assert.Equal("Bearer " + fixture, Assert.Single(request.Headers.GetValues("X-User-Authorization")));
            return Task.FromResult(ServiceReplyHandler.Json(Reply()));
        });
        var result = await rig.Resolver.ResolveAsync(Context("Bearer " + fixture).Request, default);
        Assert.Equal(Account, result.AccountId);
    }

    [Theory]
    [InlineData("PATIENT")]
    [InlineData("DOCTOR")]
    [InlineData("CLINIC_ADMIN")]
    public void Clinical_roles_pass_only_the_functional_gate(string role) =>
        AnalysisUserPolicy.RequireClinicalRole(new(Account, new[] { role }));

    [Theory]
    [InlineData("SYSTEM_ADMIN")]
    [InlineData("SUPPORT")]
    [InlineData("patient")]
    public void Unsupported_roles_do_not_gain_clinical_access(string role)
    {
        var error = Assert.Throws<UploadFailure>(() => AnalysisUserPolicy.RequireClinicalRole(new(Account, new[] { role })));
        Assert.Equal((403, "ERR_FORBIDDEN"), (error.Status, error.Code));
    }

    [Fact]
    public void Empty_roles_or_SYSTEM_ADMIN_with_patient_role_are_denied()
    {
        foreach (var roles in new[] { Array.Empty<string>(), new[] { "SYSTEM_ADMIN", "PATIENT" } })
            Assert.Equal(403, Assert.Throws<UploadFailure>(() => AnalysisUserPolicy.RequireClinicalRole(new(Account, roles))).Status);
    }

    [Fact]
    public void Service_client_registration_includes_the_user_identity_resolver()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["Services:Identity:BaseUrl"]="http://identity.test", ["Services:Profile:BaseUrl"]="http://profile.test",
              ["Services:Billing:BaseUrl"]="http://billing.test" }).Build();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAnalysisServiceClients(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.IsType<AnalysisUserIdentityResolver>(provider.GetRequiredService<IAnalysisUserIdentityResolver>());
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
    private sealed class WaitingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        { await Task.Delay(Timeout.Infinite, ct); return 0; }
    }
}
