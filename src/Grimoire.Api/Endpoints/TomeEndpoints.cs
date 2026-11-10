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
        ApiTomeResolver tomes,
        TomeManifestService manifests,
        TomePurgeService purges,
        IClock clock)
    {
        app.MapGet("/api/tomes", async (CancellationToken cancellationToken) =>
        {
            var own = await tomes.ResolveAsync(null, cancellationToken);
            var stats = await memories.StatsAsync(cancellationToken);
            var held = (await manifests.ListAsync(cancellationToken))
                .ToDictionary(manifest => manifest.Key, manifest => manifest);
            var summaries = new List<TomeSummaryResponse>(stats.ByTome.Count + held.Count);

            foreach (var key in stats.ByTome.Keys.Concat(held.Keys).Distinct().OrderBy(key => key, StringComparer.Ordinal))
            {
                DateTimeOffset? lastWrite = null;
                long active = 0;
                long tombstoned = 0;

                if (stats.ByTome.TryGetValue(key, out var census))
                {
                    if (!stats.LastWriteByTome.TryGetValue(key, out var censusWrite))
                    {
                        return ApiResponses.Error(
                            HttpStatusCode.InternalServerError,
                            "tomes_projection_mismatch",
                            $"No last-write census entry for tome '{key}'.");
                    }

                    active = census.Active;
                    tombstoned = census.Tombstoned;
                    lastWrite = censusWrite;
                }

                held.TryGetValue(key, out var manifest);
                summaries.Add(new TomeSummaryResponse(
                    key,
                    active,
                    tombstoned,
                    lastWrite,
                    manifest?.Description,
                    manifest?.CreatedBy,
                    manifest is not null,
                    key == own));
            }

            return Results.Ok(new TomesResponse(own, summaries));
        });

        app.MapPost("/api/tomes", async (CreateTomeManifestRequest request, CancellationToken cancellationToken) =>
        {
            if (!TomeKey.TryNormalize(request.Key, out var key))
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_tome",
                    TomeParam.InvalidMessage("key", request.Key));
            }

            if (ManifestParam.DescriptionError(request.Description) is { } descriptionError)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_description", descriptionError);
            }

            var result = await manifests.CreateAsync(key, request.Description, MemoryActor.Human, cancellationToken);
            return result.Outcome == TomeManifestOutcome.Created
                ? Results.Ok(ManifestPayload(result.Manifest!))
                : ApiResponses.Error(
                    HttpStatusCode.Conflict,
                    "tome_manifest_exists",
                    $"Tome '{key}' already has a manifest; patch its description instead of recreating it.");
        });

        app.MapPatch("/api/tomes/{key}/manifest", async (string key, PatchTomeManifestRequest request, CancellationToken cancellationToken) =>
        {
            if (!TomeKey.TryNormalize(key, out var target))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", key));
            }

            if (ManifestParam.DescriptionError(request.Description) is { } descriptionError)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_description", descriptionError);
            }

            var result = await manifests.UpdateAsync(target, request.Description, MemoryActor.Human, cancellationToken);
            if (result.Outcome == TomeManifestOutcome.Updated)
            {
                return Results.Ok(ManifestPayload(result.Manifest!));
            }

            if (manifests.DegradationReason is { } reason)
            {
                return ApiResponses.Error(HttpStatusCode.ServiceUnavailable, "upstream_unavailable", reason);
            }

            return ApiResponses.Error(
                HttpStatusCode.NotFound,
                "tome_manifest_not_found",
                $"Tome '{target}' has no manifest to patch; create one first.");
        });

        app.MapDelete("/api/tomes/{key}", async (string key, string? confirm, CancellationToken cancellationToken) =>
        {
            if (!TomeKey.TryNormalize(key, out var target))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", key));
            }

            if (confirm != target)
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "confirm_mismatch",
                    $"confirm must equal the tome key exactly to purge it; expected '{target}' but got '{confirm ?? "nothing"}'.");
            }

            var manifestHeld = await manifests.GetAsync(target, cancellationToken);
            var recordCount = await store.CountAsync(new MemoryFilter { Tome = target }, cancellationToken);
            if (manifestHeld is null && recordCount == 0)
            {
                if (manifests.DegradationReason is { } reason)
                {
                    return ApiResponses.Error(HttpStatusCode.ServiceUnavailable, "upstream_unavailable", reason);
                }

                return ApiResponses.Error(
                    HttpStatusCode.NotFound,
                    "not_found",
                    $"Tome '{target}' has no manifest and no records; there is nothing to purge.");
            }

            var result = await purges.PurgeTomeAsync(target, MemoryActor.Human, cancellationToken);
            return Results.Ok(new TomePurgeResponse(
                target,
                new TomePurgeCounts(result.Records.Active, result.Records.Tombstoned, result.Records.Superseded),
                result.ManifestPurged));
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

            if (await manifests.GetAsync(target, cancellationToken) is not null)
            {
                return ApiResponses.Error(
                    HttpStatusCode.Conflict,
                    "tome_manifest_exists",
                    $"Tome '{target}' already has a manifest; renaming would collide. Merge into it or pick an unused tome.");
            }

            var actor = await tomes.ResolveActorAsync(cancellationToken);
            var result = await memories.RetomeAsync(new MemoryFilter { Tome = source }, target, cancellationToken, actor);
            var manifestMoved = await manifests.MoveAsync(source, target, cancellationToken);
            return Results.Ok(new TomeRetomeResponse(source, target, result.Retomed, result.Skipped, ManifestMoved: manifestMoved));
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
            var manifestDeleted = await manifests.DeleteAsync(source, cancellationToken);
            return Results.Ok(new TomeRetomeResponse(source, target, result.Retomed, result.Skipped, ManifestDeleted: manifestDeleted));
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

    private static TomeManifestResponse ManifestPayload(TomeManifest manifest) =>
        new(manifest.Key, manifest.Description, manifest.CreatedBy, manifest.CreatedAt, manifest.UpdatedAt);
}
