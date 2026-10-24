using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Grimoire.Core.Tests.Measurement;

[CollectionDefinition("Measurement", DisableParallelization = true)]
public sealed class MeasurementCollection;

public sealed class PerTomeCompactionCostTests(ITestOutputHelper output)
{
    private const int Warmup = 3;

    private static readonly CompactionOptions Options = new();

    [Theory]
    [InlineData(50, 31)]
    [InlineData(200, 11)]
    [InlineData(500, 5)]
    public async Task Cost_ThreeTomesAtEachSize_PricesAllCompactionForks(int entriesPerTome, int repetitions)
    {
        var harness = SeededCompactionStore.Build(entriesPerTome);
        var now = harness.Clock.UtcNow;
        var storeTotal = entriesPerTome * SeededCompactionStore.ProbeTomes.Count;

        harness.Store.Records.Count.ShouldBe(storeTotal);

        var current = await Probe.MeasureAsync(Warmup, repetitions, async () =>
        {
            var journal = await harness.FetchAsync(TomeKey.DefaultTome, CancellationToken.None);
            CompactionPlanner.Plan(journal, now, Options);
        });

        var whole = await Probe.MeasureAsync(Warmup, repetitions, async () =>
        {
            var journal = await harness.FetchAsync(null, CancellationToken.None);
            CompactionPlanner.Plan(journal, now, Options);
        });

        var perTome = await Probe.MeasureAsync(Warmup, repetitions, async () =>
        {
            foreach (var tome in harness.Tomes)
            {
                var journal = await harness.FetchAsync(tome, CancellationToken.None);
                CompactionPlanner.Plan(journal, now, Options);
            }
        });

        var currentJournal = await harness.FetchAsync(TomeKey.DefaultTome, CancellationToken.None);
        var wholeJournal = await harness.FetchAsync(null, CancellationToken.None);
        var perTomeJournals = new Dictionary<string, IReadOnlyList<MemoryPoint>>();
        foreach (var tome in harness.Tomes)
        {
            perTomeJournals[tome] = await harness.FetchAsync(tome, CancellationToken.None);
        }

        var planOnlyCurrent = Probe.Measure(Warmup, repetitions, () => CompactionPlanner.Plan(currentJournal, now, Options));
        var planOnlyWhole = Probe.Measure(Warmup, repetitions, () => CompactionPlanner.Plan(wholeJournal, now, Options));
        var planOnlyPerTome = Probe.Measure(Warmup, repetitions, () => PlanPerTome());
        var stats = await Probe.MeasureAsync(Warmup, repetitions, () => harness.Service.StatsAsync(CancellationToken.None));

        var currentCovered = CompactionPlanner.Plan(currentJournal, now, Options).Sum(group => group.MemoryIds.Count);
        var wholeCovered = CompactionPlanner.Plan(wholeJournal, now, Options).Sum(group => group.MemoryIds.Count);
        var perTomeCovered = PlanPerTome().Sum(group => group.MemoryIds.Count);
        var statsSnapshot = await harness.Service.StatsAsync(CancellationToken.None);

        currentCovered.ShouldBe(entriesPerTome);
        wholeCovered.ShouldBe(storeTotal);
        perTomeCovered.ShouldBe(storeTotal);
        statsSnapshot.PendingCompaction.ShouldBe(entriesPerTome);
        statsSnapshot.ByTome.Keys.ShouldBe(SeededCompactionStore.ProbeTomes, true);

        output.WriteLine(
            $"[3x{entriesPerTome}] ms: stats={stats.MedianMilliseconds:F3} current={current.MedianMilliseconds:F3} " +
            $"whole={whole.MedianMilliseconds:F3} perTomeSum={perTome.MedianMilliseconds:F3} | planOnly: " +
            $"current={planOnlyCurrent.MedianMilliseconds:F3} whole={planOnlyWhole.MedianMilliseconds:F3} perTomeSum={planOnlyPerTome.MedianMilliseconds:F3}");
        output.WriteLine(
            $"[3x{entriesPerTome}] bytes: stats={stats.MedianAllocatedBytes:N0} current={current.MedianAllocatedBytes:N0} " +
            $"whole={whole.MedianAllocatedBytes:N0} perTomeSum={perTome.MedianAllocatedBytes:N0} | planOnly: " +
            $"current={planOnlyCurrent.MedianAllocatedBytes:N0} whole={planOnlyWhole.MedianAllocatedBytes:N0} perTomeSum={planOnlyPerTome.MedianAllocatedBytes:N0}");
        return;

        IReadOnlyList<CompactionCandidateGroup> PlanPerTome()
        {
            var groups = new List<CompactionCandidateGroup>();
            foreach (var journal in perTomeJournals.Values)
            {
                groups.AddRange(CompactionPlanner.Plan(journal, now, Options));
            }

            return groups;
        }
    }

    [Fact]
    public async Task PlanOnly_EightHundredVersusTwoHundredJournalRecords_ShowsQuadraticGrowth()
    {
        var small = SeededCompactionStore.Build(200, TomeKey.DefaultTome);
        var large = SeededCompactionStore.Build(800, TomeKey.DefaultTome);
        var now = small.Clock.UtcNow;

        var smallJournal = await small.FetchAsync(TomeKey.DefaultTome, CancellationToken.None);
        var largeJournal = await large.FetchAsync(TomeKey.DefaultTome, CancellationToken.None);

        smallJournal.Count.ShouldBe(200);
        largeJournal.Count.ShouldBe(800);
        CompactionPlanner.Plan(smallJournal, now, Options).Sum(group => group.MemoryIds.Count).ShouldBe(200);
        CompactionPlanner.Plan(largeJournal, now, Options).Sum(group => group.MemoryIds.Count).ShouldBe(800);

        var baseline = Probe.Measure(5, 31, () => CompactionPlanner.Plan(smallJournal, now, Options));
        var scaled = Probe.Measure(3, 9, () => CompactionPlanner.Plan(largeJournal, now, Options));
        var ratio = scaled.MedianMilliseconds / baseline.MedianMilliseconds;

        ratio.ShouldBeGreaterThanOrEqualTo(8.0);
        output.WriteLine(
            $"[plan-only 200 vs 800, one tome] 200={baseline.MedianMilliseconds:F3}ms/{baseline.MedianAllocatedBytes:N0}B " +
            $"800={scaled.MedianMilliseconds:F3}ms/{scaled.MedianAllocatedBytes:N0}B ratio={ratio:F1}x (n^2 predicts 16x)");
    }
}
