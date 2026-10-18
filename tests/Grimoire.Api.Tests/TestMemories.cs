using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;

namespace Grimoire.Api.Tests;

internal static class TestMemories
{
    public static MemoryActor Agent { get; } = MemoryActor.Agent("dotnet-coder", "claude");

    public static MemoryRecord Create(
        string content,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        MemoryActor? actor = null,
        DateTimeOffset? createdAt = null,
        IReadOnlyList<string>? tags = null,
        MemoryStatus status = MemoryStatus.Active,
        double importance = 0.5,
        DateTimeOffset? expiresAt = null,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null,
        string tome = TomeKey.DefaultTome)
    {
        var moment = createdAt ?? FakeClock.DefaultStart;

        return new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = scope,
            Kind = kind,
            Actor = actor ?? Agent,
            Tome = tome,
            Tags = tags ?? [],
            Importance = importance,
            CreatedAt = moment,
            LastAccessedAt = moment,
            ExpiresAt = expiresAt,
            Status = status,
            SupersededBy = supersededBy,
            TombstoneReason = tombstoneReason,
            TombstonedAt = tombstonedAt,
        };
    }

    public static MemoryRecord Seed(
        this FakeMemoryStore store,
        string content,
        float[] vector,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        MemoryActor? actor = null,
        DateTimeOffset? createdAt = null,
        IReadOnlyList<string>? tags = null,
        MemoryStatus status = MemoryStatus.Active,
        double importance = 0.5,
        DateTimeOffset? expiresAt = null,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null,
        string tome = TomeKey.DefaultTome)
    {
        var record = Create(
            content,
            scope,
            kind,
            actor,
            createdAt,
            tags,
            status,
            importance,
            expiresAt,
            supersededBy,
            tombstoneReason,
            tombstonedAt,
            tome);
        store.Seed(record, vector);
        return record;
    }
}
