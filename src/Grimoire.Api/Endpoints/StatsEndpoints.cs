using System.Net;
using Grimoire.Api.Contracts;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class StatsEndpoints
{
    public static void MapStatsEndpoints(this WebApplication app, MemoryService memories, ApiTomeResolver tomes)
    {
        app.MapGet("/api/stats", async (string? tome, bool includeTomeCompaction = false, CancellationToken cancellationToken = default) =>
        {
            if (tome is { } candidate && !TomeKey.TryNormalize(candidate, out _))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", candidate));
            }

            var stats = await memories.StatsAsync(cancellationToken);
            var byTome = new Dictionary<string, TomeStatsResponse>(stats.ByTome.Count);
            foreach (var entry in stats.ByTome)
            {
                // Fail as a structured error, not a raw KeyNotFoundException: the API face has no
                // exception handler, and a fabricated epoch here would be fake telemetry.
                if (!stats.LastWriteByTome.TryGetValue(entry.Key, out var lastWrite))
                {
                    return ApiResponses.Error(
                        HttpStatusCode.InternalServerError,
                        "stats_projection_mismatch",
                        $"No last-write census entry for tome '{entry.Key}'.");
                }

                byTome[entry.Key] = new TomeStatsResponse(entry.Value.Active, entry.Value.Tombstoned, lastWrite);
            }

            return Results.Ok(new StatsResponse(
                await tomes.ResolveAsync(tome, cancellationToken),
                stats.Active,
                stats.Superseded,
                stats.Tombstoned,
                stats.ActiveByScope,
                stats.PendingCompaction,
                stats.IdempotencyConflicts,
                byTome,
                stats.Gc)
            {
                PendingCompactionByTome = includeTomeCompaction
                    ? await memories.PendingCompactionByTomeAsync(cancellationToken)
                    : null,
            });
        });
    }
}
