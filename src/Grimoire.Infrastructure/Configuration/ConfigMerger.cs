using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Grimoire.Infrastructure.Configuration;

internal static class ConfigMerger
{
    private static readonly string[] SectionSeparators = ["__"];

    public static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (name, value) in source)
        {
            if (!target.ContainsKey(name))
            {
                throw new ConfigException($"Unknown configuration property '{name}'.");
            }

            if (target[name] is JsonObject targetSection && value is JsonObject sourceSection)
            {
                Merge(targetSection, sourceSection);
            }
            else
            {
                target[name] = value?.DeepClone();
            }
        }
    }

    public static void ApplyOverrides(JsonObject root, IReadOnlyDictionary<string, string> overrides, string prefix)
    {
        foreach (var (name, value) in overrides
                     .Where(entry => entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            Apply(root, name, name[prefix.Length..], value);
        }
    }

    private static void Apply(JsonObject root, string fullName, string path, string value)
    {
        var segments = path.Split(SectionSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ConfigException($"Environment override '{fullName}' carries no property path.");
        }

        var current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var sectionName = ResolveName(current, segments[i], fullName);
            if (current[sectionName] is not JsonObject next)
            {
                throw new ConfigException(
                    $"Environment override '{fullName}' targets '{segments[i]}' which is not a section.");
            }

            current = next;
        }

        var leaf = ResolveName(current, segments[^1], fullName);
        current[leaf] = Coerce(leaf, fullName, value, current[leaf], segments);
    }

    private static string ResolveName(JsonObject section, string segment, string fullName)
    {
        foreach (var pair in section)
        {
            if (pair.Key.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        throw new ConfigException($"Environment override '{fullName}' does not match any configuration property.");
    }

    private static JsonNode Coerce(string property, string fullName, string value, JsonNode? existing, string[] segments)
    {
        if (IsNullValue(existing))
        {
            return ConfigSchema.KindOf(segments) switch
            {
                ConfigValueKind.Number when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var nullableNumber)
                    => JsonValue.Create(nullableNumber),
                ConfigValueKind.Number => throw new ConfigException(
                    $"Environment override '{fullName}' must be a number for '{property}'."),
                ConfigValueKind.Boolean when bool.TryParse(value, out var nullableFlag)
                    => JsonValue.Create(nullableFlag),
                ConfigValueKind.Boolean => throw new ConfigException(
                    $"Environment override '{fullName}' must be a boolean for '{property}'."),
                _ => JsonValue.Create(value),
            };
        }

        if (existing is JsonValue candidate && candidate.TryGetValue<decimal>(out _))
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return JsonValue.Create(number);
            }

            throw new ConfigException($"Environment override '{fullName}' must be a number for '{property}'.");
        }

        if (existing is JsonValue flag && flag.TryGetValue<bool>(out _))
        {
            if (bool.TryParse(value, out var parsed))
            {
                return JsonValue.Create(parsed);
            }

            throw new ConfigException($"Environment override '{fullName}' must be a boolean for '{property}'.");
        }

        return JsonValue.Create(value);
    }

    // A nullable option serializes its null default; the node kind carries no type hint,
    // so a numeric-looking override becomes a number and everything else stays a string.
    private static bool IsNullValue(JsonNode? node) =>
        node is null || (node is JsonValue value && value.GetValueKind() == JsonValueKind.Null);
}
