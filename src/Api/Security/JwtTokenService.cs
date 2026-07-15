using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TicketSpan.Api.Security;

public sealed class JwtTokenService
{
    private readonly byte[] signingKey;
    private readonly string issuer;
    private readonly string audience;
    private readonly int lifetimeMinutes;
    private readonly int refreshLifetimeMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        var secret = configuration["JWT_SIGNING_KEY"] ?? "local-development-jwt-signing-key-change-me-32+chars";
        signingKey = Encoding.UTF8.GetBytes(secret);
        issuer = configuration["JWT_ISSUER"] ?? "ticketspan";
        audience = configuration["JWT_AUDIENCE"] ?? "ticketspan-clients";
        lifetimeMinutes = int.TryParse(configuration["JWT_LIFETIME_MINUTES"], out var m) ? m : 60;
        refreshLifetimeMinutes = int.TryParse(configuration["JWT_REFRESH_LIFETIME_MINUTES"], out var rm) ? rm : 43200;
    }

    public TokenParameters ValidationParameters => new()
    {
        Key = new SymmetricSecurityKey(signingKey),
        Issuer = issuer,
        Audience = audience
    };

    public (string token, long expiresAt) Issue(Guid usersId, string email, Guid? tenantsId, int role, string tenantSlug)
        => Build(usersId, email, tenantsId, role, tenantSlug, lifetimeMinutes, "access");

    public (string token, long expiresAt) IssueRefresh(Guid usersId, string email, Guid? tenantsId, int role, string tenantSlug)
        => Build(usersId, email, tenantsId, role, tenantSlug, refreshLifetimeMinutes, "refresh");

    private (string token, long expiresAt) Build(Guid usersId, string email, Guid? tenantsId, int role, string tenantSlug, int minutes, string type)
    {
        var expires = DateTime.UtcNow.AddMinutes(minutes);
        var claims = new List<Claim>
        {
            new("sub", usersId.ToString()),
            new("email", email),
            new("role", role.ToString()),
            new("tenant_slug", tenantSlug),
            new("typ", type)
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
