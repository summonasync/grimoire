using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceNoteTests
{
    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        ["note about deploys"] = [1f, 0f, 0f],
        ["deploy neighbor one"] = [1f, 0.7f, 0f],
        ["deploy neighbor two"] = [1f, 0f, 0.7f],
        ["fuzzy neighbor one"] = [1f, 0.85f, 0f],
        ["fuzzy neighbor two"] = [1f, 0f, 0.85f],
        ["bare neighbor one"] = [1f, 0.7f, 0f],
        ["bare neighbor two"] = [1f, 0f, 0.7f],
        ["coffee brewing"] = [0f, 1f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeEmbeddingProvider _embeddings = new(Vectors);

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceNoteTests()
    {
        _service = TestMemories.Service(_store, _embeddings, _clock, topics: new EmbeddingTopicClassifier(_store));
    }

    [Fact]
    public async Task Note_AgreeingNeighbors_StampTheirSharedTopic()
    {
        SeedNeighbor("deploy neighbor one", "topic:deploy");
        SeedNeighbor("deploy neighbor two", "topic:deploy");

        var (result, _) = await _service.NoteAsync(Note("note about deploys"), CancellationToken.None);

        result.Record.Tags.ShouldBe(["topic:deploy"]);
        result.Record.Kind.ShouldBe(MemoryKind.Journal);
        result.Record.Importance.ShouldBe(0.5);
        result.Record.Scope.ShouldBe(MemoryScope.Project);
        result.Record.ProjectId.ShouldBe("grim");
    }

    [Fact]
    public async Task Note_VotesWithoutAgreement_LandUntagged()
    {
        SeedNeighbor("deploy neighbor one", "topic:deploy");
        SeedNeighbor("deploy neighbor two", "topic:coffee");

        var (result, _) = await _service.NoteAsync(Note("note about deploys"), CancellationToken.None);

        result.Record.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_BelowThresholdNeighbors_LandUntagged()
    {
        _store.Seed(TestMemories.Create("fuzzy one", [1f, 0.9f, 0f], projectId: "grim", tags: ["topic:deploy"]), [1f, 0.9f, 0f]);
        _store.Seed(TestMemories.Create("fuzzy two", [1f, 0f, 0.9f], projectId: "grim", tags: ["topic:deploy"]), [1f, 0f, 0.9f]);

        var (result, _) = await _service.NoteAsync(Note("note about deploys"), CancellationToken.None);

        result.Record.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_ExplicitHint_OverridesTheNeighborVote()
    {
        SeedNeighbor("deploy neighbor one", "topic:coffee");
        SeedNeighbor("deploy neighbor two", "topic:coffee");

        var (result, _) = await _service.NoteAsync(Note("note about deploys", topic: "Deploy Lane"), CancellationToken.None);

        result.Record.Tags.ShouldBe(["topic:deploy-lane"]);
    }

    [Fact]
    public async Task Note_PrefixedHint_IsNormalizedOnce()
    {
        var (result, _) = await _service.NoteAsync(Note("note about deploys", topic: "topic:Deploy Lane"), CancellationToken.None);

        result.Record.Tags.ShouldBe(["topic:deploy-lane"]);
    }

    [Fact]
    public async Task Note_BlankHint_FallsBackToTheNeighborVote()
    {
        SeedNeighbor("deploy neighbor one", "topic:deploy");
        SeedNeighbor("deploy neighbor two", "topic:deploy");

        var (result, _) = await _service.NoteAsync(Note("note about deploys", topic: "   "), CancellationToken.None);

        result.Record.Tags.ShouldBe(["topic:deploy"]);
    }

    [Fact]
    public async Task Note_IdenticalRetry_ReplaysWithoutReclassifying()
    {
        SeedNeighbor("deploy neighbor one", "topic:deploy");
        SeedNeighbor("deploy neighbor two", "topic:deploy");
        var (first, _) = await _service.NoteAsync(Note("note about deploys"), CancellationToken.None);

        var (second, _) = await _service.NoteAsync(Note("note about deploys"), CancellationToken.None);

        second.IdempotentReplay.ShouldBeTrue();
        second.Record.Id.ShouldBe(first.Record.Id);
        second.Record.Tags.ShouldBe(["topic:deploy"]);
        _store.Records.Count(record => record.Content == "note about deploys").ShouldBe(1);
        _embeddings.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Note_SharesTheRememberIdempotencyRecipe()
    {
        var (note, _) = await _service.NoteAsync(Note("note about deploys", tags: ["ops"]), CancellationToken.None);

        var remembered = await _service.RememberAsync(new RememberRequest
        {
            Content = "note about deploys",
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = TestMemories.Agent,
            ProjectId = "grim",
            Tags = ["ops"],
        }, CancellationToken.None);

        remembered.IdempotentReplay.ShouldBeTrue();
        remembered.Record.Id.ShouldBe(note.Record.Id);
        _store.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Note_BlankContent_Throws()
    {
        var error = await Should.ThrowAsync<ArgumentException>(() =>
            _service.NoteAsync(Note("   "), CancellationToken.None));

        error.Message.ShouldContain("Note content must not be empty.");
        _store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_SessionScope_ProbesOnlyItsOwnSession()
    {
        SeedNeighbor("deploy neighbor one", "topic:deploy");
        SeedNeighbor("deploy neighbor two", "topic:deploy");

        var (result, _) = await _service.NoteAsync(Note("note about deploys", scope: MemoryScope.Session, sessionId: "s-1", projectId: null), CancellationToken.None);

        result.Record.Tags.ShouldBeEmpty();
        result.Record.ExpiresAt.ShouldBe(result.Record.CreatedAt.AddHours(24));
    }

    [Fact]
    public async Task Note_WithoutClassifier_HintsStillStamp()
    {
        var bare = TestMemories.Service(_store, _embeddings, _clock);

        var (hinted, _) = await bare.NoteAsync(Note("note about deploys", topic: "Deploy Lane"), CancellationToken.None);
        var (plain, _) = await bare.NoteAsync(Note("coffee brewing"), CancellationToken.None);

        hinted.Record.Tags.ShouldBe(["topic:deploy-lane"]);
        plain.Record.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_WeakConsensus_StampsNothingAndNamesTheCandidate()
    {
        SeedNeighbor("fuzzy neighbor one", "topic:deploy");
        SeedNeighbor("fuzzy neighbor two", "topic:deploy");

        var (result, vote) = await _service.NoteAsync(Note("note about deploys"), CancellationToken.None);

        result.Record.Tags.ShouldBeEmpty();
        vote!.Decision.ShouldBe(TopicVoteDecision.BelowConfidence);
        vote.CandidateTag.ShouldBe("topic:deploy");
        vote.Tag.ShouldBeNull();
    }

    [Fact]
    public async Task Note_ExplicitTopic_IsReportedAsExplicitAndNeedsNoAgreement()
    {
        var (_, vote) = await _service.NoteAsync(Note("note about deploys", topic: "Deploy Lane"), CancellationToken.None);

        vote!.Decision.ShouldBe(TopicVoteDecision.Explicit);
        vote.Tag.ShouldBe("topic:deploy-lane");
        vote.WinningCount.ShouldBe(0);
        vote.BestSimilarity.ShouldBeNull();
    }

    [Fact]
    public async Task Note_BareTaggedNeighbors_DoNotThemeTheNoteButShowInTheVote()
    {
        SeedNeighbor("bare neighbor one", "deploy");
        SeedNeighbor("bare neighbor two", "deploy");

        var (result, vote) = await _service.NoteAsync(Note("note about deploys"), CancellationToken.None);

        result.Record.Tags.ShouldBeEmpty();
        vote!.Decision.ShouldBe(TopicVoteDecision.NoConsensus);
        vote.Neighbors.ShouldContain(entry => entry.Tag == "deploy" && !entry.Counted);
    }

    private NoteRequest Note(
        string content,
        MemoryScope scope = MemoryScope.Project,
        string? projectId = "grim",
        string? sessionId = null,
        string? topic = null,
        IReadOnlyList<string>? tags = null) => new()
        {
            Content = content,
            Scope = scope,
            Actor = TestMemories.Agent,
            ProjectId = projectId,
            SessionId = sessionId,
            Topic = topic,
            Tags = tags ?? [],
        };

    private void SeedNeighbor(string content, string topic)
    {
        var vector = Vectors[content];
        _store.Seed(TestMemories.Create(content, vector, projectId: "grim", tags: [topic]), vector);
    }
}
