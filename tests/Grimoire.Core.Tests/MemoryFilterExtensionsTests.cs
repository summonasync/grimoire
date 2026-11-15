using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryFilterExtensionsTests
{
    private static readonly DateTimeOffset Start = FakeClock.DefaultStart;

    private static MemoryRecord Record(
        DateTimeOffset? created = null,
        MemoryActor? actor = null,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        MemoryStatus status = MemoryStatus.Active,
        IReadOnlyList<string>? tags = null) => TestMemories.Create(
            "note", [1f, 0f], scope: scope, kind: kind, actor: actor, created: created, status: status, tags: tags);

    [Fact]
    public void Matches_EmptyFilter_AcceptsAnyRecord() =>
        new MemoryFilter().Matches(Record()).ShouldBeTrue();

    [Theory]
    [InlineData(MemoryStatus.Superseded)]
    [InlineData(MemoryStatus.Tombstoned)]
    public void Matches_EmptyFilter_KeepsARecordThatIsNotActive(MemoryStatus status)
    {
        // The half the case above cannot see: its record carries the default Active, so a matcher
        // tightened to default to Active passes it and the leniency stays unstated. The leniency is
        // the rule — the store clause owns the default (MemoryStatuses.Effective renders the single
        // Active keyword when the filter names none), which is what keeps a recall narrowed exactly
        // once. 058's experiment A measured four Api failures for the tightening, and not one of
        // those failure messages said why leniency exists; this row says it.
        new MemoryFilter().Matches(Record(status: status)).ShouldBeTrue();
    }

    [Fact]
    public void Matches_CreatedAtEqualToSince_Inclusive() =>
        new MemoryFilter { CreatedAfter = Start }.Matches(Record(Start)).ShouldBeTrue();

    [Fact]
    public void Matches_CreatedAtOneTickBeforeSince_Excluded() =>
        new MemoryFilter { CreatedAfter = Start }.Matches(Record(Start.AddTicks(-1))).ShouldBeFalse();

    [Fact]
    public void Matches_CreatedAtEqualToUntil_Exclusive() =>
        new MemoryFilter { CreatedBefore = Start }.Matches(Record(Start)).ShouldBeFalse();

    [Fact]
    public void Matches_CreatedAtOneTickBeforeUntil_Included() =>
        new MemoryFilter { CreatedBefore = Start }.Matches(Record(Start.AddTicks(-1))).ShouldBeTrue();

    [Fact]
    public void Matches_ActorFullString_CaseInsensitive()
    {
        var filter = new MemoryFilter { Actor = "DotNet-Coder+Claude" };

        filter.Matches(Record()).ShouldBeTrue();
    }

    [Fact]
    public void Matches_ActorAliasOnly_MatchesVendoredRecord()
    {
        var filter = new MemoryFilter { Actor = "dotnet-coder" };

        filter.Matches(Record()).ShouldBeTrue();
    }

    [Fact]
    public void Matches_ActorAliasOnly_MatchesVendorlessRecord()
    {
        var filter = new MemoryFilter { Actor = "human" };

        filter.Matches(Record(actor: MemoryActor.Human)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("dotnet-coder+openai")]
    [InlineData("dotnet-coder+c")]
    [InlineData("dotnet-coder+claud")]
    public void Matches_ActorVendorSpecifier_RequiresExactVendor(string candidate)
    {
        var filter = new MemoryFilter { Actor = candidate };

        filter.Matches(Record()).ShouldBeFalse();
    }

    [Fact]
    public void Matches_ActorAliasOfOtherActor_Excluded()
    {
        var filter = new MemoryFilter { Actor = "human" };

        filter.Matches(Record()).ShouldBeFalse();
    }

    [Fact]
    public void Matches_AliasWithPlusInsideItsVendor_IsReachedByTheWholeVendorSegment()
    {
        // The delegation guard: the matcher owns no separator rule of its own, it asks
        // MemoryActorSemantics.Segments, which splits on the FIRST separator and leaves the rest to
        // the vendor. Re-implement that split here — LastIndexOf, or a plain Split with a parts[1]
        // read — and this case goes red while every trailing-separator test stays green, which is
        // how a second home for the parse announces itself.
        var record = Record(actor: MemoryActor.Agent("wave", "marshal+glm"));

        new MemoryFilter { Actor = "wave+marshal+glm" }.Matches(record).ShouldBeTrue();
        new MemoryFilter { Actor = "wave+glm" }.Matches(record).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Matches_BlankActorCandidate_NoConstraint(string candidate) =>
        new MemoryFilter { Actor = candidate }.Matches(Record(actor: MemoryActor.Human)).ShouldBeTrue();

    [Fact]
    public void Matches_FullConjunction_OnReferenceRecord()
    {
        var record = Record(
            created: Start,
            actor: TestMemories.Agent,
            scope: MemoryScope.Project,
            kind: MemoryKind.Journal,
            status: MemoryStatus.Active,
            tags: ["alpha", "beta"]);

        var filter = new MemoryFilter
        {
            Scopes = [MemoryScope.Project],
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Statuses = [MemoryStatus.Active],
            Tags = ["beta"],
            ExcludeTags = ["persona"],
            Tome = TomeKey.DefaultTome,
            CreatedAfter = Start.AddHours(-1),
            CreatedBefore = Start.AddHours(1),
            Actor = "dotnet-coder",
        };

        filter.Matches(record).ShouldBeTrue();
    }

    [Theory]
    [InlineData(CompositionDimension.Scope)]
    [InlineData(CompositionDimension.Kind)]
    [InlineData(CompositionDimension.Status)]
    [InlineData(CompositionDimension.Statuses)]
    [InlineData(CompositionDimension.Tags)]
    [InlineData(CompositionDimension.ExcludeTags)]
    [InlineData(CompositionDimension.Tome)]
    [InlineData(CompositionDimension.Since)]
    [InlineData(CompositionDimension.Until)]
    [InlineData(CompositionDimension.Actor)]
    public void Matches_FlippedDimension_ExcludesRecord(CompositionDimension dimension)
    {
        var record = Record(
            created: Start,
            actor: TestMemories.Agent,
            scope: MemoryScope.Project,
            kind: MemoryKind.Journal,
            status: MemoryStatus.Active,
            tags: ["alpha", "beta"]);

        var filter = new MemoryFilter
        {
            Scopes = [MemoryScope.Project],
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Statuses = [MemoryStatus.Active],
            Tags = ["beta"],
            ExcludeTags = ["persona"],
            Tome = TomeKey.DefaultTome,
            CreatedAfter = Start.AddHours(-1),
            CreatedBefore = Start.AddHours(1),
            Actor = "dotnet-coder",
        };

        var flipped = dimension switch
        {
            CompositionDimension.Scope => filter with { Scopes = [MemoryScope.Session] },
            CompositionDimension.Kind => filter with { Kind = MemoryKind.Fact },
            CompositionDimension.Status => filter with { Status = MemoryStatus.Tombstoned },
            CompositionDimension.Statuses => filter with { Statuses = [MemoryStatus.Superseded] },
            CompositionDimension.Tags => filter with { Tags = ["gamma"] },
            CompositionDimension.ExcludeTags => filter with { ExcludeTags = ["alpha"] },
            CompositionDimension.Tome => filter with { Tome = "other-tome" },
            CompositionDimension.Since => filter with { CreatedAfter = Start.AddHours(1) },
            CompositionDimension.Until => filter with { CreatedBefore = Start },
            CompositionDimension.Actor => filter with { Actor = "human" },
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        };

        flipped.Matches(record).ShouldBeFalse();
    }

    [Theory]
    [InlineData(FilterDimension.Scopes)]
    [InlineData(FilterDimension.Kind)]
    [InlineData(FilterDimension.Status)]
    [InlineData(FilterDimension.Statuses)]
    [InlineData(FilterDimension.Tags)]
    [InlineData(FilterDimension.ExcludeTags)]
    [InlineData(FilterDimension.Tome)]
    [InlineData(FilterDimension.Tomes)]
    [InlineData(FilterDimension.ProjectId)]
    [InlineData(FilterDimension.SessionId)]
    [InlineData(FilterDimension.ExpiresBefore)]
    [InlineData(FilterDimension.TombstonedBefore)]
    [InlineData(FilterDimension.IdempotencyKey)]
    [InlineData(FilterDimension.CreatedAfter)]
    public async Task Matches_AgreesWithStoreFiltering(FilterDimension dimension)
    {
        var (filter, passing, failing) = DimensionCase(dimension);
        var store = new FakeMemoryStore();
        store.Seed(passing, 1f, 0f);
        store.Seed(failing, 1f, 0f);

        var listed = await store.ListAsync(filter, CancellationToken.None);

        filter.Matches(passing).ShouldBe(listed.Any(record => record.Id == passing.Id));
        filter.Matches(failing).ShouldBe(listed.Any(record => record.Id == failing.Id));
    }

    private static (MemoryFilter Filter, MemoryRecord Passing, MemoryRecord Failing) DimensionCase(
        FilterDimension dimension) => dimension switch
        {
            FilterDimension.Scopes => (new MemoryFilter { Scopes = [MemoryScope.Project] },
                DimensionRecord(), DimensionRecord(scope: MemoryScope.Session)),
            FilterDimension.Kind => (new MemoryFilter { Kind = MemoryKind.Journal },
                DimensionRecord(), DimensionRecord(kind: MemoryKind.Fact)),
            FilterDimension.Status => (new MemoryFilter { Status = MemoryStatus.Active },
                DimensionRecord(), DimensionRecord(status: MemoryStatus.Tombstoned)),
            FilterDimension.Statuses => (new MemoryFilter { Statuses = [MemoryStatus.Active] },
                DimensionRecord(), DimensionRecord(status: MemoryStatus.Superseded)),
            FilterDimension.Tags => (new MemoryFilter { Tags = ["alpha"] },
                DimensionRecord(tags: ["alpha"]), DimensionRecord(tags: ["beta"])),
            FilterDimension.ExcludeTags => (new MemoryFilter { ExcludeTags = ["alpha"] },
                DimensionRecord(tags: ["beta"]), DimensionRecord(tags: ["alpha"])),
            FilterDimension.Tome => (new MemoryFilter { Tome = TomeKey.DefaultTome },
                DimensionRecord(), DimensionRecord(tome: "other-tome")),
            FilterDimension.Tomes => (new MemoryFilter { Tomes = [TomeKey.DefaultTome] },
                DimensionRecord(), DimensionRecord(tome: "other-tome")),
            FilterDimension.ProjectId => (new MemoryFilter { ProjectId = "p-1" },
                DimensionRecord(projectId: "p-1"), DimensionRecord(projectId: "p-2")),
            FilterDimension.SessionId => (new MemoryFilter { SessionId = "s-1" },
                DimensionRecord(sessionId: "s-1"), DimensionRecord(sessionId: "s-2")),
            FilterDimension.ExpiresBefore => (new MemoryFilter { ExpiresBefore = Start.AddHours(6) },
                DimensionRecord(expiresAt: Start.AddHours(5)), DimensionRecord(expiresAt: Start.AddHours(9))),
            FilterDimension.TombstonedBefore => (new MemoryFilter { TombstonedBefore = Start.AddHours(6) },
                DimensionRecord(tombstonedAt: Start.AddHours(5)), DimensionRecord(tombstonedAt: Start.AddHours(9))),
            FilterDimension.IdempotencyKey => (new MemoryFilter { IdempotencyKey = "key-1" },
                DimensionRecord(idempotencyKey: "key-1"), DimensionRecord(idempotencyKey: "key-2")),
            FilterDimension.CreatedAfter => (new MemoryFilter { CreatedAfter = Start },
                DimensionRecord(), DimensionRecord(created: Start.AddHours(-1))),
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        };

    private static MemoryRecord DimensionRecord(
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        MemoryStatus status = MemoryStatus.Active,
        MemoryActor? actor = null,
        DateTimeOffset? created = null,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? tombstonedAt = null,
        string? idempotencyKey = null,
        string? projectId = null,
        string? sessionId = null,
        IReadOnlyList<string>? tags = null,
        string? tome = null) => TestMemories.Create(
            "note", [1f, 0f],
            scope: scope,
            kind: kind,
            actor: actor,
            created: created,
            expiresAt: expiresAt,
            status: status,
            tombstonedAt: tombstonedAt,
            idempotencyKey: idempotencyKey,
            projectId: projectId,
            sessionId: sessionId,
            tags: tags,
            tome: tome);
}

public enum FilterDimension
{
    Scopes,
    Kind,
    Status,
    Statuses,
    Tags,
    ExcludeTags,
    Tome,
    Tomes,
    ProjectId,
    SessionId,
    ExpiresBefore,
    TombstonedBefore,
    IdempotencyKey,
    CreatedAfter,
}

public enum CompositionDimension
{
    Scope,
    Kind,
    Status,
    Statuses,
    Tags,
    ExcludeTags,
    Tome,
    Since,
    Until,
    Actor,
}
