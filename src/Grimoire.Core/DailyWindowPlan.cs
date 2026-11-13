namespace Grimoire.Core;

public sealed record DailyWindowPlan
{
    public required DailyWindow Today { get; init; }

    public required IReadOnlyList<DailyWindow> LastWorkingDayCandidates { get; init; }
}
