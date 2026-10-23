using System.Text.Json;
using System.Text.Json.Nodes;
using Grimoire.Core;

namespace Grimoire.Cli.Configuration;

internal static class ProjectRegistrySection
{
    private static readonly JsonSerializerOptions Records = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<ProjectAliasRecord> Read(JsonObject root) =>
        root["projects"] is JsonArray array
            ? array.Deserialize<List<ProjectAliasRecord>>(Records) ?? []
            : [];

    public static void Write(JsonObject root, IReadOnlyList<ProjectAliasRecord> records) =>
        root["projects"] = JsonSerializer.SerializeToNode(records, Records);
}
