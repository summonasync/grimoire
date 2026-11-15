using Grimoire.Core;
using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

// These fixtures lean on the clause the fake store does not enforce — CreatedBefore — so they
// prove the envelope arithmetic (what rides up when the loop says capped-while-short) and not
// product reachability; the production trigger is what the C8 measurement has to answer.
public sealed class MemoryServiceRecallWarningsTests
{
    private const string QueryText = "deployment";

    private static readonly float[] QueryVector = [1f, 0f, 0f];

    private static readonly float[] RejectedVector = [0.5f, 0.866f, 0f];

    private static readonly float[] KeptVector = [0.9f, 0.4359f, 0f];

    private static readonly DateTimeOffset Cutoff = FakeClock.DefaultStart;

    private readonly FakeMemoryStore _store = new();

    private readonly MemoryService _service;

    public MemoryServiceRecallWarningsTests()
    {
        var embeddings = new FakeEmbeddingProvider(new Dictionary<string, float[]> { [QueryText] = QueryVector });
        _service = TestMemories.Service(_store, embeddings, new FakeClock());
    }

    [Fact]
    public async Task Recall_WindowFillsWithRejects_EnvelopeCarriesTheCapWarning()
    {
        SeedRejected(24);

        var result = await _service.RecallAsync(Query(limit: 1), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(RecallEscalationLoop.MaxRounds);
        result.Matches.ShouldBeEmpty();
        result.Warnings.ShouldNotBeNull();
        result.Warnings!.ShouldHaveSingleItem();
        result.Warnings[0].ShouldContain("after 4 rounds");
        result.Warnings[0].ShouldContain("limit 1");
        result.Warnings[0].ShouldContain("last window 24");
    }

    [Fact]
    public async Task Recall_RoundCapWithAPartialPage_ShipsWhatItKeptAndStillWarns()
    {
        SeedRejected(120);
        var kept = SeedKept("the one honest match");

        var result = await _service.RecallAsync(Query(limit: 5), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(RecallEscalationLoop.MaxRounds);
        result.Matches.Select(match => match.Record.Id).ShouldBe([kept.Id]);
        result.Warnings.ShouldNotBeNull();
        result.Warnings![0].ShouldContain("1 of limit 5 matches");
    }

    [Fact]
    public async Task Recall_StoreCameBackShort_EnvelopeCarriesNoWarnings()
    {
        SeedKept("the only match");

        var result = await _service.RecallAsync(Query(limit: 5), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(1);
        result.Matches.ShouldHaveSingleItem();
        result.Warnings.ShouldBeNull();
    }

    [Fact]
    public async Task Recall_PageFilledByTheFirstWindow_EnvelopeCarriesNoWarnings()
    {
        for (var index = 1; index <= 3; index++)
        {
            SeedKept($"match {index}");
        }

        var result = await _service.RecallAsync(Query(limit: 1), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(1);
        result.Matches.ShouldHaveSingleItem();
        result.Warnings.ShouldBeNull();
    }

    private static RecallQuery Query(int limit) => new()
    {
        Text = QueryText,
        Limit = limit,
        CreatedBefore = Cutoff,
    };

    private MemoryRecord SeedKept(string content)
    {
        var record = TestMemories.Create(content, KeptVector, created: Cutoff.AddDays(-2));
        _store.Seed(record, KeptVector);
        return record;
    }

    private void SeedRejected(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            var record = TestMemories.Create($"noise {index}", RejectedVector, created: Cutoff.AddDays(1));
            _store.Seed(record, RejectedVector);
        }
    }
}
