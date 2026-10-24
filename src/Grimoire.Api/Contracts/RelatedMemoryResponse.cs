namespace Grimoire.Api.Contracts;

public sealed record RelatedMemoryResponse(Guid Id, string Tome, IReadOnlyList<RelatedEdgeResponse> Edges);
