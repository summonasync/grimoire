using System.Net;
using Grimoire.Api.Contracts;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class PersonaEndpoints
{
    public static void MapPersonaEndpoints(this WebApplication app, PersonaService persona, ApiTomeResolver tomes)
    {
        app.MapGet("/api/persona", async (string? tome, CancellationToken cancellationToken) =>
        {
            if (TomeParamGuard(tome) is { } error)
            {
                return error;
            }

            return Results.Ok(await persona.GetAsync(cancellationToken, await tomes.ResolveAsync(tome, cancellationToken)));
        });

        app.MapPut("/api/persona", async (PersonaDocument document, string? tome, CancellationToken cancellationToken) =>
        {
            if (TomeParamGuard(tome) is { } error)
            {
                return error;
            }

            var resolved = await tomes.ResolveAsync(tome, cancellationToken);
            var saved = await persona.SetAsync(document, MemoryActor.Human, cancellationToken, resolved);
            return Results.Ok(new PersonaWriteResponse(resolved, saved));
        });
    }

    private static IResult? TomeParamGuard(string? tome) => tome is { } candidate && !TomeKey.TryNormalize(candidate, out _)
        ? ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", candidate))
        : null;
}
