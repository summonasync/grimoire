using System.ComponentModel;
using System.Diagnostics;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class DigestTool(DigestService digests, IMemoryStore store, MemoryActorProvider actors)
{
    public const string Name = "memory_digest";

    public const string Title = "Digest a topic";

    public const string Description =
        "Pull everything one tome knows about a topic in a single read - a topic-centric digest from newest " +
        "down to tombstones. Sections: longMemo (consolidated Facts), recent (fresh Journal entries) and gone " +
        "(superseded and tombstoned records, the anti-resurrection tail). Topic resolution is deterministic: " +
        "records tagged topic:<value> union records embedding-similar (>= 0.75) to the topic string; every hit " +
        "carries match: \"tag\" or \"similar\" and the union deduplicates with tag winning. Sections fill " +
        "newest-first under char budgets, long entries are truncated and flagged, and sections that do not fit " +
        "are named in omitted. Digest hits bump access stats like recall does. Compact projection is the " +
        "default; pass fields to project hits to specific keys instead.";

    private static readonly ReadProjectionSurface Surface = new()
    {
        AllowedKeys =
        [
            "content", "scope", "kind", "tags", "createdAt", "similarity", "tombstoneReason", "truncated", "lastAccessedAt",
        ],
        AlwaysOnKeys = ["memoryId", "match"],
        CompactKeys = ["memoryId", "content", "tags", "createdAt"],
    };

    public Task<CallToolResult> Digest(
        [Description("What to look for - free text; normalized to topic:<value>, tag hits and embedding-similar records are united.")]
        string topic,
        [Description("Tome to digest; omit for your resolved tome. The response echoes the tome actually searched.")]
        string? tome = null,
        [Description("Only recent entries created at or after this; yyyy-MM-dd means local midnight, an ISO-8601 instant is used as-is.")]
        string? since = null,
        [Description("Only recent entries created before this; exclusive; yyyy-MM-dd means local midnight, an ISO-8601 instant is used as-is.")]
        string? until = null,
        [Description("Per-section cap for recent and gone, default 10, clamped server-side.")]
        int? limit = null,
        [Description("Project each hit to these camelCase keys, e.g. [\"content\", \"match\"]; memoryId and match are always included and an unknown field is rejected naming the allowed set. Takes precedence over the default compact projection.")]
        string[]? fields = null,
        [Description("Project each hit to the digest shape (memoryId, match, content, tags, createdAt) - on by default; pass fields to override.")]
        bool compact = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "topic must not be empty or whitespace.",
                nameof(topic))));
        }

        if (limit <= 0)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "limit must be a positive integer.",
                nameof(limit))));
        }

        var (range, rangeError) = DateParams.Parse(since, until);
        if (rangeError is not null)
        {
            return Task.FromResult(ToolResults.Failure(rangeError));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        IReadOnlyList<string>? projectedKeys;
        try
        {
            projectedKeys = fields is { Length: > 0 }
                ? ReadProjection.Resolve(fields, compact: false, Surface)
                : ReadProjection.Resolve(null, compact, Surface);
        }
        catch (ReadProjectionValidationException projectionError)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                projectionError.Message,
                nameof(fields))));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var result = await digests.DigestAsync(new DigestQuery
            {
                Topic = topic,
                Tome = resolvedTome,
                CreatedAfter = range.Since,
                CreatedBefore = range.Until,
                Limit = limit,
            }, ct);

            var isEmpty = result.LongMemo.Count == 0 && result.Recent.Count == 0 && result.Gone.Count == 0;
            var warnings = await EmptyTomeWarning.BuildAsync(store, isEmpty, tome, resolvedTome, ct);

            if (projectedKeys is null)
            {
                return ToolResults.Success(ToView(result, resolvedTome, warnings));
            }

            return ToolResults.Success(ToProjectedView(result, resolvedTome, projectedKeys, warnings));
        }, cancellationToken);
    }

    private static DigestView ToView(DigestResult result, string tome, IReadOnlyList<string>? warnings)
    {
        return new DigestView
        {
            Tome = tome,
            Topic = result.Topic,
            LongMemo = ToSection(result.LongMemoState, result.LongMemo),
            Recent = ToSection(result.RecentState, result.Recent),
            Gone = ToSection(result.GoneState, result.Gone),
            Omitted = [.. result.Omitted.Select(ToSectionName)],
            Warnings = warnings,
        };
    }

    private static ProjectedDigestView ToProjectedView(
        DigestResult result,
        string tome,
        IReadOnlyList<string> keys,
        IReadOnlyList<string>? warnings)
    {
        return new ProjectedDigestView
        {
            Tome = tome,
            Topic = result.Topic,
            LongMemo = ToProjectedSection(result.LongMemoState, result.LongMemo, keys),
            Recent = ToProjectedSection(result.RecentState, result.Recent, keys),
            Gone = ToProjectedSection(result.GoneState, result.Gone, keys),
            Omitted = [.. result.Omitted.Select(ToSectionName)],
            Warnings = warnings,
        };
    }

    private static BriefItemsSection<DigestHitItem> ToSection(BriefSectionState state, IReadOnlyList<DigestHit> items)
    {
        return new BriefItemsSection<DigestHitItem>
        {
            State = state,
            Items = [.. items.Select(ToHitItem)],
        };
    }

    private static DigestProjectedSection ToProjectedSection(
        BriefSectionState state,
        IReadOnlyList<DigestHit> items,
        IReadOnlyList<string> keys)
    {
        return new DigestProjectedSection
        {
            State = state,
            Items = [.. items.Select(hit => ProjectHit(hit, keys))],
        };
    }

    private static DigestHitItem ToHitItem(DigestHit hit)
    {
        return new DigestHitItem
        {
            MemoryId = hit.Id,
            Match = MatchName(hit.Match),
            Content = hit.Content,
            Truncated = hit.Truncated,
            Similarity = hit.Similarity,
            Scope = hit.Scope,
            Kind = hit.Kind,
            Tags = hit.Tags,
            TombstoneReason = hit.TombstoneReason,
            CreatedAt = hit.CreatedAt,
            LastAccessedAt = hit.LastAccessedAt,
        };
    }

    private static IReadOnlyDictionary<string, object?> ProjectHit(DigestHit hit, IReadOnlyList<string> keys)
    {
        var projected = new Dictionary<string, object?>(keys.Count, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            projected[key] = key switch
            {
                "memoryId" => hit.Id,
                "match" => MatchName(hit.Match),
                "content" => hit.Content,
                "scope" => hit.Scope,
                "kind" => hit.Kind,
                "tags" => hit.Tags,
                "similarity" => hit.Similarity,
                "tombstoneReason" => hit.TombstoneReason,
                "truncated" => hit.Truncated,
                "createdAt" => hit.CreatedAt,
                "lastAccessedAt" => hit.LastAccessedAt,
                _ => throw new UnreachableException($"key '{key}' passed validation but has no projection."),
            };
        }

        return projected;
    }

    private static string MatchName(DigestMatch match) => match switch
    {
        DigestMatch.Tag => "tag",
        DigestMatch.Similar => "similar",
        _ => throw new ArgumentOutOfRangeException(nameof(match), match, "Unknown digest match reason."),
    };

    private static string ToSectionName(DigestSection section) => section switch
    {
        DigestSection.LongMemo => "longMemo",
        DigestSection.Recent => "recent",
        DigestSection.Gone => "gone",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown digest section."),
    };
}
