using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TicketSpan.Api;
using TicketSpan.Api.Security;

static HttpContext BuildContext(
    string path,
    string clientIp = "203.0.113.10",
    string? subject = null,
    int? role = null,
    string? tenantsId = null,
    string? cloudflareIp = null,
    string? forwardedFor = null,
    string? realIp = null)
{
    var httpContext = new DefaultHttpContext();
    httpContext.Request.Path = path;
    httpContext.Request.Method = HttpMethods.Post;
    httpContext.Request.ContentType = "application/grpc-web+proto";
    httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(clientIp);
    if (cloudflareIp is not null)
    {
        httpContext.Request.Headers["CF-Connecting-IP"] = cloudflareIp;
    }
    if (forwardedFor is not null)
    {
        httpContext.Request.Headers["X-Forwarded-For"] = forwardedFor;
    }
    if (realIp is not null)
    {
        httpContext.Request.Headers["X-Real-IP"] = realIp;
    }
    var claims = new List<Claim>();
    if (subject is not null)
    {
        claims.Add(new Claim("sub", subject));
    }
    if (role is not null)
    {
        claims.Add(new Claim("role", role.Value.ToString()));
    }
    if (tenantsId is not null)
    {
        claims.Add(new Claim("tenants_id", tenantsId));
    }
    if (claims.Count > 0)
    {
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
    return httpContext;
}

static int AcquireUntilRejected(RateLimitPolicy policy, HttpContext httpContext, int ceiling)
{
    for (var attempt = 1; attempt <= ceiling; attempt++)
    {
        using var lease = policy.Limiter.AttemptAcquire(httpContext);
        if (!lease.IsAcquired)
        {
            return attempt;
        }
    }
    return -1;
}

var failures = new List<string>();

static void Check(List<string> failures, string name, bool condition, string detail)
{
    if (condition)
    {
        Console.WriteLine($"  PASS  {name}");
        return;
    }
    Console.WriteLine($"  FAIL  {name} -- {detail}");
    failures.Add(name);
}

Console.WriteLine("Auth endpoint limit (20/min per client IP)");
{
    using var policy = new RateLimitPolicy();
    var attacker = BuildContext("/ticketspan.auth.AuthService/Login", cloudflareIp: "198.51.100.7");
    var rejectedAt = AcquireUntilRejected(policy, attacker, 40);
    Check(failures, "auth rejects the 21st attempt", rejectedAt == 21, $"rejected at attempt {rejectedAt}");

    var otherClient = BuildContext("/ticketspan.auth.AuthService/Login", cloudflareIp: "198.51.100.8");
    using var otherLease = policy.Limiter.AttemptAcquire(otherClient);
    Check(failures, "a different client IP has its own bucket", otherLease.IsAcquired, "second IP was rejected");
}

Console.WriteLine("Proxy IP is not the partition key (the bug this replaces)");
{
    using var policy = new RateLimitPolicy();
    var sharedProxyIp = "172.16.0.1";
    var firstUser = BuildContext("/ticketspan.auth.AuthService/Login", clientIp: sharedProxyIp, cloudflareIp: "198.51.100.20");
    var secondUser = BuildContext("/ticketspan.auth.AuthService/Login", clientIp: sharedProxyIp, cloudflareIp: "198.51.100.21");
    var rejectedAt = AcquireUntilRejected(policy, firstUser, 40);
    Check(failures, "first client exhausts its own bucket", rejectedAt == 21, $"rejected at attempt {rejectedAt}");
    using var secondLease = policy.Limiter.AttemptAcquire(secondUser);
    Check(failures, "second client behind the same proxy is unaffected", secondLease.IsAcquired,
        "shared proxy IP still collapses clients into one bucket");
}

Console.WriteLine("Client IP header precedence");
{
    using var policy = new RateLimitPolicy();
    var cloudflareWins = BuildContext("/ticketspan.auth.AuthService/Login",
        cloudflareIp: "198.51.100.30", forwardedFor: "198.51.100.31", realIp: "198.51.100.32");
    AcquireUntilRejected(policy, cloudflareWins, 40);
    var forwardedOnly = BuildContext("/ticketspan.auth.AuthService/Login", forwardedFor: "198.51.100.30, 10.0.0.1");
    using var forwardedLease = policy.Limiter.AttemptAcquire(forwardedOnly);
    Check(failures, "CF-Connecting-IP and left-most X-Forwarded-For resolve to the same bucket",
        !forwardedLease.IsAcquired, "header precedence produced different partition keys");

    var realIpOnly = BuildContext("/ticketspan.auth.AuthService/Login", realIp: "198.51.100.40");
    using var realIpLease = policy.Limiter.AttemptAcquire(realIpOnly);
    Check(failures, "X-Real-IP is used as the final fallback", realIpLease.IsAcquired, "X-Real-IP request was rejected");
}

Console.WriteLine("Authenticated user limit (100/min)");
{
    using var policy = new RateLimitPolicy();
    var user = BuildContext("/ticketspan.event.EventService/ListEvents",
        subject: Guid.NewGuid().ToString(), role: Lookups.UserRoles.Admin, tenantsId: Guid.NewGuid().ToString());
    var rejectedAt = AcquireUntilRejected(policy, user, 200);
    Check(failures, "user rejected on the 101st request", rejectedAt == 101, $"rejected at attempt {rejectedAt}");
}

Console.WriteLine("Tenant cap (1000/hr) binds across users of one tenant");
{
    using var policy = new RateLimitPolicy();
    var tenantsId = Guid.NewGuid().ToString();
    var consumed = 0;
    for (var userIndex = 0; userIndex < 12; userIndex++)
    {
        var user = BuildContext("/ticketspan.event.EventService/ListEvents",
            subject: Guid.NewGuid().ToString(), role: Lookups.UserRoles.Admin, tenantsId: tenantsId);
        for (var request = 0; request < 100; request++)
        {
            using var lease = policy.Limiter.AttemptAcquire(user);
            if (!lease.IsAcquired)
            {
                break;
            }
            consumed++;
        }
    }
    Check(failures, "tenant total stops at 1000 across 12 distinct users", consumed == 1000, $"consumed {consumed}");

    var otherTenantUser = BuildContext("/ticketspan.event.EventService/ListEvents",
        subject: Guid.NewGuid().ToString(), role: Lookups.UserRoles.Admin, tenantsId: Guid.NewGuid().ToString());
    using var otherTenantLease = policy.Limiter.AttemptAcquire(otherTenantUser);
    Check(failures, "a different tenant is unaffected", otherTenantLease.IsAcquired, "tenant isolation leaked");
}

Console.WriteLine("Developer limit (5000/hr) is not tenant-capped");
{
    using var policy = new RateLimitPolicy();
    var developer = BuildContext("/ticketspan.log.LogService/ListLogs",
        subject: Guid.NewGuid().ToString(), role: Lookups.UserRoles.Developer);
    var consumed = 0;
    for (var request = 0; request < 5200; request++)
    {
        using var lease = policy.Limiter.AttemptAcquire(developer);
        if (!lease.IsAcquired)
        {
            break;
        }
        consumed++;
    }
    Check(failures, "developer consumes 5000 before rejection", consumed == 5000, $"consumed {consumed}");
}

Console.WriteLine("Exempt paths are never limited");
{
    using var policy = new RateLimitPolicy();
    foreach (var path in new[] { "/health/ready", "/webhooks/stripe", "/images/abc" })
    {
        var exempt = BuildContext(path);
        var rejectedAt = AcquireUntilRejected(policy, exempt, 300);
        Check(failures, $"{path} is exempt", rejectedAt == -1, $"rejected at attempt {rejectedAt}");
        Check(failures, $"{path} reports no bucket", policy.ResolveEffectiveBucket(exempt) is null, "bucket resolved for exempt path");
    }
}

Console.WriteLine("Statistics back the X-RateLimit headers");
{
    using var policy = new RateLimitPolicy();
    var user = BuildContext("/ticketspan.event.EventService/ListEvents",
        subject: Guid.NewGuid().ToString(), role: Lookups.UserRoles.Admin, tenantsId: Guid.NewGuid().ToString());
    var bucket = policy.ResolveEffectiveBucket(user);
    Check(failures, "bucket resolves for an authenticated user", bucket is not null, "bucket was null");
    Check(failures, "fresh user reports 100 remaining", policy.ReadRemainingPermits(user) == 100,
        $"remaining was {policy.ReadRemainingPermits(user)}");
    using (var lease = policy.Limiter.AttemptAcquire(user))
    {
        Debug.Assert(lease.IsAcquired);
    }
    Check(failures, "remaining drops to 99 after one request", policy.ReadRemainingPermits(user) == 99,
        $"remaining was {policy.ReadRemainingPermits(user)}");
    Check(failures, "effective bucket reports the user limit while it is the tighter one",
        policy.ResolveEffectiveBucket(user)?.PermitLimit == 100,
        $"limit was {policy.ResolveEffectiveBucket(user)?.PermitLimit}");
}

Console.WriteLine("Effective bucket switches to the tenant once it is the tighter cap");
{
    using var policy = new RateLimitPolicy();
    var tenantsId = Guid.NewGuid().ToString();
    for (var userIndex = 0; userIndex < 10; userIndex++)
    {
        var filler = BuildContext("/ticketspan.event.EventService/ListEvents",
            subject: Guid.NewGuid().ToString(), role: Lookups.UserRoles.Admin, tenantsId: tenantsId);
        for (var request = 0; request < 96; request++)
        {
            using var lease = policy.Limiter.AttemptAcquire(filler);
            if (!lease.IsAcquired)
            {
                break;
            }
        }
    }
    var freshUser = BuildContext("/ticketspan.event.EventService/ListEvents",
        subject: Guid.NewGuid().ToString(), role: Lookups.UserRoles.Admin, tenantsId: tenantsId);
    var remaining = policy.ReadRemainingPermits(freshUser);
    Check(failures, "fresh user in a drained tenant reports the tenant remainder, not 100",
        remaining == 40, $"remaining was {remaining}");
    Check(failures, "effective bucket reports the tenant limit",
        policy.ResolveEffectiveBucket(freshUser)?.PermitLimit == 1000,
        $"limit was {policy.ResolveEffectiveBucket(freshUser)?.PermitLimit}");
}

Console.WriteLine();
if (failures.Count > 0)
{
    Console.WriteLine($"FAILED ({failures.Count}): {string.Join(", ", failures)}");
    return 1;
}
Console.WriteLine("All rate limit checks passed.");
return 0;
