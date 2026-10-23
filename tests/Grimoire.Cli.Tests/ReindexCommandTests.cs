using System.Globalization;
using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ReindexCommandTests : IDisposable
{
    private const string TargetModel = "qwen3-embedding";

    private readonly RecordingVectorDb _db = new() { CollectionExists = true };

    private readonly TestHost _host;

    public ReindexCommandTests()
    {
        _host = new TestHost(
            new StubEmbeddings(new Dictionary<string, float[]>
            {
                ["alpha"] = [1f, 0],
                ["beta"] = [0f, 1],
            }),
            new MemoryEngineOptions { ReindexBatchSize = 1 },
            _db);
    }

    [Fact]
    public async Task RunAsync_Reindex_ReembedsActiveRecordsInBatches()
    {
        await SeedAsync("alpha");
        await SeedAsync("beta");

        var code = await _host.RunAsync("reindex");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Embeddings.Requests.ShouldBe(["alpha", "beta"]);
        var output = _host.Output.ToString();
        output.ShouldContain("1/2");
        output.ShouldContain("2/2");
        output.ShouldContain("Reindex complete: 2 re-embedded, 0 already current, 2 scanned");

        foreach (var id in SeededIds)
        {
            (await PayloadOfAsync(id))[MemoryPayloadFields.VectorModel].StringValue.ShouldBe(TargetModel);
        }
    }

    [Fact]
    public async Task RunAsync_Reindex_SecondRun_SkipsCompletedRecords()
    {
        await SeedAsync("alpha");
        await SeedAsync("beta");
        await _host.RunAsync("reindex");
        _host.Embeddings.Requests.Clear();

        var code = await _host.RunAsync("reindex");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Embeddings.Requests.ShouldBeEmpty();
        _host.Output.ToString().ShouldContain("0 re-embedded, 2 already current, 2 scanned");
    }

    [Fact]
    public async Task RunAsync_Reindex_AfterInterruptedSweep_SkipsFinishedRecordsAndClearsRefusal()
    {
        await SeedAsync("alpha", alreadyCurrent: true);
        await SeedAsync("beta");
        await SeedMarkerAsync(reindexInProgress: true);
        _host.Store.ListFailure = new ReindexRequiredException(
            "An embedding reindex to 'nomic-embed-text' is in progress — run `grimoire reindex` to resume it.");

        var refused = await _host.RunAsync("list");

        refused.ShouldBe(ExitCodes.ReindexRequired);
        _host.Output.ToString().ShouldContain("error [reindex_required]");
        _host.Output.ToString().ShouldContain("run `grimoire reindex`");

        var resumed = await _host.RunAsync("reindex");

        resumed.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Embeddings.Requests.ShouldBe(["beta"]);
        _host.Output.ToString().ShouldContain("1 re-embedded, 1 already current, 2 scanned");
        (await PayloadOfAsync(EmbeddingFingerprint.MarkerPointId))
            .ShouldNotContainKey(EmbeddingFingerprint.ReindexStateField);
    }

    [Fact]
    public async Task RunAsync_Reindex_AfterDimensionsChange_RecreatesCollectionAndReportsDiscards()
    {
        await SeedAsync("alpha");
        await SeedAsync("beta");
        await SeedMarkerAsync(reindexInProgress: false);
        _db.CollectionDimensions = 4;

        var code = await _host.RunAsync("reindex");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _db.DroppedCount.ShouldBe(1);
        _db.CreateCollectionCount.ShouldBe(1);
        _db.CollectionDimensions.ShouldBe(1024);
        _host.Embeddings.Requests.ShouldBeEmpty();
        var output = _host.Output.ToString();
        output.ShouldContain("was recreated under 1024 dimensions");
        output.ShouldContain("2 active records of the previous embedding were discarded");
        output.ShouldContain("Reindex complete: 0 re-embedded, 0 already current, 0 scanned");
    }

    private List<Guid> SeededIds { get; } = [];

    private async Task<Guid> SeedAsync(string content, bool alreadyCurrent = false)
    {
        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = MemoryActor.Human,
            CreatedAt = Moment,
            LastAccessedAt = Moment,
        };
        var payload = new Dictionary<string, Value>(MemoryPayloadMapper.ToPayload(record));

        if (alreadyCurrent)
        {
            payload[MemoryPayloadFields.VectorModel] = TargetModel;
        }

        var point = new PointStruct { Id = record.Id, Vectors = new float[2] };

        foreach (var pair in payload)
        {
            point.Payload.Add(pair.Key, pair.Value);
        }

        await _db.UpsertAsync("grimoire", [point], CancellationToken.None);
        SeededIds.Add(record.Id);
        return record.Id;
    }

    private async Task SeedMarkerAsync(bool reindexInProgress)
    {
        var payload = new Dictionary<string, Value>(EmbeddingFingerprint.From(TargetModel, 2).ToPayload());

        if (reindexInProgress)
        {
            payload[EmbeddingFingerprint.ReindexStateField] = EmbeddingFingerprint.ReindexStateInProgress;
        }

        var marker = new PointStruct { Id = EmbeddingFingerprint.MarkerPointId, Vectors = new float[2] };

        foreach (var pair in payload)
        {
            marker.Payload.Add(pair.Key, pair.Value);
        }

        await _db.UpsertAsync("grimoire", [marker], CancellationToken.None);
    }

    private async Task<IReadOnlyDictionary<string, Value>> PayloadOfAsync(Guid id)
    {
        var points = await _db.RetrieveAsync("grimoire", [id], withVectors: false, CancellationToken.None);
        return points.Single().Payload;
    }

    private static DateTimeOffset Moment => DateTimeOffset.Parse("2026-10-17T12:00:00Z", CultureInfo.InvariantCulture);

    public void Dispose() => _host.Dispose();
}
