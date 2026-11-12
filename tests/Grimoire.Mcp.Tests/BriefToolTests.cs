using Grimoire.Core;
using ModelContextProtocol.Client;

namespace Grimoire.Mcp.Tests;

public sealed class BriefToolTests
{
    [Fact]
    public async Task Brief_EmptyStore_ReturnsExplicitEmptyStates()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("persona").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("facts").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("tombstones").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("journal").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("facts").GetProperty("items").GetArrayLength().ShouldBe(0);
        payload.GetProperty("omitted").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Brief_EmptyTome_TeachesTheScryDoor()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        result.Payload().GetProperty("hint").GetString().ShouldContain("tome is empty — use memory_scry for cross-tome context");
    }

    [Fact]
    public async Task Brief_PopulatedTome_CarriesTheStartupContract()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "standing rule",
            ["scope"] = "Global",
            ["kind"] = "Fact",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var hint = result.Payload().GetProperty("hint").GetString();
        hint.ShouldStartWith("startup contract");
        hint.ShouldNotContain("tome is empty");
    }

    [Fact]
    public async Task Brief_StartupContract_TeachesTheCompactionDueDoor()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var hint = result.Payload().GetProperty("hint").GetString()!;
        var route = hint[hint.IndexOf("Compaction loop", StringComparison.Ordinal)..];
        route.ShouldContain("compactionDue");
        route.ShouldContain("memory_compact_candidates");
    }

    [Fact]
    public async Task Brief_StartupContract_TeachesTheTomeIdentityDoors()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var hint = result.Payload().GetProperty("hint").GetString()!;
        var conventions = hint[hint.IndexOf("Conventions", StringComparison.Ordinal)..];
        conventions.ShouldContain("your tome is the Tome field of this brief, and the tome keys scry accepts are the memory_stats.byTome keys");
    }

    [Fact]
    public async Task Brief_StartupContract_TeachesTheRoutingContract()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var hint = result.Payload().GetProperty("hint").GetString()!;
        var conventions = hint[hint.IndexOf("Conventions", StringComparison.Ordinal)..];
        conventions.ShouldContain("Routing: facts to grimoire, live process state to the Overmind board; " +
            "link a board entity with a tag shaped overmind://entry/<uuid> - recall and list filter by tags.");
    }

    [Fact]
    public async Task Brief_DefaultPath_NamesTheResolvedTome()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe(McpTestSession.ClientActor);
    }

    [Fact]
    public async Task Brief_StaleSimilarJournal_NamesThePendingEntries()
    {
        await using var session = await McpTestSession.StartAsync();
        var stale = session.Clock.UtcNow.AddDays(-30);
        await session.Store.UpsertAsync(StaleJournal("Mediator dispatch generics keep biting us", stale), new float[] { 1, 0.05f, 0 }, CancellationToken.None);
        await session.Store.UpsertAsync(StaleJournal("Generic dispatch issues with Mediator again", stale), new float[] { 1, 0.05f, 0.05f }, CancellationToken.None);

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var due = result.Payload().GetProperty("compactionDue");
        due.GetProperty("pendingEntries").GetInt64().ShouldBe(2);
    }

    [Fact]
    public async Task Brief_NothingDue_OmitsTheCompactionKeyEntirely()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "one fresh journal note",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        result.Payload().TryGetProperty("compactionDue", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_OneStaleJournalBelowClusterFloor_OmitsTheCompactionKey()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Store.UpsertAsync(
            StaleJournal("a single aging note", session.Clock.UtcNow.AddDays(-30)),
            new float[] { 1, 0, 0 },
            CancellationToken.None);

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        result.Payload().TryGetProperty("compactionDue", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_TomeParam_BriefsTheNamedTomeAndEchoesIt()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "alpha squad standing rule",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tome"] = "alpha",
        });

        var alpha = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["tome"] = "alpha",
        });

        alpha.ShouldSucceed();
        var alphaPayload = alpha.Payload();
        alphaPayload.GetProperty("tome").GetString().ShouldBe("alpha");
        alphaPayload.GetProperty("facts").GetProperty("items")[0].GetProperty("content").GetString()
            .ShouldBe("alpha squad standing rule");

        var own = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        own.ShouldSucceed();
        own.Payload().GetProperty("facts").GetProperty("state").GetString().ShouldBe("Empty");
    }

    [Fact]
    public async Task Brief_InvalidTome_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["tome"] = "tome!",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tome");
    }

    [Fact]
    public async Task Brief_PopulatedStore_ReturnsAllFourSections()
    {
        await using var session = await McpTestSession.StartAsync();
        await SeedPersonaAsync(session);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "deploy gates on green tests",
            ["scope"] = "Project",
            ["kind"] = "Fact",
            ["projectId"] = "grimoire",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "wired Serilog before the first run",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        var retracted = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "roll our own vector math",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = retracted.Payload().GetProperty("memoryId").GetGuid().ToString(),
            ["reason"] = "log setup redone in the config PR",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["projectId"] = "grimoire",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("omitted").GetArrayLength().ShouldBe(0);

        var persona = payload.GetProperty("persona");
        persona.GetProperty("state").GetString().ShouldBe("Populated");
        persona.GetProperty("document").GetProperty("identity").GetString().ShouldBe("Senior .NET developer");

        var facts = payload.GetProperty("facts");
        facts.GetProperty("state").GetString().ShouldBe("Populated");
        facts.GetProperty("items")[0].GetProperty("content").GetString().ShouldBe("deploy gates on green tests");
        facts.GetProperty("items")[0].GetProperty("score").GetDouble().ShouldBeGreaterThan(0);

        var tombstones = payload.GetProperty("tombstones");
        tombstones.GetProperty("state").GetString().ShouldBe("Populated");
        var tombstone = tombstones.GetProperty("items")[0];
        tombstone.GetProperty("reason").GetString().ShouldBe("log setup redone in the config PR");
        tombstone.TryGetProperty("content", out _).ShouldBeFalse();

        var journal = payload.GetProperty("journal");
        journal.GetProperty("state").GetString().ShouldBe("Populated");
        journal.GetProperty("items")[0].GetProperty("content").GetString().ShouldBe("wired Serilog before the first run");
        journal.GetProperty("items")[0].GetProperty("truncated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_LongJournalEntry_IsCappedWithTruncatedMarker()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = new string('x', 500),
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        var item = result.Payload().GetProperty("journal").GetProperty("items")[0];
        item.GetProperty("truncated").GetBoolean().ShouldBeTrue();
        item.GetProperty("content").GetString()!.Length.ShouldBe(400);
    }

    [Fact]
    public async Task Brief_LimitHints_AreClampedServerSide()
    {
        await using var session = await McpTestSession.StartAsync();
        foreach (var content in new[] { "one", "two", "three" })
        {
            await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
            {
                ["content"] = content,
                ["scope"] = "Global",
                ["kind"] = "Fact",
            });
            session.Clock.UtcNow = session.Clock.UtcNow.AddMinutes(1);
        }

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["factsPerScope"] = 0,
            ["journalPerScope"] = -5,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("facts").GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Brief_ScopesOverride_NarrowsSections()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "global fact",
            ["kind"] = "Fact",
            ["scope"] = "Global",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "project fact",
            ["kind"] = "Fact",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["scopes"] = new[] { "Global" },
        });

        var items = result.Payload().GetProperty("facts").GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("content").GetString().ShouldBe("global fact");
        items[0].GetProperty("scope").GetString().ShouldBe("Global");
    }

    [Fact]
    public async Task Brief_OverBudgetSections_AreNamedInOmitted()
    {
        var engineOptions = new MemoryEngineOptions { BriefMaxTotalChars = 5 };
        await using var session = await McpTestSession.StartAsync(engineOptions: engineOptions);
        await SeedPersonaAsync(session);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "one small fact",
            ["scope"] = "Global",
            ["kind"] = "Fact",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "one journal note",
            ["scope"] = "Global",
        });
        var entry = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "retracted journal note",
            ["scope"] = "Global",
        });
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = entry.Payload().GetProperty("memoryId").GetGuid().ToString(),
            ["reason"] = "stale note",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        var omitted = payload.GetProperty("omitted").EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        omitted.ShouldBe(["persona", "facts", "tombstones", "journal"]);
        payload.GetProperty("persona").GetProperty("state").GetString().ShouldBe("Omitted");
        payload.GetProperty("facts").GetProperty("state").GetString().ShouldBe("Omitted");
        payload.GetProperty("facts").GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Brief_ScopesSchema_SpellsOutTheScopeValues()
    {
        await using var session = await McpTestSession.StartAsync();

        var schema = (await session.Client.ListToolMap())["memory_brief"].JsonSchema;
        var items = schema.GetProperty("properties").GetProperty("scopes").GetProperty("items");

        items.GetProperty("type").GetString().ShouldBe("string");
        items.GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString())
            .ShouldBe(["Session", "Project", "Global", "Personal"]);
    }

    [Fact]
    public async Task Brief_IsRegisteredReadOnlyIdempotentAndNonDestructive()
    {
        await using var session = await McpTestSession.StartAsync();

        var tools = await session.Client.ListToolsAsync();
        var tool = tools.Single(item => item.Name == "memory_brief");

        var annotations = tool.ProtocolTool.Annotations.ShouldNotBeNull();
        annotations.ReadOnlyHint.ShouldBe(true);
        annotations.IdempotentHint.ShouldBe(true);
        annotations.DestructiveHint.ShouldBe(false);
    }

    private static MemoryRecord StaleJournal(string content, DateTimeOffset accessed) => new()
    {
        Id = Guid.NewGuid(),
        Content = content,
        Scope = MemoryScope.Project,
        Kind = MemoryKind.Journal,
        Actor = MemoryActor.Human,
        Tome = McpTestSession.ClientActor,
        ProjectId = "grimoire",
        CreatedAt = accessed,
        LastAccessedAt = accessed,
    };

    private static async Task SeedPersonaAsync(McpTestSession session)
    {
        var persona = new PersonaService(session.Store, session.Embeddings, session.Clock);
        await persona.SetAsync(new PersonaDocument
        {
            Identity = "Senior .NET developer",
            Tone = "informal",
            Preferences = "dark mode, Vim bindings",
            Rules = "never commit without being asked",
        }, MemoryActor.Human, CancellationToken.None, McpTestSession.ClientActor);
    }


    [Fact]
    public async Task Brief_UnknownExplicitTome_CarriesTheKnownTomeWarning()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "warm the store",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["tome"] = "nonesuch-tome",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("facts").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("warnings")[0].GetString()
            .ShouldBe("tome 'nonesuch-tome' is empty or does not exist. Known tome keys: test-client");
    }

    [Fact]
    public async Task Brief_WithoutATomeParam_StaysSilentOnAColdStore()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        result.Payload().TryGetProperty("warnings", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_WithManifests_ListsTheTomeTable()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Manifests.Seed(Manifest("alpha", "alpha squad routing"));
        session.Manifests.Seed(Manifest("beta", "beta squad routing"));

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var tomes = result.Payload().GetProperty("tomes");
        tomes.GetProperty("state").GetString().ShouldBe("Populated");
        var items = tomes.GetProperty("items");
        items.GetArrayLength().ShouldBe(2);
        items[0].GetProperty("key").GetString().ShouldBe("alpha");
        items[0].GetProperty("description").GetString().ShouldBe("alpha squad routing");
        items[0].GetProperty("createdBy").GetString().ShouldBe("human");
        tomes.GetProperty("more").GetInt32().ShouldBe(0);
        tomes.TryGetProperty("note", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_TomeTable_CapsAtSixteenWithMoreOverflow()
    {
        await using var session = await McpTestSession.StartAsync();
        for (var index = 0; index < 18; index++)
        {
            session.Manifests.Seed(Manifest($"tome-{index:D2}", "overflow probe"));
        }

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var tomes = result.Payload().GetProperty("tomes");
        tomes.GetProperty("items").GetArrayLength().ShouldBe(16);
        tomes.GetProperty("more").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Brief_WhenTheManifestStoreThrows_CarriesTheNoteAndStillSucceeds()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Manifests.Failure = new InvalidOperationException("qdrant is down");

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var tomes = result.Payload().GetProperty("tomes");
        tomes.GetProperty("note").GetString().ShouldContain("unavailable");
        tomes.GetProperty("note").GetString().ShouldContain("qdrant is down");
        tomes.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Brief_WhenTheManifestStoreIsDegraded_CarriesTheNote()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Manifests.DegradationReason = "Tome manifest store is unreachable - manifest data may be stale";

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var tomes = result.Payload().GetProperty("tomes");
        tomes.GetProperty("note").GetString().ShouldContain("unreachable");
        tomes.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Brief_WithoutManifests_OmitsTheTomesSection()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        result.Payload().TryGetProperty("tomes", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_OverBudgetTomeTable_IsNamedInOmitted()
    {
        var engineOptions = new MemoryEngineOptions { BriefMaxTotalChars = 5 };
        await using var session = await McpTestSession.StartAsync(engineOptions: engineOptions);
        session.Manifests.Seed(Manifest("alpha", "alpha squad routing"));

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("omitted").EnumerateArray()
            .Select(value => value.GetString())
            .ShouldContain("tomes");
        payload.GetProperty("tomes").GetProperty("state").GetString().ShouldBe("Omitted");
        payload.GetProperty("tomes").GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Brief_DailyFlag_ReturnsGroupsByActorWithEntriesOldestFirst()
    {
        await using var session = await McpTestSession.StartAsync();
        await SeedDailyAsync(session, "medivh opened the board", DailyNote(0), "medivh", "claude");
        await SeedDailyAsync(session, "medivh pushed the seam", DailyNote(30), "medivh", "qwen");
        await SeedDailyAsync(session, "thrall wired the door", DailyNote(10), "thrall", "claude");

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["daily"] = true,
        });

        result.ShouldSucceed();
        var daily = result.Payload().GetProperty("daily");
        daily.GetProperty("state").GetString().ShouldBe("Populated");
        daily.GetProperty("more").GetInt32().ShouldBe(0);

        var groups = daily.GetProperty("groups");
        groups.GetArrayLength().ShouldBe(2);
        groups[0].GetProperty("actor").GetString().ShouldBe("medivh");
        groups[0].GetProperty("bucket").GetString().ShouldBe("Today");
        groups[0].GetProperty("day").GetString().ShouldBe(ExpectedLocalDay());
        groups[0].GetProperty("entries").EnumerateArray()
            .Select(entry => entry.GetProperty("content").GetString())
            .ShouldBe(["medivh opened the board", "medivh pushed the seam"]);
        groups[1].GetProperty("actor").GetString().ShouldBe("thrall");
        groups[0].GetProperty("entries")[0].GetProperty("truncated").GetBoolean().ShouldBeFalse();
        groups[0].GetProperty("entries")[0].GetProperty("memoryId").GetGuid().ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Brief_DailyFlag_OversizedBodyNamesDailyInOmittedInsteadOfThrowing()
    {
        await using var session = await McpTestSession.StartAsync();
        await SeedDailyAsync(session, new string('x', 2000), DailyNote(0), "medivh", "claude");

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["daily"] = true,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("omitted").EnumerateArray()
            .Select(value => value.GetString())
            .ShouldContain("daily");
        var entry = payload.GetProperty("daily").GetProperty("groups")[0].GetProperty("entries")[0];
        entry.GetProperty("truncated").GetBoolean().ShouldBeTrue();
        entry.GetProperty("content").GetString()!.Length.ShouldBe(1500);
    }

    [Fact]
    public async Task Brief_DailyFlag_OverBudgetTailCountsMoreAndNamesOmitted()
    {
        var engineOptions = new MemoryEngineOptions { BriefDailyMaxTotalChars = 100 };
        await using var session = await McpTestSession.StartAsync(engineOptions: engineOptions);
        await SeedDailyAsync(session, new string('a', 40), DailyNote(0), "medivh", "claude");
        await SeedDailyAsync(session, new string('b', 40), DailyNote(10), "medivh", "claude");
        await SeedDailyAsync(session, new string('c', 40), DailyNote(20), "medivh", "claude");

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["daily"] = true,
        });

        result.ShouldSucceed();
        var daily = result.Payload().GetProperty("daily");
        daily.GetProperty("groups")[0].GetProperty("entries").GetArrayLength().ShouldBe(2);
        daily.GetProperty("more").GetInt32().ShouldBe(1);
        result.Payload().GetProperty("omitted").EnumerateArray()
            .Select(value => value.GetString())
            .ShouldContain("daily");
    }

    [Fact]
    public async Task Brief_WithoutDailyParam_CarriesNoDailyKeyAndMatchesTheRecordedWirePayload()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.TryGetProperty("daily", out _).ShouldBeFalse();
        payload.TryGetProperty("warnings", out _).ShouldBeFalse();
        Sha256Hex(result.Text()).ShouldBe("d670c7ae84f38c99c1d3ffc401005e39755c1a8c3c41323ef98dadc756ee70ee");
    }

    [Fact]
    public async Task Brief_DailyFalseParam_MatchesTheOmittedParamPayloadByteForByte()
    {
        await using var session = await McpTestSession.StartAsync();

        var omitted = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());
        var declined = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["daily"] = false,
        });

        omitted.ShouldSucceed();
        declined.ShouldSucceed();
        Sha256Hex(declined.Text()).ShouldBe(Sha256Hex(omitted.Text()));
        declined.Payload().TryGetProperty("daily", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_DailyFlag_WithUnresolvableConfiguredZone_WarnsNamingTheId()
    {
        var engineOptions = new MemoryEngineOptions { DailyTimeZone = "Not/AZone" };
        await using var session = await McpTestSession.StartAsync(engineOptions: engineOptions);

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["daily"] = true,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("daily").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("warnings").EnumerateArray()
            .Select(value => value.GetString())
            .ShouldBe(["DailyTimeZone 'Not/AZone' did not resolve; daily buckets use UTC."]);
    }

    [Fact]
    public async Task Brief_DailyFlag_ZoneWarningAndEmptyTomeWarningAreBothKept()
    {
        var engineOptions = new MemoryEngineOptions { DailyTimeZone = "Not/AZone" };
        await using var session = await McpTestSession.StartAsync(engineOptions: engineOptions);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "warm the store",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["daily"] = true,
            ["tome"] = "nonesuch-tome",
        });

        result.ShouldSucceed();
        var warnings = result.Payload().GetProperty("warnings").EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        warnings.Length.ShouldBe(2, customMessage:
            "the projection must UNION the Core payload warning with the empty-tome note; " +
            "one item means one of the two was overwritten");
        warnings[0].ShouldBe("DailyTimeZone 'Not/AZone' did not resolve; daily buckets use UTC.");
        warnings[1].ShouldBe("tome 'nonesuch-tome' is empty or does not exist. Known tome keys: test-client");
    }

    [Fact]
    public async Task Brief_DailyFlag_WithResolvableZone_KeepsTheWarningsKeyAbsent()
    {
        var engineOptions = new MemoryEngineOptions { DailyTimeZone = "Europe/Moscow" };
        await using var session = await McpTestSession.StartAsync(engineOptions: engineOptions);
        await SeedDailyAsync(session, "moscow evening note", DailyNote(0), "medivh", "claude");

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["daily"] = true,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("daily").GetProperty("state").GetString().ShouldBe("Populated");
        payload.GetProperty("hint").GetString()!.ShouldContain("tome is empty",
            customMessage: "characterization of today's behavior, not endorsement: IsEmpty() ignores DailyState, " +
                "so a store whose only content is daily notes is still taught the scry door on a daily:true brief");
        payload.TryGetProperty("warnings", out _).ShouldBeFalse("an empty warnings list would serialize as a key");
    }

    [Fact]
    public async Task Brief_DailySchema_ParamIsListedAndDescribed()
    {
        await using var session = await McpTestSession.StartAsync();

        var schema = (await session.Client.ListToolMap())["memory_brief"].JsonSchema;
        schema.TryGetProperty("properties", out var properties).ShouldBeTrue();
        properties.TryGetProperty("daily", out var daily).ShouldBeTrue("the daily param is missing from the schema");

        daily.GetProperty("description").GetString()!.ShouldContain("grouped by actor");
        schema.TryGetProperty("required", out _).ShouldBeFalse(
            "memory_brief requires nothing, the new daily param included");
    }

    private static readonly DateTimeOffset BriefClockInstant = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset DailyNote(int minutes) => BriefClockInstant.AddMinutes(minutes);

    private static string ExpectedLocalDay() =>
        TimeZoneInfo.ConvertTime(BriefClockInstant, TimeZoneInfo.Local).ToString("yyyy-MM-dd");

    private static async Task SeedDailyAsync(
        McpTestSession session,
        string content,
        DateTimeOffset created,
        string alias,
        string vendor) =>
        await session.Store.UpsertAsync(new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = MemoryActor.Agent(alias, vendor),
            Tome = McpTestSession.ClientActor,
            Tags = ["daily-notes"],
            CreatedAt = created,
            LastAccessedAt = created,
        }, ReadOnlyMemory<float>.Empty, CancellationToken.None);

    private static string Sha256Hex(string text) =>
        Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    private static TomeManifest Manifest(string key, string description) => new()
    {
        Key = key,
        Description = description,
        CreatedBy = "human",
        CreatedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
    };
}
