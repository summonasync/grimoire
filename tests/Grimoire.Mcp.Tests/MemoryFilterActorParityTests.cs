using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Mcp.Tests;

public sealed class MemoryFilterActorParityTests
{
    private static readonly DateTimeOffset Moment = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly float[] Vector = [1f, 0f];

    private readonly FakeMemoryStore _store = new();

    [Fact]
    public async Task List_WithAnAlias_KeepsOnlyThatActor()
    {
        await SeedSpreadAsync();

        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad" }, CancellationToken.None);

        records.Select(record => record.Content).ShouldBe(["mine"]);
    }

    [Fact]
    public async Task List_WithADifferentCaseAlias_ReturnsNothing()
    {
        await SeedSpreadAsync();

        var records = await _store.ListAsync(new MemoryFilter { Actor = "Kelthuzad" }, CancellationToken.None);

        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithAWrongVendor_ReturnsNothing()
    {
        await SeedSpreadAsync();

        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad+claude" }, CancellationToken.None);

        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithATrailingSeparator_ReadsAsAliasOnly()
    {
        await SeedSpreadAsync();

        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad+" }, CancellationToken.None);

        records.Select(record => record.Content).ShouldBe(["mine"]);
    }

    // Sequential on purpose: FakeMemoryStore.UpsertAsync yields before it writes into a plain
    // dictionary, so seeding in parallel races the collection and the failure surfaces from inside
    // the double's filter lambda rather than from the test. Found once already in this feature.
    private async Task SeedSpreadAsync()
    {
        await SeedAsync("mine", MemoryActor.Agent("kelthuzad", "qwen"));
        await SeedAsync("other", MemoryActor.Agent("thrall", "claude"));
        await SeedAsync("human", MemoryActor.Human);
    }

    private async Task SeedAsync(string content, MemoryActor actor)
    {
        await _store.UpsertAsync(
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
                CreatedAt = Moment,
                LastAccessedAt = Moment,
                AccessCount = 0,
                Status = MemoryStatus.Active,
                Sources = [],
            },
            Vector,
            CancellationToken.None);
    }
}
