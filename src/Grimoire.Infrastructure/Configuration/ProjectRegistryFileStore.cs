using System.Text.Json;
using System.Text.Json.Nodes;
using Grimoire.Core;

namespace Grimoire.Infrastructure.Configuration;

public sealed class ProjectRegistryFileStore(string? path = null) : IProjectRegistryStore
{
    private static readonly JsonSerializerOptions Root = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions Records = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path = path ?? ConfigLoader.DefaultPath;

    public IReadOnlyList<ProjectAliasRecord> Read()
    {
        var (text, existed) = ReadText();
        return existed ? ReadSection(ParseRoot(text!)) : [];
    }

    public string Mutate(Func<List<ProjectAliasRecord>, string> mutate)
    {
        var (previous, existed) = ReadText();
        var root = existed ? ParseRoot(previous!) : DefaultsRoot();
        var records = new List<ProjectAliasRecord>(ReadSection(root));
        var report = mutate(records);
        var errors = ProjectRegistryValidator.Validate(records);
        if (errors.Count > 0)
        {
            throw new ProjectRegistryMutationException($"Project registry is invalid: {string.Join(" ", errors)}");
        }

        root["projects"] = JsonSerializer.SerializeToNode(records, Records);
        WriteFile(_path, root.ToJsonString(Root));
        try
        {
            _ = new ConfigLoader(_path).Load();
        }
        catch (ConfigException)
        {
            Rollback(previous, existed);
            throw;
        }

        return report;
    }

    private (string? Text, bool Existed) ReadText() =>
        File.Exists(_path) ? (File.ReadAllText(_path), true) : (null, false);

    private JsonObject ParseRoot(string text) =>
        JsonNode.Parse(text) as JsonObject
        ?? throw new ConfigException($"Config file '{_path}' must contain a JSON object at the root.");

    private static JsonObject DefaultsRoot() =>
        JsonSerializer.SerializeToNode(new GrimoireOptions(), Root)!.AsObject();

    private static IReadOnlyList<ProjectAliasRecord> ReadSection(JsonObject root) =>
        root["projects"] is JsonArray array
            ? array.Deserialize<List<ProjectAliasRecord>>(Records) ?? []
            : [];

    private void Rollback(string? previous, bool existed)
    {
        if (existed)
        {
            WriteFile(_path, previous!);
        }
        else
        {
            File.Delete(_path);
        }
    }

    private static void WriteFile(string path, string text)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, text);
    }
}
