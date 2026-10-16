using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AURA.Identity.Api;

public class ServiceTokenIssuer
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly int _lifetimeMinutes;

    public ServiceTokenIssuer(IConfiguration configuration)
    {
        _issuer = configuration["ServiceAuth:Issuer"] ?? "aura-identity-local";
        _lifetimeMinutes = configuration.GetValue("ServiceAuth:TokenLifetimeMinutes", 5);

        // Generate or load RSA key
        _rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsa)
        {
            KeyId = "aura-identity-key-1"
        };
    }

    public string IssueToken(string clientId, string targetAudience, IEnumerable<string> scopes)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new("client_id", clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("scope", string.Join(" ", scopes))
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_lifetimeMinutes),
            Issuer = _issuer,
            Audience = targetAudience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    public RsaSecurityKey GetPublicKey() => _signingKey;
}
