using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace AURA.Identity.Api;

public sealed record TokenIssueResult(bool Success, string? AccessToken, int ExpiresIn, string? ErrorCode, string? ErrorMessage);
public sealed record ClientRegistration(string ClientId, Guid PrincipalId, string? ClientSecretHash,
    Dictionary<string, List<string>> AllowedTargets);

/// <summary>Loads provisioned credentials only. Never creates keys, secrets or fallback clients.</summary>
public sealed class ServiceTokenIssuer : IDisposable
{
    private readonly RSA _rsa;
    private readonly RSA _verificationRsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _publicKey;
    private readonly Dictionary<string, ClientRegistration> _clients;
    private readonly int _tokenLifetimeSeconds;
    public string Issuer { get; }
    public string KeyId { get; }

    public ServiceTokenIssuer(IConfiguration configuration, ILogger<ServiceTokenIssuer> logger)
    {
        Issuer = configuration["ServiceAuth:Issuer"] ?? "";
        if (!ValidName(Issuer, 128)) throw InvalidConfiguration();
        var lifetime = configuration["ServiceAuth:TokenLifetimeSeconds"];
        if (!int.TryParse(lifetime, out _tokenLifetimeSeconds) || _tokenLifetimeSeconds is < 1 or > 300)
            throw InvalidConfiguration();
        _rsa = RSA.Create();
        _verificationRsa = RSA.Create();
        try
        {
            var pem = ReadConfiguredFile(configuration["ServiceAuth:PrivateKeyPath"], 32 * 1024);
            var trimmed = pem.Trim();
            if (!(trimmed.StartsWith("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal)
                  || trimmed.StartsWith("-----BEGIN RSA PRIVATE KEY-----", StringComparison.Ordinal)))
                throw InvalidConfiguration();
            _rsa.ImportFromPem(trimmed);
            if (_rsa.KeySize < 2048 || _rsa.ExportParameters(true).D is not { Length: > 0 })
                throw InvalidConfiguration();
            var spki = _rsa.ExportSubjectPublicKeyInfo();
            KeyId = "aura-local-" + Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant()[..16];
            _verificationRsa.ImportSubjectPublicKeyInfo(spki, out _);
            _signingKey = new RsaSecurityKey(_rsa) { KeyId = KeyId, CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } };
            _publicKey = new RsaSecurityKey(_verificationRsa) { KeyId = KeyId, CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } };
            _clients = LoadClients(ReadConfiguredFile(configuration["ServiceAuth:RegistryPath"], 256 * 1024));
            logger.LogInformation("Loaded provisioned service identity configuration; client count={Count}", _clients.Count);
        }
        catch
        {
            _rsa.Dispose();
            _verificationRsa.Dispose();
            // Configuration failures must not expose PEM, JSON, secret hashes or file contents.
            throw InvalidConfiguration();
        }
    }

    private Dictionary<string, ClientRegistration> LoadClients(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var root = document.RootElement;
        RequireUniqueProperties(root);
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("issuer").GetString() != Issuer
            || root.GetProperty("algorithm").GetString() != "RS256"
            || root.GetProperty("tokenLifetimeSeconds").GetInt32() != _tokenLifetimeSeconds
            || root.GetProperty("keyId").GetString() != KeyId)
            throw InvalidConfiguration();
        var nodes = root.GetProperty("clients");
        if (nodes.ValueKind != JsonValueKind.Array || nodes.GetArrayLength() is < 1 or > 128)
            throw InvalidConfiguration();
        var clients = new Dictionary<string, ClientRegistration>(StringComparer.Ordinal);
        var principals = new HashSet<Guid>();
        foreach (var node in nodes.EnumerateArray())
        {
            var id = node.GetProperty("clientId").GetString() ?? "";
            if (!ValidName(id) || !Guid.TryParse(node.GetProperty("principalId").GetString(), out var principal)
                || principal == Guid.Empty || !principals.Add(principal)) throw InvalidConfiguration();
            var hash = node.GetProperty("clientSecretHash").GetString() ?? "";
            if (!TryDecodeHash(hash, out _, out _)) throw InvalidConfiguration();
            var targetsNode = node.GetProperty("allowedTargets");
            if (targetsNode.ValueKind != JsonValueKind.Object) throw InvalidConfiguration();
            var targets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var target in targetsNode.EnumerateObject())
            {
                if (!ValidName(target.Name) || target.Value.ValueKind != JsonValueKind.Array
                    || target.Value.GetArrayLength() is < 1 or > 32) throw InvalidConfiguration();
                var scopes = new List<string>();
                foreach (var value in target.Value.EnumerateArray())
                {
                    var scope = value.GetString() ?? "";
                    var parts = scope.Split(':');
                    if (parts.Length != 2 || !ValidName(parts[0]) || !ValidName(parts[1])
                        || scopes.Contains(scope, StringComparer.Ordinal)) throw InvalidConfiguration();
                    scopes.Add(scope);
                }
                targets.Add(target.Name, scopes);
            }
            if (targets.Count is < 1 or > 16 || !clients.TryAdd(id, new(id, principal, hash, targets)))
                throw InvalidConfiguration();
        }
        return clients;
    }

    private static void RequireUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw InvalidConfiguration();
                RequireUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RequireUniqueProperties(item);
    }

    private static string ReadConfiguredFile(string? path, int maxBytes)
    {
        if (string.IsNullOrWhiteSpace(path)) throw InvalidConfiguration();
        using var stream = File.OpenRead(path);
        if (stream.Length is < 1 || stream.Length > maxBytes) throw InvalidConfiguration();
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        if (text.Length > maxBytes) throw InvalidConfiguration();
        return text;
    }

    private static bool ValidName(string? value, int max = 64) => value is { Length: > 0 }
        && value.Length <= max && value[0] is >= 'a' and <= 'z'
        && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    private static InvalidOperationException InvalidConfiguration() => new(
        "Service authentication configuration is missing or invalid. Provision credentials externally and configure explicit paths.");

    public TokenIssueResult IssueToken(string clientId, string clientSecret, string? targetAudience, string? requestedScope)
    {
        if (!ValidName(clientId) || string.IsNullOrWhiteSpace(clientSecret) || clientSecret.Length > 1024)
            return Reject("ERR_VALIDATION", "Thiếu hoặc sai thông tin client.");
        if (!_clients.TryGetValue(clientId, out var client) || !VerifySecret(clientSecret, client.ClientSecretHash!))
            return Reject("ERR_UNAUTHORIZED", "Thông tin xác thực client không hợp lệ.");
        var audience = targetAudience?.Trim();
        if (string.IsNullOrWhiteSpace(audience))
        {
            if (client.AllowedTargets.Count != 1) return Reject("ERR_VALIDATION", "Thiếu target audience.");
            audience = client.AllowedTargets.Keys.Single();
        }
        if (!client.AllowedTargets.TryGetValue(audience, out var allowed))
            return Reject("ERR_FORBIDDEN", "Audience không được cấp phép.");
        List<string> scopes;
        if (string.IsNullOrWhiteSpace(requestedScope)) scopes = allowed;
        else
        {
            if (requestedScope.Length > 2048) return Reject("ERR_VALIDATION", "Scope không hợp lệ.");
            scopes = requestedScope.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal).ToList();
            if (scopes.Count == 0) return Reject("ERR_VALIDATION", "Scope không hợp lệ.");
            if (scopes.Any(s => !allowed.Contains(s, StringComparer.Ordinal)))
                return Reject("ERR_FORBIDDEN", "Scope không được cấp phép.");
        }
        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, client.PrincipalId.ToString("D")),
                new Claim("client_id", clientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
                new Claim("scope", string.Join(" ", scopes))
            }),
            IssuedAt = now.UtcDateTime, NotBefore = now.UtcDateTime,
            Expires = now.AddSeconds(_tokenLifetimeSeconds).UtcDateTime,
            Issuer = Issuer, Audience = audience,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        return new(true, handler.WriteToken(handler.CreateToken(descriptor)), _tokenLifetimeSeconds, null, null);
    }

    private static TokenIssueResult Reject(string code, string message) => new(false, null, 0, code, message);

    public (bool Valid, ClaimsPrincipal? Principal, string? ErrorCode, string? ErrorMessage) ValidateServiceToken(
        string token, string expectedAudience, string expectedScope)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024
            || !ValidName(expectedAudience) || string.IsNullOrWhiteSpace(expectedScope))
            return (false, null, "ERR_UNAUTHORIZED", "Service token không hợp lệ.");
        try
        {
            var handler = new JwtSecurityTokenHandler { MaximumTokenSizeInBytes = 16 * 1024, MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = _publicKey,
                RequireSignedTokens = true, RequireExpirationTime = true,
                ValidateIssuer = true, ValidIssuer = Issuer,
                ValidateAudience = true, ValidAudience = expectedAudience,
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
            }, out var validated);
            if (validated is not JwtSecurityToken jwt || jwt.Header.Alg != SecurityAlgorithms.RsaSha256
                || jwt.Header.Kid != KeyId || jwt.Audiences.Count() != 1
                || jwt.Claims.Count(c => c.Type == "client_id") != 1
                || jwt.Claims.Count(c => c.Type == "sub") != 1
                || !Guid.TryParse(jwt.Subject, out var subject)
                || !_clients.TryGetValue(principal.FindFirst("client_id")?.Value ?? "", out var client)
                || client.PrincipalId != subject)
                return (false, null, "ERR_UNAUTHORIZED", "Service token không hợp lệ.");
            var scopes = principal.FindAll("scope").ToArray();
            if (scopes.Length != 1 || !client.AllowedTargets.TryGetValue(expectedAudience, out var allowed))
                return (false, null, "ERR_FORBIDDEN", "Service token thiếu quyền phù hợp.");
            var values = scopes[0].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!values.Contains(expectedScope, StringComparer.Ordinal) || values.Any(s => !allowed.Contains(s, StringComparer.Ordinal)))
                return (false, null, "ERR_FORBIDDEN", "Service token thiếu quyền phù hợp.");
            return (true, principal, null, null);
        }
        catch (SecurityTokenInvalidAudienceException) { return (false, null, "ERR_FORBIDDEN", "Audience không hợp lệ."); }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return (false, null, "ERR_UNAUTHORIZED", "Service token không hợp lệ.");
        }
    }

    public string GetPublicKeyPem() => _verificationRsa.ExportSubjectPublicKeyInfoPem();
    public RsaSecurityKey GetPublicKey() => _publicKey;

    public static string HashSecret(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, 600_000, HashAlgorithmName.SHA256, 32);
        return "pbkdf2-sha256$600000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
    }

    private static bool TryDecodeHash(string? stored, out byte[] salt, out byte[] hash)
    {
        salt = Array.Empty<byte>(); hash = Array.Empty<byte>();
        if (stored is null || stored.Length > 128) return false;
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || parts[1] != "600000") return false;
        try
        {
            salt = Convert.FromBase64String(parts[2]); hash = Convert.FromBase64String(parts[3]);
            return salt.Length == 16 && hash.Length == 32
                && Convert.ToBase64String(salt) == parts[2] && Convert.ToBase64String(hash) == parts[3];
        }
        catch (FormatException) { return false; }
    }

    public static bool VerifySecret(string secret, string storedHash)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length > 1024 || !TryDecodeHash(storedHash, out var salt, out var expected))
            return false;
        var actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, 600_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public void Dispose() { _rsa.Dispose(); _verificationRsa.Dispose(); }
}
