namespace Grimoire.Core;

public sealed class CorrectionService(IMemoryStore store, IClock clock, MemoryEngineOptions? options = null)
{
    private const int PreviewCharLimit = 120;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<RestoreResult> RestoreAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken,
        string? tome = null,
        MemoryActor? actor = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A restore reason is required.", nameof(reason));
        }

        var resolved = ResolveTome(tome);
        var record = await store.GetAsync(id, cancellationToken);
        if (record is null || record.Tome != resolved)
        {
            return new RestoreResult { Id = id };
        }

        if (record.Status == MemoryStatus.Active)
        {
            return new RestoreResult { Id = id, Record = record };
        }

        var now = clock.UtcNow;
        var by = actor ?? MemoryActor.Human;
        var restored = CorrectionTrail.Append(record with
        {
            Status = MemoryStatus.Active,
            TombstoneReason = null,
            TombstonedAt = null,
        }, now, by, CorrectionAction.Restore, reason);

        var superseder = record.SupersededBy is { } successorId
            ? await TombstoneSuccessorAsync(successorId, record.Id, now, by, cancellationToken)
            : null;

        await store.UpdateAsync(restored, cancellationToken);

        return new RestoreResult
        {
            Id = id,
            Restored = true,
            Record = restored,
            Superseder = superseder is null ? null : new SupersederState
            {
                Id = superseder.Id,
                Status = superseder.Status,
                TombstoneReason = superseder.TombstoneReason,
            },
        };
    }

    public async Task<HistoryResult> HistoryAsync(Guid id, CancellationToken cancellationToken, string? tome = null)
    {
        var records = await store.ListAsync(
            new MemoryFilter { Tome = ResolveTome(tome), Statuses = MemoryStatuses.All },
            cancellationToken);
        var byId = records.ToDictionary(record => record.Id);

        if (!byId.TryGetValue(id, out var target))
        {
            return new HistoryResult { Id = id };
        }

        var chainIds = CollectChain(target, byId);
        var chain = chainIds
            .Select(chainId => byId[chainId])
            .OrderBy(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .Select(ToHistoryEntry)
            .ToArray();

        return new HistoryResult { Id = id, Found = true, Chain = chain };
    }

    private string ResolveTome(string? tome) => TomeKey.Normalize(tome ?? _options.DefaultTome);

    private async Task<MemoryRecord?> TombstoneSuccessorAsync(
        Guid successorId,
        Guid restoredId,
        DateTimeOffset now,
        MemoryActor actor,
        CancellationToken cancellationToken)
    {
        var successor = await store.GetAsync(successorId, cancellationToken);
        if (successor is null || successor.Status == MemoryStatus.Tombstoned)
        {
            return successor;
        }

        var reason = $"restored {restoredId}";
        var tombstoned = CorrectionTrail.Append(successor with
        {
            Status = MemoryStatus.Tombstoned,
            TombstoneReason = reason,
            TombstonedAt = now,
        }, now, actor, CorrectionAction.Supersede, reason);
        await store.UpdateAsync(tombstoned, cancellationToken);
        return tombstoned;
    }

    private static HashSet<Guid> CollectChain(MemoryRecord target, IReadOnlyDictionary<Guid, MemoryRecord> byId)
    {
        var chain = new HashSet<Guid> { target.Id };

        var cursor = target;
        while (cursor.SupersededBy is { } successorId
               && !chain.Contains(successorId)
               && byId.TryGetValue(successorId, out var successor))
        {
            chain.Add(successorId);
            cursor = successor;
        }

        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var candidate in byId.Values)
            {
                if (candidate.SupersededBy is { } link && chain.Contains(link) && chain.Add(candidate.Id))
                {
                    grew = true;
                }
            }
        }

        return chain;
    }

    private static HistoryEntry ToHistoryEntry(MemoryRecord record)
    {
        var firstLine = record.Content.Split('\n')[0].TrimEnd('\r');

        return new HistoryEntry
        {
            Id = record.Id,
            Status = record.Status,
            Tome = record.Tome,
            CreatedAt = record.CreatedAt,
            TombstonedAt = record.TombstonedAt,
            TombstoneReason = record.TombstoneReason,
            Actor = record.Actor,
            BodyPreview = firstLine.Length <= PreviewCharLimit ? firstLine : firstLine[..PreviewCharLimit],
        };
    }
}
