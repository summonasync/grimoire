namespace Grimoire.Api.Contracts;

public sealed record TomeGraphResponse(string Tome, IReadOnlyList<GraphNodeResponse> Nodes, IReadOnlyList<GraphEdgeResponse> Edges);
