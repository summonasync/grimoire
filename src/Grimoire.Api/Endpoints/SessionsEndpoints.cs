using System.Net;
using Grimoire.Api.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class SessionsEndpoints
{
    // The two numeric knobs arrive as text and are parsed in this repository, never by the framework:
    // NumberStyles.Float under CultureInfo.InvariantCulture — sign, '.' fraction and 'e' exponent bind,
    // a comma used as a group separator never does.
    public static void MapSessionsEndpoints(this WebApplication app, SessionHandshakeProjection sessions)
    {
        app.MapGet("/api/sessions", async (string? limit, string? windowHours, CancellationToken cancellationToken) =>
        {
            var rows = SessionsQueryValues.Limit(limit);
            var hours = SessionsQueryValues.WindowHours(windowHours);
            if ((rows.Rejection ?? hours.Rejection ?? SessionsQueryBounds.Rejection(rows.Value, hours.Value)) is { } rejection)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_request", rejection);
            }

            var response = await sessions.ProjectAsync(rows.Value, hours.Value, cancellationToken);
            return Results.Ok(response);
        });
    }
}
