using Grimoire.Core;
using Grimoire.Core.Consolidation;
using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceConsolidationTests
{
    private static readonly DateTimeOffset Stale = FakeClock.DefaultStart.AddDays(-30);

    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly FakeConsolidationStateStore _state = new();

    private MemoryService CreateService(IReadOnlyDictionary<string, float[]>? vectors = null) => new(
        _store,
        new FallbackEmbeddings(vectors ?? new Dictionary<string, float[]>()),
        _clock,
        consolidationStateStore: _state);

    [Fact]
    public async Task Consolidate_StaleCluster_ProducesOneCitedFactAndTagsSources()
    {
        var service = CreateService();
        var ids = SeedCluster(4, scope: MemoryScope.Project);

        var result = await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        result.Clusters.ShouldBe(1);
        var facts = Facts();
        facts.Count.ShouldBe(1);
        var fact = facts[0];
        fact.Kind.ShouldBe(MemoryKind.Fact);
        fact.Scope.ShouldBe(MemoryScope.Project);
        fact.Tome.ShouldBe(TomeKey.DefaultTome);
        fact.Tags.ShouldContain(ConsolidationOptions.Tag);
        fact.Sources.OrderBy(id => id).ShouldBe(ids.OrderBy(id => id));
        fact.Importance.ShouldBe(0.76);
        fact.Content.ShouldStartWith("Consolidated from 4 records . ");
        foreach (var id in ids)
        {
            fact.Content.ShouldContain($"grimoire://memory/{id}");
        }

        foreach (var source in Sources(ids))
        {
            source.Tags.ShouldContain(ConsolidationOptions.Tag);
            source.Status.ShouldBe(MemoryStatus.Active);
        }

        var readable = await service.ListAsync(new MemoryFilter { Kind = MemoryKind.Journal, Status = MemoryStatus.Active }, CancellationToken.None);
        readable.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Consolidate_SecondRun_AddsNothing()
    {
        var service = CreateService();
        SeedCluster(4, scope: MemoryScope.Project);

        await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);
        var second = await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        second.Clusters.ShouldBe(0);
        Facts().Count.ShouldBe(1);
    }

    [Fact]
    public async Task Consolidate_ConvergesAnOrphanFactLeftByAMidCrash()
    {
        var service = CreateService();
        var ids = SeedCluster(4, scope: MemoryScope.Project);
        var orphan = TestMemories.Create(
            "consolidated fact from an interrupted run",
            [0.995f, 0.0999f, 0f],
            scope: MemoryScope.Project,
            kind: MemoryKind.Fact,
            tags: [ConsolidationOptions.Tag]);
        _store.Seed(orphan, 0.995f, 0.0999f, 0f);

        var result = await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        result.Clusters.ShouldBe(1);
        var facts = Facts();
        facts.Count.ShouldBe(1);
        facts[0].Id.ShouldNotBe(orphan.Id);
        facts[0].Sources.OrderBy(id => id).ShouldBe(ids.OrderBy(id => id));

        var buried = await _store.GetAsync(orphan.Id, CancellationToken.None);
        buried!.Status.ShouldBe(MemoryStatus.Superseded);
        buried.SupersededBy.ShouldBe(facts[0].Id);
    }

    [Fact]
    public async Task Consolidate_IgnoresFreshClusters()
    {
        var service = CreateService();
        SeedCluster(4, scope: MemoryScope.Project, accessed: FakeClock.DefaultStart);

        var result = await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        result.Clusters.ShouldBe(0);
        Facts().ShouldBeEmpty();
    }

    [Fact]
    public async Task Consolidate_IgnoresClustersBelowMinConsolidationSize()
    {
        var service = CreateService();
        SeedCluster(3, scope: MemoryScope.Project);

        var result = await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        result.Clusters.ShouldBe(0);
        Facts().ShouldBeEmpty();
    }

    [Fact]
    public async Task Consolidate_SkipsAlreadyConsolidatedClusters()
    {
        var service = CreateService();
        var ids = SeedCluster(4, scope: MemoryScope.Project);
        foreach (var record in Sources(ids))
        {
            _store.Seed(record with { Tags = [ConsolidationOptions.Tag] }, [1f, 0f, 0f]);
        }

        var result = await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        result.Clusters.ShouldBe(0);
        Facts().ShouldBeEmpty();
    }

    [Fact]
    public async Task Consolidate_CitesSupersedeChainHeadsOnly()
    {
        var service = CreateService();
        var ids = SeedCluster(4, scope: MemoryScope.Project);
        var tail = Sources([ids[0]])[0];
        var head = Sources([ids[1]])[0];
        _store.Seed(tail with { SupersededBy = head.Id }, [1f, 0f, 0f]);

        var result = await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        result.Clusters.ShouldBe(1);
        var fact = Facts()[0];
        fact.Sources.Count.ShouldBe(3);
        fact.Sources.ShouldNotContain(ids[0]);
        fact.Sources.ShouldContain(ids[1]);
        fact.Content.ShouldNotContain($"grimoire://memory/{ids[0]}");
        fact.Importance.ShouldBe(0.72);
    }

    [Fact]
    public async Task Consolidate_StampsTopicTagWhenHalfTheMembersCarryOne()
    {
        var service = CreateService();
        var ids = SeedCluster(4, scope: MemoryScope.Project);
        var members = Sources(ids);
        _store.Seed(members[0] with { Tags = ["topic:deploy"] }, [1f, 0f, 0f]);
        _store.Seed(members[1] with { Tags = ["topic:deploy", "misc"] }, [0.97f, 0.24f, 0f]);

        await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        Facts()[0].Tags.ShouldBe([ConsolidationOptions.Tag, "topic:deploy"]);
        Facts()[0].Content.ShouldContain("topic:deploy");
    }

    [Fact]
    public async Task Consolidate_ClampsImportanceAtNineTenths()
    {
        var service = CreateService();
        SeedCluster(8, scope: MemoryScope.Project);

        await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        Facts()[0].Importance.ShouldBe(0.9);
    }

    [Fact]
    public async Task Consolidate_PicksMajorityScopeAndProject()
    {
        var service = CreateService();
        var ids = SeedCluster(4, scope: MemoryScope.Project);
        var members = Sources(ids);
        _store.Seed(members[0] with { Scope = MemoryScope.Global, ProjectId = "proj-a" }, [1f, 0f, 0f]);
        _store.Seed(members[1] with { ProjectId = "proj-a" }, [0.97f, 0.24f, 0f]);
        _store.Seed(members[2] with { ProjectId = "proj-a" }, [0.94f, 0.34f, 0f]);

        await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        var fact = Facts()[0];
        fact.Scope.ShouldBe(MemoryScope.Project);
        fact.ProjectId.ShouldBe("proj-a");
    }

    [Fact]
    public async Task Consolidate_KeepsTomesSeparate()
    {
        var service = CreateService();
        SeedCluster(4, scope: MemoryScope.Project);
        SeedCluster(4, scope: MemoryScope.Project, tome: "alpha");

        var result = await service.ConsolidateAsync(TestMemories.Agent, CancellationToken.None);

        result.Clusters.ShouldBe(2);
        Facts().Select(fact => fact.Tome).ShouldBe(["alpha", TomeKey.DefaultTome], ignoreOrder: true);
    }

    [Fact]
    public async Task Stats_WithoutAStateStore_ReportsUnknownConsolidationHeartbeat()
    {
        var service = new MemoryService(_store, new FallbackEmbeddings(new Dictionary<string, float[]>()), _clock);

        var stats = await service.StatsAsync(CancellationToken.None);

        stats.Consolidation.IsUnknown.ShouldBeTrue();
        stats.Consolidation.UnknownReason.ShouldBe("consolidation state store is not configured");
        stats.Consolidation.Stale.ShouldBeTrue();
    }

    [Fact]
    public async Task Stats_ReportsTheConsolidationHeartbeatFreshEnoughToNotBeStale()
    {
        _state.Current = new ConsolidationState
        {
            LastAttemptAt = _clock.UtcNow.AddHours(-1),
            LastSuccessAt = _clock.UtcNow.AddHours(-1),
            ConsolidatedClusters = 3,
        };
        var service = CreateService();

        var stats = await service.StatsAsync(CancellationToken.None);

        stats.Consolidation.IsUnknown.ShouldBeFalse();
        stats.Consolidation.LastSuccessAt.ShouldBe(_clock.UtcNow.AddHours(-1));
        stats.Consolidation.ConsolidatedClusters.ShouldBe(3);
        stats.Consolidation.Stale.ShouldBeFalse();
    }

    [Fact]
    public async Task Stats_FlagsTheConsolidationHeartbeatStalePastTwiceTheSweepInterval()
    {
        _state.Current = new ConsolidationState
        {
            LastAttemptAt = _clock.UtcNow.AddHours(-24),
            LastSuccessAt = _clock.UtcNow.AddHours(-24),
        };
        var service = CreateService();

        var stats = await service.StatsAsync(CancellationToken.None);

        stats.Consolidation.Stale.ShouldBeTrue();
    }

    private List<Guid> SeedCluster(
        int size,
        MemoryScope scope,
        string? tome = null,
        DateTimeOffset? accessed = null)
    {
        float[][] vectors =
        [
            [1f, 0f, 0f],
            [0.97f, 0.24f, 0f],
            [0.94f, 0.34f, 0f],
            [0.9f, 0.4359f, 0f],
            [0.87f, 0.49f, 0.1f],
            [0.95f, 0.3f, 0.05f],
            [0.92f, 0.38f, 0.08f],
            [0.89f, 0.45f, 0.05f],
        ];

        var ids = new List<Guid>(size);
        for (var index = 0; index < size; index++)
        {
            var record = TestMemories.Create(
                $"stale journal {index}",
                vectors[index],
                scope: scope,
                kind: MemoryKind.Journal,
                accessed: accessed ?? Stale,
                tome: tome);
            ids.Add(record.Id);
            _store.Seed(record, vectors[index]);
        }

        return ids;
    }

    private IReadOnlyList<MemoryRecord> Sources(IReadOnlyList<Guid> ids) => ids
        .Select(id => _store.GetAsync(id, CancellationToken.None).Result)
        .OfType<MemoryRecord>()
        .ToList();

    private List<MemoryRecord> Facts() => _store.Records
        .Where(record => record.Kind == MemoryKind.Fact && record.Status == MemoryStatus.Active)
        .ToList();

    private sealed class FallbackEmbeddings(IReadOnlyDictionary<string, float[]> vectors) : IEmbeddingProvider
    {
        private static readonly float[] Fallback = [1f, 0f, 0f];

        public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            var vector = vectors.TryGetValue(text, out var known) ? known : Fallback;
            return Task.FromResult(new ReadOnlyMemory<float>(vector));
        }
    }
}
