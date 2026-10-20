using Grimoire.Api.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class VersionEndpoints
{
    public static void MapVersionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/version", () => Results.Ok(new VersionResponse(VersionText())));
    }

    private static string VersionText() =>
        typeof(VersionEndpoints).Assembly.GetName().Version is { } version ? version.ToString(3) : "0.1.0";
}
