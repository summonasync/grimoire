using Grimoire.Core;

namespace Grimoire.Infrastructure.Tests.Memory;

internal static class TestMemories
{
    public static readonly DateTimeOffset Moment = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public static MemoryActor Agent { get; } = MemoryActor.Agent("dotnet-coder", "claude");

    public static MemoryRecord Create(
        string content,
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
        Guid? id = null,
        string? idempotencyKey = null)
    {
        var moment = created ?? Moment;

        return new MemoryRecord
        {
            Id = id ?? Guid.NewGuid(),
            Content = content,
            Scope = scope,
            Kind = kind,
            Actor = actor ?? Agent,
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
}
