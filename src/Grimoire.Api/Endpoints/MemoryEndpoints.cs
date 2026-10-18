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
        MemoryQueryRunner queryRunner,
        IMemoryStore store,
        ApiTomeResolver tomes)
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

            var tome = parsed.Filter.Tome ?? await tomes.ResolveAsync(null, cancellationToken);
            var records = await queryRunner.RunAsync(
                query.Query,
                parsed with { Filter = parsed.Filter with { Tome = tome } },
                cancellationToken);

            return Results.Ok(new MemoryListResponse(tome, records.Count, records));
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
                var result = await memories.RememberAsync(new RememberRequest
                {
                    Content = request.Content,
                    Scope = request.Scope,
                    Kind = request.Kind,
                    Actor = MemoryActor.Human,
                    Tome = tome,
                    ProjectId = request.ProjectId,
                    SessionId = request.SessionId,
                    Tags = request.Tags,
                    Importance = request.Importance,
                    Sources = request.Sources,
                }, cancellationToken);

                return Results.Created(
                    $"/api/memories/{result.Record.Id}",
                    new CreateMemoryResponse(
                        result.Record,
                        tome,
                        result.SupersededId,
                        result.IdempotentReplay,
                        result.MaintenancePending));
            }
            catch (ArgumentException ex)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_memory", ex.Message);
            }
        });

        app.MapPatch("/api/memories/{id:guid}", async (Guid id, PatchMemoryRequest request, CancellationToken cancellationToken) =>
        {
            try
            {
                var updated = await editor.EditAsync(id, request, cancellationToken);
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
    }
}
