using System.Net;
using Grimoire.Api.Contracts;
using Grimoire.Api.Graphing;
using Grimoire.Api.Querying;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class GraphEndpoints
{
    public static void MapGraphEndpoints(this WebApplication app, IMemoryStore store, MemoryEngineOptions engine)
    {
        app.MapGet("/api/memories/{id:guid}/related", async (Guid id, string? classes, CancellationToken cancellationToken) =>
        {
            IReadOnlyList<RecordEdgeClass> requested;
            try
            {
                requested = GraphClassesParser.Parse(classes);
            }
            catch (MemoryQueryException ex)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, ex.Code, ex.Message);
            }

            var record = await store.GetAsync(id, cancellationToken);
            if (record is null)
            {
                return ApiResponses.Error(HttpStatusCode.NotFound, "not_found", $"Memory '{id}' does not exist.");
            }

            var points = await store.FetchAsync(new MemoryFilter { Tome = record.Tome }, cancellationToken);
            var graph = RecordGraphProjection.ProjectPoints(points, GraphOptions(requested, engine));
            var recordsById = points.ToDictionary(point => point.Record.Id, point => point.Record);
            var edges = graph.Edges
                .Where(edge => requested.Contains(edge.Class))
                .Where(edge => edge.FromId == id || edge.ToId == id)
                .Select(edge => RelatedEdge(id, edge, recordsById))
                .ToArray();

            return Results.Ok(new RelatedMemoryResponse(id, record.Tome, edges));
        });

        app.MapGet("/api/tomes/{key}/graph", async (string key, string? classes, CancellationToken cancellationToken) =>
        {
            IReadOnlyList<RecordEdgeClass> requested;
            try
            {
                requested = GraphClassesParser.Parse(classes);
            }
            catch (MemoryQueryException ex)
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, ex.Code, ex.Message);
            }

            if (!TomeKey.TryNormalize(key, out var tome))
            {
                return ApiResponses.Error(HttpStatusCode.BadRequest, "invalid_tome", TomeParam.InvalidMessage("tome", key));
            }

            var points = await store.FetchAsync(new MemoryFilter { Tome = tome }, cancellationToken);
            var graph = RecordGraphProjection.ProjectPoints(points, GraphOptions(requested, engine));

            var nodes = points
                .Where(point => point.Record.Status == MemoryStatus.Active)
                .Select(point => new GraphNodeResponse(
                    point.Record.Id,
                    RecordSnippets.Of(point.Record.Content),
                    point.Record.Kind,
                    point.Record.ProjectId))
                .ToArray();

            var edges = graph.Edges
                .Where(edge => requested.Contains(edge.Class))
                .Select(edge => new GraphEdgeResponse(edge.FromId, edge.ToId, edge.Class, edge.TargetStatus))
                .ToArray();

            return Results.Ok(new TomeGraphResponse(tome, nodes, edges));
        });
    }

    private static RecordGraphOptions GraphOptions(IReadOnlyList<RecordEdgeClass> classes, MemoryEngineOptions engine) =>
        new()
        {
            IncludeCoMembership = classes.Contains(RecordEdgeClass.CoMembership),
            RelatedSimilarityThreshold = classes.Contains(RecordEdgeClass.Similarity)
                ? engine.RelatedSimilarityThreshold
                : null,
        };

    private static RelatedEdgeResponse RelatedEdge(
        Guid id,
        RecordEdge edge,
        IReadOnlyDictionary<Guid, MemoryRecord> recordsById)
    {
        var outbound = edge.FromId == id;
        var neighbor = recordsById[outbound ? edge.ToId : edge.FromId];

        return new RelatedEdgeResponse(
            edge.Class,
            outbound ? GraphDirection.Outbound : GraphDirection.Inbound,
            new RelatedTargetResponse(neighbor.Id, RecordSnippets.Of(neighbor.Content), neighbor.Status, neighbor.Tome));
    }
}
