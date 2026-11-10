using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class TomePurgeServiceTests
{
    private static readonly MemoryActor Agent = MemoryActor.Agent("dotnet-coder", "claude");

    private readonly FakeMemoryStore _store = new();

    private readonly FakeTomeManifestStore _manifests = new();

    private readonly TomePurgeService _service;

    public TomePurgeServiceTests()
    {
        _service = new TomePurgeService(_store, _manifests);
    }

    [Fact]
    public async Task Purge_RemovesEveryStatusInTheTome_AndTheManifest()
    {
        Seed("dev", MemoryStatus.Active);
        Seed("dev", MemoryStatus.Superseded, supersededBy: Guid.NewGuid());
        Seed("dev", MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);
        Seed("crew", MemoryStatus.Active);
        SeedManifest("dev");

        var result = await _service.PurgeTomeAsync("dev", MemoryActor.Human, CancellationToken.None);

        result.Records.Active.ShouldBe(1);
        result.Records.Superseded.ShouldBe(1);
        result.Records.Tombstoned.ShouldBe(1);
        result.ManifestPurged.ShouldBeTrue();
        _store.Records.Select(record => record.Tome).ShouldBe(["crew"]);
        _manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Purge_AttemptsManifestDeleteAfterRecordDelete()
    {
        var log = new List<string>();
        var loggingStore = new LoggingMemoryStore(_store, log);
        var manifests = new LoggingManifestStore(_manifests, log);
        Seed("dev", MemoryStatus.Active);
        SeedManifest("dev");

        var service = new TomePurgeService(loggingStore, manifests);
        await service.PurgeTomeAsync("dev", MemoryActor.Human, CancellationToken.None);

        log.ShouldBe(["records:deleted:dev", "manifest:delete:dev"]);
    }

    [Fact]
    public async Task Purge_WhenManifestStoreThrows_ReportsManifestPurgedFalseAndKeepsRecordCounts()
    {
        Seed("dev", MemoryStatus.Active);
        Seed("dev", MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);
        _manifests.Failure = new InvalidOperationException("manifest store exploded");

        var result = await _service.PurgeTomeAsync("dev", MemoryActor.Human, CancellationToken.None);

        result.Records.Active.ShouldBe(1);
        result.Records.Tombstoned.ShouldBe(1);
        result.Records.Superseded.ShouldBe(0);
        result.ManifestPurged.ShouldBeFalse();
        _store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Purge_WithAgentActor_ThrowsAndDeletesNothing()
    {
        Seed("dev", MemoryStatus.Active);
        SeedManifest("dev");

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _service.PurgeTomeAsync("dev", Agent, CancellationToken.None));

        _store.Records.ShouldNotBeEmpty();
        _manifests.Manifests.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Purge_ReRunOnPurgedTome_ConvergesToZeroCounts()
    {
        await _service.PurgeTomeAsync("ghost", MemoryActor.Human, CancellationToken.None);

        var result = await _service.PurgeTomeAsync("ghost", MemoryActor.Human, CancellationToken.None);

        result.Records.Active.ShouldBe(0);
        result.Records.Superseded.ShouldBe(0);
        result.Records.Tombstoned.ShouldBe(0);
        result.ManifestPurged.ShouldBeTrue();
    }

    [Fact]
    public async Task MemoryPaths_IgnoreAThrowingManifestStore()
    {
        _manifests.Failure = new InvalidOperationException("manifest store exploded");
        var vectors = new Dictionary<string, float[]> { ["note survives"] = [1f, 0f, 0f] };
        var memories = TestMemories.Service(_store, new FakeEmbeddingProvider(vectors), new FakeClock());
        _manifests.Seed(new TomeManifest
        {
            Key = "dev",
            Description = "Project work",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });

        var noted = await memories.NoteAsync(new NoteRequest
        {
            Content = "note survives",
            Actor = Agent,
            Tome = "dev",
        }, CancellationToken.None);
        var listed = await memories.ListAsync(new MemoryFilter { Tome = "dev" }, CancellationToken.None);

        noted.Record.Tome.ShouldBe("dev");
        listed.ShouldContain(record => record.Id == noted.Record.Id);
        _store.Records.ShouldNotBeEmpty();
    }

    private void Seed(
        string tome,
        MemoryStatus status = MemoryStatus.Active,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null)
    {
        _store.Seed(TestMemories.Create(
            $"record in {tome}",
            [1f, 0f, 0f],
            status: status,
            supersededBy: supersededBy,
            tombstoneReason: tombstoneReason,
            tombstonedAt: tombstonedAt,
            tome: tome));
    }

    private void SeedManifest(string key)
    {
        _manifests.Seed(new TomeManifest
        {
            Key = key,
            Description = "Project work",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });
    }

    private sealed class LoggingMemoryStore(IMemoryStore inner, List<string> log) : IMemoryStore
    {
        public Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float> vector, CancellationToken cancellationToken) =>
            inner.UpsertAsync(record, vector, cancellationToken);

        public Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken) =>
            inner.UpdateAsync(record, cancellationToken);

        public Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken) =>
            inner.ListAsync(filter, cancellationToken);

        public Task<IReadOnlyList<MemoryPoint>> FetchAsync(MemoryFilter filter, CancellationToken cancellationToken) =>
            inner.FetchAsync(filter, cancellationToken);

        public Task<IReadOnlyList<VectorMatch>> SearchAsync(ReadOnlyMemory<float> vector, MemoryFilter filter, int limit, CancellationToken cancellationToken) =>
            inner.SearchAsync(vector, filter, limit, cancellationToken);

        public Task<long> CountAsync(MemoryFilter filter, CancellationToken cancellationToken) =>
            inner.CountAsync(filter, cancellationToken);

        public Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
            inner.DeleteAsync(ids, cancellationToken);

        public async Task<DeleteByTomeResult> DeleteByTomeAsync(string tome, CancellationToken cancellationToken)
        {
            var result = await inner.DeleteByTomeAsync(tome, cancellationToken);
            log.Add($"records:deleted:{tome}");
            return result;
        }

        public IAsyncEnumerable<MemoryPoint> EnumerateAllAsync(CancellationToken cancellationToken) =>
            inner.EnumerateAllAsync(cancellationToken);

        public Task UpsertBatchAsync(IReadOnlyList<MemoryPoint> points, CancellationToken cancellationToken) =>
            inner.UpsertBatchAsync(points, cancellationToken);
    }

    private sealed class LoggingManifestStore(ITomeManifestStore inner, List<string> log) : ITomeManifestStore
    {
        public async Task CreateAsync(TomeManifest manifest, CancellationToken cancellationToken)
        {
            await inner.CreateAsync(manifest, cancellationToken);
            log.Add($"manifest:create:{manifest.Key}");
        }

        public Task<TomeManifest?> GetAsync(string key, CancellationToken cancellationToken) =>
            inner.GetAsync(key, cancellationToken);

        public async Task UpdateAsync(string key, string description, MemoryActor actor, DateTimeOffset now, CancellationToken cancellationToken)
        {
            await inner.UpdateAsync(key, description, actor, now, cancellationToken);
            log.Add($"manifest:update:{key}");
        }

        public async Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            await inner.DeleteAsync(key, cancellationToken);
            log.Add($"manifest:delete:{key}");
        }

        public Task<IReadOnlyList<TomeManifest>> ListAsync(CancellationToken cancellationToken) =>
            inner.ListAsync(cancellationToken);
    }
}
