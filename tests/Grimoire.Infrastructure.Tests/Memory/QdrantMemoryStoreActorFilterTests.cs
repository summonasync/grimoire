using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

// The store is where an actor filter has to bite. Before this slice the translator emitted no
// condition for it, so every one of these reads came back with the whole spread and the caller's
// post-filter quietly cleaned up afterwards — which is exactly the shape that lets a recall query
// return fewer rows than asked for while every individual test still looks green.
public sealed class QdrantMemoryStoreActorFilterTests
{
    private static readonly MemoryActor Mine = MemoryActor.Agent("kelthuzad", "qwen");
    private static readonly MemoryActor MineOtherVendor = MemoryActor.Agent("kelthuzad", "claude");
    private static readonly MemoryActor Human = MemoryActor.Human;
    private static readonly MemoryActor Sibling = MemoryActor.Agent("thrall", "claude");

    private static readonly float[] QueryVector = [1f, 0f, 0f, 0f];

    [Fact]
    public async Task List_WithAliasOnly_KeepsEveryVendorOfThatAliasAndDropsTheRest()
    {
        var (store, ids) = await SeedAsync();

        var records = await store.ListAsync(new MemoryFilter { Actor = "kelthuzad" }, CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([ids[0], ids[1]], ignoreOrder: true);
    }

    [Fact]
    public async Task List_WithAliasAndVendor_DoesNotSettleForAWrongVendor()
    {
        var (store, ids) = await SeedAsync();

        var records = await store.ListAsync(new MemoryFilter { Actor = "kelthuzad+qwen" }, CancellationToken.None);

        records.Select(record => record.Id).ShouldHaveSingleItem().ShouldBe(ids[0]);
    }

    [Fact]
    public async Task List_WithAHumanAlias_DropsRowsThatCarryAVendorSegment()
    {
        var (store, ids) = await SeedAsync();

        var records = await store.ListAsync(new MemoryFilter { Actor = "human" }, CancellationToken.None);

        records.Select(record => record.Id).ShouldHaveSingleItem().ShouldBe(ids[3]);
    }

    [Fact]
    public async Task Search_WithAnActor_DrawsItsWindowFromThatActorAlone()
    {
        var (store, ids) = await SeedAsync();

        var matches = await store.SearchAsync(QueryVector, new MemoryFilter { Actor = "thrall+claude" }, 10, CancellationToken.None);

        matches.Select(match => match.Record.Id).ShouldHaveSingleItem().ShouldBe(ids[2]);
    }

    [Fact]
    public async Task Count_WithAnActor_CountsWhatListWouldReturn()
    {
        var (store, _) = await SeedAsync();

        (await store.CountAsync(new MemoryFilter { Actor = "kelthuzad" }, CancellationToken.None))
            .ShouldBe(2);
        (await store.CountAsync(new MemoryFilter { Actor = "kelthuzad+qwen" }, CancellationToken.None))
            .ShouldBe(1);
    }

    [Fact]
    public async Task List_WithAnUnknownActor_ReturnsNothingRatherThanEverything()
    {
        var (store, _) = await SeedAsync();

        var records = await store.ListAsync(new MemoryFilter { Actor = "nobody+qwen" }, CancellationToken.None);

        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithADifferentCaseAlias_ReturnsNothingBecauseKeywordMatchingIsExact()
    {
        var (store, _) = await SeedAsync();

        // Pinned consequence of the owner's exact-match ruling, asserted against the same double the
        // store seam runs on. Real Qdrant behaves identically; the live run is recorded in the task log.
        var records = await store.ListAsync(new MemoryFilter { Actor = "Kelthuzad" }, CancellationToken.None);

        records.ShouldBeEmpty();
    }

    private static async Task<(QdrantMemoryStore Store, Guid[] Ids)> SeedAsync()
    {
        var db = new FakeQdrantVectorDb();
        db.CollectionExists = true;
        var store = new QdrantMemoryStore(
            db,
            "grimoire",
            new EmbeddingOptions { Model = "nomic-embed-text", Dimensions = 4 });

        var spread = new[]
        {
            TestMemories.Create("mine-qwen", actor: Mine),
            TestMemories.Create("mine-claude", actor: MineOtherVendor),
            TestMemories.Create("sibling", actor: Sibling),
            TestMemories.Create("human-written", actor: Human),
        };

        var ids = new List<Guid>();
        var vectors = new float[][]
        {
            [1f, 0f, 0f, 0f],
            [0f, 1f, 0f, 0f],
            [0f, 0f, 1f, 0f],
            [0f, 0f, 0f, 1f],
        };

        for (var index = 0; index < spread.Length; index++)
        {
            await store.UpsertAsync(spread[index], vectors[index], CancellationToken.None);
            ids.Add(spread[index].Id);
        }

        return (store, [.. ids]);
    }
}
