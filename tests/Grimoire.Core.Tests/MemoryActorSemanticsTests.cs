using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

// One home for "what does an actor candidate mean at the store", for the same reason task 01 opened
// that home for statuses: the translator and the in-process doubles used to hold private opinions,
// and a double that ignores a dimension makes an unprovable claim look tested.
public sealed class MemoryActorSemanticsTests
{
    [Fact]
    public void Segments_AliasOnly_YieldsTheAliasAlone()
    {
        MemoryActorSemantics.Segments("kelthuzad").ShouldBe([("kelthuzad", null)]);
    }

    [Fact]
    public void Segments_AliasAndVendor_YieldsBoth()
    {
        MemoryActorSemantics.Segments("kelthuzad+qwen").ShouldBe([("kelthuzad", "qwen")]);
    }

    [Fact]
    public void Segments_TrailingSeparator_NamesNoVendor()
    {
        MemoryActorSemantics.Segments("kelthuzad+").ShouldBe([("kelthuzad", null)]);
    }

    [Fact]
    public void Segments_KeepsTheCandidateVerbatimIncludingCase()
    {
        MemoryActorSemantics.Segments("Kelthuzad+QWEN").ShouldBe([("Kelthuzad", "QWEN")]);
    }

    [Fact]
    public void Segments_SplitsOnTheFirstSeparator_KeepingTheRestAsVendor()
    {
        // A vendor is whatever follows the first separator; a stray one inside an alias is the
        // alias's business, same rule ActorResolver.Parse applies on the write side.
        MemoryActorSemantics.Segments("wave+marshal+glm").ShouldBe([("wave", "marshal+glm")]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Segments_BlankCandidate_ConstrainsNothing(string? candidate)
    {
        MemoryActorSemantics.Segments(candidate).ShouldBeEmpty();
    }

    [Fact]
    public void MatchesExactly_IsCaseSensitiveOnPurposeAndSaysSo()
    {
        var mine = MemoryActor.Agent("kelthuzad", "qwen");

        MemoryActorSemantics.MatchesExactly(mine, "kelthuzad").ShouldBeTrue();
        MemoryActorSemantics.MatchesExactly(mine, "kelthuzad+qwen").ShouldBeTrue();
        MemoryActorSemantics.MatchesExactly(mine, "Kelthuzad").ShouldBeFalse();
        MemoryActorSemantics.MatchesExactly(mine, "kelthuzad+claude").ShouldBeFalse();
    }

    [Fact]
    public void MatchesExactly_AVendorlessRowIsNotReachedByAVendorCandidate()
    {
        var human = MemoryActor.Human;

        MemoryActorSemantics.MatchesExactly(human, "human").ShouldBeTrue();
        MemoryActorSemantics.MatchesExactly(human, "human+qwen").ShouldBeFalse();
    }

    [Fact]
    public void MatchesExactly_AVendorlessRowIsReachedByTheTrailingSeparatorCandidate()
    {
        // Pinned divergence, not a fix: the matcher compares "human+" with MemoryActor.ToString()
        // ("human", the vendor is absent) using an ordinal-insensitive equality and rejects the
        // record outright, while the store clause reads the same candidate as alias-only and keeps
        // it. Both readings are on record here; neither is smoothed over.
        var human = MemoryActor.Human;
        var record = TestMemories.Create("note", [1f, 0f], actor: human);

        MemoryActorSemantics.MatchesExactly(human, "human+").ShouldBeTrue();
        new MemoryFilter { Actor = "human+" }.Matches(record).ShouldBeFalse();
    }

    [Fact]
    public void MatchesExactly_BlankCandidate_LeavesEverythingIn()
    {
        MemoryActorSemantics.MatchesExactly(MemoryActor.Agent("kelthuzad", "qwen"), null).ShouldBeTrue();
        MemoryActorSemantics.MatchesExactly(MemoryActor.Agent("kelthuzad", "qwen"), "").ShouldBeTrue();
    }
}
