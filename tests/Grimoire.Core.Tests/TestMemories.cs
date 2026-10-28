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
        string? tome = null,
        IReadOnlyList<CorrectionEntry>? corrections = null)
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
            Corrections = corrections ?? [],
        };
    }

    public static MemoryPoint Point(
        string content,
        float[] vector,
        string? tome = null,
        MemoryStatus status = MemoryStatus.Active,
        string? projectId = null,
        IReadOnlyList<string>? tags = null,
        MemoryActor? actor = null)
        => new()
        {
            Record = Create(content, vector, tome: tome, status: status, projectId: projectId, tags: tags, actor: actor),
            Vector = vector,
        };

    public static CorrectionEntry Patch(DateTimeOffset at, string reason = "corrected") => new()
    {
        At = at,
        Actor = Agent,
        Action = CorrectionAction.Patch,
        Reason = reason,
    };

    public static MemoryService Service(
        IMemoryStore store,
        FakeEmbeddingProvider embeddings,
        FakeClock clock,
        MemoryEngineOptions? options = null,
        GcOptions? gcOptions = null,
        IGcStateStore? gcState = null,
        ITopicClassifier? topics = null,
        ConsolidationOptions? consolidationOptions = null,
        IConsolidationStateStore? consolidationState = null)
        => new(store, embeddings, clock, options, gcOptions, gcState, topics, consolidationOptions, consolidationState);

    public static CorrectionService Corrections(IMemoryStore store, FakeClock clock, MemoryEngineOptions? options = null)
        => new(store, clock, options);
}
