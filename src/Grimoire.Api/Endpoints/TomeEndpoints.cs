using System.Net;
using Grimoire.Api.Contracts;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class TomeEndpoints
{
    public static void MapTomesEndpoints(this WebApplication app, MemoryService memories, ApiTomeResolver tomes)
    {
        app.MapGet("/api/tomes", async (CancellationToken cancellationToken) =>
        {
            var own = await tomes.ResolveAsync(null, cancellationToken);
            var stats = await memories.StatsAsync(cancellationToken);
            var summaries = new List<TomeSummaryResponse>(stats.ByTome.Count);
            foreach (var entry in stats.ByTome)
            {
                if (!stats.LastWriteByTome.TryGetValue(entry.Key, out var lastWrite))
                {
                    return ApiResponses.Error(
                        HttpStatusCode.InternalServerError,
                        "tomes_projection_mismatch",
                        $"No last-write census entry for tome '{entry.Key}'.");
                }

                summaries.Add(new TomeSummaryResponse(
                    entry.Key,
                    entry.Value.Active,
                    entry.Value.Tombstoned,
                    lastWrite,
                    entry.Key == own));
            }

            return Results.Ok(new TomesResponse(own, summaries));
        });
    }
}
