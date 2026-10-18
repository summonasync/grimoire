using System.Security.Cryptography;
using System.Text;

namespace Grimoire.Core;

public static class IdempotencyKey
{
    private const string Tome = "default";

    public static string Compute(RememberRequest request)
    {
        var payload = new StringBuilder();
        AppendPart(payload, Tome);
        AppendPart(payload, request.Scope.ToString());
        AppendPart(payload, request.Kind.ToString());
        AppendPart(payload, request.Actor.Alias);
        AppendPart(payload, request.Actor.Vendor ?? string.Empty);
        AppendPart(payload, request.ProjectId ?? string.Empty);
        AppendPart(payload, request.SessionId ?? string.Empty);
        AppendPart(payload, request.Content);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()))).ToLowerInvariant();
    }

    private static void AppendPart(StringBuilder payload, string part)
    {
        payload.Append(Encoding.UTF8.GetByteCount(part));
        payload.Append(':');
        payload.Append(part);
    }
}
