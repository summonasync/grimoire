namespace Grimoire.Core;

public sealed record CorrectionEntry
{
    public required DateTimeOffset At { get; init; }

    public required MemoryActor Actor { get; init; }

    public required CorrectionAction Action { get; init; }

    public required string Reason { get; init; }
}
