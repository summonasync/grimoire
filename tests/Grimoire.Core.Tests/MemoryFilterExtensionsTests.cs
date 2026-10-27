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
