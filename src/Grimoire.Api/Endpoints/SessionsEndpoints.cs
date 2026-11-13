using Grimoire.Api.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class SessionsEndpoints
{
    public static void MapSessionsEndpoints(this WebApplication app, SessionHandshakeProjection sessions)
    {
        app.MapGet("/api/sessions", async (int? limit, double? windowHours, CancellationToken cancellationToken) =>
        {
            var response = await sessions.ProjectAsync(limit, windowHours, cancellationToken);
            return Results.Ok(response);
        });
    }
}
