using Grimoire.Core;

namespace Grimoire.Api.Querying;

public sealed class MemoryQueryRunner(IMemoryStore store, IEmbeddingProvider embeddings, MemoryEngineOptions? options = null)
{
    private readonly RecallEscalationLoop _recallWindows = new(store, options);

    public async Task<MemoryQueryResult> RunAsync(
        string? queryText,
        ParsedMemoryQuery parsed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parsed);

        if (string.IsNullOrWhiteSpace(queryText))
        {
            return new MemoryQueryResult { Records = await ListAsync(parsed, cancellationToken) };
        }

        var vector = await embeddings.EmbedAsync(queryText, cancellationToken);
        var merged = new List<VectorMatch>();
        IReadOnlyList<string>? warnings = null;

        foreach (var status in parsed.Statuses)
        {
            var scan = await _recallWindows.SearchAsync(
                vector,
                parsed.Filter with { Status = status },
                parsed.Limit,
                cancellationToken);

            merged.AddRange(scan.Kept);
            warnings = WarningMerge.Merge(warnings, RoundCapWarning.Build(scan, parsed.Limit));
        }

        return new MemoryQueryResult
        {
            Records = [.. merged
                .OrderByDescending(match => match.Similarity)
                .ThenBy(match => match.Record.Id)
                .Take(parsed.Limit)
                .Select(match => match.Record)],
            Warnings = warnings,
        };
    }

    private async Task<IReadOnlyList<MemoryRecord>> ListAsync(ParsedMemoryQuery parsed, CancellationToken cancellationToken)
    {
        var merged = new List<MemoryRecord>();
        foreach (var status in parsed.Statuses)
        {
            var records = await store.ListAsync(parsed.Filter with { Status = status }, cancellationToken);
            merged.AddRange(records);
        }

        return [.. merged
            .Where(parsed.Filter.Matches)
            .OrderByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.Id)
            .Take(parsed.Limit)];
    }
}
