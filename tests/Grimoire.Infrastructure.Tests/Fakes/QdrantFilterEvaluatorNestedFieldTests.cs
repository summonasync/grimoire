using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Memory;
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

    [Fact]
    public void MissingActorField_IsNotReachedWhileTheMatcherWouldKeepTheRow()
    {
        // Divergence (b) stated as a divergence: the fake cannot match a point whose payload has no
        // actor struct at all, while MemoryFilterExtensions.MatchesActor keeps exactly such a record
        // for any candidate. Unobservable in production writes (MemoryPayloadMapper always stores the
        // struct and RequiredActor refuses a point without it), pinned so nobody "fixes" one side.
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["no-actor"] = new Dictionary<string, Value> { [MemoryPayloadFields.Status] = "Active" },
        };

        MatchingIds(new Filter { Must = { DotPath("actor.alias", "human") } }, points).ShouldBeEmpty();
        new MemoryFilter { Actor = "human" }.Matches(ActorBearingRecord()).ShouldBeTrue();
    }

    [Fact]
    public void DotPath_IntoAScalarPrefix_AnswersNoMatchLikeTheServer()
    {
        // Handed forward from task 02's review as a shape where the double was looser than the
        // server. Measured directly on 127.0.0.1:6353 against a throwaway collection holding this
        // same payload: a keyword aimed at "status.x" returns no rows, because a dotted key only
        // descends into a struct. The old fake stopped at the scalar and matched it — TRUE here.
        // The second assertion is the control: the row and the value really are there.
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["mine"] = Payload(Alias("kelthuzad", "qwen")),
        };

        MatchingIds(DotFilter("status.x", "Active"), points).ShouldBeEmpty();
        MatchingIds(DotFilter(MemoryPayloadFields.Status, "Active"), points).ShouldBe(["mine"]);
    }

    [Fact]
    public void DotPath_IntoAListPosition_AnswersNoMatchLikeTheServer()
    {
        // The fake used to give every list a positional grammar ("tags.1" resolving to the second
        // element). This server has none: the same query shape over ["a","b"] returned no rows,
        // while the keyword list the translator actually emits for tags returned the point.
        //
        // The same probe's side finding — a single keyword over that list does match on the server
        // and does not here — is a named case beside this one, where it was purchased to live.
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["mine"] = TaggedPayload(["a", "b"]),
        };
        var anyOfTheProductShapes = new Filter
        {
            Must = { Conditions.Match(MemoryPayloadFields.Tags, ["b"]) },
        };

        MatchingIds(DotFilter("tags.1", "b"), points).ShouldBeEmpty();
        MatchingIds(anyOfTheProductShapes, points).ShouldBe(["mine"]);
    }

    [Fact]
    public void SingleKeyword_OverAListPayload_StaysTheStricterSideAndSaysSo()
    {
        // 058 WARNING-1, purchased into a named case instead of staying a comment. Measured in this
        // slice on the server the product actually runs against — 1.19.2 at 127.0.0.1:6353, throwaway
        // collection holding this same payload, dropped with the collection here: a field condition
        // "tags" match value "b" over ["a","b"] returned the point, the same shape with "z" returned
        // none, and the any-of shape the translator emits for tags returned it too. So the server
        // reads a single keyword as "some element equals" while this double reads it as "the field is
        // that string" — the fake is the stricter side, the one direction that cannot hand back a row
        // the product did not ask for. Harmless because the translator never aims a single keyword at
        // a list field. Widen the Keyword arm to walk ListValue and this case is the first thing red;
        // the two controls beside it prove the false comes from the payload shape and not a dead
        // fixture or a dead keyword arm.
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["mine"] = TaggedPayload(["a", "b"]),
        };
        var keywordsTheProductEmits = new Filter
        {
            Must = { Conditions.Match(MemoryPayloadFields.Tags, ["b"]) },
        };

        MatchingIds(DotFilter(MemoryPayloadFields.Tags, "b"), points).ShouldBeEmpty();
        MatchingIds(keywordsTheProductEmits, points).ShouldBe(["mine"]);
        MatchingIds(DotFilter(MemoryPayloadFields.Status, "Active"), points).ShouldBe(["mine"]);
    }

    [Fact]
    public void DotPath_ThroughAListOfStructs_AnswersNoMatchLikeTheServer()
    {
        // Both dotted shapes over a list of structs, probed on the same collection: implicit
        // traversal ("items.name") and explicit index ("items.0.name") each answer no rows. The
        // must-not-is_empty assertion proves the list is actually stored, so this is not an
        // empty-fixture green.
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["mine"] = ItemsPayload(["alpha", "beta"]),
        };
        var present = new Condition { IsEmpty = new IsEmptyCondition { Key = "items" } };

        MatchingIds(DotFilter("items.name", "alpha"), points).ShouldBeEmpty();
        MatchingIds(DotFilter("items.0.name", "alpha"), points).ShouldBeEmpty();
        MatchingIds(new Filter { MustNot = { present } }, points).ShouldBe(["mine"]);
    }

    [Fact]
    public void MalformedKey_RefusesTheQueryInsteadOfAnsweringIt()
    {
        // The third divergence is an error shape, not a row shape: "actor." made the server reject
        // the whole request ("Invalid json path"), while the fake quietly matched nothing. Quiet and
        // empty is exactly how a loose double hides a bug, so it refuses to answer instead, and a
        // test that builds such a key fails loudly rather than passing on an empty page.
        var points = new Dictionary<string, IReadOnlyDictionary<string, Value>>
        {
            ["mine"] = Payload(Alias("kelthuzad", "qwen")),
        };

        var thrown = Record.Exception(() =>
            QdrantFilterEvaluator.Matches(DotFilter("actor.", "kelthuzad"), points["mine"]));

        thrown.ShouldBeOfType<NotSupportedException>();
        thrown!.Message.ShouldContain("Invalid json path");
        QdrantFilterEvaluator.Matches(DotFilter("actor.alias", "kelthuzad"), points["mine"]).ShouldBeTrue();
    }

    private static MemoryRecord ActorBearingRecord() =>
        TestMemories.Create("written by the human", actor: MemoryActor.Human);

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

    private static IReadOnlyDictionary<string, Value> TaggedPayload(IReadOnlyList<string> tags)
    {
        var list = new ListValue();
        foreach (var tag in tags)
        {
            list.Values.Add(tag);
        }

        return new Dictionary<string, Value>
        {
            [MemoryPayloadFields.Status] = "Active",
            [MemoryPayloadFields.Tags] = new Value { ListValue = list },
        };
    }

    private static IReadOnlyDictionary<string, Value> ItemsPayload(IReadOnlyList<string> names)
    {
        var list = new ListValue();
        foreach (var name in names)
        {
            var item = new Struct();
            item.Fields.Add("name", name);
            list.Values.Add(new Value { StructValue = item });
        }

        return new Dictionary<string, Value>
        {
            [MemoryPayloadFields.Status] = "Active",
            ["items"] = new Value { ListValue = list },
        };
    }

    private static string[] MatchingIds(Filter filter, IReadOnlyDictionary<string, IReadOnlyDictionary<string, Value>> points)
    {
        var matched = points
            .Where(pair => QdrantFilterEvaluator.Matches(filter, pair.Value))
            .Select(pair => pair.Key)
            .ToArray();

        return [.. matched.OrderBy(id => id, StringComparer.Ordinal)];
    }
}
