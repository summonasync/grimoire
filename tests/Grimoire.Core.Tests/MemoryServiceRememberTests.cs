using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceRememberTests
{
    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        ["deploy on friday"] = [1f, 0f, 0f],
        ["deploy on fridays"] = [0.97f, 0.24f, 0f],
        ["halfway related"] = [0.5f, 0.86f, 0f],
        ["coffee brewing"] = [0f, 1f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeEmbeddingProvider _embeddings = new(Vectors);

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceRememberTests()
    {
        _service = TestMemories.Service(_store, _embeddings, _clock);
    }

    private RememberRequest Request(
        string content,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        string? projectId = "grim",
        string? sessionId = null) => new()
        {
            Content = content,
            Scope = scope,
            Kind = kind,
            Actor = TestMemories.Agent,
            ProjectId = projectId,
            SessionId = sessionId,
            Tags = ["ops"],
        };

    [Fact]
    public async Task Remember_NearDuplicate_MarksOldRecordSuperseded()
    {
        var first = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var second = await _service.RememberAsync(Request("deploy on fridays"), CancellationToken.None);

        second.SupersededExisting.ShouldBeTrue();
        second.SupersededId.ShouldBe(first.Record.Id);

        var oldRecord = (await _store.GetAsync(first.Record.Id, CancellationToken.None))!;
        oldRecord.Status.ShouldBe(MemoryStatus.Superseded);
        oldRecord.SupersededBy.ShouldBe(second.Record.Id);
        oldRecord.TombstoneReason.ShouldBeNull();
    }

    [Fact]
    public async Task Remember_MultipleDuplicatesAboveThreshold_SupersedesAllMatches()
    {
        var orphan = TestMemories.Create("deploy on friday", [1f, 0f, 0f], projectId: "grim", tags: ["ops"]);
        var nearDuplicate = TestMemories.Create("deploy on fridays", [0.97f, 0.24f, 0f], projectId: "grim", tags: ["ops"]);
        _store.Seed(orphan, [1f, 0f, 0f]);
        _store.Seed(nearDuplicate, [0.97f, 0.24f, 0f]);

        var result = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.SupersededExisting.ShouldBeTrue();
        result.SupersededId.ShouldBe(orphan.Id);
        (await _store.GetAsync(orphan.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Superseded);
        (await _store.GetAsync(nearDuplicate.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Superseded);
        _store.Records.Count(record => record.Status == MemoryStatus.Active).ShouldBe(1);
        _store.Records.Single(record => record.Status == MemoryStatus.Active).Id.ShouldBe(result.Record.Id);
    }

    [Fact]
    public async Task Remember_DistinctContent_InsertsAlongsideExisting()
    {
        await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        var second = await _service.RememberAsync(Request("coffee brewing"), CancellationToken.None);

        second.SupersededExisting.ShouldBeFalse();
        second.SupersededId.ShouldBeNull();
        _store.Records.Count.ShouldBe(2);
        _store.Records.ShouldAllBe(record => record.Status == MemoryStatus.Active);
    }

    [Fact]
    public async Task Remember_BelowDedupThreshold_DoesNotSupersede()
    {
        await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        var second = await _service.RememberAsync(Request("halfway related"), CancellationToken.None);

        second.SupersededExisting.ShouldBeFalse();
        _store.Records.ShouldAllBe(record => record.Status == MemoryStatus.Active);
    }

    [Fact]
    public async Task Remember_DoesNotSupersedeAcrossScopes()
    {
        await _service.RememberAsync(Request("deploy on friday", scope: MemoryScope.Project), CancellationToken.None);

        var global = await _service.RememberAsync(Request("deploy on friday", scope: MemoryScope.Global, projectId: null), CancellationToken.None);

        global.SupersededExisting.ShouldBeFalse();
        _store.Records.ShouldAllBe(record => record.Status == MemoryStatus.Active);
    }

    [Fact]
    public async Task Remember_DoesNotSupersedeAcrossKinds()
    {
        await _service.RememberAsync(Request("deploy on friday", kind: MemoryKind.Journal), CancellationToken.None);

        var fact = await _service.RememberAsync(Request("deploy on friday", kind: MemoryKind.Fact), CancellationToken.None);

        fact.SupersededExisting.ShouldBeFalse();
    }

    [Fact]
    public async Task Remember_DoesNotSupersedeAcrossSessions()
    {
        await _service.RememberAsync(Request("deploy on friday", scope: MemoryScope.Session, sessionId: "s-1", projectId: null), CancellationToken.None);

        var otherSession = await _service.RememberAsync(Request("deploy on friday", scope: MemoryScope.Session, sessionId: "s-2", projectId: null), CancellationToken.None);

        otherSession.SupersededExisting.ShouldBeFalse();
    }

    [Fact]
    public async Task Remember_SupersedesWithinSameSession()
    {
        await _service.RememberAsync(Request("deploy on friday", scope: MemoryScope.Session, sessionId: "s-1", projectId: null), CancellationToken.None);

        var retry = await _service.RememberAsync(Request("deploy on fridays", scope: MemoryScope.Session, sessionId: "s-1", projectId: null), CancellationToken.None);

        retry.SupersededExisting.ShouldBeTrue();
    }

    [Fact]
    public async Task Remember_DoesNotSupersedePersonaRecords()
    {
        var persona = TestMemories.Create(
            "deploy on friday",
            [1f, 0f, 0f],
            scope: MemoryScope.Personal,
            kind: MemoryKind.Fact,
            tags: [PersonaSectionExtensions.PersonaTag, PersonaSection.Rules.SectionTag()]);
        _store.Seed(persona, [1f, 0f, 0f]);

        var result = await _service.RememberAsync(Request("deploy on fridays", scope: MemoryScope.Personal, kind: MemoryKind.Fact, projectId: null), CancellationToken.None);

        result.SupersededExisting.ShouldBeFalse();
        (await _store.GetAsync(persona.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Remember_AppliesSpecDefaults()
    {
        var result = await _service.RememberAsync(Request("coffee brewing"), CancellationToken.None);

        var record = result.Record;
        record.Id.ShouldNotBe(Guid.Empty);
        record.Importance.ShouldBe(0.5);
        record.CreatedAt.ShouldBe(_clock.UtcNow);
        record.LastAccessedAt.ShouldBe(_clock.UtcNow);
        record.AccessCount.ShouldBe(0);
        record.Status.ShouldBe(MemoryStatus.Active);
        record.ExpiresAt.ShouldBeNull();
        record.Actor.ShouldBe(TestMemories.Agent);
        record.Tags.ShouldBe(["ops"]);
        record.Sources.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_SessionScope_SetsExpiryFromOptions()
    {
        var options = new MemoryEngineOptions { SessionTtlHours = 2 };

        var service = TestMemories.Service(_store, new FakeEmbeddingProvider(Vectors), _clock, options);
        var result = await service.RememberAsync(Request("coffee brewing", scope: MemoryScope.Session, sessionId: "s-1", projectId: null), CancellationToken.None);

        result.Record.ExpiresAt.ShouldBe(_clock.UtcNow.AddHours(2));
    }

    [Fact]
    public async Task Remember_ClampsImportanceIntoUnitRange()
    {
        var request = Request("coffee brewing") with { Importance = 7 };

        var result = await _service.RememberAsync(request, CancellationToken.None);

        result.Record.Importance.ShouldBe(1);
    }

    [Fact]
    public async Task Remember_EmptyContent_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(() => _service.RememberAsync(Request("  "), CancellationToken.None));
    }

    [Fact]
    public async Task Remember_CarriesSourcesProvenance()
    {
        var source = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        var request = Request("coffee brewing", kind: MemoryKind.Fact) with { Sources = [source.Record.Id] };

        var compacted = await _service.RememberAsync(request, CancellationToken.None);

        compacted.Record.Sources.ShouldBe([source.Record.Id]);
    }

    [Fact]
    public async Task Remember_FirstWrite_IsNotReplayAndStampsContentKey()
    {
        var result = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.IdempotentReplay.ShouldBeFalse();
        result.MaintenancePending.ShouldBeFalse();
        result.Record.IdempotencyKey.ShouldBe(IdempotencyKey.Compute(Request("deploy on friday")));
    }

    [Fact]
    public async Task Remember_RetryWithinWindow_ReturnsOriginalWithoutReembedding()
    {
        var first = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(10));

        var retry = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        retry.IdempotentReplay.ShouldBeTrue();
        retry.Record.Id.ShouldBe(first.Record.Id);
        retry.Record.CreatedAt.ShouldBe(first.Record.CreatedAt);
        retry.SupersededExisting.ShouldBeFalse();
        retry.MaintenancePending.ShouldBeFalse();
        _embeddings.Requests.Count.ShouldBe(1);
        _store.UpsertCount.ShouldBe(1);
    }

    [Fact]
    public async Task Remember_ReplayAfterCrash_CompletesPendingSupersede()
    {
        var request = Request("deploy on friday");
        var crashed = TestMemories.Create(
            "deploy on friday",
            [1f, 0f, 0f],
            projectId: "grim",
            tags: ["ops"],
            idempotencyKey: IdempotencyKey.Compute(request));
        var straggler = TestMemories.Create("deploy on friday", [1f, 0f, 0f], projectId: "grim", tags: ["ops"]);
        _store.Seed(crashed, [1f, 0f, 0f]);
        _store.Seed(straggler, [1f, 0f, 0f]);

        var replay = await _service.RememberAsync(request, CancellationToken.None);

        replay.IdempotentReplay.ShouldBeTrue();
        replay.Record.Id.ShouldBe(crashed.Id);
        replay.MaintenancePending.ShouldBeFalse();
        _embeddings.Requests.ShouldBeEmpty();
        (await _store.GetAsync(straggler.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Superseded);
        (await _store.GetAsync(straggler.Id, CancellationToken.None))!.SupersededBy.ShouldBe(crashed.Id);
        _store.Records.Count(record => record.Status == MemoryStatus.Active).ShouldBe(1);
    }

    [Fact]
    public async Task Remember_SupersedeFailureAfterCommit_ReportsPendingWithoutThrowing()
    {
        var fragile = new FragileMemoryStore(_store) { UpdateFailuresRemaining = 1 };
        var service = TestMemories.Service(fragile, _embeddings, _clock);
        _store.Seed(TestMemories.Create("deploy on friday", [1f, 0f, 0f], projectId: "grim", tags: ["ops"]), [1f, 0f, 0f]);

        var result = await service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.MaintenancePending.ShouldBeTrue();
        result.SupersededExisting.ShouldBeFalse();
        result.Record.Status.ShouldBe(MemoryStatus.Active);
        (await _store.GetAsync(result.Record.Id, CancellationToken.None)).ShouldNotBeNull();
        _store.UpsertCount.ShouldBe(1);
    }

    [Fact]
    public async Task Remember_ReplayConvergesPendingSupersede()
    {
        var fragile = new FragileMemoryStore(_store) { UpdateFailuresRemaining = 1 };
        var service = TestMemories.Service(fragile, _embeddings, _clock);
        _store.Seed(TestMemories.Create("deploy on friday", [1f, 0f, 0f], projectId: "grim", tags: ["ops"]), [1f, 0f, 0f]);

        var first = await service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        first.MaintenancePending.ShouldBeTrue();

        var retry = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        retry.IdempotentReplay.ShouldBeTrue();
        retry.Record.Id.ShouldBe(first.Record.Id);
        retry.MaintenancePending.ShouldBeFalse();
        _store.Records.Count(record => record.Status == MemoryStatus.Active).ShouldBe(1);
        _store.Records.Single(record => record.Status == MemoryStatus.Active).Id.ShouldBe(first.Record.Id);
        _embeddings.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Remember_WindowExpiry_FallsThroughToNormalFlow()
    {
        var first = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(25));

        var retry = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        retry.IdempotentReplay.ShouldBeFalse();
        retry.Record.Id.ShouldNotBe(first.Record.Id);
        retry.SupersededExisting.ShouldBeTrue();
        retry.SupersededId.ShouldBe(first.Record.Id);
        _embeddings.Requests.Count.ShouldBe(2);
        _store.UpsertCount.ShouldBe(2);
    }
}
