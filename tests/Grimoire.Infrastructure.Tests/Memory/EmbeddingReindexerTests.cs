using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

public sealed class EmbeddingReindexerTests
{
    private static readonly EmbeddingFingerprint OldModel = EmbeddingFingerprint.From("nomic-embed-text", 4);

    private static readonly EmbeddingFingerprint NewModel = EmbeddingFingerprint.From("bge-small", 4);

    [Fact]
    public async Task Run_ReembedsActiveRecordsInBatches_StampsVectorModelAndKeepsPayload()
    {
        var harness = await Harness.SeededAsync("alpha", "beta", "gamma");
        var progress = new List<ReindexProgress>();

        var outcome = await harness.RunAsync(NewModel, batchSize: 2, progress);

        outcome.ShouldBe(new ReindexOutcome(3, 3, 0));
        progress.Select(p => $"{p.Processed}/{p.Total}").ShouldBe(["2/3", "3/3"]);
        harness.Provider.Requests.ShouldBe(["alpha", "beta", "gamma"]);

        foreach (var id in harness.RecordIds)
        {
            harness.Db.PayloadOf(id)[MemoryPayloadFields.VectorModel].StringValue.ShouldBe(NewModel.Model);
            harness.Db.PayloadOf(id)[MemoryPayloadFields.Content].StringValue.ShouldNotBeNullOrEmpty();
            harness.Db.PayloadOf(id)[MemoryPayloadFields.Tome].StringValue.ShouldBe(TomeKey.DefaultTome);
            harness.Db.VectorOf(id).ShouldNotBeEmpty();
        }
    }

    [Fact]
    public async Task Run_CrashMidSweep_LeavesStoreRefusedAndResumesWithoutReembeddingFinishedRecords()
    {
        var harness = await Harness.SeededAsync("alpha", "beta", "gamma");
        harness.Provider.FailureText = "beta";

        await Should.ThrowAsync<UpstreamUnavailableException>(
            () => harness.RunAsync(NewModel, batchSize: 1, progress: null));

        harness.Provider.Requests.ShouldBe(["alpha", "beta"]);
        var freshStore = new QdrantMemoryStore(harness.Db, "grimoire", new EmbeddingOptions { Model = NewModel.Model, Dimensions = 4 });
        await Should.ThrowAsync<ReindexRequiredException>(
            () => freshStore.ListAsync(new MemoryFilter(), CancellationToken.None));

        harness.Provider.FailureText = null;
        harness.Provider.Requests.Clear();

        var outcome = await harness.RunAsync(NewModel, batchSize: 64, progress: null);

        outcome.ShouldBe(new ReindexOutcome(3, 2, 1));
        harness.Provider.Requests.ShouldBe(["beta", "gamma"]);
        await freshStore.ListAsync(new MemoryFilter(), CancellationToken.None);
    }

