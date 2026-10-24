namespace Grimoire.Api.Contracts;

public sealed record AgentActivityResponse(IReadOnlyList<AgentActivityItem> Agents, int WindowDays);

public sealed record AgentActivityItem(
    string Alias,
    string? Vendor,
    int RecordCount,
    DateTimeOffset LastActivity,
    bool IsHuman);
