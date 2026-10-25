using System.Net;
using System.Text;
using AURA.Analysis.Api.Upload;

namespace AURA.Analysis.Api.Tests;

internal sealed class FakeServiceTokens : IServiceTokenProvider
{
    public readonly List<(string Audience, string Scope, string? Correlation)> Calls = new();
    public string Token = "test-service-token";
    public Task<string> GetAsync(string audience, string scope, CancellationToken ct, string? correlationId = null)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add((audience, scope, correlationId));
        return Task.FromResult(Token);
    }
}

internal sealed class ServiceReplyHandler : HttpMessageHandler
{
    public int Calls;
    public string? Body;
    public HttpRequestMessage? Request;
    public Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> Reply =
        (_, _, _) => Task.FromResult(Json("{}"));

    public static HttpResponseMessage Json(string text, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Request = request;
        Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        return await Reply(request, Body, ct);
    }
}

internal sealed class ServiceHttpFactory(HttpClient client) : IHttpClientFactory
{
    public readonly List<string> Names = new();
    public HttpClient CreateClient(string name) { Names.Add(name); return client; }
}
