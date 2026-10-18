using System.Text.Json;
using Grimoire.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class McpToolContractTests
{
    private static readonly string[] ToolNames =
    [
        "memory_brief",
        "memory_compact_candidates",
        "memory_forget",
        "memory_history",
        "memory_list",
        "memory_persona_get",
        "memory_recall",
        "memory_remember",
        "memory_restore",
        "memory_scry",
        "memory_stats",
    ];

    public static TheoryData<string, string[]> TeachingKeywords()
    {
        var data = new TheoryData<string, string[]>();
        data.Add("memory_remember", ["projectId", "supersedes", "Never store secrets", "tome"]);
        data.Add("memory_recall", ["session start", "before you assume", "tome"]);
        data.Add("memory_forget", ["tombstone", "forgotten=false", "tome"]);
        data.Add("memory_restore", ["undo door", "one active truth", "restored=false", "correction trail", "tome"]);
        data.Add("memory_history", ["replacement lineage", "oldest to newest", "found=false", "read-only", "tome"]);
        data.Add("memory_list", ["newest first", "active", "tome"]);
        data.Add("memory_persona_get", ["identity, tone, preferences, rules", "were never filled", "per-tome"]);
        data.Add("memory_compact_candidates", ["pendingCompaction", "memory_forget", "distill", "tome"]);
        data.Add("memory_stats", ["pendingCompaction", "memory_compact_candidates", "byTome"]);
        data.Add("memory_brief", ["session start", "omitted", "tombstone", "cold start", "upstream_unavailable", "memory_scry"]);
        data.Add("memory_scry", ["OTHER tomes", "memory_recall", "journal", "anonymous", "session", "read-only"]);
        return data;
    }

    public static TheoryData<string, string[], string[]> ParameterContracts()
    {
        var data = new TheoryData<string, string[], string[]>();
        data.Add("memory_remember", ["content", "scope"], ["kind", "projectId", "sessionId", "tags", "importance", "tome"]);
        data.Add("memory_recall", ["query"], ["scope", "kind", "projectId", "sessionId", "tags", "limit", "tome"]);
        data.Add("memory_forget", ["memoryId", "reason"], ["tome"]);
        data.Add("memory_restore", ["memoryId", "reason"], ["tome"]);
        data.Add("memory_history", ["memoryId"], ["tome"]);
        data.Add("memory_list", [], ["scope", "kind", "status", "projectId", "sessionId", "tags", "limit", "tome"]);
        data.Add("memory_persona_get", [], ["tome"]);
        data.Add("memory_compact_candidates", [], ["tome"]);
        data.Add("memory_stats", [], ["tome"]);
        data.Add("memory_brief", [], ["projectId", "scopes", "factsPerScope", "journalPerScope", "tombstoneLimit", "tome"]);
        data.Add("memory_scry", ["query"], ["tomes", "includeJournal", "attribution", "limit"]);
        return data;
    }

    public static TheoryData<string, string, string[]> EnumContracts()
    {
        var data = new TheoryData<string, string, string[]>();
        data.Add("memory_remember", "scope", ["Session", "Project", "Global", "Personal"]);
        data.Add("memory_remember", "kind", ["Journal", "Fact"]);
        data.Add("memory_list", "status", ["Active", "Superseded", "Tombstoned"]);
        data.Add("memory_scry", "attribution", ["None", "Light", "Full"]);
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
        data.Add("memory_recall", "tome", "string");
        data.Add("memory_forget", "memoryId", "string");
        data.Add("memory_restore", "memoryId", "string");
        data.Add("memory_restore", "reason", "string");
        data.Add("memory_history", "memoryId", "string");
        data.Add("memory_list", "limit", "integer");
        data.Add("memory_brief", "scopes", "array");
        data.Add("memory_brief", "factsPerScope", "integer");
        data.Add("memory_scry", "query", "string");
        data.Add("memory_scry", "tomes", "array");
        data.Add("memory_scry", "includeJournal", "boolean");
        data.Add("memory_scry", "limit", "integer");
        return data;
    }

    [Fact]
    public async Task ExposesExactlyTheElevenContractedTools()
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
