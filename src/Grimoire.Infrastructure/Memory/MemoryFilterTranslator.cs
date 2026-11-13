using Grimoire.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

internal static class MemoryFilterTranslator
{
    internal static Filter Translate(MemoryFilter filter)
    {
        var translated = new Filter();
        // Which statuses are meant comes from Core; which wire shape they take stays here, so an
        // explicit one-element list keeps rendering as any-of and an omitted clause keeps
        // rendering as the single Active keyword. Same bytes, one home for the meaning.
        var statuses = MemoryStatuses.Effective(filter);
        if (filter.Statuses.Count > 0)
        {
            translated.Must.Add(StatusAnyCondition(statuses));
        }
        else
        {
            translated.Must.Add(Conditions.MatchKeyword(
                MemoryPayloadFields.Status,
                statuses[0].ToString()));
        }

        if (filter.Scopes.Count > 0)
        {
            translated.Must.Add(Conditions.Match(MemoryPayloadFields.Scope, ScopeValues(filter.Scopes)));
        }

        if (filter.Kind is { } kind)
        {
            translated.Must.Add(Conditions.MatchKeyword(MemoryPayloadFields.Kind, kind.ToString()));
        }

        if (filter.Tome is { } tome)
        {
            translated.Must.Add(TomeCondition(tome));
        }

        if (filter.Tomes.Count > 0)
        {
            translated.Must.Add(TomeAnyCondition(filter.Tomes));
        }

        if (filter.Tags.Count > 0)
        {
            translated.Must.Add(Conditions.Match(MemoryPayloadFields.Tags, [.. filter.Tags]));
        }

        if (filter.ExcludeTags.Count > 0)
        {
            translated.MustNot.Add(Conditions.Match(MemoryPayloadFields.Tags, [.. filter.ExcludeTags]));
        }

        if (filter.ProjectId is { } projectId)
        {
            translated.Must.Add(Conditions.MatchKeyword(MemoryPayloadFields.ProjectId, projectId));
        }

        if (filter.SessionId is { } sessionId)
        {
            translated.Must.Add(Conditions.MatchKeyword(MemoryPayloadFields.SessionId, sessionId));
        }

        if (filter.IdempotencyKey is { } idempotencyKey)
        {
            translated.Must.Add(Conditions.MatchKeyword(MemoryPayloadFields.IdempotencyKey, idempotencyKey));
        }

        if (filter.ExpiresBefore is { } expiresBefore)
        {
            translated.Must.Add(Conditions.DatetimeRange(
                MemoryPayloadFields.ExpiresAt,
                lte: expiresBefore.UtcDateTime));
        }

        if (filter.TombstonedBefore is { } tombstonedBefore)
        {
            translated.Must.Add(Conditions.DatetimeRange(
                MemoryPayloadFields.TombstonedAt,
                lte: tombstonedBefore.UtcDateTime));
        }

        if (filter.CreatedAfter is not null || filter.CreatedBefore is not null)
        {
            // The window is half-open by contract: MemoryFilterExtensions excludes the record sitting
            // exactly on CreatedBefore, so the upper bound is lt. The lte shape of the two branches
            // above would pull that record back inside the window.
            translated.Must.Add(Conditions.DatetimeRange(
                MemoryPayloadFields.CreatedAt,
                gte: filter.CreatedAfter?.UtcDateTime,
                lt: filter.CreatedBefore?.UtcDateTime));
        }

        return translated;
    }

    // Points written before the tome field existed carry no tome payload at all; they belong
    // to the default tome, so a default-tome filter must also accept the absent field instead
    // of hiding every pre-tome point on real Qdrant.
    private static Condition TomeCondition(string tome)
    {
        if (tome != TomeKey.DefaultTome)
        {
            return Conditions.MatchKeyword(MemoryPayloadFields.Tome, tome);
        }

        var legacyOrDefault = new Filter();
        legacyOrDefault.Should.Add(Conditions.MatchKeyword(MemoryPayloadFields.Tome, TomeKey.DefaultTome));
        legacyOrDefault.Should.Add(new Condition { IsEmpty = new IsEmptyCondition { Key = MemoryPayloadFields.Tome } });
        return new Condition { Filter = legacyOrDefault };
    }

    private static Condition TomeAnyCondition(IReadOnlyList<string> tomes)
    {
        var anyOf = new Filter();
        foreach (var tome in tomes)
        {
            anyOf.Should.Add(Conditions.MatchKeyword(MemoryPayloadFields.Tome, tome));
            if (tome == TomeKey.DefaultTome)
            {
                anyOf.Should.Add(new Condition { IsEmpty = new IsEmptyCondition { Key = MemoryPayloadFields.Tome } });
            }
        }

        return new Condition { Filter = anyOf };
    }

    private static Condition StatusAnyCondition(IReadOnlyList<MemoryStatus> statuses)
    {
        var anyOf = new Filter();
        foreach (var status in statuses)
        {
            anyOf.Should.Add(Conditions.MatchKeyword(MemoryPayloadFields.Status, status.ToString()));
        }

        return new Condition { Filter = anyOf };
    }

    private static string[] ScopeValues(IReadOnlyList<MemoryScope> scopes) =>
        [.. scopes.Select(scope => scope.ToString())];
}
