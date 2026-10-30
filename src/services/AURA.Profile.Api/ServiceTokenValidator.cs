using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AURA.Profile.Api;

/// <summary>
/// Verifies using explicitly configured public key(s). Supports dual-key rotation buffer for zero-downtime key rotation.
/// </summary>
public sealed class ServiceTokenValidator : IDisposable
{
    private readonly List<RSA> _rsaInstances = new();
    private readonly List<RsaSecurityKey> _keys = new();
    private readonly HashSet<string> _keyIds = new(StringComparer.Ordinal);
    private readonly string _issuer;
    private readonly string _audience;

    public ServiceTokenValidator(IConfiguration configuration, ILogger<ServiceTokenValidator> logger)
    {
        _issuer = configuration["ServiceAuth:Issuer"] ?? "";
        _audience = configuration["ServiceAuth:Audience"] ?? "";

        try
        {
            if (string.IsNullOrWhiteSpace(_issuer) || _issuer.Length > 128
                || _issuer.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(_audience)
                || _audience.Length > 64 || _audience.Any(char.IsWhiteSpace))
            {
                throw new InvalidOperationException();
            }

            // Load primary key
            var primaryPem = LoadPem(configuration["ServiceAuth:PublicKeyPem"], configuration["ServiceAuth:PublicKeyPath"]);
            AddRsaKey(primaryPem);

            // Load optional previous/secondary key for rotation window (AC3 SCRUM-340)
            var prevPem = configuration["ServiceAuth:PreviousPublicKeyPem"];
            var prevPath = configuration["ServiceAuth:PreviousPublicKeyPath"];
            if (!string.IsNullOrWhiteSpace(prevPem) || !string.IsNullOrWhiteSpace(prevPath))
            {
                var secondaryPem = LoadPem(prevPem, prevPath);
                AddRsaKey(secondaryPem);
                logger.LogInformation("Loaded secondary service public key for zero-downtime rotation buffer.");
            }

            logger.LogInformation("Loaded configured service public verification keys (count = {KeyCount}).", _keys.Count);
        }
        catch
        {
            Dispose();
            throw new InvalidOperationException("Service public-key configuration is missing or invalid. Configure one explicit public key source.");
        }
    }

    private static string LoadPem(string? pem, string? path)
    {
        if (!string.IsNullOrWhiteSpace(pem) && !string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException();

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
        if (!(trimmed.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)
              || trimmed.StartsWith("-----BEGIN RSA PUBLIC KEY-----", StringComparison.Ordinal))
            || trimmed.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException();
        }

        return trimmed;
    }

    private void AddRsaKey(string pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        if (rsa.KeySize < 2048) throw new InvalidOperationException();

        var keyId = "aura-local-" + Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant()[..16];
        var secKey = new RsaSecurityKey(rsa)
        {
            KeyId = keyId,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        _rsaInstances.Add(rsa);
        _keys.Add(secKey);
        _keyIds.Add(keyId);
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
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = _keys,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
            }, out var validated);

            if (validated is not JwtSecurityToken jwt || jwt.Header.Alg != SecurityAlgorithms.RsaSha256
                || !_keyIds.Contains(jwt.Header.Kid) || jwt.Audiences.Count() != 1
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

    public void Dispose()
    {
        foreach (var rsa in _rsaInstances)
        {
            rsa.Dispose();
        }
        _rsaInstances.Clear();
        _keys.Clear();
        _keyIds.Clear();
    }
}
