using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class DigestToolTests
{
    [Fact]
    public async Task Digest_BlankTopic_RejectsWithoutTouchingTheStore()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "   ",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("topic");
        message.ShouldContain("topic must not be empty");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Digest_NonPositiveLimit_Rejects()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["limit"] = 0,
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("limit");
        message.ShouldContain("positive integer");
        session.Store.Points.ShouldBeEmpty();
    }

    [Fact]
    public async Task Digest_UnparsableSince_RejectsNamingTheParam()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["since"] = "not-a-date",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("since");
        message.ShouldContain("'since'");
        message.ShouldContain("not-a-date");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Digest_SinceLaterThanUntil_Rejects()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["since"] = "2026-10-20",
            ["until"] = "2026-10-19",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("since");
    }

    [Fact]
    public async Task Digest_InvalidTome_RejectsWithoutTouchingTheStore()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["tome"] = "Not Valid!!",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tome");
        message.ShouldContain("'Not Valid!!'");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Digest_UnknownField_RejectsNamingTheAllowedSet()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["fields"] = new[] { "nope" },
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("fields");
        message.ShouldContain("Unknown field 'nope'");
        message.ShouldContain("Allowed fields:");
        session.Store.Points.ShouldBeEmpty();
    }

    [Fact]
    public async Task Digest_EmptyTome_ReturnsExplicitEmptyStates()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("tome").GetString().ShouldBe(McpTestSession.ClientActor);
        payload.GetProperty("topic").GetString().ShouldBe("topic:deploy");
        payload.GetProperty("longMemo").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("recent").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("gone").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("omitted").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Digest_SectionsCarryMatchReasonsAndTombstoneContext()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });
        var tagged = await SeedAsync(session, "ship gates decision", new float[] { 0, 0, 1 },
            tags: new[] { "topic:deploy" });
        var similar = await SeedAsync(session, "untyped deploy journal", new float[] { 1, 0.7f, 0 },
            kind: MemoryKind.Journal);
        var retired = await SeedAsync(session, "retracted deploy note", new float[] { 0, 0, 1 },
            kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned, tombstoneReason: "wrong claim",
            tags: new[] { "topic:deploy" });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["compact"] = false,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        var longMemo = payload.GetProperty("longMemo").GetProperty("items");
        longMemo.GetArrayLength().ShouldBe(1);
        longMemo[0].GetProperty("memoryId").GetGuid().ShouldBe(tagged.Id);
        longMemo[0].GetProperty("match").GetString().ShouldBe("tag");
        longMemo[0].GetProperty("kind").GetString().ShouldBe("Fact");
        longMemo[0].GetProperty("tags")[0].GetString().ShouldBe("topic:deploy");

        var recent = payload.GetProperty("recent").GetProperty("items");
        recent.GetArrayLength().ShouldBe(1);
        recent[0].GetProperty("memoryId").GetGuid().ShouldBe(similar.Id);
        recent[0].GetProperty("match").GetString().ShouldBe("similar");
        recent[0].GetProperty("similarity").GetDouble().ShouldBeGreaterThanOrEqualTo(0.75);

        var gone = payload.GetProperty("gone").GetProperty("items");
        gone.GetArrayLength().ShouldBe(1);
        gone[0].GetProperty("memoryId").GetGuid().ShouldBe(retired.Id);
        gone[0].GetProperty("tombstoneReason").GetString().ShouldBe("wrong claim");
    }

    [Fact]
    public async Task Digest_CompactProjection_IsTheDefault()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });
        await SeedAsync(session, "ship gates decision", new float[] { 0, 0, 1 }, tags: new[] { "topic:deploy" });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
        });

        result.ShouldSucceed();
        var item = result.Payload().GetProperty("longMemo").GetProperty("items")[0];
        item.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(["content", "createdAt", "match", "memoryId", "tags"]);
    }

    [Fact]
    public async Task Digest_FieldsProjection_OverridesCompactAndAppliesToEverySection()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });
        await SeedAsync(session, "ship gates decision", new float[] { 0, 0, 1 }, tags: new[] { "topic:deploy" });
        await SeedAsync(session, "untyped deploy journal", new float[] { 1, 0.7f, 0 }, kind: MemoryKind.Journal);
        await SeedAsync(session, "retracted deploy note", new float[] { 0, 0, 1 },
            kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned, tombstoneReason: "wrong claim",
            tags: new[] { "topic:deploy" });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["fields"] = new[] { "content", "truncated" },
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        foreach (var sectionName in new[] { "longMemo", "recent", "gone" })
        {
            var item = payload.GetProperty(sectionName).GetProperty("items")[0];
            item.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
                .ShouldBe(["content", "match", "memoryId", "truncated"]);
        }

        var goneItem = payload.GetProperty("gone").GetProperty("items")[0];
        goneItem.TryGetProperty("tombstoneReason", out _).ShouldBeFalse("tombstoneReason was not projected");
    }

    [Fact]
    public async Task Digest_FieldsCreatedAt_IsAnAllowedProjectionKey()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });
        await SeedAsync(session, "ship gates decision", new float[] { 0, 0, 1 }, tags: new[] { "topic:deploy" });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["fields"] = new[] { "createdAt" },
        });

        result.ShouldSucceed();
        var item = result.Payload().GetProperty("longMemo").GetProperty("items")[0];
        item.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(["createdAt", "match", "memoryId"]);
    }

    [Fact]
    public async Task Digest_HitsBumpAccessStatsOnTheStore()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });
        var seeded = await SeedAsync(session, "ship gates decision", new float[] { 0, 0, 1 },
            tags: new[] { "topic:deploy" });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["compact"] = false,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("longMemo").GetProperty("items")[0]
            .GetProperty("lastAccessedAt").GetString().ShouldNotBeNull();
        var stored = session.Store.Points.Single(point => point.Record.Id == seeded.Id).Record;
        stored.AccessCount.ShouldBe(1);
        stored.LastAccessedAt.ShouldBe(session.Clock.UtcNow);
    }

    [Fact]
    public async Task Digest_OverBudgetSection_IsNamedInOmitted()
    {
        await using var session = await McpTestSession.StartAsync(
            engineOptions: new MemoryEngineOptions { DigestMaxTotalChars = 60 });
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });
        await SeedAsync(session, new string('d', 500), new float[] { 0, 0, 1 }, tags: new[] { "topic:deploy" });
        await SeedAsync(session, "small deploy journal", new float[] { 0, 0, 1 },
            kind: MemoryKind.Journal, tags: new[] { "topic:deploy" });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("longMemo").GetProperty("state").GetString().ShouldBe("Omitted");
        payload.GetProperty("longMemo").GetProperty("items").GetArrayLength().ShouldBe(0);
        payload.GetProperty("omitted")[0].GetString().ShouldBe("longMemo");
        payload.GetProperty("recent").GetProperty("state").GetString().ShouldBe("Populated");
    }

    [Fact]
    public async Task Digest_Limit_ClampsRecent()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });
        foreach (var index in Enumerable.Range(0, 3))
        {
            await SeedAsync(session, $"deploy journal {index}", new float[] { 0, 0, 1 },
                kind: MemoryKind.Journal, tags: new[] { "topic:deploy" },
                created: session.Clock.UtcNow.AddDays(-index));
        }

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["limit"] = 1,
        });

        result.ShouldSucceed();
        var recent = result.Payload().GetProperty("recent").GetProperty("items");
        recent.GetArrayLength().ShouldBe(1);
        recent[0].GetProperty("content").GetString().ShouldBe("deploy journal 0");
    }

    [Fact]
    public async Task Digest_SinceUntil_BoundTheRecentWindow()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });
        var early = session.Clock.UtcNow.AddDays(-3);
        await SeedAsync(session, "early deploy note", new float[] { 0, 0, 1 },
            kind: MemoryKind.Journal, tags: new[] { "topic:deploy" }, created: early);
        session.Clock.UtcNow = session.Clock.UtcNow.AddDays(3);
        await SeedAsync(session, "late deploy note", new float[] { 1, 0.7f, 0 }, kind: MemoryKind.Journal);

        var recent = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["since"] = "2026-10-19",
        });
        var all = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
        });

        recent.ShouldSucceed();
        var recentItems = recent.Payload().GetProperty("recent").GetProperty("items");
        recentItems.GetArrayLength().ShouldBe(1);
        recentItems[0].GetProperty("content").GetString().ShouldBe("late deploy note");

        all.ShouldSucceed();
        all.Payload().GetProperty("recent").GetProperty("items").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Digest_ExplicitTome_EchoesTheSearchedTome()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy", new float[] { 1, 0, 0 });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["tome"] = "Alpha Squad",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("alpha-squad");
    }

    private static async Task<MemoryRecord> SeedAsync(
        McpTestSession session,
        string content,
        float[] vector,
        MemoryKind kind = MemoryKind.Fact,
        MemoryStatus status = MemoryStatus.Active,
        IReadOnlyList<string>? tags = null,
        string? tombstoneReason = null,
        DateTimeOffset? created = null)
    {
        var moment = created ?? session.Clock.UtcNow;
        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = kind,
            Actor = MemoryActor.Human,
            Tome = McpTestSession.ClientActor,
            ProjectId = "grimoire",
            Tags = tags ?? [],
            CreatedAt = moment,
            LastAccessedAt = moment,
            Status = status,
            TombstoneReason = tombstoneReason,
        };

        await session.Store.UpsertAsync(record, vector, CancellationToken.None);
        return record;
    }


    [Fact]
    public async Task Digest_UnknownTomeOnEmptyStore_WarnsWithNoneYet()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["tome"] = "legacy-app",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("longMemo").GetProperty("items").GetArrayLength().ShouldBe(0);
        payload.GetProperty("warnings")[0].GetString()
            .ShouldBe("tome 'legacy-app' is empty or does not exist. Known tome keys: (none yet)");
    }

    [Fact]
    public async Task Digest_UnknownTomeWithHitsElsewhere_WarnsNamingTheKnownTome()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("LUCA field bug", [1f, 0f, 0f]);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "LUCA field bug",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tags"] = new[] { "topic:luca" },
            ["tome"] = "inno-crew",
        });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
            ["tome"] = "legacy-app",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("longMemo").GetProperty("items").GetArrayLength().ShouldBe(0);
        payload.GetProperty("warnings")[0].GetString()
            .ShouldBe("tome 'legacy-app' is empty or does not exist. Known tome keys: inno-crew");
    }

    [Fact]
    public async Task Digest_HitsInTheRequestedTome_StayWarningFree()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("LUCA field bug", [1f, 0f, 0f]);
        session.Embeddings.Register("luca", [1f, 0f, 0f]);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "LUCA field bug",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tags"] = new[] { "topic:luca" },
            ["tome"] = "inno-crew",
        });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "luca",
            ["tome"] = "inno-crew",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("longMemo").GetProperty("items").GetArrayLength().ShouldBe(1);
        payload.TryGetProperty("warnings", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Digest_KnownTomeWithoutTopicHits_StaysSilent()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("LUCA field bug", [1f, 0f, 0f]);
        session.Embeddings.Register("grocery-list", [0f, 1f, 0f]);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "LUCA field bug",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tags"] = new[] { "topic:luca" },
            ["tome"] = "inno-crew",
        });

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "grocery list",
            ["tome"] = "inno-crew",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("longMemo").GetProperty("items").GetArrayLength().ShouldBe(0);
        payload.TryGetProperty("warnings", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Digest_WithoutATomeParam_NeverWarns()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "deploy",
        });

        result.ShouldSucceed();
        result.Payload().TryGetProperty("warnings", out _).ShouldBeFalse();
    }
}
