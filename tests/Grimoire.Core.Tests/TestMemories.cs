using Grimoire.Core.Tests.Fakes;

namespace Grimoire.Core.Tests;

internal static class TestMemories
{
    public static MemoryActor Agent { get; } = MemoryActor.Agent("dotnet-coder", "claude");

    public static MemoryRecord Create(
        string content,
        float[] vector,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        MemoryActor? actor = null,
        DateTimeOffset? created = null,
        DateTimeOffset? accessed = null,
        double importance = 0.5,
        long accessCount = 0,
        string? projectId = null,
        string? sessionId = null,
        IReadOnlyList<string>? tags = null,
        MemoryStatus status = MemoryStatus.Active,
        DateTimeOffset? expiresAt = null,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null,
        IReadOnlyList<Guid>? sources = null,
        string? idempotencyKey = null,
        string? tome = null)
    {
        var moment = created ?? FakeClock.DefaultStart;

        return new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = scope,
            Kind = kind,
            Actor = actor ?? Agent,
            Tome = tome ?? TomeKey.DefaultTome,
            ProjectId = projectId,
            SessionId = sessionId,
            Tags = tags ?? [],
            Importance = importance,
            CreatedAt = moment,
            LastAccessedAt = accessed ?? moment,
            AccessCount = accessCount,
            ExpiresAt = expiresAt,
            Status = status,
            SupersededBy = supersededBy,
            TombstoneReason = tombstoneReason,
            TombstonedAt = tombstonedAt,
            Sources = sources ?? [],
            IdempotencyKey = idempotencyKey,
        };
    }

    public static MemoryService Service(IMemoryStore store, FakeEmbeddingProvider embeddings, FakeClock clock, MemoryEngineOptions? options = null)
        => new(store, embeddings, clock, options);
}
