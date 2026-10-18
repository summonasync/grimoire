using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class StatsEndpoints
{
    public static void MapStatsEndpoints(this WebApplication app, MemoryService memories)
    {
        app.MapGet("/api/stats", async (CancellationToken cancellationToken) =>
            Results.Ok(await memories.StatsAsync(cancellationToken)));
    }
}
