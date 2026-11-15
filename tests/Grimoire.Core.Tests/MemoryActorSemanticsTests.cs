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
    public void TrailingSeparator_ReachesAVendorlessRowOnBothSides()
    {
        // The candidate that used to split the readers: a human row carries no vendor, so the store
        // clause read "human+" as alias-only and kept it while the matcher compared it against
        // ToString() ("human") and threw it away. The matcher splits through this same Segments now,
        // so one candidate means one thing — the assertion below is the parity, not a pinned
        // disagreement.
        var human = MemoryActor.Human;
        var record = TestMemories.Create("note", [1f, 0f], actor: human);

        MemoryActorSemantics.MatchesExactly(human, "human+").ShouldBeTrue();
        new MemoryFilter { Actor = "human+" }.Matches(record).ShouldBeTrue();
    }

    [Fact]
    public void MatchesExactly_ABareAliasDoesNotReachAVendorlessRowOfAnotherAlias()
    {
        // The case the whole suite was missing: under a precedence re-association of the vendor arm
        // (A && vendor is null || Vendor == vendor) this becomes true while the rule says false, and
        // every existing unit case kept passing. The harnesses killed that mutant; only this line
        // makes the unit suite see it too.
        MemoryActorSemantics.MatchesExactly(MemoryActor.Human, "kelthuzad").ShouldBeFalse();
    }

    [Fact]
    public void DoubleSeparator_NamesAPlusAsTheVendorOnBothSides()
    {
        // "a++" splits on the first separator, so the candidate names alias "a" with the vendor "+".
        // A row written under vendor "plus" is rejected by the clause and by the matcher — for the
        // same stated reason now, because both ask this Segments what the candidate means.
        var storeVendorTheCandidateCannotName = MemoryActor.Agent("a", "plus");

        MemoryActorSemantics.MatchesExactly(storeVendorTheCandidateCannotName, "a++").ShouldBeFalse();
        new MemoryFilter { Actor = "a++" }.Matches(
            TestMemories.Create("note", [1f, 0f], actor: storeVendorTheCandidateCannotName)).ShouldBeFalse();
        MemoryActorSemantics.Segments("a++").ShouldBe([("a", "+")]);
    }

    [Fact]
    public void MatchesExactly_BlankCandidate_LeavesEverythingIn()
    {
        MemoryActorSemantics.MatchesExactly(MemoryActor.Agent("kelthuzad", "qwen"), null).ShouldBeTrue();
        MemoryActorSemantics.MatchesExactly(MemoryActor.Agent("kelthuzad", "qwen"), "").ShouldBeTrue();
    }
}
