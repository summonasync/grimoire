using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

// The loop stops capped only when a round came back full and the post-filter still dropped
// enough rows to leave the page short. The plain fake can hand that state to recall alone,
// through the one clause it does not cut on (CreatedBefore), and never to scry, which has no
// date parameter to send it. It can also never hand a door both warning sources at once: an
// empty tome means an empty first round, which is exhaustion, not a cap. This double
// manufactures the shape each of those tests needs — windows filled up to the asked size with
// rows the post-filter rejects (session scope against a long-term page, a tome nobody asked
// for). What the tests pin is the door reporting the stop; whether the real store can hand that
// state to a door at all is the C8 measurement, not a claim made here.
internal sealed class WindowPaddingStore(IMemoryStore inner) : IMemoryStore
{
    private static readonly MemoryRecord[] Padding =
    [
        PaddedRow("padded session row one"),
        PaddedRow("padded session row two"),
    ];

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(
        ReadOnlyMemory<float> vector,
        MemoryFilter filter,
        int limit,
        CancellationToken cancellationToken)
    {
        var returned = await inner.SearchAsync(vector, filter, limit, cancellationToken);
        if (returned.Count >= limit)
        {
            return returned;
        }

        var padded = new List<VectorMatch>(limit);
        padded.AddRange(returned);
        for (var index = returned.Count; index < limit; index++)
        {
            padded.Add(new VectorMatch { Record = Padding[index % Padding.Length], Similarity = 0.4 });
        }

        return padded;
    }

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

    public Task<long> CountAsync(MemoryFilter filter, CancellationToken cancellationToken) =>
        inner.CountAsync(filter, cancellationToken);

    public Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        inner.DeleteAsync(ids, cancellationToken);

    public Task<DeleteByTomeResult> DeleteByTomeAsync(string tome, CancellationToken cancellationToken) =>
        inner.DeleteByTomeAsync(tome, cancellationToken);

    public IAsyncEnumerable<MemoryPoint> EnumerateAllAsync(CancellationToken cancellationToken) =>
        inner.EnumerateAllAsync(cancellationToken);

    public Task UpsertBatchAsync(IReadOnlyList<MemoryPoint> points, CancellationToken cancellationToken) =>
        inner.UpsertBatchAsync(points, cancellationToken);

    private static MemoryRecord PaddedRow(string content) => new()
    {
        Id = Guid.NewGuid(),
        Content = content,
        Scope = MemoryScope.Session,
        Kind = MemoryKind.Journal,
        Actor = MemoryActor.Human,
        Tome = "window-padding",
        CreatedAt = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero),
        LastAccessedAt = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero),
    };
}
