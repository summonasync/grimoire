using Grimoire.Core;
using Grimoire.Core.Consolidation;
using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests.Consolidation;

public sealed class ExtractiveSummarizerTests
{
    private readonly ExtractiveSummarizer _summarizer = new();

    [Fact]
    public void Summarize_BuildsHeaderBulletsAndPointers()
    {
        var sources = new[]
        {
            Source("first note", created: FakeClock.DefaultStart.AddDays(-3), id: s_firstId),
            Source("second note", created: FakeClock.DefaultStart, id: s_secondId),
        };

        var content = _summarizer.Summarize(sources);

        var lines = content.Split('\n');
        lines.Length.ShouldBe(3);
        lines[0].ShouldBe($"Consolidated from 2 records . {FakeClock.DefaultStart.AddDays(-3):yyyy-MM-dd}..{FakeClock.DefaultStart:yyyy-MM-dd}");
        lines[1].ShouldBe($"- {FakeClock.DefaultStart:yyyy-MM-dd} [{TestMemories.Agent}] second note (grimoire://memory/{s_secondId})");
        lines[2].ShouldBe($"- {FakeClock.DefaultStart.AddDays(-3):yyyy-MM-dd} [{TestMemories.Agent}] first note (grimoire://memory/{s_firstId})");
    }

    [Fact]
    public void Summarize_StampsMajorityTopicIntoTheHeader()
    {
        var sources = new[]
        {
            Source("a", tags: ["topic:deploy", "misc"]),
            Source("b", tags: ["topic:deploy"]),
        };

        var content = _summarizer.Summarize(sources);

        content.ShouldStartWith("Consolidated from 2 records . topic:deploy . ");
    }

    [Fact]
    public void Summarize_DropsTopicBelowTheHalfRule()
    {
        var sources = new[]
        {
            Source("a", tags: ["topic:deploy"]),
            Source("b"),
            Source("c"),
            Source("d"),
        };

        var content = _summarizer.Summarize(sources);

        content.ShouldNotContain("topic:");
    }

    [Fact]
    public void Summarize_TrimsLongSourceTextToTwoHundredChars()
    {
        var longText = new string('x', 500);
        var content = _summarizer.Summarize([Source(longText)]);
        var bullet = content.Split('\n')[1];

        var text = bullet[(bullet.IndexOf("] ", StringComparison.Ordinal) + 2)..];
        text = text[..text.LastIndexOf(" (grimoire://", StringComparison.Ordinal)];
        text.Length.ShouldBe(200);
        text.All(character => character == 'x').ShouldBeTrue();
    }

    [Fact]
    public void Summarize_FlattensMultilineSourceTextIntoOneBullet()
    {
        var content = _summarizer.Summarize([Source("line one\nline two")]);

        content.Split('\n').Length.ShouldBe(2);
        content.ShouldContain("line one line two");
    }

    [Fact]
    public void Summarize_RejectsAnEmptyCluster()
    {
        Should.Throw<ArgumentException>(() => _summarizer.Summarize([]));
    }

    [Fact]
    public void MajorityTopic_PrefersHighestCountThenOrdinal()
    {
        var records = new[]
        {
            Source("a", tags: ["topic:beta", "topic:alpha"]),
            Source("b", tags: ["topic:beta"]),
        };

        ExtractiveSummarizer.MajorityTopic(records).ShouldBe("topic:beta");
        ExtractiveSummarizer.MajorityTopic([Source("c")]).ShouldBeNull();
    }

    private static readonly Guid s_firstId = Guid.NewGuid();

    private static readonly Guid s_secondId = Guid.NewGuid();

    private static MemoryRecord Source(
        string content,
        DateTimeOffset? created = null,
        Guid? id = null,
        IReadOnlyList<string>? tags = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Content = content,
        Scope = MemoryScope.Project,
        Kind = MemoryKind.Journal,
        Actor = TestMemories.Agent,
        Tome = TomeKey.DefaultTome,
        Tags = tags ?? [],
        CreatedAt = created ?? FakeClock.DefaultStart,
        LastAccessedAt = created ?? FakeClock.DefaultStart,
    };
}
