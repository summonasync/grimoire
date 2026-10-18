using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class PersonaEndpoints
{
    public static void MapPersonaEndpoints(this WebApplication app, PersonaService persona)
    {
        app.MapGet("/api/persona", async (CancellationToken cancellationToken) =>
            Results.Ok(await persona.GetAsync(cancellationToken)));

        app.MapPut("/api/persona", async (PersonaDocument document, CancellationToken cancellationToken) =>
            Results.Ok(await persona.SetAsync(document, MemoryActor.Human, cancellationToken)));
    }
}
