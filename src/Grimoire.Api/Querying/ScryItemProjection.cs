using System.Diagnostics;
using Grimoire.Api.Contracts;
using Grimoire.Core;

namespace Grimoire.Api.Querying;

public static class ScryItemProjection
{
    public static readonly ReadProjectionSurface Surface = new()
    {
        AllowedKeys =
        [
            "content", "tome", "scope", "kind", "actor", "tags", "matchedTerms",
            "projectId", "similarity", "createdAt",
        ],
        AlwaysOnKeys = ["memoryId", "score"],
        CompactKeys = [],
    };

    public static IReadOnlyDictionary<string, object?> Project(ScryItem item, IReadOnlyList<string> keys)
    {
        var projected = new Dictionary<string, object?>(keys.Count, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            projected[key] = key switch
            {
                "memoryId" => item.MemoryId,
                "content" => item.Content,
                "tome" => item.Tome,
                "scope" => item.Scope,
                "kind" => item.Kind,
                "actor" => item.Actor,
                "tags" => item.Tags,
                "matchedTerms" => item.MatchedTerms,
                "projectId" => item.ProjectId,
                "similarity" => item.Similarity,
                "score" => item.Score,
                "createdAt" => item.CreatedAt,
                _ => throw new UnreachableException($"key '{key}' passed validation but has no projection."),
            };
        }

        return projected;
    }
}
