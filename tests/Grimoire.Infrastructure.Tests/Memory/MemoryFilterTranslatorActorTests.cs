using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

// MemoryFilter.Actor was a live user-facing dimension that the translator dropped on the way to the
// store: recall asked for one actor and got a candidate window drawn from all of them, narrowed
// afterwards. These tests pin the condition the translator is now required to emit, in the exact
// shape the owner chose — alias keyword, plus a vendor keyword only when the candidate names one.
public sealed class MemoryFilterTranslatorActorTests
{
    private static Condition[] FieldConditions(Filter filter, string key) =>
        [.. filter.Must
            .Where(item => item.Field is not null && item.Field.Key == key)];

    // Counts actor conditions wherever they sit, including inside a compounded status filter: the
    // top-level-only helper above would report "no actor condition" for a filter that hides one in
    // a nested should-list, which is not a proof of anything.
    private static int ActorConditionCount(Filter filter) =>
        filter.Must.Sum(Count);

    private static int Count(Condition condition)
    {
        if (condition.Field is { } field)
        {
            return field.Key.StartsWith("actor.", StringComparison.Ordinal) ? 1 : 0;
        }

        return condition.Filter is { } nested ? nested.Must.Sum(Count) : 0;
    }

    [Fact]
    public void Translate_WithoutAnActor_EmitsNoActorCondition()
    {
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter());

        FieldConditions(filter, "actor.alias").ShouldBeEmpty();
        FieldConditions(filter, "actor.vendor").ShouldBeEmpty();
    }

    [Fact]
    public void Translate_WithABareAlias_ConstrainsOnlyTheAliasSegment()
    {
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { Actor = "kelthuzad" });

        var alias = FieldConditions(filter, "actor.alias").ShouldHaveSingleItem();
        alias.Field!.Match.Keyword.ShouldBe("kelthuzad");
        FieldConditions(filter, "actor.vendor").ShouldBeEmpty();
    }

    [Fact]
    public void Translate_WithAliasAndVendor_ConstrainsBothSegments()
    {
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { Actor = "kelthuzad+qwen" });

        FieldConditions(filter, "actor.alias").ShouldHaveSingleItem().Field!.Match.Keyword.ShouldBe("kelthuzad");
        FieldConditions(filter, "actor.vendor").ShouldHaveSingleItem().Field!.Match.Keyword.ShouldBe("qwen");
    }

    [Fact]
    public void Translate_KeepsTheAliasSegmentVerbatimSoCaseCannotBeBlamedOnTheTranslator()
    {
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { Actor = "Kelthuzad" });

        // The store clause is case-sensitive by decision; the translator's job is to pass the
        // candidate through untouched so that property is visible at the wire, not invented here.
        FieldConditions(filter, "actor.alias").ShouldHaveSingleItem().Field!.Match.Keyword.ShouldBe("Kelthuzad");
    }

    [Fact]
    public void Translate_WithATrailingSeparator_TreatsTheCandidateAsAliasOnly()
    {
        // Divergence pinned, not fixed: MemoryFilterExtensions.MatchesActor compares "alias+" against
        // MemoryActor.ToString() and therefore matches NOTHING for it, while this clause matches every
        // row written by that alias. Both readings are defensible; the owner chose the alias-only
        // reading here and asked that the difference be written down rather than hidden.
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { Actor = "kelthuzad+" });

        FieldConditions(filter, "actor.alias").ShouldHaveSingleItem().Field!.Match.Keyword.ShouldBe("kelthuzad");
        FieldConditions(filter, "actor.vendor").ShouldBeEmpty();
    }

    [Fact]
    public void Translate_ActorConditionSitsBesideTheStatusConditionRatherThanReplacingIt()
    {
        var filter = MemoryFilterTranslator.Translate(
            new MemoryFilter { Actor = "kelthuzad+qwen", Statuses = [MemoryStatus.Superseded] });

        FieldConditions(filter, MemoryPayloadFields.Status).ShouldBeEmpty();
        filter.Must.ShouldContain(item => item.Filter != null);
        ActorConditionCount(filter).ShouldBe(2);
    }
}
