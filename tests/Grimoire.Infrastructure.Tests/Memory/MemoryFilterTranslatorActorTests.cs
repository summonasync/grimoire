using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Translate_WithABlankCandidate_LeavesTheDimensionUnconstrainedWithoutThrowing(string candidate)
    {
        // Found by hard review: Actor="" used to be an unconstrained read on main (the matcher
        // treats blank as no clause) and the new branch made it an ArgumentOutOfRangeException
        // inside every store read that carries it - reachable from memory_recall, memory_list
        // and the Api query parameter.
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { Actor = candidate });

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
        // The owner's chosen reading of a trailing separator, and since 061 the only reading: the
        // in-process matcher asks MemoryActorSemantics.Segments the same question this clause does,
        // so "alias+" means this alias with any vendor on both sides of a recall. The case
        // asymmetry beside it stays — this keyword is exact, the matcher ignores case.
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { Actor = "kelthuzad+" });

        FieldConditions(filter, "actor.alias").ShouldHaveSingleItem().Field!.Match.Keyword.ShouldBe("kelthuzad");
        FieldConditions(filter, "actor.vendor").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("+", null)]
    [InlineData("+qwen", "qwen")]
    public void Translate_ABareSeparatorCandidate_EmitsAnEmptyAliasKeywordRatherThanNoClause(string candidate, string? vendor)
    {
        // 058 WARNING-2's wire half, beside the in-process parity case 061 bought it with. The shape is
        // the quiet one: the clause survives as a keyword on an empty alias, so the store stays
        // constrained and nets nothing. Measured on the live 1.19.2 the product runs against, the
        // scroll filter "actor.alias" match value "" answered 200 with zero rows over a point whose
        // alias is "kelthuzad" (and the same point came back for alias "kelthuzad"), so the real
        // server agrees with both doubles on this candidate. What this test actually guards is the
        // other reading: decide a blank alias segment means unconstrained and the clause vanishes
        // here first, which is a store returning every actor's rows to a query that named none — the
        // in-process matcher would follow it into the same wrong answer, so the page would look full
        // and be wrong.
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { Actor = candidate });

        FieldConditions(filter, "actor.alias").ShouldHaveSingleItem().Field!.Match.Keyword.ShouldBe(string.Empty);

        if (vendor is null)
        {
            FieldConditions(filter, "actor.vendor").ShouldBeEmpty();
        }
        else
        {
            FieldConditions(filter, "actor.vendor").ShouldHaveSingleItem().Field!.Match.Keyword.ShouldBe(vendor);
        }

        var payload = StoredActor("kelthuzad", "qwen");

        QdrantFilterEvaluator.Matches(filter, payload).ShouldBeFalse();
        QdrantFilterEvaluator.Matches(MemoryFilterTranslator.Translate(new MemoryFilter()), payload).ShouldBeTrue();
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

    // The payload shape MemoryPayloadMapper writes: status plus an actor struct carrying both segments.
    private static IReadOnlyDictionary<string, Value> StoredActor(string alias, string vendor)
    {
        var actor = new Struct();
        actor.Fields.Add("alias", alias);
        actor.Fields.Add("vendor", vendor);

        return new Dictionary<string, Value>
        {
            [MemoryPayloadFields.Status] = "Active",
            [MemoryPayloadFields.Actor] = new Value { StructValue = actor },
        };
    }
}
