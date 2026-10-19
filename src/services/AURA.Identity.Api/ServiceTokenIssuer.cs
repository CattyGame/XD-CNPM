using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.Tokens;

namespace AURA.Identity.Api;

public sealed record TokenIssueResult(
    bool Success,
    string? AccessToken,
    int ExpiresIn,
    string? ErrorCode,
    string? ErrorMessage
);

public sealed record ClientRegistration(
    string ClientId,
    Guid PrincipalId,
    string? ClientSecretHash,
    Dictionary<string, List<string>> AllowedTargets
);

public class ServiceTokenIssuer
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _publicKey;
    private readonly string _issuer;
    private readonly int _tokenLifetimeSeconds;
    private readonly string _keyId;
    private readonly Dictionary<string, ClientRegistration> _clients = new(StringComparer.Ordinal);
    private readonly ILogger<ServiceTokenIssuer> _logger;

    public string Issuer => _issuer;
    public string KeyId => _keyId;

    public ServiceTokenIssuer(IConfiguration configuration, ILogger<ServiceTokenIssuer> logger)
    {
        _logger = logger;
        _issuer = configuration["ServiceAuth:Issuer"] ?? "aura-identity-local";
        _tokenLifetimeSeconds = configuration.GetValue("ServiceAuth:TokenLifetimeSeconds", 300);

        // 1. Load or initialize RSA Key
        var privateKeyPath = configuration["ServiceAuth:PrivateKeyPath"];
        var keyFilePath = !string.IsNullOrWhiteSpace(privateKeyPath) && File.Exists(privateKeyPath)
            ? privateKeyPath
            : Path.Combine(AppContext.BaseDirectory, "signing-key.pem");

        if (File.Exists(keyFilePath))
        {
            _rsa = RSA.Create();
            _rsa.ImportFromPem(File.ReadAllText(keyFilePath));
            _logger.LogInformation("Loaded persistent service signing key from {KeyPath}", keyFilePath);
        }
        else
        {
            _rsa = RSA.Create(2048);
            try
            {
                File.WriteAllText(keyFilePath, _rsa.ExportPkcs8PrivateKeyPem());
                _logger.LogInformation("Generated and saved persistent service signing key to {KeyPath}", keyFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save persistent key to {KeyPath}, using in-memory RSA key", keyFilePath);
            }
        }

        var pubSpki = _rsa.ExportSubjectPublicKeyInfo();
        _keyId = "aura-local-" + Convert.ToHexString(SHA256.HashData(pubSpki)).ToLowerInvariant()[..16];

        _signingKey = new RsaSecurityKey(_rsa) { KeyId = _keyId };

        var publicRsa = RSA.Create();
        publicRsa.ImportSubjectPublicKeyInfo(pubSpki, out _);
        _publicKey = new RsaSecurityKey(publicRsa) { KeyId = _keyId };

        // 2. Load Client Registry
        LoadClients(configuration);
    }

    private void LoadClients(IConfiguration configuration)
    {
        var registryPath = configuration["ServiceAuth:RegistryPath"];
        if (string.IsNullOrWhiteSpace(registryPath) || !File.Exists(registryPath))
        {
            // Check repo default paths
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "service-auth", "identity-clients.json"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "infra", "service-auth", "clients.example.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "infra", "service-auth", "clients.example.json")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand))
                {
                    registryPath = Path.GetFullPath(cand);
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(registryPath) && File.Exists(registryPath))
        {
            try
            {
                var jsonText = File.ReadAllText(registryPath);
                var root = JsonNode.Parse(jsonText)?.AsObject();
                var clientNodes = root?["clients"]?.AsArray();

                if (clientNodes != null)
                {
                    foreach (var c in clientNodes)
                    {
                        var cObj = c?.AsObject();
                        if (cObj == null) continue;

                        var clientId = cObj["clientId"]?.GetValue<string>();
                        var principalIdStr = cObj["principalId"]?.GetValue<string>();
                        var secretHash = cObj["clientSecretHash"]?.GetValue<string>();

                        if (string.IsNullOrWhiteSpace(clientId) || !Guid.TryParse(principalIdStr, out var principalId))
                        {
                            continue;
                        }

                        var allowedTargets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                        var targetsNode = cObj["allowedTargets"]?.AsObject();
                        if (targetsNode != null)
                        {
                            foreach (var prop in targetsNode)
                            {
                                var scopes = prop.Value?.AsArray()
                                    .Select(x => x?.GetValue<string>())
                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                    .Select(x => x!)
                                    .ToList() ?? new List<string>();

                                allowedTargets[prop.Key] = scopes;
                            }
                        }

                        // Seed default dev secret hashes if secretHash is empty
                        if (string.IsNullOrWhiteSpace(secretHash))
                        {
                            secretHash = HashSecret(clientId switch
                            {
                                "analysis-api" => "secret_analysis_pass",
                                "identity-api" => "secret_identity_pass",
                                "billing-api" => "secret_billing_pass",
                                _ => "secret_" + clientId + "_pass"
                            });
                        }

                        _clients[clientId] = new ClientRegistration(clientId, principalId, secretHash, allowedTargets);
                    }
                    _logger.LogInformation("Loaded {Count} service clients from registry {Path}", _clients.Count, registryPath);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse registry from {Path}, using standard seed clients", registryPath);
            }
        }

        // Standard seed clients according to clients.example.json
        SeedDefaultClients();
    }

    private void SeedDefaultClients()
    {
        _clients["analysis-api"] = new ClientRegistration(
            "analysis-api",
            Guid.Parse("a1000000-0000-4000-8000-000000000001"),
            HashSecret("secret_analysis_pass"),
            new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["aura-identity"] = new() { "identity:context" },
                ["aura-profile"] = new() { "profile:resolve" },
                ["aura-billing"] = new() { "billing:reserve", "billing:read", "billing:release" }
            }
        );

        _clients["identity-api"] = new ClientRegistration(
            "identity-api",
            Guid.Parse("a1000000-0000-4000-8000-000000000002"),
            HashSecret("secret_identity_pass"),
            new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["aura-profile"] = new() { "profile:provision" }
            }
        );

        _clients["billing-api"] = new ClientRegistration(
            "billing-api",
            Guid.Parse("a1000000-0000-4000-8000-000000000003"),
            HashSecret("secret_billing_pass"),
            new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["aura-profile"] = new() { "profile:resolve" }
            }
        );

        _logger.LogInformation("Initialized 3 default service clients (analysis-api, identity-api, billing-api)");
    }

    public TokenIssueResult IssueToken(string clientId, string clientSecret, string? targetAudience, string? requestedScope)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return new TokenIssueResult(false, null, 0, "ERR_VALIDATION", "Thiếu clientId hoặc clientSecret.");
        }

        if (!_clients.TryGetValue(clientId, out var client))
        {
            return new TokenIssueResult(false, null, 0, "ERR_UNAUTHORIZED", $"Client '{clientId}' không tồn tại trong hệ thống.");
        }

        // Verify Secret Hash
        if (string.IsNullOrWhiteSpace(client.ClientSecretHash) || !VerifySecret(clientSecret, client.ClientSecretHash))
        {
            return new TokenIssueResult(false, null, 0, "ERR_UNAUTHORIZED", "Mã bí mật client (clientSecret) không chính xác.");
        }

        // Validate Audience
        var audience = targetAudience?.Trim();
        if (string.IsNullOrWhiteSpace(audience))
        {
            // If only one target allowed, pick it; otherwise fail
            if (client.AllowedTargets.Count == 1)
            {
                audience = client.AllowedTargets.Keys.First();
            }
            else
            {
                return new TokenIssueResult(false, null, 0, "ERR_VALIDATION", "Thiếu target audience.");
            }
        }

        if (!client.AllowedTargets.TryGetValue(audience, out var allowedScopes))
        {
            return new TokenIssueResult(false, null, 0, "ERR_FORBIDDEN", $"Client '{clientId}' không được phép yêu cầu token cho audience '{audience}'.");
        }

        // Validate Scopes
        List<string> effectiveScopes;
        if (!string.IsNullOrWhiteSpace(requestedScope))
        {
            var requestedList = requestedScope.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var req in requestedList)
            {
                if (!allowedScopes.Contains(req, StringComparer.Ordinal))
                {
                    return new TokenIssueResult(false, null, 0, "ERR_FORBIDDEN", $"Scope '{req}' không được phép cấp cho audience '{audience}'.");
                }
            }
            effectiveScopes = requestedList.ToList();
        }
        else
        {
            effectiveScopes = allowedScopes;
        }

        // Issue JWT
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var now = DateTimeOffset.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.PrincipalId.ToString("D")),
            new("client_id", clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("scope", string.Join(" ", effectiveScopes))
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            Expires = now.AddSeconds(_tokenLifetimeSeconds).UtcDateTime,
            Issuer = _issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        var jwtString = handler.WriteToken(token);

        return new TokenIssueResult(true, jwtString, _tokenLifetimeSeconds, null, null);
    }

    public (bool Valid, ClaimsPrincipal? Principal, string? ErrorCode, string? ErrorMessage) ValidateServiceToken(
        string token, string expectedAudience, string expectedScope)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, null, "ERR_UNAUTHORIZED", "Thiếu service token.");
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return (false, null, "ERR_UNAUTHORIZED", "Token không đúng định dạng JWT.");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _publicKey,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = expectedAudience,
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
            return (false, null, "ERR_UNAUTHORIZED", $"Issuer của token không hợp lệ.");
        }
        catch (Exception ex)
        {
            return (false, null, "ERR_UNAUTHORIZED", $"Token không hợp lệ: {ex.Message}");
        }
    }

    public string GetPublicKeyPem() => _rsa.ExportSubjectPublicKeyInfoPem();

    public RsaSecurityKey GetPublicKey() => _publicKey;

    public static string HashSecret(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            secret,
            salt,
            600_000,
            HashAlgorithmName.SHA256,
            32);

        return "pbkdf2-sha256$600000$" +
            Convert.ToBase64String(salt) + "$" +
            Convert.ToBase64String(hash);
    }

    public static bool VerifySecret(string secret, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || parts[1] != "600000")
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            secret,
            salt,
            600_000,
            HashAlgorithmName.SHA256,
            32);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
