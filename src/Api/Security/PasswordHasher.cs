using System.Security.Cryptography;
using System.Text;

namespace TicketSpan.Api.Security;

public sealed class PasswordHasher
{
    private readonly IReadOnlyDictionary<short, byte[]> peppers;
    private readonly short currentVersion;
    private readonly int workFactor;

    public PasswordHasher(IConfiguration configuration)
    {
        var map = new Dictionary<short, byte[]>();
        short highest = 0;
        for (short version = 1; version <= 16; version++)
        {
            var secret = configuration[$"PASSWORD_PEPPER_V{version}"];
            if (!string.IsNullOrEmpty(secret))
            {
                map[version] = Encoding.UTF8.GetBytes(secret);
                highest = version;
            }
        }
        if (map.Count == 0)
        {
            var fallback = configuration["PASSWORD_PEPPER_V1"] ?? "local-development-pepper-change-me";
            map[1] = Encoding.UTF8.GetBytes(fallback);
            highest = 1;
        }
        peppers = map;
        currentVersion = configuration["PASSWORD_PEPPER_CURRENT"] is { Length: > 0 } c
            ? short.Parse(c)
            : highest;

        var wfConfig = configuration["BCRYPT_WORK_FACTOR"];
        workFactor = int.TryParse(wfConfig, out var parsedWf) && parsedWf >= 4 && parsedWf <= 31
            ? parsedWf
            : 10;
    }

    public short CurrentVersion => currentVersion;

    public string Hash(string password)
    {
        return Hash(password, currentVersion);
    }

    public Task<string> HashAsync(string password)
    {
        return Task.Run(() => Hash(password));
    }

    public string Hash(string password, short pepperVersion)
    {
        var peppered = ApplyPepper(password, pepperVersion);
        return BCrypt.Net.BCrypt.EnhancedHashPassword(peppered, workFactor);
    }

    public Task<string> HashAsync(string password, short pepperVersion)
    {
        return Task.Run(() => Hash(password, pepperVersion));
    }

    public bool Verify(string password, string hash, short pepperVersion)
    {
        if (!peppers.ContainsKey(pepperVersion))
        {
            return false;
        }
        var peppered = ApplyPepper(password, pepperVersion);
        return BCrypt.Net.BCrypt.EnhancedVerify(peppered, hash);
    }

    public Task<bool> VerifyAsync(string password, string hash, short pepperVersion)
    {
        return Task.Run(() => Verify(password, hash, pepperVersion));
    }

    public bool NeedsRehash(short pepperVersion) => pepperVersion != currentVersion;

    private string ApplyPepper(string password, short pepperVersion)
    {
        var key = peppers[pepperVersion];
        using var hmac = new HMACSHA256(key);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
