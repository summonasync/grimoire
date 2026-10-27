using System.Diagnostics;
using Grimoire.Core;

namespace Grimoire.Api.Querying;

public static class MemoryRecordProjection
{
    public static readonly ReadProjectionSurface Surface = new()
    {
        AllowedKeys =
        [
            "content", "scope", "kind", "actor", "tome", "projectId", "sessionId", "tags",
            "importance", "createdAt", "lastAccessedAt", "accessCount", "expiresAt", "status",
            "supersededBy", "tombstoneReason", "tombstonedAt", "sources", "corrections",
        ],
        AlwaysOnKeys = ["id"],
        CompactKeys = [],
    };

    public static IReadOnlyDictionary<string, object?> Project(MemoryRecord record, IReadOnlyList<string> keys)
    {
        var projected = new Dictionary<string, object?>(keys.Count, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            projected[key] = key switch
            {
                "id" => record.Id,
                "content" => record.Content,
                "scope" => record.Scope,
                "kind" => record.Kind,
                "actor" => record.Actor,
                "tome" => record.Tome,
                "projectId" => record.ProjectId,
                "sessionId" => record.SessionId,
                "tags" => record.Tags,
                "importance" => record.Importance,
                "createdAt" => record.CreatedAt,
                "lastAccessedAt" => record.LastAccessedAt,
                "accessCount" => record.AccessCount,
                "expiresAt" => record.ExpiresAt,
                "status" => record.Status,
                "supersededBy" => record.SupersededBy,
                "tombstoneReason" => record.TombstoneReason,
                "tombstonedAt" => record.TombstonedAt,
                "sources" => record.Sources,
                "corrections" => record.Corrections,
                _ => throw new UnreachableException($"key '{key}' passed validation but has no projection."),
            };
        }

        return projected;
    }
}
