namespace Grimoire.Api.Contracts;

public sealed record SessionsResponse(
    IReadOnlyList<SessionHandshakeItem> Items,
    int Limit,
    double WindowHours,
    string? DegradationReason);

public sealed record SessionHandshakeItem(
    string? ClientName,
    string? ClientVersion,
    string? ProtocolVersion,
    string? ActorAlias,
    string? ActorVendor,
    DateTimeOffset OccurredAt,
    double AgeHours,
    string Host);
