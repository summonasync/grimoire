using Grimoire.Api.Agents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app, AgentActivityProjection agents)
    {
        app.MapGet("/api/agents", async (int? windowDays, CancellationToken cancellationToken) =>
        {
            var activity = await agents.ProjectAsync(windowDays, cancellationToken);
            return Results.Ok(activity);
        });
    }
}
