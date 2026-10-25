using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AURA.Profile.Api;

/// <summary>Verifies using an explicitly configured public key. No private-key or network fallback.</summary>
public sealed class ServiceTokenValidator : IDisposable
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _keyId;

    public ServiceTokenValidator(IConfiguration configuration, ILogger<ServiceTokenValidator> logger)
    {
        _issuer = configuration["ServiceAuth:Issuer"] ?? "";
        _audience = configuration["ServiceAuth:Audience"] ?? "";
        _rsa = RSA.Create();
        try
        {
            if (string.IsNullOrWhiteSpace(_issuer) || _issuer.Length > 128
                || _issuer.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(_audience)
                || _audience.Length > 64 || _audience.Any(char.IsWhiteSpace)) throw new InvalidOperationException();
            var pem = configuration["ServiceAuth:PublicKeyPem"];
            var path = configuration["ServiceAuth:PublicKeyPath"];
            // Ambiguous or invalid explicit configuration never falls through to another source.
            if (!string.IsNullOrWhiteSpace(pem) && !string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException();
            if (string.IsNullOrWhiteSpace(pem))
            {
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException();
                using var stream = File.OpenRead(path);
                if (stream.Length is < 1 or > 32 * 1024) throw new InvalidOperationException();
                using var reader = new StreamReader(stream);
                pem = reader.ReadToEnd();
            }
            if (pem.Length > 32 * 1024) throw new InvalidOperationException();
            var trimmed = pem.Trim();
            // ImportFromPem accepts private keys too; only public PEM labels are allowed here.
            if (!(trimmed.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)
                  || trimmed.StartsWith("-----BEGIN RSA PUBLIC KEY-----", StringComparison.Ordinal))
                || trimmed.Contains("PRIVATE KEY", StringComparison.Ordinal)) throw new InvalidOperationException();
            _rsa.ImportFromPem(trimmed);
            if (_rsa.KeySize < 2048) throw new InvalidOperationException();
            _keyId = "aura-local-" + Convert.ToHexString(SHA256.HashData(_rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant()[..16];
            _key = new RsaSecurityKey(_rsa) { KeyId = _keyId, CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } };
            logger.LogInformation("Loaded configured service public verification key.");
        }
        catch
        {
            _rsa.Dispose();
            throw new InvalidOperationException("Service public-key configuration is missing or invalid. Configure one explicit public key source.");
        }
    }

    public Task<(bool Valid, ClaimsPrincipal? Principal, string? ErrorCode, string? ErrorMessage)> ValidateTokenAsync(
        string token, string expectedAudience, string expectedScope) => Task.FromResult(ValidateToken(token, expectedAudience, expectedScope));

    public (bool Valid, ClaimsPrincipal? Principal, string? ErrorCode, string? ErrorMessage) ValidateToken(
        string token, string expectedAudience, string expectedScope)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024 || expectedAudience != _audience
            || string.IsNullOrWhiteSpace(expectedScope)) return Unauthorized();
        try
        {
            var handler = new JwtSecurityTokenHandler { MaximumTokenSizeInBytes = 16 * 1024, MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = _key,
                RequireSignedTokens = true, RequireExpirationTime = true,
                ValidateIssuer = true, ValidIssuer = _issuer,
                ValidateAudience = true, ValidAudience = _audience,
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
            }, out var validated);
            if (validated is not JwtSecurityToken jwt || jwt.Header.Alg != SecurityAlgorithms.RsaSha256
                || jwt.Header.Kid != _keyId || jwt.Audiences.Count() != 1
                || jwt.Claims.Count(c => c.Type == "sub") != 1 || !Guid.TryParse(jwt.Subject, out var subject) || subject == Guid.Empty
                || jwt.Claims.Count(c => c.Type == "client_id") != 1
                || string.IsNullOrWhiteSpace(principal.FindFirst("client_id")?.Value)) return Unauthorized();
            var scopes = principal.FindAll("scope").ToArray();
            if (scopes.Length != 1 || !scopes[0].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(expectedScope, StringComparer.Ordinal))
                return (false, null, "ERR_FORBIDDEN", "Service token thiếu scope bắt buộc.");
            return (true, principal, null, null);
        }
        catch (SecurityTokenInvalidAudienceException) { return (false, null, "ERR_FORBIDDEN", "Audience không hợp lệ."); }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException) { return Unauthorized(); }
    }

    private static (bool Valid, ClaimsPrincipal? Principal, string? ErrorCode, string? ErrorMessage) Unauthorized()
        => (false, null, "ERR_UNAUTHORIZED", "Service token không hợp lệ.");
    public void Dispose() => _rsa.Dispose();
}
