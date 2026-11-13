using Grimoire.Core;
using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryFilterActorParityTests
{
    private static readonly MemoryActor Mine = MemoryActor.Agent("kelthuzad", "qwen");
    private static readonly MemoryActor Other = MemoryActor.Agent("thrall", "claude");

    private readonly FakeMemoryStore _store = new();

    public MemoryFilterActorParityTests()
    {
        Seed("mine", Mine);
        Seed("other", Other);
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
        // Same rule the store applies; the matcher beside it would keep the row. Pinned, not aligned.
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

    private void Seed(string content, MemoryActor actor)
    {
        var record = TestMemories.Create(content, [1f, 0f], actor: actor);
        _store.Seed(record, [1f, 0f]);
    }
}
