using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public sealed class MemoryFilterActorParityTests
{
    private static readonly float[] Vector = [1f, 0f];

    private readonly FakeMemoryStore _store = new();

    public MemoryFilterActorParityTests()
    {
        Seed("mine", MemoryActor.Agent("kelthuzad", "qwen"));
        Seed("other", MemoryActor.Agent("thrall", "claude"));
        Seed("human", MemoryActor.Human);
    }

    [Fact]
    public async Task List_WithAnAlias_KeepsOnlyThatActor()
    {
        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad" }, CancellationToken.None);

        records.Select(record => record.Content).ShouldBe(["mine"]);
    }

    [Fact]
    public async Task List_WithADifferentCaseAlias_ReturnsNothing()
    {
        var records = await _store.ListAsync(new MemoryFilter { Actor = "Kelthuzad" }, CancellationToken.None);

        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithAWrongVendor_ReturnsNothing()
    {
        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad+claude" }, CancellationToken.None);

        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithATrailingSeparator_ReadsAsAliasOnly()
    {
        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad+" }, CancellationToken.None);

        records.Select(record => record.Content).ShouldBe(["mine"]);
    }

    private void Seed(string content, MemoryActor actor) =>
        _store.Seed(
            new MemoryRecord
            {
                Id = Guid.NewGuid(),
                Content = content,
                Scope = MemoryScope.Project,
                Kind = MemoryKind.Journal,
                Actor = actor,
                Tome = TomeKey.DefaultTome,
                Tags = [],
                Importance = 0.5,
                CreatedAt = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero),
                LastAccessedAt = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero),
                AccessCount = 0,
                Status = MemoryStatus.Active,
                Sources = [],
            },
            Vector);
}
