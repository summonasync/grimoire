using System.Text.Json;
using System.Text.Json.Nodes;
using Grimoire.Core;
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
        "memory_digest",
        "memory_forget",
        "memory_history",
        "memory_list",
        "memory_note",
        "memory_persona_get",
        "memory_recall",
        "memory_remember",
        "memory_restore",
        "memory_scry",
        "memory_stats",
        "projects",
    ];

    public static TheoryData<string, string[]> TeachingKeywords()
    {
        var data = new TheoryData<string, string[]>();
        data.Add("memory_remember", ["supersedes", "nearDuplicates", "actor provenance", "never store secrets", "tome", "durable facts in English", "quotes as spoken"]);
        data.Add("memory_recall", ["before you assume", "memory_scry", "tome", "access stats", "narrow with scope"]);
        data.Add("memory_forget", ["tombstone", "forgotten=false", "tome"]);
        data.Add("memory_restore", ["undo door", "one active truth", "restored=false", "correction trail", "tome", "converges"]);
        data.Add("memory_history", ["replacement lineage", "oldest to newest", "found=false", "read-only", "tome", "120-char"]);
        data.Add("memory_list", ["newest first", "active", "tome"]);
        data.Add("memory_note", ["topic:", "Journal", "0.5", "nearDuplicates", "replays idempotently", "memory_remember"]);
        data.Add("memory_digest", ["longMemo", "recent", "gone", "tag", "similar", "omitted", "truncated", "bump access stats", "compact"]);
        data.Add("memory_persona_get", ["identity, tone, preferences, rules", "never filled", "human-curated"]);
        data.Add("memory_compact_candidates", ["pendingCompaction", "memory_forget", "distill", "tome"]);
        data.Add("memory_stats", ["pendingCompaction", "byTome", "gc", "stale=true", "whole store"]);
        data.Add("memory_brief", ["startup contract", "omitted", "cold start", "hint", "truncated"]);
        data.Add("memory_scry", ["OTHER tomes", "cross-tome", "access bump", "session rows never cross", "includeJournal", "anonymous"]);
        data.Add("projects", ["strict-case", "owner config", "kebab-case", "absolute", "whole path list", "never deletes"]);
        return data;
    }

    public static TheoryData<string, string[], string[]> ParameterContracts()
    {
        var data = new TheoryData<string, string[], string[]>();
        data.Add("memory_remember", ["content", "scope"], ["kind", "projectId", "sessionId", "tags", "importance", "tome", "supersedes"]);
        data.Add("memory_recall", ["query"], ["scope", "kind", "projectId", "sessionId", "tags", "since", "until", "actor", "limit", "tome", "fields", "compact"]);
        data.Add("memory_forget", ["memoryId", "reason"], ["tome"]);
        data.Add("memory_restore", ["memoryId", "reason"], ["tome"]);
        data.Add("memory_history", ["memoryId"], ["tome"]);
        data.Add("memory_list", [], ["scope", "kind", "status", "projectId", "sessionId", "tags", "since", "until", "actor", "limit", "tome", "fields", "compact"]);
        data.Add("memory_note", ["content"], ["scope", "projectId", "sessionId", "topic", "tags", "tome"]);
        data.Add("memory_digest", ["topic"], ["tome", "since", "until", "limit", "fields", "compact"]);
        data.Add("memory_persona_get", [], ["tome"]);
        data.Add("memory_compact_candidates", [], ["tome"]);
        data.Add("memory_stats", [], ["tome", "includeTomeCompaction"]);
        data.Add("memory_brief", [], ["projectId", "scopes", "factsPerScope", "journalPerScope", "tombstoneLimit", "tome"]);
        data.Add("memory_scry", ["query"], ["tomes", "includeJournal", "attribution", "limit"]);
        data.Add("projects", ["action"], ["alias", "boardProjectId", "displayName", "paths"]);
        return data;
    }

    public static TheoryData<string, string, string[]> EnumContracts()
    {
        var data = new TheoryData<string, string, string[]>();
        data.Add("memory_remember", "scope", ["Session", "Project", "Global", "Personal"]);
        data.Add("memory_remember", "kind", ["Journal", "Fact"]);
        data.Add("memory_note", "scope", ["Session", "Project", "Global", "Personal"]);
        data.Add("memory_list", "scope", ["Session", "Project", "Global", "Personal"]);
        data.Add("memory_list", "kind", ["Journal", "Fact"]);
        data.Add("memory_list", "status", ["Active", "Superseded", "Tombstoned"]);
        data.Add("memory_recall", "scope", ["Session", "Project", "Global", "Personal"]);
        data.Add("memory_recall", "kind", ["Journal", "Fact"]);
        data.Add("memory_scry", "attribution", ["None", "Light", "Full"]);
        data.Add("memory_brief", "scopes", ["Session", "Project", "Global", "Personal"]);
        data.Add("projects", "action", ["List", "Add", "Update", "Remove", "Sync"]);
        return data;
    }

    public static TheoryData<string, string, string> TypeContracts()
    {
        var data = new TheoryData<string, string, string>();
        data.Add("memory_remember", "content", "string");
        data.Add("memory_remember", "tags", "array");
        data.Add("memory_remember", "importance", "number");
        data.Add("memory_remember", "supersedes", "string");
        data.Add("memory_note", "content", "string");
        data.Add("memory_note", "topic", "string");
        data.Add("memory_note", "tags", "array");
        data.Add("memory_digest", "topic", "string");
        data.Add("memory_digest", "tome", "string");
        data.Add("memory_digest", "since", "string");
        data.Add("memory_digest", "until", "string");
        data.Add("memory_digest", "limit", "integer");
        data.Add("memory_digest", "fields", "array");
        data.Add("memory_digest", "compact", "boolean");
        data.Add("memory_recall", "query", "string");
        data.Add("memory_recall", "limit", "integer");
        data.Add("memory_recall", "tome", "string");
        data.Add("memory_recall", "since", "string");
        data.Add("memory_recall", "until", "string");
        data.Add("memory_recall", "actor", "string");
        data.Add("memory_forget", "memoryId", "string");
        data.Add("memory_restore", "memoryId", "string");
        data.Add("memory_restore", "reason", "string");
        data.Add("memory_history", "memoryId", "string");
        data.Add("memory_list", "limit", "integer");
        data.Add("memory_list", "since", "string");
        data.Add("memory_list", "until", "string");
        data.Add("memory_list", "actor", "string");
        data.Add("memory_brief", "scopes", "array");
        data.Add("memory_brief", "factsPerScope", "integer");
        data.Add("memory_scry", "query", "string");
        data.Add("memory_stats", "includeTomeCompaction", "boolean");
        data.Add("memory_scry", "tomes", "array");
        data.Add("memory_scry", "includeJournal", "boolean");
        data.Add("memory_scry", "limit", "integer");
        data.Add("memory_list", "fields", "array");
        data.Add("memory_list", "compact", "boolean");
        data.Add("memory_recall", "fields", "array");
        data.Add("memory_recall", "compact", "boolean");
        data.Add("projects", "action", "string");
        data.Add("projects", "alias", "string");
        data.Add("projects", "paths", "array");
        return data;
    }

    [Fact]
    public async Task ExposesExactlyTheFourteenContractedTools()
    {
        await using var session = await McpTestSession.StartAsync();

        var names = await session.Client.ListToolNames();

        names.OrderBy(name => name, StringComparer.Ordinal).ShouldBe(ToolNames);
    }

    public static TheoryData<string> Tools()
    {
        var data = new TheoryData<string>();
        foreach (var name in ToolNames)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Contract_MatchesTheFrozenSnapshot(string tool)
    {
        await using var session = await McpTestSession.StartAsync();

        var observed = ToolSnapshotJson.DescribeContract((await session.Client.ListToolMap())[tool]);

        if (ToolSnapshotFiles.UpdateMode)
        {
            ToolSnapshotFiles.SaveContract(tool, observed);
            return;
        }

        var pinned = ToolSnapshotJson.Canonical(ToolSnapshotFiles.LoadContract(tool));
        var actual = ToolSnapshotJson.Canonical(observed);

        actual.ShouldBe(pinned, customMessage:
            $"tool contract drifted for '{tool}' — update tests/Grimoire.Mcp.Tests/TestData/snapshots/{tool}.json " +
            "in the same change, or regenerate the snapshots with GRIMOIRE_UPDATE_TOOL_SNAPSHOTS=1");
    }

    [Fact]
    public async Task OutputShapes_MatchTheFrozenSnapshots()
    {
        await using var session = await McpTestSession.StartAsync(gcState: new FakeGcStateStore(new GcState
        {
            LastAttemptAt = new DateTimeOffset(2026, 10, 17, 12, 30, 0, TimeSpan.Zero),
            LastSuccessAt = new DateTimeOffset(2026, 10, 17, 12, 30, 0, TimeSpan.Zero),
            RemovedExpired = 2,
            RemovedTombstones = 1,
        }), consolidationState: new FakeConsolidationStateStore(new ConsolidationState
        {
            LastAttemptAt = new DateTimeOffset(2026, 10, 17, 12, 30, 0, TimeSpan.Zero),
            LastSuccessAt = new DateTimeOffset(2026, 10, 17, 12, 30, 0, TimeSpan.Zero),
            ConsolidatedClusters = 2,
        }));
        var results = await ToolSurfaceProbes.RunAsync(session);

        foreach (var tool in ToolNames)
        {
            var observed = ToolSnapshotJson.DescribeShape(results[tool].Payload());

            if (ToolSnapshotFiles.UpdateMode)
            {
                ToolSnapshotFiles.SaveShape(tool, observed);
                continue;
            }

            var pinned = ToolSnapshotJson.Canonical(ToolSnapshotFiles.LoadShape(tool));
            var actual = ToolSnapshotJson.Canonical(observed);

            actual.ShouldBe(pinned, customMessage:
                $"output shape drifted for '{tool}' — update tests/Grimoire.Mcp.Tests/TestData/snapshots/{tool}.json " +
                "in the same change, or regenerate the snapshots with GRIMOIRE_UPDATE_TOOL_SNAPSHOTS=1");
        }
    }

    [Fact]
    public async Task ToolRoster_MatchesTheSnapshotDirectory()
    {
        await using var session = await McpTestSession.StartAsync();

        var names = await session.Client.ListToolNames();
        var (unpinned, orphans) = ToolSnapshotFiles.CompareRoster(names, ToolSnapshotFiles.PinnedToolNames());

        unpinned.ShouldBe([], customMessage:
            $"unpinned tools registered without a snapshot: {string.Join(", ", unpinned)} — " +
            "add tests/Grimoire.Mcp.Tests/TestData/snapshots/<tool>.json in the same change");
        orphans.ShouldBe([], customMessage:
            $"orphan snapshots without a registered tool: {string.Join(", ", orphans)} — " +
            "delete them or rename them to the surviving tool");
    }

    [Fact]
    public void SnapshotDrift_TurnsTheContractPinRed()
    {
        var pinned = ToolSnapshotJson.Canonical(ToolSnapshotFiles.LoadContract("memory_restore"));
        var tampered = (JsonObject)JsonNode.Parse(pinned)!;
        tampered["idempotent"] = !tampered["idempotent"]!.GetValue<bool>();
        var drifted = ToolSnapshotJson.Canonical(tampered);

        Should.Throw<ShouldAssertException>(() => drifted.ShouldBe(pinned));
    }

    [Fact]
    public void RosterGuard_TurnsUnpinnedToolsAndOrphanSnapshotsRed()
    {
        var (unpinned, orphans) = ToolSnapshotFiles.CompareRoster(
            ["memory_list", "memory_hypothetical"], ["memory_list"]);

        unpinned.ShouldBe(["memory_hypothetical"]);
        orphans.ShouldBe([]);

        var (unpinnedAfterRemoval, orphansAfterRemoval) = ToolSnapshotFiles.CompareRoster(
            ["memory_list"], ["memory_list", "memory_ghost"]);

        unpinnedAfterRemoval.ShouldBe([]);
        orphansAfterRemoval.ShouldBe(["memory_ghost"]);
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
        var tokenHolder = property.TryGetProperty("items", out var items) && items.TryGetProperty("enum", out _)
            ? items
            : property;
        var type = tokenHolder.GetProperty("type");
        if (type.ValueKind == JsonValueKind.Array)
        {
            type.EnumerateArray().ShouldContain(value => value.GetString() == "string");
        }
        else
        {
            type.GetString().ShouldBe("string");
        }

        tokenHolder.GetProperty("enum").EnumerateArray()
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
