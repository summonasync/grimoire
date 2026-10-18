using Grimoire.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

internal static class MemoryFilterTranslator
{
    internal static Filter Translate(MemoryFilter filter)
    {
        var translated = new Filter();
        translated.Must.Add(Conditions.MatchKeyword(
            MemoryPayloadFields.Status,
            (filter.Status ?? MemoryStatus.Active).ToString()));

        if (filter.Scopes.Count > 0)
        {
            translated.Must.Add(Conditions.Match(MemoryPayloadFields.Scope, ScopeValues(filter.Scopes)));
        }

        if (filter.Kind is { } kind)
        {
            translated.Must.Add(Conditions.MatchKeyword(MemoryPayloadFields.Kind, kind.ToString()));
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

        return translated;
    }

    private static string[] ScopeValues(IReadOnlyList<MemoryScope> scopes) =>
        [.. scopes.Select(scope => scope.ToString())];
}
