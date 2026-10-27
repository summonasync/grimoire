using System.Net;
using Grimoire.Api.Contracts;
using Grimoire.Api.Querying;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class ScryEndpoints
{
    public static void MapScryEndpoints(this WebApplication app, ScryQueryRunner scry)
    {
        app.MapGet("/scry", async ([AsParameters] ScryQuery query, CancellationToken cancellationToken) =>
        {
            ParsedScryQuery parsed;
            try
            {
                parsed = ScryQueryParser.Parse(query);
            }
            catch (MemoryQueryException ex)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, ex.Code, ex.Message);
            }

            var response = await scry.RunAsync(parsed, cancellationToken);
            return Results.Ok(parsed.ProjectedKeys is { } keys
                ? new ProjectedScryResponse(
                    response.Tomes,
                    response.Attribution,
                    response.Count,
                    [.. response.Items.Select(item => ScryItemProjection.Project(item, keys))],
                    response.ElapsedMs)
                : response);
        });
    }
}
