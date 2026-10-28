using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class EmbeddingTopicClassifierTests
{
    private readonly FakeMemoryStore _store = new();

    private readonly EmbeddingTopicClassifier _classifier;

    public EmbeddingTopicClassifierTests()
    {
        _classifier = new EmbeddingTopicClassifier(_store);
    }

    [Fact]
    public async Task Classify_TwoAgreeingNeighborsAboveTheThreshold_ReturnsTheSharedTag()
    {
        Seed("neighbor one", [1f, 0.7f, 0f], "topic:deploy");
        Seed("neighbor two", [1f, 0f, 0.7f], "topic:deploy");

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBe("topic:deploy");
    }

    [Fact]
    public async Task Classify_SingleAgreeingNeighbor_ReturnsNull()
    {
        Seed("neighbor one", [1f, 0.7f, 0f], "topic:deploy");

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBeNull();
    }

    [Fact]
    public async Task Classify_AgreementBelowTheThreshold_ReturnsNull()
    {
        Seed("fuzzy one", [1f, 0.9f, 0f], "topic:deploy");
        Seed("fuzzy two", [1f, 0f, 0.9f], "topic:deploy");

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBeNull();
    }

    [Fact]
    public async Task Classify_SplitVotesAcrossTopics_ReturnsNull()
    {
        Seed("neighbor one", [1f, 0.7f, 0f], "topic:deploy");
        Seed("neighbor two", [1f, 0f, 0.7f], "topic:coffee");

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBeNull();
    }

    [Fact]
    public async Task Classify_TieBetweenTopics_BreaksLexicographic()
    {
        Seed("tie alpha one", [1f, 0.7f, 0f], "topic:alpha");
        Seed("tie alpha two", [1f, 0.75f, 0f], "topic:alpha");
        Seed("tie beta one", [1f, 0f, 0.7f], "topic:beta");
        Seed("tie beta two", [1f, 0f, 0.75f], "topic:beta");

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBe("topic:alpha");
    }

    [Fact]
    public async Task Classify_OnlyTheTopFiveNeighborsVote()
    {
        Seed("near beta one", [1f, 0.2f, 0f], "topic:beta");
        Seed("near beta two", [1f, 0.25f, 0f], "topic:beta");
        Seed("near beta three", [1f, 0.3f, 0f], "topic:beta");
        Seed("far alpha one", [1f, 0.35f, 0f], "topic:alpha");
        Seed("far alpha two", [1f, 0.4f, 0f], "topic:alpha");
        Seed("far alpha three", [1f, 0.45f, 0f], "topic:alpha");

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBe("topic:beta");
    }

    [Fact]
    public async Task Classify_NeighborsInAnotherTome_AreIgnored()
    {
        Seed("foreign one", [1f, 0.7f, 0f], "topic:deploy", tome: "elsewhere");
        Seed("foreign two", [1f, 0f, 0.7f], "topic:deploy", tome: "elsewhere");

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBeNull();
    }

    [Fact]
    public async Task Classify_NeighborsOfAnotherProject_AreIgnored()
    {
        Seed("alien one", [1f, 0.7f, 0f], "topic:deploy", projectId: "other");
        Seed("alien two", [1f, 0f, 0.7f], "topic:deploy", projectId: "other");

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBeNull();
    }

    [Fact]
    public async Task Classify_NeighborsOfAnotherKind_AreIgnored()
    {
        Seed("standing fact", [1f, 0.7f, 0f], "topic:deploy", kind: MemoryKind.Fact);
        Seed("second fact", [1f, 0f, 0.7f], "topic:deploy", kind: MemoryKind.Fact);

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBeNull();
    }

    [Fact]
    public async Task Classify_SupersededNeighbors_AreIgnored()
    {
        Seed("old one", [1f, 0.7f, 0f], "topic:deploy", status: MemoryStatus.Superseded);
        Seed("old two", [1f, 0f, 0.7f], "topic:deploy", status: MemoryStatus.Superseded);

        var topic = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Project, "grim", null, CancellationToken.None);

        topic.ShouldBeNull();
    }

    [Fact]
    public async Task Classify_SessionProbe_SeesOnlyItsOwnSession()
    {
        Seed("session one", [1f, 0.7f, 0f], "topic:deploy", scope: MemoryScope.Session, projectId: null, sessionId: "s-1");
        Seed("session two", [1f, 0f, 0.7f], "topic:deploy", scope: MemoryScope.Session, projectId: null, sessionId: "s-1");

        var otherSession = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Session, null, "s-2", CancellationToken.None);
        var sameSession = await _classifier.ClassifyAsync(Vector(1f, 0f, 0f), TomeKey.DefaultTome, MemoryScope.Session, null, "s-1", CancellationToken.None);

        otherSession.ShouldBeNull();
        sameSession.ShouldBe("topic:deploy");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("Deploy Lane", "topic:deploy-lane")]
    [InlineData("topic:deploy", "topic:deploy")]
    [InlineData("  Mixed Case  ", "topic:mixed-case")]
    [InlineData("already-kebab", "topic:already-kebab")]
    public void Normalize_TurnsHintsIntoTopicTags(string? hint, string? expected)
    {
        ITopicClassifier.Normalize(hint).ShouldBe(expected);
    }

    private static ReadOnlyMemory<float> Vector(params float[] values) => values;

    private void Seed(
        string content,
        float[] vector,
        string topic,
        string? tome = null,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        string? projectId = "grim",
        string? sessionId = null,
        MemoryStatus status = MemoryStatus.Active)
        => _store.Seed(TestMemories.Create(
            content,
            vector,
            scope: scope,
            kind: kind,
            projectId: projectId,
            sessionId: sessionId,
            tags: [topic],
            status: status,
            tome: tome), vector);
}
