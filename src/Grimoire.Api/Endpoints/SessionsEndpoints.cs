using System.Net;
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
            if (SessionsQueryBounds.Rejection(limit, windowHours) is { } rejection)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_request", rejection);
            }

            var response = await sessions.ProjectAsync(limit, windowHours, cancellationToken);
            return Results.Ok(response);
        });
    }
}
