namespace Grimoire.Core;

internal readonly record struct BriefOmissionLine(
    Guid Id,
    string Text,
    int GroupRank,
    double Score,
    DateTimeOffset Stamp,
    BriefOmissionReason Reason,
    string? Actor = null);
