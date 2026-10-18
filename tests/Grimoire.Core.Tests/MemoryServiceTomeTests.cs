using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceTomeTests
{
    private const string QueryText = "deployment";

    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        [QueryText] = [1f, 0f, 0f],
        ["deploy on friday"] = [0.98f, 0.199f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceTomeTests()
    {
        _service = TestMemories.Service(_store, new FakeEmbeddingProvider(Vectors), _clock);
    }

    [Fact]
    public async Task Remember_ExplicitTome_IsNormalizedOnRecord()
    {
        var result = await _service.RememberAsync(Request("deploy on friday", tome: "Alpha Squad"), CancellationToken.None);

        result.Record.Tome.ShouldBe("alpha-squad");
    }

    [Fact]
    public async Task Remember_WithoutTome_StampsWithDefault()
    {
        var result = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.Record.Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public async Task Remember_UsesConfiguredDefaultTome()
    {
        var service = TestMemories.Service(
            _store,
            new FakeEmbeddingProvider(Vectors),
            _clock,
            new MemoryEngineOptions { DefaultTome = "Product Assistants" });

        var result = await service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.Record.Tome.ShouldBe("product-assistants");
    }

    [Fact]
    public async Task Remember_SameContentInDifferentTomes_ProducesDistinctKeys()
    {
        var first = await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);
        var second = await _service.RememberAsync(Request("deploy on friday", tome: "beta"), CancellationToken.None);

        first.IdempotentReplay.ShouldBeFalse();
        second.IdempotentReplay.ShouldBeFalse();
        first.Record.IdempotencyKey.ShouldNotBe(second.Record.IdempotencyKey);
        _store.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Remember_ReplayLookup_DoesNotLeakAcrossTomes()
    {
        await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);

        var replay = await _service.RememberAsync(Request("deploy on friday", tome: "beta"), CancellationToken.None);

        replay.IdempotentReplay.ShouldBeFalse();
        replay.Record.Tome.ShouldBe("beta");
        _store.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Remember_DuplicateSupersedeStaysWithinTome()
    {
        await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);

        var beta = await _service.RememberAsync(Request("deploy on friday", tome: "beta"), CancellationToken.None);

        beta.SupersededExisting.ShouldBeFalse();
        _store.Records.Count(record => record.Tome == "alpha" && record.Status == MemoryStatus.Active).ShouldBe(1);
        _store.Records.Count(record => record.Tome == "beta" && record.Status == MemoryStatus.Active).ShouldBe(1);
    }

    [Fact]
    public async Task Recall_OnlySeesResolvedTome()
    {
        await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);
        await _service.RememberAsync(Request("deploy on friday", tome: "beta"), CancellationToken.None);

        var alpha = await _service.RecallAsync(Query(tome: "alpha"), CancellationToken.None);
        var beta = await _service.RecallAsync(Query(tome: "beta"), CancellationToken.None);

        alpha.Select(match => match.Record.Tome).ShouldBe(["alpha"]);
        beta.Select(match => match.Record.Tome).ShouldBe(["beta"]);
    }

    [Fact]
    public async Task Recall_DefaultsToDefaultTome()
    {
        await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);

        var unscoped = await _service.RecallAsync(Query(), CancellationToken.None);
        var alpha = await _service.RecallAsync(Query(tome: "alpha"), CancellationToken.None);

        unscoped.Select(match => match.Record.Tome).ShouldBe([TomeKey.DefaultTome]);
        alpha.Select(match => match.Record.Tome).ShouldBe(["alpha"]);
    }

    [Fact]
    public async Task List_FiltersByResolvedTome()
    {
        await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);
        await _service.RememberAsync(Request("deploy on friday", tome: "beta"), CancellationToken.None);

        var listed = await _service.ListAsync(new MemoryFilter(), CancellationToken.None);

        listed.Count.ShouldBe(0);
        var beta = await _service.ListAsync(new MemoryFilter { Tome = "beta" }, CancellationToken.None);
        beta.Select(record => record.Tome).ShouldBe(["beta"]);
    }

    [Fact]
    public async Task Forget_InOtherTome_RefusesAndKeepsRecord()
    {
        var written = await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);

        var forgotten = await _service.ForgetAsync(written.Record.Id, "forgotten", CancellationToken.None, "beta");

        forgotten.ShouldBeFalse();
        _store.Records.Single(record => record.Id == written.Record.Id).Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Forget_InOwnTome_Tombstones()
    {
        var written = await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);

        var forgotten = await _service.ForgetAsync(written.Record.Id, "forgotten", CancellationToken.None, "alpha");

        forgotten.ShouldBeTrue();
        _store.Records.Single(record => record.Id == written.Record.Id).Status.ShouldBe(MemoryStatus.Tombstoned);
    }

    [Fact]
    public async Task Recall_ExplicitTomeList_ReadsAcrossTheListedTomes()
    {
        await _service.RememberAsync(Request("deploy on friday", tome: "alpha"), CancellationToken.None);
        await _service.RememberAsync(Request("deploy on friday", tome: "beta"), CancellationToken.None);
        await _service.RememberAsync(Request("deploy on friday", tome: "gamma"), CancellationToken.None);

        var across = await _service.RecallAsync(new RecallQuery
        {
            Text = QueryText,
            Tomes = ["alpha", "beta"],
        }, CancellationToken.None);

        across.Count.ShouldBe(2);
        across.Select(match => match.Record.Tome).OrderBy(tome => tome, StringComparer.Ordinal)
            .ShouldBe(["alpha", "beta"]);
    }

    [Fact]
    public async Task Recall_TomeList_IsNormalizedDeduplicatedAndOrdered()
    {
        await _service.RememberAsync(Request("deploy on friday", tome: "alpha-squad"), CancellationToken.None);

        var across = await _service.RecallAsync(new RecallQuery
        {
            Text = QueryText,
            Tomes = ["gamma", "Alpha Squad", "alpha-squad"],
        }, CancellationToken.None);

        across.Select(match => match.Record.Tome).ShouldBe(["alpha-squad"]);
    }

    [Fact]
    public async Task GetCompactionCandidates_PlansOwnTomeOnly()
    {
        var stale = FakeClock.DefaultStart.AddDays(-30);
        SeedJournal("stale alpha one", "alpha", [1f, 0f, 0f], stale);
        SeedJournal("stale alpha two", "alpha", [0.97f, 0.24f, 0f], stale);
        SeedJournal("stale beta one", "beta", [0.99f, 0.141f, 0f], stale);
        SeedJournal("stale beta two", "beta", [0.96f, 0.28f, 0f], stale);

        var alphaGroups = await _service.GetCompactionCandidatesAsync(CancellationToken.None, "alpha");
        var betaGroups = await _service.GetCompactionCandidatesAsync(CancellationToken.None, "beta");

        alphaGroups.SelectMany(group => group.MemoryIds).Count().ShouldBe(2);
        alphaGroups.ShouldAllBe(group => group.MemoryIds.All(id =>
            _store.Records.Single(record => record.Id == id).Tome == "alpha"));
        betaGroups.ShouldAllBe(group => group.MemoryIds.All(id =>
            _store.Records.Single(record => record.Id == id).Tome == "beta"));
    }

    private void SeedJournal(string content, string tome, float[] vector, DateTimeOffset accessed)
    {
        var record = TestMemories.Create(
            content,
            vector,
            scope: MemoryScope.Project,
            kind: MemoryKind.Journal,
            accessed: accessed,
            tome: tome);
        _store.Seed(record, vector);
    }

    private static RememberRequest Request(string content, string? tome = null) => new()
    {
        Content = content,
        Scope = MemoryScope.Project,
        Kind = MemoryKind.Journal,
        Actor = TestMemories.Agent,
        Tome = tome,
    };

    private static RecallQuery Query(string? tome = null) => new()
    {
        Text = QueryText,
        Tome = tome,
    };
}
