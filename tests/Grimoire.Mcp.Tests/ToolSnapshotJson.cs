using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;

namespace Grimoire.Mcp.Tests;

internal static class ToolSnapshotJson
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static JsonObject DescribeContract(McpClientTool tool)
    {
        var annotations = tool.ProtocolTool.Annotations;
        var parameters = new JsonObject();
        if (tool.JsonSchema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                parameters.Add(property.Name, DescribeParameter(property.Value));
            }
        }

        return new JsonObject
        {
            ["name"] = tool.Name,
            ["title"] = tool.Title,
            ["description"] = tool.Description,
            ["readOnly"] = annotations?.ReadOnlyHint,
            ["destructive"] = annotations?.DestructiveHint,
            ["idempotent"] = annotations?.IdempotentHint,
            ["input"] = new JsonObject
            {
                ["parameters"] = parameters,
                ["required"] = RequiredNames(tool.JsonSchema),
            },
        };
    }

    public static JsonObject DescribeShape(JsonElement payload)
    {
        return DescribeObject(payload);
    }

    public static string Canonical(JsonNode? node)
    {
        return SortedClone(node).ToJsonString(Indented) + "\n";
    }

    private static JsonObject DescribeParameter(JsonElement property)
    {
        var described = new JsonObject();
        if (property.TryGetProperty("default", out var defaultValue))
        {
            described["default"] = JsonNode.Parse(defaultValue.GetRawText());
        }

        if (property.TryGetProperty("type", out var type))
        {
            described["type"] = JsonNode.Parse(type.GetRawText());
        }

        return described;
    }

    private static JsonArray RequiredNames(JsonElement schema)
    {
        var names = new JsonArray();
        if (schema.TryGetProperty("required", out var required))
        {
            var observed = required.EnumerateArray()
                .Select(value => value.GetString())
                .OrderBy(name => name, StringComparer.Ordinal);
            foreach (var name in observed)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static JsonObject DescribeObject(JsonElement payload)
    {
        var shape = new JsonObject();
        foreach (var field in payload.EnumerateObject().OrderBy(field => field.Name, StringComparer.Ordinal))
        {
            shape.Add(field.Name, DescribeValue(field.Value));
        }

        return shape;
    }

    private static JsonNode DescribeValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Array => DescribeArray(value),
            JsonValueKind.Object => DescribeObject(value),
            JsonValueKind.String => JsonValue.Create("string")!,
            JsonValueKind.Number => JsonValue.Create("number")!,
            JsonValueKind.True or JsonValueKind.False => JsonValue.Create("boolean")!,
            _ => JsonValue.Create("null")!,
        };
    }

    private static JsonObject DescribeArray(JsonElement array)
    {
        var first = array.EnumerateArray().FirstOrDefault();
        return new JsonObject
        {
            ["arrayOf"] = first.ValueKind == JsonValueKind.Undefined
                ? JsonValue.Create("unobserved")
                : DescribeValue(first),
        };
    }

    private static JsonNode? SortedClone(JsonNode? node)
    {
        return node switch
        {
            JsonObject @object => SortObject(@object),
            JsonArray array => SortArray(array),
            _ => node?.DeepClone(),
        };
    }

    private static JsonObject SortObject(JsonObject @object)
    {
        var sorted = new JsonObject();
        foreach (var property in @object.OrderBy(property => property.Key, StringComparer.Ordinal))
        {
            sorted.Add(property.Key, SortedClone(property.Value));
        }

        return sorted;
    }

    private static JsonArray SortArray(JsonArray array)
    {
        var sorted = new JsonArray();
        foreach (var item in array)
        {
            sorted.Add(SortedClone(item));
        }

        return sorted;
    }
}
