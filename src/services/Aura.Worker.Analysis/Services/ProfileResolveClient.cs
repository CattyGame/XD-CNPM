using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aura.Worker.Analysis.Configuration;

namespace Aura.Worker.Analysis.Services;

public interface IProfileResolveClient
{
    Task<ProfileResolveResult> RecheckAccessAsync(Guid tenantId, Guid patientId, string action, CancellationToken cancellationToken);
}

public class ProfileResolveResult
{
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
    public bool IsTransientError { get; set; }
}

public class ProfileResolveClient : IProfileResolveClient
{
    private readonly HttpClient _httpClient;
    private readonly ProfileClientOptions _options;
    private readonly IServiceTokenProvider _tokenProvider;
    private readonly ILogger<ProfileResolveClient> _logger;

    public ProfileResolveClient(
        HttpClient httpClient,
        IOptions<ProfileClientOptions> options,
        IServiceTokenProvider tokenProvider,
        ILogger<ProfileResolveClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task<ProfileResolveResult> RecheckAccessAsync(Guid tenantId, Guid patientId, string action, CancellationToken cancellationToken)
    {
        try
        {
            var token = _tokenProvider.GenerateToken(_options.Audience, _options.Scope);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/internal/v1/profile/access/resolve");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                tenantId = tenantId,
                patientId = patientId,
                action = action
            };
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new ProfileResolveResult { Allowed = true };
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            {
                return new ProfileResolveResult
                {
                    Allowed = false,
                    IsTransientError = true,
                    Reason = $"Profile service unavailable (HTTP {(int)response.StatusCode})"
                };
            }

            return new ProfileResolveResult
            {
                Allowed = false,
                IsTransientError = false,
                Reason = $"Access revoked or unauthorized (HTTP {(int)response.StatusCode})"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Transient network error contacting Profile service");
            return new ProfileResolveResult { Allowed = false, IsTransientError = true, Reason = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error rechecking Profile access");
            return new ProfileResolveResult { Allowed = false, IsTransientError = false, Reason = ex.Message };
        }
    }
}
