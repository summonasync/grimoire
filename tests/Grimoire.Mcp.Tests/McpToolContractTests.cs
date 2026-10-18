using System.Text.Json;
using Grimoire.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class McpToolContractTests
{
    private static readonly string[] ToolNames =
    [
        "memory_compact_candidates",
        "memory_forget",
        "memory_list",
        "memory_persona_get",
        "memory_recall",
        "memory_remember",
        "memory_stats",
    ];

    public static TheoryData<string, string[]> TeachingKeywords()
    {
        var data = new TheoryData<string, string[]>();
        data.Add("memory_remember", ["projectId", "supersedes", "Never store secrets"]);
        data.Add("memory_recall", ["session start", "before you assume"]);
        data.Add("memory_forget", ["tombstone", "forgotten=false"]);
        data.Add("memory_list", ["newest first", "active"]);
        data.Add("memory_persona_get", ["identity, tone, preferences, rules", "were never filled"]);
        data.Add("memory_compact_candidates", ["pendingCompaction", "memory_forget", "distill"]);
        data.Add("memory_stats", ["pendingCompaction", "memory_compact_candidates"]);
        return data;
    }

    public static TheoryData<string, string[], string[]> ParameterContracts()
    {
        var data = new TheoryData<string, string[], string[]>();
        data.Add("memory_remember", ["content", "scope"], ["kind", "projectId", "sessionId", "tags", "importance"]);
        data.Add("memory_recall", ["query"], ["scope", "kind", "projectId", "sessionId", "tags", "limit"]);
        data.Add("memory_forget", ["memoryId", "reason"], []);
        data.Add("memory_list", [], ["scope", "kind", "status", "projectId", "sessionId", "tags", "limit"]);
        data.Add("memory_persona_get", [], []);
        data.Add("memory_compact_candidates", [], []);
        data.Add("memory_stats", [], []);
        return data;
    }

    public static TheoryData<string, string, string[]> EnumContracts()
    {
        var data = new TheoryData<string, string, string[]>();
        data.Add("memory_remember", "scope", ["Session", "Project", "Global", "Personal"]);
        data.Add("memory_remember", "kind", ["Journal", "Fact"]);
        data.Add("memory_list", "status", ["Active", "Superseded", "Tombstoned"]);
        return data;
    }

    public static TheoryData<string, string, string> TypeContracts()
    {
        var data = new TheoryData<string, string, string>();
        data.Add("memory_remember", "content", "string");
        data.Add("memory_remember", "tags", "array");
        data.Add("memory_remember", "importance", "number");
        data.Add("memory_recall", "query", "string");
        data.Add("memory_recall", "limit", "integer");
        data.Add("memory_forget", "memoryId", "string");
        data.Add("memory_list", "limit", "integer");
        return data;
    }

    [Fact]
    public async Task ExposesExactlyTheSevenContractedTools()
    {
        await using var session = await McpTestSession.StartAsync();

        var names = await session.Client.ListToolNames();

        names.OrderBy(name => name, StringComparer.Ordinal).ShouldBe(ToolNames);
    }

    [Theory]
    [MemberData(nameof(TeachingKeywords))]
    public async Task Descriptions_KeepTheirTeachingKeywords(string tool, string[] keywords)
    {
        await using var session = await McpTestSession.StartAsync();

        var tools = await session.Client.ListToolMap();

        tools[tool].Description.ShouldNotBeNull();
        foreach (var keyword in keywords)
        {
            tools[tool].Description.ShouldContain(keyword, customMessage: $"tool '{tool}' description drifted");
        }
    }

    [Theory]
    [MemberData(nameof(ParameterContracts))]
    public async Task Parameters_FollowTheContract(string tool, string[] required, string[] optional)
    {
        await using var session = await McpTestSession.StartAsync();

        var schema = (await session.Client.ListToolMap())[tool].JsonSchema;
        var properties = PropertyNames(schema);

        Required(schema).OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(required.OrderBy(name => name, StringComparer.Ordinal));
        properties.OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(required.Concat(optional).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(EnumContracts))]
    public async Task EnumParameters_SpellOutThePayloadValues(string tool, string parameter, string[] expected)
    {
        await using var session = await McpTestSession.StartAsync();

        var property = Property((await session.Client.ListToolMap())[tool].JsonSchema, parameter);

        property.GetProperty("type").GetString().ShouldBe("string");
        property.GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString()!)
            .ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(TypeContracts))]
    public async Task Parameters_KeepTheirWireTypes(string tool, string parameter, string expectedType)
    {
        await using var session = await McpTestSession.StartAsync();

        var property = Property((await session.Client.ListToolMap())[tool].JsonSchema, parameter);
        var type = property.GetProperty("type");

        if (type.ValueKind == JsonValueKind.Array)
        {
            type.EnumerateArray().ShouldContain(value => value.GetString() == expectedType);
            return;
        }

        type.GetString().ShouldBe(expectedType);
    }

    [Fact]
    public async Task ServerInstructions_TeachTheStartupAndCompactionLoop()
    {
        await using var session = await McpTestSession.StartAsync();

        var instructions = session.Client.ServerInstructions;

        instructions.ShouldNotBeNull();
        instructions.ShouldContain("memory_persona_get");
        instructions.ShouldContain("memory_recall");
        instructions.ShouldContain("memory_remember");
        instructions.ShouldContain("pendingCompaction");
    }

    private static string[] Required(JsonElement schema)
    {
        return schema.TryGetProperty("required", out var required)
            ? [.. required.EnumerateArray().Select(value => value.GetString()!)]
            : [];
    }

    private static string[] PropertyNames(JsonElement schema)
    {
        return schema.TryGetProperty("properties", out var properties)
            ? [.. properties.EnumerateObject().Select(property => property.Name)]
            : [];
    }

    private static JsonElement Property(JsonElement schema, string name)
    {
        return schema.GetProperty("properties").GetProperty(name);
    }
}
