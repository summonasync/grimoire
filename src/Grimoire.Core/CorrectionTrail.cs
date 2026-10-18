namespace Grimoire.Core;

public static class CorrectionTrail
{
    public static MemoryRecord Append(
        MemoryRecord record,
        DateTimeOffset at,
        MemoryActor actor,
        CorrectionAction action,
        string reason)
    {
        var line = new CorrectionEntry
        {
            At = at,
            Actor = actor,
            Action = action,
            Reason = reason,
        };

        return record with { Corrections = [.. record.Corrections, line] };
    }
}
