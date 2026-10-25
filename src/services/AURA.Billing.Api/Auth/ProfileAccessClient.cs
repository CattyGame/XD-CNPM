using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Billing.Api.Models;

namespace AURA.Billing.Api.Auth;

public sealed record ProfileAccessValidationResult(
    bool Allowed,
    bool DependencyUnavailable,
    string Code,
    string Message);

/// <summary>
/// Re-resolves the caller/patient context with Profile before moving credit.
/// The billing owner supplied by the caller is treated as a claim to verify,
/// never as an authority for choosing a wallet.
/// </summary>
public sealed class ProfileAccessClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ProfileAccessClient> logger)
{
    public async Task<ProfileAccessValidationResult> ValidateForAnalysisCreateAsync(
        ReserveCreditRequest request,
        CancellationToken cancellationToken)
    {
        var clientId = configuration["ServiceAuth:ClientId"] ?? "billing-api";
        var clientSecret = configuration["ServiceAuth:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            return Unavailable("ServiceAuth client credentials are not configured for Billing.");
        }

        var identityHttp = httpClientFactory.CreateClient("IdentityApi");
        string accessToken;
        try
        {
            using var tokenResponse = await identityHttp.PostAsJsonAsync(
                "/internal/v1/identity/service-tokens",
                new ServiceTokenRequest(clientId, clientSecret, "aura-profile", "profile:resolve"),
                cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Identity service-token request for Profile returned HTTP {StatusCode}.",
                    (int)tokenResponse.StatusCode);
                return Unavailable("Billing could not obtain a service token for Profile.");
            }

            var token = await tokenResponse.Content.ReadFromJsonAsync<ServiceTokenResponse>(
                cancellationToken: cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return Unavailable("Identity returned an invalid service-token response.");
            }
            accessToken = token.AccessToken;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or JsonException
            or OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to obtain a service token from Identity.");
            return Unavailable("Identity service is not available for Profile access validation.");
        }

        var clinicContextId = string.Equals(
            request.BillingOwner.Type, "CLINIC", StringComparison.OrdinalIgnoreCase)
            ? request.BillingOwner.Id
            : (Guid?)null;

        try
        {
            var profileHttp = httpClientFactory.CreateClient("ProfileApi");
            using var message = new HttpRequestMessage(
                HttpMethod.Post, "/internal/v1/profile/access/resolve")
            {
                Content = JsonContent.Create(new ProfileAccessResolveRequest(
                    request.ActorAccountId,
                    request.PatientId,
                    clinicContextId,
                    "ANALYSIS_CREATE"))
            };
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", accessToken);

            using var response = await profileHttp.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.LogWarning("Profile rejected Billing's service token with HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return Unavailable("Profile service-token authorization is not configured correctly.");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Profile access resolve returned HTTP {StatusCode}.", (int)response.StatusCode);
                return Unavailable("Profile access validation is temporarily unavailable.");
            }

            var decision = await response.Content.ReadFromJsonAsync<ProfileAccessResolveResponse>(
                cancellationToken: cancellationToken);
            if (decision is null || !decision.Allowed)
            {
                return Denied();
            }

            var expectedOwnerType = request.BillingOwner.Type.Trim().ToUpperInvariant();
            var expectedTenantKind = expectedOwnerType == "ACCOUNT" ? "PERSONAL" : "CLINIC";
            var matches = decision.TenantId == request.TenantId
                && decision.PatientId == request.PatientId
                && decision.BillingOwner is not null
                && string.Equals(decision.BillingOwner.Type, expectedOwnerType, StringComparison.Ordinal)
                && decision.BillingOwner.Id == request.BillingOwner.Id
                && string.Equals(decision.TenantKind, expectedTenantKind, StringComparison.Ordinal);

            return matches ? new ProfileAccessValidationResult(true, false, string.Empty, string.Empty) : Denied();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or JsonException
            or OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to resolve Profile access before Billing reservation.");
            return Unavailable("Profile access validation is temporarily unavailable.");
        }
    }

    private static ProfileAccessValidationResult Denied() =>
        new(false, false, "ERR_FORBIDDEN", "Profile does not authorize this billing owner/context.");

    private static ProfileAccessValidationResult Unavailable(string message) =>
        new(false, true, "ERR_BILLING_DEPENDENCY_UNAVAILABLE", message);

    private sealed record ServiceTokenRequest(
        string ClientId,
        string ClientSecret,
        string Audience,
        string Scope);

    private sealed record ServiceTokenResponse(string? AccessToken, string? TokenType, int ExpiresIn);

    private sealed record ProfileAccessResolveRequest(
        Guid ActorAccountId,
        Guid PatientId,
        Guid? ClinicContextId,
        string Action);

    private sealed record ProfileAccessResolveResponse(
        bool Allowed,
        Guid? PatientId,
        Guid? TenantId,
        string? TenantKind,
        ProfileBillingOwner? BillingOwner);

    private sealed record ProfileBillingOwner(string? Type, Guid Id);
}
