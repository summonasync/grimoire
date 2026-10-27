using Grimoire.Core;

namespace Grimoire.Api.Querying;

public sealed class MemoryQueryRunner(IMemoryStore store, IEmbeddingProvider embeddings)
{
    public async Task<IReadOnlyList<MemoryRecord>> RunAsync(string? queryText, ParsedMemoryQuery parsed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parsed);

        if (string.IsNullOrWhiteSpace(queryText))
        {
            return await ListAsync(parsed, cancellationToken);
        }

        var vector = await embeddings.EmbedAsync(queryText, cancellationToken);
        var merged = new List<VectorMatch>();
        foreach (var status in parsed.Statuses)
        {
            var matches = await store.SearchAsync(vector, parsed.Filter with { Status = status }, parsed.Limit, cancellationToken);
            merged.AddRange(matches);
        }

        return [.. merged
            .Where(match => parsed.Filter.Matches(match.Record))
            .OrderByDescending(match => match.Similarity)
            .ThenBy(match => match.Record.Id)
            .Take(parsed.Limit)
            .Select(match => match.Record)];
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
