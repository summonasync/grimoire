namespace Grimoire.Core;

public sealed record BriefCompactionDue
{
    public required long PendingEntries { get; init; }
}
