using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Fakes;

// The evaluator is the seam that decides whether a stored payload satisfies a translated filter.
// It resolved only top-level payload keys, which silently made it blind to anything the translator
// aims at a field inside a struct — an actor condition among them. These tests are the evaluator's
// own proof, written before its behavior changed.
public sealed class QdrantFilterEvaluatorNestedFieldTests
{
    private static readonly Filter ActiveOnly = new()
    {
        Must = { Conditions.MatchKeyword(MemoryPayloadFields.Status, "Active") },
    };

    [Fact]
    public void DotPath_MatchesInsideAStructPayload()
    {
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["mine"] = Payload(Alias("kelthuzad", "qwen")),
            ["other"] = Payload(Alias("thrall", "claude")),
        };
        var filter = new Filter { Must = { DotPath("actor.alias", "kelthuzad") } };

        MatchingIds(filter, points).ShouldBe(["mine"]);
    }

    [Fact]
    public void DotPath_IsCaseSensitiveLikeAKeywordIndex()
    {
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["lower"] = Payload(Alias("kelthuzad", "qwen")),
            ["upper"] = Payload(Alias("Kelthuzad", "qwen")),
        };

        MatchingIds(DotFilter("actor.alias", "kelthuzad"), points).ShouldBe(["lower"]);
        MatchingIds(DotFilter("actor.alias", "Kelthuzad"), points).ShouldBe(["upper"]);
    }

    [Fact]
    public void DotPath_CarriesBothSegmentsOfAnAliasPlusVendorCandidate()
    {
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["exact"] = Payload(Alias("kelthuzad", "qwen")),
            ["wrong-vendor"] = Payload(Alias("kelthuzad", "claude")),
            ["other-alias"] = Payload(Alias("thrall", "qwen")),
        };
        var filter = new Filter
        {
            Must = { DotPath("actor.alias", "kelthuzad"), DotPath("actor.vendor", "qwen") },
        };

        MatchingIds(filter, points).ShouldBe(["exact"]);
    }

    [Fact]
    public void DotPath_ResolvesInsideACompoundedFilterNotOnlyAtTheTopLevel()
    {
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["hit"] = Payload(Alias("kelthuzad", "qwen")),
            ["miss"] = Payload(Alias("thrall", "claude")),
        };
        var anyOf = new Condition
        {
            Filter = new Filter
            {
                Should = { DotPath("actor.alias", "kelthuzad"), DotPath("actor.alias", "nobody") },
            },
        };

        MatchingIds(new Filter { Must = { anyOf } }, points).ShouldBe(["hit"]);
    }

    [Fact]
    public void IsEmpty_DotPathTreatsAnOmittedSegmentAsEmpty()
    {
        // MemoryPayloadMapper.ToValue adds "vendor" only when the actor carries one, so a human
        // write has no vendor key at all rather than a null one. This is the shape the product stores.
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["no-vendor-key"] = Payload(Alias("kelthuzad", null)),
            ["with-vendor"] = Payload(Alias("kelthuzad", "qwen")),
        };
        var isEmpty = new Condition { IsEmpty = new IsEmptyCondition { Key = "actor.vendor" } };

        MatchingIds(new Filter { Must = { isEmpty } }, points).ShouldBe(["no-vendor-key"]);
        MatchingIds(new Filter { MustNot = { isEmpty } }, points).ShouldBe(["with-vendor"]);
    }

    [Fact]
    public void IsEmpty_DotPathSeesAMissingStructAsHavingNoSegments()
    {
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["no-actor-at-all"] = new Dictionary<string, Value> { [MemoryPayloadFields.Status] = "Active" },
        };

        MatchingIds(new Filter { MustNot = { DotPath("actor.alias", "kelthuzad") } }, points).ShouldBe(["no-actor-at-all"]);
    }

    [Fact]
    public void DotPath_OverAPlainKeywordFieldStillBehavesLikeBefore()
    {
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["active"] = Payload(Alias("kelthuzad", "qwen")),
        };

        MatchingIds(ActiveOnly, points).ShouldBe(["active"]);
    }

    private static Condition DotPath(string key, string value) =>
        Conditions.MatchKeyword(key, value);

    private static Filter DotFilter(string key, string value) =>
        new() { Must = { DotPath(key, value) } };

    private static Struct Alias(string alias, string? vendor)
    {
        var structValue = new Struct();
        structValue.Fields.Add("alias", alias);

        // Same rule as the product mapper: no vendor means no key, not a null value.
        if (vendor is not null)
        {
            structValue.Fields.Add("vendor", vendor);
        }

        return structValue;
    }

    private static IReadOnlyDictionary<string, Value> Payload(Struct actor) =>
        new Dictionary<string, Value>
        {
            [MemoryPayloadFields.Status] = "Active",
            [MemoryPayloadFields.Actor] = new Value { StructValue = actor },
        };

    private static string[] MatchingIds(Filter filter, IReadOnlyDictionary<string, IReadOnlyDictionary<string, Value>> points)
    {
        var matched = points
            .Where(pair => QdrantFilterEvaluator.Matches(filter, pair.Value))
            .Select(pair => pair.Key)
            .ToArray();

        return [.. matched.OrderBy(id => id, StringComparer.Ordinal)];
    }
}
