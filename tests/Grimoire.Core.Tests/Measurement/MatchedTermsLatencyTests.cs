using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Grimoire.Core.Tests.Measurement;

[Collection("Measurement")]
public sealed class MatchedTermsLatencyTests(ITestOutputHelper output)
{
    private const int EntriesPerTome = 200;

    private const int RecallLimit = 10;

    private const int MatcherWarmup = 20;

    private const int MatcherRepetitions = 101;

    private const int RecallWarmup = 3;

    private const int RecallRepetitions = 11;

    [Fact]
    public async Task Matcher_OverRecallHitSets_MeasuresLatencyAndAllocations()
    {
        var pairs = new NearDuplicatePairs();
        var clock = new FakeClock();
        var store = new FakeMemoryStore();
        var queries = QueryTexts(pairs.Topics);
        var embeddings = new FakeEmbeddingProvider(queries.ToDictionary(text => text, _ => pairs.UnitVector()));
        var service = TestMemories.Service(store, embeddings, clock);

        foreach (var tome in SeededCompactionStore.ProbeTomes)
        {
            for (var ordinal = 0; ordinal < EntriesPerTome; ordinal++)
            {
                var (record, vector) = pairs.NextJournal(tome, clock.UtcNow.AddDays(-30));
                store.Seed(record, vector);
            }
        }

        store.Records.Count.ShouldBe(EntriesPerTome * SeededCompactionStore.ProbeTomes.Count);

        foreach (var query in queries)
        {
            var hits = await service.RecallAsync(new RecallQuery { Text = query, Limit = RecallLimit }, CancellationToken.None);

            hits.Count.ShouldBe(RecallLimit);

            var terms = LexicalTermMatcher.Tokenize(query);
            terms.Count.ShouldBe(3);

            var matcher = Probe.Measure(MatcherWarmup, MatcherRepetitions, () =>
            {
                var tokens = LexicalTermMatcher.Tokenize(query);
                foreach (var hit in hits)
                {
                    LexicalTermMatcher.Match(tokens, hit.Record);
                }
            });

            var recall = await Probe.MeasureAsync(RecallWarmup, RecallRepetitions, () =>
                service.RecallAsync(new RecallQuery { Text = query, Limit = RecallLimit }, CancellationToken.None));

            var matchedTerms = hits.Sum(hit => LexicalTermMatcher.Match(terms, hit.Record).Count);

            matchedTerms.ShouldBeGreaterThan(0);

            output.WriteLine(
                $"query=\"{query}\" hits={hits.Count} matchedTerms={matchedTerms} " +
                $"matcher={matcher.MedianMilliseconds:F4}ms/{matcher.MedianAllocatedBytes:N0}B per set | " +
                $"recall={recall.MedianMilliseconds:F3}ms/{recall.MedianAllocatedBytes:N0}B");
        }
    }

    private static IReadOnlyList<string> QueryTexts(IReadOnlyList<string> topics)
    {
        var queries = new List<string>();

        for (var offset = 0; offset < 4; offset++)
        {
            queries.Add($"{topics[offset]} {topics[(offset + 1) % topics.Count]} {topics[(offset + 2) % topics.Count]}");
        }

        return queries;
    }
}
