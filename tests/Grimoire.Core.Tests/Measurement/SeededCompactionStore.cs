using Grimoire.Core.Tests.Fakes;

namespace Grimoire.Core.Tests.Measurement;

public sealed class SeededCompactionStore(
    FakeMemoryStore store,
    FakeClock clock,
    MemoryService service,
    IReadOnlyList<string> tomes)
{
    public static readonly IReadOnlyList<string> ProbeTomes = [TomeKey.DefaultTome, "beta", "gamma"];

    public FakeMemoryStore Store { get; } = store;

    public FakeClock Clock { get; } = clock;

    public MemoryService Service { get; } = service;

    public IReadOnlyList<string> Tomes { get; } = tomes;

    public Task<IReadOnlyList<MemoryPoint>> FetchAsync(string? tome, CancellationToken cancellationToken)
    {
        return Store.FetchAsync(new MemoryFilter
        {
            Scopes = MemoryScopes.LongTerm,
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Tome = tome,
        }, cancellationToken);
    }

    public static SeededCompactionStore Build(int entriesPerTome, params string[] tomes)
    {
        if (entriesPerTome % 2 != 0)
        {
            throw new ArgumentException("Twin pairing needs an even entry count per tome.", nameof(entriesPerTome));
        }

        var pairs = new NearDuplicatePairs();
        var clock = new FakeClock();
        var store = new FakeMemoryStore();
        var tomeKeys = tomes.Length > 0 ? tomes : ProbeTomes;
        var accessed = clock.UtcNow.AddDays(-30);

        foreach (var tome in tomeKeys)
        {
            for (var pair = 0; pair < entriesPerTome / 2; pair++)
            {
                foreach (var (record, vector) in pairs.NextTwinJournals(tome, accessed))
                {
                    store.Seed(record, vector);
                }
            }
        }

        var service = TestMemories.Service(store, new FakeEmbeddingProvider(new Dictionary<string, float[]>()), clock);

        return new SeededCompactionStore(store, clock, service, [.. tomeKeys]);
    }
}
