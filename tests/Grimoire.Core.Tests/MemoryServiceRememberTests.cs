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

    [Fact]
    public async Task Remember_MissingSource_ThrowsListingTheMissingIds()
    {
        var known = TestMemories.Create("known source", [0f, 1f, 0f]);
        _store.Seed(known, [0f, 1f, 0f]);
        var ghost = Guid.NewGuid();

        var error = await Should.ThrowAsync<ArgumentException>(() =>
            _service.RememberAsync(Request("deploy on friday") with { Sources = [known.Id, ghost] }, CancellationToken.None));

        error.Message.ShouldContain(ghost.ToString());
        error.Message.ShouldNotContain(known.Id.ToString());
    }

    [Fact]
    public async Task Remember_WithExistingSources_SucceedsAndKeepsTheLinks()
    {
        var source = TestMemories.Create("known source", [0f, 1f, 0f]);
        _store.Seed(source, [0f, 1f, 0f]);

        var result = await _service.RememberAsync(
            Request("deploy on friday") with { Sources = [source.Id] },
            CancellationToken.None);

        result.Record.Sources.ShouldBe([source.Id]);
        (await _store.GetAsync(source.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Remember_SourceInOtherTome_IsRejectedAsMissing()
    {
        var foreign = TestMemories.Create("foreign source", [0f, 1f, 0f], tome: "elsewhere");
        _store.Seed(foreign, [0f, 1f, 0f]);

        var error = await Should.ThrowAsync<ArgumentException>(() =>
            _service.RememberAsync(Request("deploy on friday") with { Sources = [foreign.Id] }, CancellationToken.None));

        error.Message.ShouldContain(foreign.Id.ToString());
    }

    [Fact]
    public async Task Remember_SupersedeDuplicate_AppendsTrailLineOnTheOldRecord()
    {
        var first = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        await _service.RememberAsync(Request("deploy on fridays"), CancellationToken.None);

        var old = (await _store.GetAsync(first.Record.Id, CancellationToken.None))!;
        old.Status.ShouldBe(MemoryStatus.Superseded);
        var line = old.Corrections.Single();
        line.Action.ShouldBe(CorrectionAction.Supersede);
        line.Reason.ShouldBe($"superseded by {old.SupersededBy}");
        line.Actor.ShouldBe(TestMemories.Agent);
        line.At.ShouldBe(_clock.UtcNow);
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

    [Fact]
    public async Task Remember_BelowNearBand_CarriesNoNearMatches()
    {
        await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        var second = await _service.RememberAsync(Request("halfway related"), CancellationToken.None);

        second.NearMatches.ShouldBeEmpty();
        second.SupersededExisting.ShouldBeFalse();
    }

    [Fact]
    public async Task Remember_InNearBand_ReturnsMatchAndKeepsOriginal()
    {
        var original = TestMemories.Create("staging deploy window moved", [0.85f, 0.52678f, 0f], projectId: "grim", tags: ["ops"]);
        _store.Seed(original, [0.85f, 0.52678f, 0f]);

        var result = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        var near = result.NearMatches.Single();
        near.MemoryId.ShouldBe(original.Id);
        near.Similarity.ShouldBe(0.85, 0.001);
        near.Snippet.ShouldBe("staging deploy window moved");
        result.SupersededExisting.ShouldBeFalse();
        (await _store.GetAsync(original.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Remember_InNearBand_AppliesConfiguredThreshold()
    {
        var options = new MemoryEngineOptions { NearDuplicateSimilarityThreshold = 0.86 };
        var service = TestMemories.Service(_store, _embeddings, _clock, options);
        _store.Seed(
            TestMemories.Create("staging deploy window moved", [0.85f, 0.52678f, 0f], projectId: "grim", tags: ["ops"]),
            [0.85f, 0.52678f, 0f]);

        var result = await service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.NearMatches.ShouldBeEmpty();
        result.SupersededExisting.ShouldBeFalse();
    }

    [Fact]
    public async Task Remember_InNearBand_OrdersBySimilarityAndCapsAtThree()
    {
        SeedNear(0.85f);
        SeedNear(0.84f);
        SeedNear(0.83f);
        SeedNear(0.82f);

        var result = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.NearMatches.Count.ShouldBe(3);
        result.NearMatches[0].Similarity.ShouldBe(0.85, 0.001);
        result.NearMatches[1].Similarity.ShouldBe(0.84, 0.001);
        result.NearMatches[2].Similarity.ShouldBe(0.83, 0.001);
    }

    [Fact]
    public async Task Remember_InNearBand_TruncatesSnippetToFirstLineWithinLimit()
    {
        var content = new string('x', 200) + "\nsecond line";
        _store.Seed(TestMemories.Create(content, [0.85f, 0.52678f, 0f], projectId: "grim", tags: ["ops"]), [0.85f, 0.52678f, 0f]);

        var result = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.NearMatches.Single().Snippet.ShouldBe(new string('x', 160));
    }

    [Fact]
    public async Task Remember_InDedupBand_SupersedesWithoutNearMatches()
    {
        var first = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var second = await _service.RememberAsync(Request("deploy on fridays"), CancellationToken.None);

        second.SupersededExisting.ShouldBeTrue();
        second.NearMatches.ShouldBeEmpty();
        (await _store.GetAsync(first.Record.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Superseded);
    }

    [Fact]
    public async Task Remember_InNearBand_ReusesProbeEmbedding()
    {
        await _service.RememberAsync(Request("coffee brewing"), CancellationToken.None);
        _store.Seed(
            TestMemories.Create("staging deploy window moved", [0.85f, 0.52678f, 0f], projectId: "grim", tags: ["ops"]),
            [0.85f, 0.52678f, 0f]);

        await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        _embeddings.Requests.ShouldBe(["coffee brewing", "deploy on friday"]);
    }

    [Fact]
    public async Task Remember_WithSupersedes_SupersedesTargetAndPrefersItInResult()
    {
        var target = await _service.RememberAsync(Request("coffee brewing"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.RememberAsync(Request("deploy on friday") with { Supersedes = target.Record.Id }, CancellationToken.None);

        result.SupersededId.ShouldBe(target.Record.Id);
        result.NearMatches.ShouldBeEmpty();
        var superseded = (await _store.GetAsync(target.Record.Id, CancellationToken.None))!;
        superseded.Status.ShouldBe(MemoryStatus.Superseded);
        superseded.SupersededBy.ShouldBe(result.Record.Id);
        var line = superseded.Corrections.Single();
        line.Action.ShouldBe(CorrectionAction.Supersede);
        line.Reason.ShouldBe($"superseded by {result.Record.Id}");
    }

    [Fact]
    public async Task Remember_SupersedesUnknownTarget_AppliesNothing()
    {
        var error = await Should.ThrowAsync<ArgumentException>(
            () => _service.RememberAsync(Request("deploy on friday") with { Supersedes = Guid.NewGuid() }, CancellationToken.None));

        error.Message.ShouldContain("does not exist in tome");
        _store.Records.ShouldBeEmpty();
        _embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_SupersedesForeignTomeTarget_AppliesNothing()
    {
        var foreign = TestMemories.Create("coffee brewing", [0f, 1f, 0f], tome: "elsewhere");
        _store.Seed(foreign, [0f, 1f, 0f]);

        var error = await Should.ThrowAsync<ArgumentException>(
            () => _service.RememberAsync(Request("deploy on friday") with { Supersedes = foreign.Id }, CancellationToken.None));

        error.Message.ShouldContain("does not exist in tome");
        _store.Records.Single().Id.ShouldBe(foreign.Id);
        _embeddings.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(MemoryStatus.Tombstoned)]
    [InlineData(MemoryStatus.Superseded)]
    public async Task Remember_SupersedesInactiveTarget_AppliesNothing(MemoryStatus status)
    {
        var target = TestMemories.Create(
            "coffee brewing",
            [0f, 1f, 0f],
            status: status,
            tombstoneReason: status == MemoryStatus.Tombstoned ? "gone" : null,
            tombstonedAt: status == MemoryStatus.Tombstoned ? FakeClock.DefaultStart : null,
            supersededBy: status == MemoryStatus.Superseded ? Guid.NewGuid() : null);
        _store.Seed(target, [0f, 1f, 0f]);

        var error = await Should.ThrowAsync<ArgumentException>(
            () => _service.RememberAsync(Request("deploy on friday") with { Supersedes = target.Id }, CancellationToken.None));

        error.Message.ShouldContain("not active");
        _store.Records.Single().Id.ShouldBe(target.Id);
        target.Status.ShouldBe(status);
        _embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_ProbeAndExplicitNameSameTarget_SupersedesOnce()
    {
        var target = TestMemories.Create("deploy on friday", [1f, 0f, 0f], projectId: "grim", tags: ["ops"]);
        _store.Seed(target, [1f, 0f, 0f]);

        var result = await _service.RememberAsync(Request("deploy on friday") with { Supersedes = target.Id }, CancellationToken.None);

        result.SupersededId.ShouldBe(target.Id);
        var superseded = (await _store.GetAsync(target.Id, CancellationToken.None))!;
        superseded.Status.ShouldBe(MemoryStatus.Superseded);
        superseded.SupersededBy.ShouldBe(result.Record.Id);
        superseded.Corrections.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Remember_ProbeAndExplicitNameDifferentTargets_SupersedesBoth()
    {
        var probed = TestMemories.Create("deploy on friday", [1f, 0f, 0f], projectId: "grim", tags: ["ops"]);
        _store.Seed(probed, [1f, 0f, 0f]);
        var explicitTarget = await _service.RememberAsync(Request("coffee brewing"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.RememberAsync(Request("deploy on friday") with { Supersedes = explicitTarget.Record.Id }, CancellationToken.None);

        result.SupersededId.ShouldBe(explicitTarget.Record.Id);
        (await _store.GetAsync(probed.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Superseded);
        (await _store.GetAsync(explicitTarget.Record.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Superseded);
        _store.Records.Count(record => record.Status == MemoryStatus.Active).ShouldBe(1);
    }

    [Fact]
    public async Task Remember_ExplicitTargetInNearBand_SupersedesItAndSkipsTheNearListing()
    {
        var target = TestMemories.Create("staging deploy window moved", [0.85f, 0.52678f, 0f], projectId: "grim", tags: ["ops"]);
        _store.Seed(target, [0.85f, 0.52678f, 0f]);

        var result = await _service.RememberAsync(Request("deploy on friday") with { Supersedes = target.Id }, CancellationToken.None);

        result.SupersededId.ShouldBe(target.Id);
        result.NearMatches.ShouldBeEmpty();
        (await _store.GetAsync(target.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Superseded);
    }

    [Fact]
    public async Task Restore_ExplicitSupersede_ReversesLikeAuto()
    {
        var target = await _service.RememberAsync(Request("coffee brewing"), CancellationToken.None);
        var successor = await _service.RememberAsync(Request("deploy on friday") with { Supersedes = target.Record.Id }, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(3));
        var corrections = TestMemories.Corrections(_store, _clock);

        var restored = await corrections.RestoreAsync(target.Record.Id, "coffee was right", CancellationToken.None);

        restored.Restored.ShouldBeTrue();
        var revived = (await _store.GetAsync(target.Record.Id, CancellationToken.None))!;
        revived.Status.ShouldBe(MemoryStatus.Active);
        revived.SupersededBy.ShouldBe(successor.Record.Id);
        revived.Corrections.Last().Action.ShouldBe(CorrectionAction.Restore);
        var tombstonedSuccessor = (await _store.GetAsync(successor.Record.Id, CancellationToken.None))!;
        tombstonedSuccessor.Status.ShouldBe(MemoryStatus.Tombstoned);
        tombstonedSuccessor.TombstoneReason.ShouldBe($"restored {target.Record.Id}");
    }

    [Fact]
    public async Task Remember_TaughtFollowUpWithSupersedes_IsNotAReplayAndSupersedesTarget()
    {
        var target = await _service.RememberAsync(Request("coffee brewing"), CancellationToken.None);
        await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var followUp = await _service.RememberAsync(Request("deploy on friday") with { Supersedes = target.Record.Id }, CancellationToken.None);

        followUp.IdempotentReplay.ShouldBeFalse();
        followUp.SupersededId.ShouldBe(target.Record.Id);
        (await _store.GetAsync(target.Record.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Superseded);
        _store.Records.Count(record => record.Content == "deploy on friday").ShouldBe(2);
    }

    [Fact]
    public async Task Remember_RetryWithSameSupersedes_ReplaysWithoutDoubleSupersede()
    {
        var target = await _service.RememberAsync(Request("coffee brewing"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var first = await _service.RememberAsync(Request("deploy on friday") with { Supersedes = target.Record.Id }, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var retry = await _service.RememberAsync(Request("deploy on friday") with { Supersedes = target.Record.Id }, CancellationToken.None);

        retry.IdempotentReplay.ShouldBeTrue();
        retry.Record.Id.ShouldBe(first.Record.Id);
        retry.SupersededId.ShouldBeNull();
        var superseded = (await _store.GetAsync(target.Record.Id, CancellationToken.None))!;
        superseded.Status.ShouldBe(MemoryStatus.Superseded);
        superseded.SupersededBy.ShouldBe(first.Record.Id);
        superseded.Corrections.Count.ShouldBe(1);
        _store.UpsertCount.ShouldBe(2);
    }

    [Fact]
    public async Task Remember_AtExactNearBoundary_MatchIsIncluded()
    {
        var seed = new float[] { 0.85f, 0.52678f, 0f };
        var boundary = VectorMath.CosineSimilarity(Vectors["deploy on friday"], seed);
        var options = new MemoryEngineOptions { NearDuplicateSimilarityThreshold = boundary };
        var service = TestMemories.Service(_store, _embeddings, _clock, options);
        _store.Seed(TestMemories.Create("staging deploy window moved", seed, projectId: "grim", tags: ["ops"]), seed);

        var result = await service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.NearMatches.Single().Similarity.ShouldBe(boundary);
    }

    [Fact]
    public async Task Remember_AtExactDedupBoundary_MatchIsSupersededAndNotListed()
    {
        var seed = new float[] { 0.85f, 0.52678f, 0f };
        var boundary = VectorMath.CosineSimilarity(Vectors["deploy on friday"], seed);
        var options = new MemoryEngineOptions { DedupSimilarityThreshold = boundary };
        var service = TestMemories.Service(_store, _embeddings, _clock, options);
        _store.Seed(TestMemories.Create("staging deploy window moved", seed, projectId: "grim", tags: ["ops"]), seed);

        var result = await service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.SupersededId.ShouldNotBeNull();
        result.NearMatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_BlankFirstLine_YieldsEmptySnippet()
    {
        _store.Seed(
            TestMemories.Create("\nsecond line has the words", [0.85f, 0.52678f, 0f], projectId: "grim", tags: ["ops"]),
            [0.85f, 0.52678f, 0f]);

        var result = await _service.RememberAsync(Request("deploy on friday"), CancellationToken.None);

        result.NearMatches.Single().Snippet.ShouldBeEmpty();
    }

    private void SeedNear(float similarity)
    {
        var vector = new[] { similarity, MathF.Sqrt(1f - similarity * similarity), 0f };
        _store.Seed(TestMemories.Create($"near {similarity}", vector, projectId: "grim", tags: ["ops"]), vector);
    }
}