    [Fact]
    public async Task Run_CompletedRecords_AreSkippedOnRerun()
    {
        var harness = await Harness.SeededAsync("alpha", "beta");
        await harness.RunAsync(NewModel, batchSize: 64, progress: null);
        harness.Provider.Requests.Clear();

        var rerun = await harness.RunAsync(NewModel, batchSize: 64, progress: null);

        rerun.ShouldBe(new ReindexOutcome(2, 0, 2));
        harness.Provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_RecordsWrittenBetweenReindexes_CarryTheMarkerAndAreSkipped()
    {
        var harness = await Harness.SeededAsync("alpha");
        await harness.RunAsync(NewModel, batchSize: 64, progress: null);
        var postReindex = harness.NewModelStore();
        var added = TestMemories.Create("delta", id: Guid.Parse("00000000-0000-0000-0000-000000000009"));
        float[] vector = [1f, 0, 0, 0];
        await postReindex.UpsertAsync(added, vector, CancellationToken.None);
        harness.Provider.Requests.Clear();

        var rerun = await harness.RunAsync(NewModel, batchSize: 64, progress: null);

        rerun.ShouldBe(new ReindexOutcome(2, 0, 2));
        harness.Provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_SkipsTombstonedAndSupersededRecords()
    {
        var harness = await Harness.SeededWithGoneRecordsAsync();

        var outcome = await harness.RunAsync(NewModel, batchSize: 64, progress: null);

        outcome.ShouldBe(new ReindexOutcome(1, 1, 0));
        harness.Provider.Requests.ShouldBe(["alpha"]);
    }

    [Fact]
    public async Task Run_AfterLastBatch_CommitsFingerprintAndRecallWorksOnNewModel()
    {
        var harness = await Harness.SeededAsync("alpha", "beta");
        await harness.RunAsync(NewModel, batchSize: 1, progress: null);

        harness.Db.PayloadOf(EmbeddingFingerprint.MarkerPointId).ShouldNotContainKey(EmbeddingFingerprint.ReindexStateField);
        harness.Db.PayloadOf(EmbeddingFingerprint.MarkerPointId)[EmbeddingFingerprint.ModelField].StringValue
            .ShouldBe(NewModel.Model);
        await harness.Bootstrapper.EnsureAsync(NewModel, CancellationToken.None);

        var store = new QdrantMemoryStore(harness.Db, "grimoire", new EmbeddingOptions { Model = NewModel.Model, Dimensions = 4 });
        float[] query = [0f, 0, 0, 1];
        var matches = await store.SearchAsync(query, new MemoryFilter(), 1, CancellationToken.None);

        var top = matches.Single();
        top.Record.Content.ShouldBe("alpha");
        top.Similarity.ShouldBe(1.0, 1e-6);
    }

    [Fact]
    public async Task Run_WhenCollectionMissing_BootstrapsCollectionAndReportsEmpty()
    {
        var harness = Harness.Empty();

        var outcome = await harness.RunAsync(NewModel, batchSize: 64, progress: null);

        outcome.ShouldBe(new ReindexOutcome(0, 0, 0));
        await harness.Bootstrapper.EnsureAsync(NewModel, CancellationToken.None);
    }

    private sealed class Harness(FakeQdrantVectorDb db, FakeEmbeddingProvider provider)
    {
        public FakeQdrantVectorDb Db { get; } = db;

        public FakeEmbeddingProvider Provider { get; } = provider;

        public QdrantCollectionBootstrapper Bootstrapper { get; } = new(db, "grimoire");

        private QdrantMemoryStore Store { get; } = new(
            db, "grimoire", new EmbeddingOptions { Model = OldModel.Model, Dimensions = 4 });

        public IReadOnlyList<Guid> RecordIds { get; private set; } = [];

        public QdrantMemoryStore NewModelStore() =>
            new(Db, "grimoire", new EmbeddingOptions { Model = NewModel.Model, Dimensions = 4 });

        public static Harness Empty() =>
            new(new FakeQdrantVectorDb(), new FakeEmbeddingProvider(new Dictionary<string, float[]>()));

        public static async Task<Harness> SeededAsync(params string[] contents)
        {
            var harness = new Harness(new FakeQdrantVectorDb(), new FakeEmbeddingProvider(Vectors()));
            var ids = new List<Guid>();

            foreach (var (index, content) in contents.Index())
            {
                var record = TestMemories.Create(content, id: Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}"));
                await harness.Store.UpsertAsync(record, UnitVector(index), CancellationToken.None);
                ids.Add(record.Id);
            }

            harness.RecordIds = ids;
            return harness;
        }

        public static async Task<Harness> SeededWithGoneRecordsAsync()
        {
            var harness = await SeededAsync("alpha");
            var active = harness.RecordIds.Single();
            await harness.Store.UpsertAsync(
                TestMemories.Create("retired", status: MemoryStatus.Superseded, supersededBy: active), SupersededVector, CancellationToken.None);
            await harness.Store.UpsertAsync(
                TestMemories.Create("dropped", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment),
                TombstonedVector,
                CancellationToken.None);
            return harness;
        }

        public async Task<ReindexOutcome> RunAsync(EmbeddingFingerprint target, int batchSize, List<ReindexProgress>? progress)
        {
            Action<ReindexProgress>? sink = progress is null ? null : progress.Add;
            return await new EmbeddingReindexer(Db, "grimoire", Bootstrapper, Provider, batchSize)
                .RunAsync(target, sink, CancellationToken.None);
        }

        private static Dictionary<string, float[]> Vectors() => new()
        {
            ["alpha"] = [0f, 0, 0, 1],
            ["beta"] = [1f, 1, 0, 0],
            ["gamma"] = [0f, 1, 1, 0],
        };

        private static readonly float[] SupersededVector = [0f, 0, 0, 1];

        private static readonly float[] TombstonedVector = [0f, 0, 1, 0];

        private static float[] UnitVector(int index) => index switch
        {
            0 => [1f, 0, 0, 0],
            1 => [0f, 1, 0, 0],
            _ => [0f, 0, 1, 0],
        };
    }
}
