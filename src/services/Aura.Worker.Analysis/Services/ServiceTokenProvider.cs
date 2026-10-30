using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Aura.Worker.Analysis.Configuration;

namespace Aura.Worker.Analysis.Services;

public interface IServiceTokenProvider
{
    string GenerateToken(string audience, string scope);
}

public class ServiceTokenProvider : IServiceTokenProvider
{
    private readonly ServiceAuthOptions _options;
    private readonly ILogger<ServiceTokenProvider> _logger;
    private RSA? _rsaKey;

    public ServiceTokenProvider(IOptions<ServiceAuthOptions> options, ILogger<ServiceTokenProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        InitializeKey();
    }

    private void InitializeKey()
    {
        if (!string.IsNullOrEmpty(_options.PrivateKeyPemPath) && File.Exists(_options.PrivateKeyPemPath))
        {
            try
            {
                var pemContent = File.ReadAllText(_options.PrivateKeyPemPath);
                _rsaKey = RSA.Create();
                _rsaKey.ImportFromPem(pemContent);
                _logger.LogInformation("Loaded RSA key from {Path}", _options.PrivateKeyPemPath);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load PEM from {Path}, using ephemeral test key", _options.PrivateKeyPemPath);
            }
        }

        // Ephemeral RSA key for test/fallback environments
        _rsaKey = RSA.Create(2048);
        _logger.LogInformation("Initialized ephemeral RSA key for service tokens");
    }

    public string GenerateToken(string audience, string scope)
    {
        var now = DateTime.UtcNow;
        var signingCredentials = new SigningCredentials(new RsaSecurityKey(_rsaKey), SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, _options.PrincipalId.ToString()),
            new("client_id", _options.ClientId),
            new("scope", scope)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.AddSeconds(-30), // 30s clock skew tolerance
            Expires = now.AddSeconds(300),    // 300s TTL
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
