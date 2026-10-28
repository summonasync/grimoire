using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class StatsToolTests
{
    [Fact]
    public async Task Stats_EmptyStore_ReportsZeroesAndNoPendingCompaction()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("active").GetInt64().ShouldBe(0);
        payload.GetProperty("superseded").GetInt64().ShouldBe(0);
        payload.GetProperty("tombstoned").GetInt64().ShouldBe(0);
        payload.GetProperty("pendingCompaction").GetInt64().ShouldBe(0);
        payload.GetProperty("idempotencyConflicts").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Stats_ReportsTheGcHeartbeatWithStaleness()
    {
        await using var session = await McpTestSession.StartAsync(gcOptions: new GcOptions { SweepInterval = TimeSpan.FromMinutes(30) });
        session.Gc.Current = new GcState
        {
            LastAttemptAt = session.Clock.UtcNow.AddMinutes(-10),
            LastSuccessAt = session.Clock.UtcNow.AddMinutes(-10),
            RemovedExpired = 2,
            RemovedTombstones = 1,
        };

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var gc = result.Payload().GetProperty("gc");
        gc.GetProperty("stale").GetBoolean().ShouldBeFalse();
        gc.GetProperty("lastSuccessAt").GetDateTimeOffset().ShouldBe(session.Clock.UtcNow.AddMinutes(-10));
        gc.GetProperty("removedExpired").GetInt32().ShouldBe(2);
        gc.GetProperty("removedTombstones").GetInt32().ShouldBe(1);
        gc.GetProperty("isUnknown").GetBoolean().ShouldBeFalse();
        gc.TryGetProperty("lastError", out _).ShouldBeFalse();
        gc.TryGetProperty("unknownReason", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Stats_ReportsTheConsolidationHeartbeat()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Consolidation.Current = new ConsolidationState
        {
            LastAttemptAt = session.Clock.UtcNow.AddMinutes(-10),
            LastSuccessAt = session.Clock.UtcNow.AddMinutes(-10),
            ConsolidatedClusters = 4,
        };

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var consolidation = result.Payload().GetProperty("consolidation");
        consolidation.GetProperty("stale").GetBoolean().ShouldBeFalse();
        consolidation.GetProperty("lastSuccessAt").GetDateTimeOffset().ShouldBe(session.Clock.UtcNow.AddMinutes(-10));
        consolidation.GetProperty("consolidatedClusters").GetInt32().ShouldBe(4);
        consolidation.GetProperty("isUnknown").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Stats_FlagsStaleWhenTheHeartbeatIsOlderThanTwiceTheSweepInterval()
    {
        await using var session = await McpTestSession.StartAsync(gcOptions: new GcOptions { SweepInterval = TimeSpan.FromMinutes(30) });
        session.Gc.Current = new GcState
        {
            LastAttemptAt = session.Clock.UtcNow.AddHours(-5),
            LastSuccessAt = session.Clock.UtcNow.AddHours(-5),
        };

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var gc = result.Payload().GetProperty("gc");
        gc.GetProperty("stale").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Stats_SurfacesAnUnreadableHeartbeatAsUnknownWithoutFailing()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Gc.Current = GcState.Unknown("gc state file is not valid JSON: boom");

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var gc = result.Payload().GetProperty("gc");
        gc.GetProperty("stale").GetBoolean().ShouldBeTrue();
        gc.GetProperty("unknownReason").GetString().ShouldBe("gc state file is not valid JSON: boom");
        gc.TryGetProperty("lastSuccessAt", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Stats_ReflectsCountsPerStatusScopeAndPendingCompaction()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("first", new float[] { 1, 0, 0 });
        session.Embeddings.Register("second", new float[] { 0, 1, 0 });
        session.Embeddings.Register("third", new float[] { 0, 0, 1 });
        foreach (var content in new[] { "first", "second", "third" })
        {
            await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
            {
                ["content"] = content,
                ["scope"] = "Project",
                ["projectId"] = "grimoire",
            });
        }

        var third = session.Store.Points.Single(point => point.Record.Content == "third").Record;
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = third.Id.ToString(),
            ["reason"] = "wrong assumption",
        });

        var stale = session.Clock.UtcNow.AddDays(-30);
        var similar = new float[][] { [1, 0.05f, 0], [1, 0, 0.05f], [1, 0.05f, 0.05f] };
        foreach (var vector in similar)
        {
            await session.Store.UpsertAsync(
                Journal(Guid.NewGuid(), $"stale journal {vector[1]}{vector[2]}", stale),
                vector,
                CancellationToken.None);
        }

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        var payload = result.Payload();
        payload.GetProperty("active").GetInt64().ShouldBe(5);
        payload.GetProperty("superseded").GetInt64().ShouldBe(0);
        payload.GetProperty("tombstoned").GetInt64().ShouldBe(1);
        payload.GetProperty("pendingCompaction").GetInt64().ShouldBe(3);
        payload.GetProperty("activeByScope").GetProperty("Project").GetInt64().ShouldBe(5);
        payload.GetProperty("activeByScope").GetProperty("Session").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Stats_CountsActiveDuplicatesAsIdempotencyConflicts()
    {
        await using var session = await McpTestSession.StartAsync();
        foreach (var index in new[] { 1, 2 })
        {
            await session.Store.UpsertAsync(
                Journal(Guid.NewGuid(), $"crashed write {index}", session.Clock.UtcNow) with { IdempotencyKey = "shared-key" },
                new float[] { 1, 0, 0 },
                CancellationToken.None);
        }

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.Payload().GetProperty("idempotencyConflicts").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Stats_EchoesTheResolvedTomeAndPerTomeCounts()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "own tome note",
            ["scope"] = "Global",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "alpha squad note",
            ["scope"] = "Global",
            ["tome"] = "alpha",
        });

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("tome").GetString().ShouldBe(McpTestSession.ClientActor);
        payload.GetProperty("byTome").GetProperty(McpTestSession.ClientActor).GetProperty("active").GetInt64().ShouldBe(1);
        payload.GetProperty("byTome").GetProperty("alpha").GetProperty("active").GetInt64().ShouldBe(1);
        payload.GetProperty("byTome").GetProperty("alpha").GetProperty("tombstoned").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Stats_ByDefault_OmitsThePerTomeBacklog()
    {
        await using var session = await McpTestSession.StartAsync();
        await SeedStaleJournals(session, ("alpha", [1f, 0.05f, 0f]), ("alpha", [1f, 0f, 0.05f]));

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("pendingCompaction").GetInt64().ShouldBe(2L);
        payload.TryGetProperty("pendingCompactionByTome", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Stats_WithIncludeTomeCompaction_SplitsTheBacklogPerTome()
    {
        await using var session = await McpTestSession.StartAsync();
        await SeedStaleJournals(
            session,
            ("alpha", [1f, 0.05f, 0f]),
            ("alpha", [1f, 0f, 0.05f]),
            (McpTestSession.ClientActor, [0f, 1f, 0.05f]),
            (McpTestSession.ClientActor, [0f, 0.95f, 0.05f]));

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>
        {
            ["includeTomeCompaction"] = true,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        var pending = payload.GetProperty("pendingCompactionByTome");
        pending.GetProperty("alpha").GetInt64().ShouldBe(2);
        pending.GetProperty(McpTestSession.ClientActor).GetInt64().ShouldBe(2);
    }

    private static async Task SeedStaleJournals(McpTestSession session, params (string Tome, float[] Vector)[] journals)
    {
        var stale = session.Clock.UtcNow.AddDays(-30);
        foreach (var (tome, vector) in journals)
        {
            await session.Store.UpsertAsync(
                Journal(Guid.NewGuid(), $"stale journal {tome} {vector[0]}{vector[1]}{vector[2]}", stale) with { Tome = tome },
                vector,
                CancellationToken.None);
        }
    }

    private static MemoryRecord Journal(Guid id, string content, DateTimeOffset accessed)
    {
        return new MemoryRecord
        {
            Id = id,
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = MemoryActor.Human,
            ProjectId = "grimoire",
            CreatedAt = accessed,
            LastAccessedAt = accessed,
        };
    }
}
