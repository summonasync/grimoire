namespace Grimoire.Core;

public sealed class RecallEscalationLoop(IMemoryStore store, MemoryEngineOptions? options = null)
{
    public const int MaxRounds = 4;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<RecallEscalationResult> SearchAsync(
        ReadOnlyMemory<float> vector,
        MemoryFilter filter,
        int limit,
        CancellationToken cancellationToken)
    {
        var window = limit * Math.Max(_options.RecallOversampling, 1);
        var kept = new Dictionary<Guid, VectorMatch>();
        var rounds = 0;

        while (true)
        {
            var round = await store.SearchAsync(vector, filter, window, cancellationToken);
            rounds++;

            foreach (var match in round)
            {
                if (filter.Matches(match.Record))
                {
                    kept.TryAdd(match.Record.Id, match);
                }
            }

            if (kept.Count >= limit)
            {
                return Stop(kept, rounds, window, RecallStopReason.PageFilled);
            }

            if (round.Count < window)
            {
                return Stop(kept, rounds, window, RecallStopReason.StoreExhausted);
            }

            if (rounds == MaxRounds)
            {
                return Stop(kept, rounds, window, RecallStopReason.RoundCapReached);
            }

            window *= 2;
        }
    }

    private static RecallEscalationResult Stop(
        Dictionary<Guid, VectorMatch> kept,
        int rounds,
        int window,
        RecallStopReason reason) => new()
        {
            Kept = [.. kept.Values],
            Rounds = rounds,
            LastWindow = window,
            StoppedBy = reason,
        };
}
