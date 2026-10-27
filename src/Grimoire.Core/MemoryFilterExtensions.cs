namespace Grimoire.Core;

public static class MemoryFilterExtensions
{
    public static bool Matches(this MemoryFilter filter, MemoryRecord record)
    {
        if (filter.Scopes.Count > 0 && !filter.Scopes.Contains(record.Scope))
        {
            return false;
        }

        if (filter.Kind is not null && record.Kind != filter.Kind)
        {
            return false;
        }

        if (filter.Status is not null && record.Status != filter.Status)
        {
            return false;
        }

        if (filter.Statuses.Count > 0 && !filter.Statuses.Contains(record.Status))
        {
            return false;
        }

        if (filter.Tags.Count > 0 && !filter.Tags.Any(tag => record.Tags.Contains(tag)))
        {
            return false;
        }

        if (filter.ExcludeTags.Count > 0 && filter.ExcludeTags.Any(tag => record.Tags.Contains(tag)))
        {
            return false;
        }

        if (filter.Tome is not null && record.Tome != filter.Tome)
        {
            return false;
        }

        if (filter.Tomes.Count > 0 && !filter.Tomes.Contains(record.Tome))
        {
            return false;
        }

        if (filter.ProjectId is not null && record.ProjectId != filter.ProjectId)
        {
            return false;
        }

        if (filter.SessionId is not null && record.SessionId != filter.SessionId)
        {
            return false;
        }

        if (filter.ExpiresBefore is not null && (record.ExpiresAt is null || record.ExpiresAt > filter.ExpiresBefore))
        {
            return false;
        }

        if (filter.TombstonedBefore is not null && (record.TombstonedAt is null || record.TombstonedAt > filter.TombstonedBefore))
        {
            return false;
        }

        if (filter.IdempotencyKey is not null && record.IdempotencyKey != filter.IdempotencyKey)
        {
            return false;
        }

        if (filter.CreatedAfter is not null && record.CreatedAt < filter.CreatedAfter)
        {
            return false;
        }

        if (filter.CreatedBefore is not null && record.CreatedAt >= filter.CreatedBefore)
        {
            return false;
        }

        return MatchesActor(filter.Actor, record.Actor);
    }

    private static bool MatchesActor(string? candidate, MemoryActor actor)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return true;
        }

        if (candidate.Contains('+'))
        {
            return actor.ToString().Equals(candidate, StringComparison.OrdinalIgnoreCase);
        }

        return actor.Alias.Equals(MemoryActor.AliasPart(candidate), StringComparison.OrdinalIgnoreCase);
    }
}
