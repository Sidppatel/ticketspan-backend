using System.Security.Cryptography;
using System.Text;

namespace TicketSpan.Api.Security;

public static class EmailHasher
{
    public static string Hash(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
