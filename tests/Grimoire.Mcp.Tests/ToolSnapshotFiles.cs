using System.Text.Json.Nodes;

namespace Grimoire.Mcp.Tests;

internal static class ToolSnapshotFiles
{
    private const string UpdateVariable = "GRIMOIRE_UPDATE_TOOL_SNAPSHOTS";

    private static readonly string SnapshotDirectory = ResolveDirectory();

    public static bool UpdateMode => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    public static IReadOnlyList<string> PinnedToolNames()
    {
        return [.. Directory.EnumerateFiles(SnapshotDirectory, "*.json")
            .Select(file => Path.GetFileNameWithoutExtension(file)!)
            .OrderBy(name => name, StringComparer.Ordinal)];
    }

    public static JsonObject LoadContract(string tool)
    {
        return Section(tool, "contract");
    }

    public static JsonObject LoadShape(string tool)
    {
        return Section(tool, "output");
    }

    public static void SaveContract(string tool, JsonObject contract)
    {
        SaveSection(tool, "contract", contract);
    }

    public static void SaveShape(string tool, JsonObject shape)
    {
        SaveSection(tool, "output", shape);
    }

    public static (IReadOnlyList<string> Unpinned, IReadOnlyList<string> Orphan) CompareRoster(
        IEnumerable<string> registered,
        IEnumerable<string> pinned)
    {
        var pinnedNames = pinned.ToHashSet(StringComparer.Ordinal);
        var registeredNames = registered.ToHashSet(StringComparer.Ordinal);
        return (
            [.. registeredNames.Except(pinnedNames).OrderBy(name => name, StringComparer.Ordinal)],
            [.. pinnedNames.Except(registeredNames).OrderBy(name => name, StringComparer.Ordinal)]);
    }

    private static JsonObject Section(string tool, string section)
    {
        var document = Read(tool)
            ?? throw new InvalidOperationException($"snapshot '{tool}.json' is not a JSON object.");
        return document[section] as JsonObject
            ?? throw new InvalidOperationException($"snapshot '{tool}.json' has no '{section}' section.");
    }

    private static void SaveSection(string tool, string section, JsonObject data)
    {
        var document = Read(tool) ?? new JsonObject();
        document[section] = data.DeepClone();
        File.WriteAllText(FileFor(tool), ToolSnapshotJson.Canonical(document));
    }

    private static JsonObject? Read(string tool)
    {
        if (!File.Exists(FileFor(tool)))
        {
            return null;
        }

        return JsonNode.Parse(File.ReadAllText(FileFor(tool))) as JsonObject
            ?? throw new InvalidOperationException($"snapshot '{tool}.json' is not a JSON object.");
    }

    private static string FileFor(string tool)
    {
        return Path.Combine(SnapshotDirectory, tool + ".json");
    }

    private static string ResolveDirectory()
    {
        var candidate = AppContext.BaseDirectory;
        for (var depth = 0; depth < 6; depth++)
        {
            var snapshots = Path.Combine(candidate, "TestData", "snapshots");
            if (Directory.Exists(snapshots))
            {
                return snapshots;
            }

            candidate = Path.GetFullPath(Path.Combine(candidate, ".."));
        }

        throw new InvalidOperationException(
            "TestData/snapshots was not found above the test binaries; contract snapshots live in the Grimoire.Mcp.Tests project.");
    }
}
