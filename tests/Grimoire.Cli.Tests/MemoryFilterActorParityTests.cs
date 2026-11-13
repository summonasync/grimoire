using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class MemoryFilterActorParityTests
{
    private static readonly float[] Vector = [1f, 0f];

    private readonly RecordingMemoryStore _store = new();

    public MemoryFilterActorParityTests()
    {
        _store.Add("mine", Vector, actor: MemoryActor.Agent("kelthuzad", "qwen"));
        _store.Add("other", Vector, actor: MemoryActor.Agent("thrall", "claude"));
        _store.Add("human", Vector, actor: MemoryActor.Human);
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
}
