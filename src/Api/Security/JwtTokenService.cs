using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Svyne.Api.Security;

public sealed class JwtTokenService
{
    private readonly byte[] signingKey;
    private readonly string issuer;
    private readonly string audience;
    private readonly int lifetimeMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        var secret = configuration["JWT_SIGNING_KEY"] ?? "local-development-jwt-signing-key-change-me-32+chars";
        signingKey = Encoding.UTF8.GetBytes(secret);
        issuer = configuration["JWT_ISSUER"] ?? "svyne";
        audience = configuration["JWT_AUDIENCE"] ?? "svyne-clients";
        lifetimeMinutes = int.TryParse(configuration["JWT_LIFETIME_MINUTES"], out var m) ? m : 60;
    }

    public TokenParameters ValidationParameters => new()
    {
        Key = new SymmetricSecurityKey(signingKey),
        Issuer = issuer,
        Audience = audience
    };

    public (string token, long expiresAt) Issue(Guid usersId, string email, Guid? tenantsId, int role, string tenantSlug)
    {
        var expires = DateTime.UtcNow.AddMinutes(lifetimeMinutes);
        var claims = new List<Claim>
        {
            new("sub", usersId.ToString()),
            new("email", email),
            new("role", role.ToString()),
            new("tenant_slug", tenantSlug)
        };
        if (tenantsId is { } t)
        {
            claims.Add(new Claim("tenants_id", t.ToString()));
        }
        var creds = new SigningCredentials(new SymmetricSecurityKey(signingKey), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: creds);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (token, new DateTimeOffset(expires).ToUnixTimeSeconds());
    }
}

public sealed class TokenParameters
{
    public required SymmetricSecurityKey Key { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
}
