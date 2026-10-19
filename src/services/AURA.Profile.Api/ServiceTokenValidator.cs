using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AURA.Profile.Api;

public class ServiceTokenValidator
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ServiceTokenValidator> _logger;
    private RsaSecurityKey? _publicKey;

    public ServiceTokenValidator(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ServiceTokenValidator> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _issuer = configuration["ServiceAuth:Issuer"] ?? "aura-identity-local";
        _audience = configuration["ServiceAuth:Audience"] ?? "aura-profile";

        LoadPublicKey();
    }

    private void LoadPublicKey()
    {
        // 1. Check direct PEM config
        var pemText = _configuration["ServiceAuth:PublicKeyPem"];
        if (!string.IsNullOrWhiteSpace(pemText))
        {
            try
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(pemText);
                _publicKey = new RsaSecurityKey(rsa);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load public key from ServiceAuth:PublicKeyPem config");
            }
        }

        // 2. Check public key file path
        var keyPath = _configuration["ServiceAuth:PublicKeyPath"];
        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "signing-public.pem"),
                Path.Combine(AppContext.BaseDirectory, "public", "signing-public.pem"),
                Path.Combine(AppContext.BaseDirectory, "signing-key.pem"),
                Path.Combine(AppContext.BaseDirectory, "..", "AURA.Identity.Api", "signing-key.pem")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand))
                {
                    keyPath = Path.GetFullPath(cand);
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
        {
            try
            {
                var text = File.ReadAllText(keyPath);
                var rsa = RSA.Create();
                rsa.ImportFromPem(text);
                _publicKey = new RsaSecurityKey(rsa);
                _logger.LogInformation("Loaded service public key from {KeyPath}", keyPath);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load public key from {Path}", keyPath);
            }
        }

        _logger.LogInformation("Public key not available locally, will fetch dynamically from Identity API if needed");
    }

    private async Task<RsaSecurityKey?> EnsurePublicKeyAsync()
    {
        if (_publicKey != null) return _publicKey;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("IdentityClient");
                var response = await client.GetAsync("/internal/v1/identity/public-key");
                if (response.IsSuccessStatusCode)
                {
                    var pem = await response.Content.ReadAsStringAsync();
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(pem);
                    _publicKey = new RsaSecurityKey(rsa);
                    _logger.LogInformation("Successfully fetched service public key from Identity service");
                    return _publicKey;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attempt {Attempt}: Could not fetch public key from Identity service", attempt + 1);
            }

            if (attempt < 2)
            {
                await Task.Delay(500);
            }
        }

        return _publicKey;
    }

    public async Task<(bool Valid, ClaimsPrincipal? Principal, string? ErrorCode, string? ErrorMessage)> ValidateTokenAsync(
        string token, string expectedAudience, string expectedScope)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, null, "ERR_UNAUTHORIZED", "Thiếu service token.");
        }

        var key = await EnsurePublicKeyAsync();
        if (key == null)
        {
            return (false, null, "ERR_INTERNAL_AUTH", "Khóa công khai để xác thực Service Token chưa sẵn sàng.");
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return (false, null, "ERR_UNAUTHORIZED", "Token không đúng định dạng JWT.");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidateAudience = expectedAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwt || jwt.Header.Alg != SecurityAlgorithms.RsaSha256)
            {
                return (false, null, "ERR_UNAUTHORIZED", "Thuật toán token không hợp lệ (yêu cầu RS256).");
            }

            var scopeClaim = principal.FindFirst("scope")?.Value
                             ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;

            if (string.IsNullOrWhiteSpace(scopeClaim))
            {
                return (false, null, "ERR_FORBIDDEN", "Token không chứa quyền (scope).");
            }

            var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!scopes.Contains(expectedScope, StringComparer.Ordinal))
            {
                return (false, null, "ERR_FORBIDDEN", $"Token thiếu scope bắt buộc: '{expectedScope}'.");
            }

            return (true, principal, null, null);
        }
        catch (SecurityTokenExpiredException)
        {
            return (false, null, "ERR_UNAUTHORIZED", "Service token đã hết hạn.");
        }
        catch (SecurityTokenInvalidAudienceException)
        {
            return (false, null, "ERR_FORBIDDEN", $"Audience của token không khớp yêu cầu '{expectedAudience}'.");
        }
        catch (SecurityTokenInvalidIssuerException)
        {
            return (false, null, "ERR_UNAUTHORIZED", "Issuer của token không hợp lệ.");
        }
        catch (Exception ex)
        {
            return (false, null, "ERR_UNAUTHORIZED", $"Token không hợp lệ: {ex.Message}");
        }
    }

    public (bool Valid, ClaimsPrincipal? Principal, string? ErrorCode, string? ErrorMessage) ValidateToken(
        string token, string expectedAudience, string expectedScope)
    {
        return ValidateTokenAsync(token, expectedAudience, expectedScope).GetAwaiter().GetResult();
    }
}
