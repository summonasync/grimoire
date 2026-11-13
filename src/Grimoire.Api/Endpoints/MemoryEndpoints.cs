using System.Net;
using Grimoire.Api.Contracts;
using Grimoire.Api.Editing;
using Grimoire.Api.Querying;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(
        this WebApplication app,
        MemoryService memories,
        MemoryEditor editor,
        CorrectionService corrections,
        MemoryQueryRunner queryRunner,
        IMemoryStore store,
        ApiTomeResolver tomes,
        ProjectTagResolver projectTags,
        ICallerPathProvider callerPaths)
    {
        app.MapGet("/api/memories", async ([AsParameters] MemoryListQuery query, CancellationToken cancellationToken) =>
        {
            ParsedMemoryQuery parsed;
            try
            {
                parsed = MemoryQueryParser.Parse(query);
            }
            catch (MemoryQueryException ex)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, ex.Code, ex.Message);
            }

            var requestedTome = parsed.Filter.Tome;
            var tome = requestedTome ?? await tomes.ResolveAsync(null, cancellationToken);
            var result = await queryRunner.RunAsync(
                query.Query,
                parsed with { Filter = parsed.Filter with { Tome = tome } },
                cancellationToken);
            var records = result.Records;

            var warnings = WarningMerge.Merge(
                result.Warnings,
                await EmptyTomeWarning.BuildAsync(
                    store, records.Count == 0, requestedTome, tome, cancellationToken));

            object response = parsed.ProjectedKeys is { } keys
                ? new ProjectedMemoryListResponse(
                    tome,
                    records.Count,
                    [.. records.Select(record => MemoryRecordProjection.Project(record, keys))])
                { Warnings = warnings }
                : new MemoryListResponse(tome, records.Count, records) { Warnings = warnings };

            return Results.Ok(response);
        });

        app.MapGet("/api/memories/{id:guid}", async (Guid id, CancellationToken cancellationToken) =>
        {
            var record = await store.GetAsync(id, cancellationToken);
            return record is null
                ? ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.")
                : Results.Ok(record);
        });

        app.MapPost("/api/memories", async (CreateMemoryRequest request, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_memory", "Memory content must not be empty.");
            }

            if (request.Scope == MemoryScope.Session && request.ProjectId is not null)
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "param_conflict",
                    $"projectId only applies to scopes 'project', 'global' and 'personal'; omit it or change the scope " +
                    $"(got projectId '{request.ProjectId}' with scope 'session').");
            }

            if (request.Scope != MemoryScope.Session && request.SessionId is not null)
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "param_conflict",
                    $"sessionId only applies to scope 'session'; omit it or change the scope " +
                    $"(got sessionId '{request.SessionId}' with scope '{request.Scope.ToString().ToLowerInvariant()}').");
            }

            var projectTag = projectTags.Resolve(request.Scope, request.ProjectId, callerPaths.Path);
            if (projectTag.Rejection is { } rejection)
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    ProjectTagErrors.Code(rejection),
                    ProjectTagErrors.Message(rejection, projectTag, request.ProjectId, callerPaths.Path));
            }

            if (request.Tome is { } requestedTome && !TomeKey.TryNormalize(requestedTome, out _))
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_tome",
                    TomeParam.InvalidMessage("tome", requestedTome));
            }

            try
            {
                var tome = await tomes.ResolveAsync(request.Tome, cancellationToken);
                var actor = await tomes.ResolveActorAsync(cancellationToken);
                var result = await memories.RememberAsync(new RememberRequest
                {
                    Content = request.Content,
                    Scope = request.Scope,
                    Kind = request.Kind,
                    Actor = actor,
                    Tome = tome,
                    ProjectId = projectTag.ProjectId,
                    SessionId = request.SessionId,
                    Tags = request.Tags,
                    Importance = request.Importance,
                    Sources = request.Sources,
                    Supersedes = request.Supersedes,
                }, cancellationToken);

                return Results.Created(
                    $"/api/memories/{result.Record.Id}",
                    new CreateMemoryResponse(
                        result.Record,
                        tome,
                        result.SupersededId,
                        result.IdempotentReplay,
                        result.MaintenancePending)
                    {
                        NearDuplicates = result.NearMatches.Count == 0
                            ? null
                            : [.. result.NearMatches.Select(match => new NearDuplicateResponse(
                                match.MemoryId,
                                match.Similarity,
                                match.Snippet))],
                    });
            }
            catch (ArgumentException ex)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_memory", ex.Message);
            }
        });

        app.MapPatch("/api/memories/{id:guid}", async (Guid id, PatchMemoryRequest request, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_reason", "A patch reason is required.");
            }

            try
            {
                var actor = await tomes.ResolveActorAsync(cancellationToken);
                var updated = await editor.EditAsync(id, request, actor, cancellationToken);
                return updated is null
                    ? ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.")
                    : Results.Ok(updated);
            }
            catch (MemoryNotEditableException ex)
            {
                return ApiResponses.Error(HttpStatusCode.Conflict, "not_editable", ex.Message);
            }
            catch (ArgumentException ex)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_patch", ex.Message);
            }
        });

        app.MapPost("/api/memories/{id:guid}/forget", async (Guid id, ForgetMemoryRequest request, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_reason", "A tombstone reason is required.");
            }

            if (request.Tome is { } requestedTome && !TomeKey.TryNormalize(requestedTome, out _))
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_tome",
                    TomeParam.InvalidMessage("tome", requestedTome));
            }

            var tome = await tomes.ResolveAsync(request.Tome, cancellationToken);
            var record = await store.GetAsync(id, cancellationToken);
            if (record is null)
            {
                return ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.");
            }

            if (record.Status == MemoryStatus.Tombstoned)
            {
                return ApiResponses.Error(HttpStatusCode.Conflict, "already_tombstoned", $"Memory '{id}' is already tombstoned.");
            }

            if (!await memories.ForgetAsync(id, request.Reason, cancellationToken, tome))
            {
                var raced = await store.GetAsync(id, cancellationToken);
                return raced is null
                    ? ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.")
                    : raced.Status == MemoryStatus.Tombstoned
                        ? ApiResponses.Error(HttpStatusCode.Conflict, "already_tombstoned", $"Memory '{id}' is already tombstoned.")
                        : ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist in tome '{tome}'.");
            }

            return await store.GetAsync(id, cancellationToken) is { } forgotten
                ? Results.Ok(forgotten)
                : ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.");
        });

        app.MapPost("/api/memories/{id:guid}/restore", async (Guid id, RestoreMemoryRequest request, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_reason", "A restore reason is required.");
            }

            if (request.Tome is { } requestedTome && !TomeKey.TryNormalize(requestedTome, out _))
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_tome",
                    TomeParam.InvalidMessage("tome", requestedTome));
            }

            var tome = await tomes.ResolveAsync(request.Tome, cancellationToken);
            var record = await store.GetAsync(id, cancellationToken);
            if (record is null)
            {
                return ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.");
            }

            if (record.Tome != tome)
            {
                return ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist in tome '{tome}'.");
            }

            var actor = await tomes.ResolveActorAsync(cancellationToken);
            var result = await corrections.RestoreAsync(id, request.Reason, cancellationToken, tome, actor);
            return result.Record is { } current
                ? Results.Ok(new RestoreMemoryResponse(current, tome, result.Restored, result.Superseder))
                : ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.");
        });

        app.MapGet("/api/memories/{id:guid}/history", async (Guid id, string? tome, CancellationToken cancellationToken) =>
        {
            if (tome is { } requestedTome && !TomeKey.TryNormalize(requestedTome, out _))
            {
                return ApiResponses.Error(
                    HttpStatusCode.BadRequest,
                    "invalid_tome",
                    TomeParam.InvalidMessage("tome", requestedTome));
            }

            var resolved = await tomes.ResolveAsync(tome, cancellationToken);
            var record = await store.GetAsync(id, cancellationToken);
            if (record is null)
            {
                return ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.");
            }

            if (record.Tome != resolved)
            {
                return ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist in tome '{resolved}'.");
            }

            var history = await corrections.HistoryAsync(id, cancellationToken, resolved);
            return history.Found
                ? Results.Ok(new HistoryResponse(resolved, history.Chain))
                : ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.");
        });
    }
}
