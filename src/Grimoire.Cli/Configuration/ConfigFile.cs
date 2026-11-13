using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grimoire.Infrastructure.Configuration;

namespace Grimoire.Cli.Configuration;

internal static class ConfigFile
{
    internal static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static bool WriteDefaultIfMissing(string path)
    {
        if (File.Exists(path))
        {
            return false;
        }

        Write(path, JsonSerializer.Serialize(new GrimoireOptions(), WriteOptions));
        return true;
    }

    public static void Write(string path, string text)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, text);
    }

    public static (string? Text, bool Existed) Read(string path) =>
        File.Exists(path) ? (File.ReadAllText(path), true) : (null, false);

    public static JsonObject DefaultsRoot() =>
        JsonSerializer.SerializeToNode(new GrimoireOptions(), WriteOptions)!.AsObject();

    public static JsonObject ParseRoot(string text, string path)
    {
        if (JsonNode.Parse(text) is not JsonObject root)
        {
            throw new CommandException("config", $"Config file '{path}' must contain a JSON object at the root.", ExitCodes.Config);
        }

        return root;
    }

    public static void ApplyValue(JsonObject root, JsonObject schema, string key, string value)
    {
        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            throw new CommandException("config", $"Configuration keys look like 'section.property', got '{key}'.", ExitCodes.Config);
        }

        var node = root;
        var schemaNode = schema;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            var schemaName = Resolve(schemaNode, segments[index])
                ?? throw new CommandException("config", $"Unknown configuration section '{segments[index]}' in '{key}'.", ExitCodes.Config);
            if (schemaNode[schemaName] is not JsonObject schemaSection)
            {
                throw new CommandException("config", $"Configuration property '{string.Join('.', segments[..(index + 1)])}' is not a section.", ExitCodes.Config);
            }

            var nodeName = Resolve(node, segments[index]) ?? schemaName;
            if (node[nodeName] is not JsonObject nextNode)
            {
                node[nodeName] = nextNode = [];
            }

            node = nextNode;
            schemaNode = schemaSection;
        }

        var leafName = Resolve(schemaNode, segments[^1])
            ?? throw new CommandException("config", $"Unknown configuration property '{key}'.", ExitCodes.Config);
        if (schemaNode[leafName] is not JsonValue && schemaNode[leafName] is not null)
        {
            throw new CommandException("config", $"Configuration property '{key}' is a section, not a value.", ExitCodes.Config);
        }

        var targetName = Resolve(node, segments[^1]) ?? leafName;
        node[targetName] = Coerce(schemaNode[leafName], segments, key, value);
    }

    private static JsonValue Coerce(JsonNode? schema, string[] segments, string key, string value)
    {
        if (schema is null || (schema is JsonValue nullish && nullish.GetValueKind() == JsonValueKind.Null))
        {
            return ConfigSchema.KindOf(segments) switch
            {
                ConfigValueKind.Number when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var nullableNumber)
                    => JsonValue.Create(nullableNumber)!,
                ConfigValueKind.Number => throw new CommandException(
                    "config", $"Configuration property '{key}' must be a number, got '{value}'.", ExitCodes.Config),
                ConfigValueKind.Boolean when bool.TryParse(value, out var nullableFlag)
                    => JsonValue.Create(nullableFlag)!,
                ConfigValueKind.Boolean => throw new CommandException(
                    "config", $"Configuration property '{key}' must be a boolean, got '{value}'.", ExitCodes.Config),
                _ => JsonValue.Create(value)!,
            };
        }

        if (schema is JsonValue box && box.TryGetValue<decimal>(out _))
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? JsonValue.Create(number)!
                : throw new CommandException("config", $"Configuration property '{key}' must be a number, got '{value}'.", ExitCodes.Config);
        }

        if (schema is JsonValue flag && flag.TryGetValue<bool>(out _))
        {
            return bool.TryParse(value, out var parsed)
                ? JsonValue.Create(parsed)!
                : throw new CommandException("config", $"Configuration property '{key}' must be a boolean, got '{value}'.", ExitCodes.Config);
        }

        return JsonValue.Create(value)!;
    }

    private static string? Resolve(JsonObject node, string segment)
    {
        foreach (var pair in node)
        {
            if (pair.Key.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return null;
    }
}
