using System.Text.Json;
using System.Text.Json.Serialization;
using Grimoire.Core;

namespace Grimoire.Api.Tests;

internal static class TestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
