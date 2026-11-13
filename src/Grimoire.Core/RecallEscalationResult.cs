namespace Grimoire.Core;

public sealed record RecallEscalationResult
{
    public required IReadOnlyList<VectorMatch> Kept { get; init; }

    public required int Rounds { get; init; }

    public required int LastWindow { get; init; }

    public required RecallStopReason StoppedBy { get; init; }
}
