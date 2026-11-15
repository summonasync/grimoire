using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Grimoire.Core;

namespace Grimoire.Infrastructure.Configuration;

public sealed class ConfigLoader(string? path = null, IReadOnlyDictionary<string, string>? environment = null)
{
    private static readonly JsonSerializerOptions NodeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // An explicit JSON null for a non-nullable option must fail here, not as a later null reference.
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private const string EnvironmentPrefix = "GRIMOIRE_";

    // Reserved for the faces (actor, tome, install home root); never a configuration property.
    private static readonly IReadOnlySet<string> ReservedVariables = new HashSet<string>(
        ["GRIMOIRE_ACTOR", "GRIMOIRE_TOME", "GRIMOIRE_HOME"], StringComparer.OrdinalIgnoreCase);

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".grimoire",
        "config.json");

    private readonly string _path = path ?? DefaultPath;
    private readonly IReadOnlyDictionary<string, string> _environment = environment ?? SnapshotEnvironment();

    public GrimoireOptions Load()
    {
        var root = JsonSerializer.SerializeToNode(new GrimoireOptions(), NodeOptions)!.AsObject();
        MergeFile(root);
        ConfigMerger.ApplyOverrides(root, NonReserved(_environment), EnvironmentPrefix);
        var options = Deserialize(root);
        EnsureProjectRegistryValid(options.Projects);
        EnsureQdrantCollectionsValid(options.Qdrant);
        return options;
    }

    private static void EnsureProjectRegistryValid(IReadOnlyList<ProjectAliasRecord> projects)
    {
        var errors = ProjectRegistryValidator.Validate(projects);
        if (errors.Count > 0)
        {
            throw new ConfigException($"Project registry is invalid: {string.Join(" ", errors)}");
        }
    }

    private static void EnsureQdrantCollectionsValid(QdrantOptions qdrant)
    {
        var errors = QdrantCollectionsValidator.Validate(qdrant);
        if (errors.Count > 0)
        {
            throw new ConfigException($"Qdrant collections are invalid: {string.Join(" ", errors)}");
        }
    }

    private void MergeFile(JsonObject root)
    {
        if (!File.Exists(_path))
        {
            return;
        }

        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (IOException ex)
        {
            throw new ConfigException($"Config file '{_path}' could not be read: {ex.Message}");
        }

        JsonNode? fileNode;
        try
        {
            fileNode = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Config file '{_path}' is not valid JSON: {ex.Message}");
        }

        if (fileNode is null)
        {
            return;
        }

        if (fileNode is not JsonObject fileRoot)
        {
            throw new ConfigException($"Config file '{_path}' must contain a JSON object at the root.");
        }

        ConfigMerger.Merge(root, fileRoot);
    }

    private static GrimoireOptions Deserialize(JsonObject root)
    {
        try
        {
            return root.Deserialize<GrimoireOptions>(NodeOptions) ?? new GrimoireOptions();
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Configuration is invalid: {ex.Message}");
        }
    }

    private static IReadOnlyDictionary<string, string> SnapshotEnvironment() =>
        Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Where(entry => entry.Value is string)
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);

    private static IReadOnlyDictionary<string, string> NonReserved(IReadOnlyDictionary<string, string> overrides) => overrides
        .Where(entry => !ReservedVariables.Contains(entry.Key))
        .ToDictionary(entry => entry.Key, entry => entry.Value);
}
