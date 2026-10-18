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
        app.MapGet("/api/stats", async (string? tome, CancellationToken cancellationToken) =>
        {
            if (tome is { } candidate && !TomeKey.TryNormalize(candidate, out _))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", candidate));
            }

            var stats = await memories.StatsAsync(cancellationToken);
            return Results.Ok(new StatsResponse(
                await tomes.ResolveAsync(tome, cancellationToken),
                stats.Active,
                stats.Superseded,
                stats.Tombstoned,
                stats.ActiveByScope,
                stats.PendingCompaction,
                stats.IdempotencyConflicts,
                stats.ByTome));
        });
    }
}
