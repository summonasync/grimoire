using System.Net;
using Grimoire.Api.Contracts;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class TomeEndpoints
{
    public static void MapTomesEndpoints(
        this WebApplication app,
        MemoryService memories,
        IMemoryStore store,
        ApiTomeResolver tomes)
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

        app.MapPost("/api/tomes/records/retome", async (RetomeRecordsRequest request, CancellationToken cancellationToken) =>
        {
            if (request.Ids.Count == 0)
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_retome",
                    "A retome needs at least one memory id.");
            }

            if (!TomeKey.TryNormalize(request.To, out var target))
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_tome",
                    TomeParam.InvalidMessage("to", request.To));
            }

            var unknown = new List<Guid>();
            foreach (var id in request.Ids)
            {
                if (await store.GetAsync(id, cancellationToken) is null)
                {
                    unknown.Add(id);
                }
            }

            if (unknown.Count > 0)
            {
                var listed = string.Join(", ", unknown.Select(id => $"'{id}'"));
                return ApiResponses.Error(
                    HttpStatusCode.NotFound,
                    "not_found",
                    $"{(unknown.Count == 1 ? "Memory" : "Memories")} {listed} do{(unknown.Count == 1 ? "es" : "")} not exist.");
            }

            var actor = await tomes.ResolveActorAsync(cancellationToken);
            var result = await memories.RetomeAsync(request.Ids, target, cancellationToken, actor);
            return Results.Ok(new RetomeResponse(result.Retomed, target, result.Skipped));
        });

        app.MapPost("/api/tomes/{key}/rename", async (string key, RenameTomeRequest request, CancellationToken cancellationToken) =>
        {
            if (!TomeKey.TryNormalize(key, out var source))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", key));
            }

            if (!TomeKey.TryNormalize(request.To, out var target))
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_tome",
                    TomeParam.InvalidMessage("to", request.To));
            }

            if (await store.CountAsync(new MemoryFilter { Tome = target }, cancellationToken) > 0)
            {
                return ApiResponses.Error(
                    HttpStatusCode.Conflict,
                    "tome_exists",
                    $"Tome '{target}' already has records; renaming would mix histories. Merge into it or pick an unused tome.");
            }

            var actor = await tomes.ResolveActorAsync(cancellationToken);
            var result = await memories.RetomeAsync(new MemoryFilter { Tome = source }, target, cancellationToken, actor);
            return Results.Ok(new TomeRetomeResponse(source, target, result.Retomed, result.Skipped));
        });

        app.MapPost("/api/tomes/{key}/merge", async (string key, MergeTomeRequest request, CancellationToken cancellationToken) =>
        {
            if (!TomeKey.TryNormalize(key, out var source))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", key));
            }

            if (!TomeKey.TryNormalize(request.Into, out var target))
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_tome",
                    TomeParam.InvalidMessage("into", request.Into));
            }

            if (await store.CountAsync(new MemoryFilter { Tome = target }, cancellationToken) == 0)
            {
                return ApiResponses.Error(
                    HttpStatusCode.NotFound,
                    "not_found",
                    $"Tome '{target}' has no records; there is nothing to merge into. Rename to it instead.");
            }

            var actor = await tomes.ResolveActorAsync(cancellationToken);
            var result = await memories.RetomeAsync(new MemoryFilter { Tome = source }, target, cancellationToken, actor);
            return Results.Ok(new TomeRetomeResponse(source, target, result.Retomed, result.Skipped));
        });

        app.MapPost("/api/tomes/{key}/forget", async (string key, ForgetTomeRequest request, CancellationToken cancellationToken) =>
        {
            if (!TomeKey.TryNormalize(key, out var source))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", key));
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_reason", "A tombstone reason is required.");
            }

            var actor = await tomes.ResolveActorAsync(cancellationToken);
            var result = await memories.ForgetTomeAsync(source, request.Reason, cancellationToken, actor);
            return Results.Ok(new TomeForgetResponse(source, result.Forgotten, result.Skipped));
        });
    }
}
