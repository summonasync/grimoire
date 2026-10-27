namespace Grimoire.Api.Contracts;

public sealed record NearDuplicateResponse(Guid MemoryId, double Similarity, string Snippet);
