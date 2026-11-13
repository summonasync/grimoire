namespace Grimoire.Core;

public static class RoundCapWarning
{
    public static IReadOnlyList<string>? Build(RecallEscalationResult scan, int limit) =>
        scan.StoppedBy == RecallStopReason.RoundCapReached
            ? [$"recall page came up short after {scan.Rounds} rounds of window doubling: " +
               $"{scan.Kept.Count} of limit {limit} matches, last window {scan.LastWindow} points — narrow the query or raise limit"]
            : null;
}
