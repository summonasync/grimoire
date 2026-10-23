namespace Grimoire.Api.Contracts;

public sealed record TomesResponse(string Tome, IReadOnlyList<TomeSummaryResponse> Tomes);
