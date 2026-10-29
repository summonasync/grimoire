using System.Text.Json;
using System.Text.Json.Serialization;
using Grimoire.Core;
using Grimoire.Infrastructure.Processes;

namespace Grimoire.Infrastructure.Identity;

public sealed class AgentIdentityFileReader(string? home = null, int? parentPid = null) : IAgentIdentitySource
{
    public const int MaxAliasLength = 32;

    public MemoryActor? Get()
    {
        var pid = parentPid ?? ParentProcessId.Resolve();
        var root = pid is null ? null : home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (root is null or "")
        {
            return null;
        }

        string payload;
        try
        {
            payload = File.ReadAllText(
                Path.Combine(root, ".overmind", "identity", $"parent-{pid}.json"));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            return Actor(JsonSerializer.Deserialize<IdentityFilePayload>(payload));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MemoryActor? Actor(IdentityFilePayload? payload)
    {
        var alias = payload?.Alias?.Trim();
        if (alias is null or "" || alias.Length > MaxAliasLength || alias.Any(char.IsWhiteSpace))
        {
            return null;
        }

        var vendor = payload!.Vendor?.Trim();
        return new MemoryActor(alias, string.IsNullOrEmpty(vendor) ? null : vendor);
    }

    private sealed record IdentityFilePayload(
        [property: JsonPropertyName("alias")] string? Alias,
        [property: JsonPropertyName("vendor")] string? Vendor,
        [property: JsonPropertyName("sessionId")] Guid? SessionId,
        [property: JsonPropertyName("registeredAt")] DateTimeOffset? RegisteredAt);
}
