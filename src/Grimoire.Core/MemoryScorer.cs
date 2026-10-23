namespace Grimoire.Core;

public static class MemoryScorer
{
    public static double Score(
        double similarity,
        MemoryRecord record,
        DateTimeOffset now,
        RecencyHalfLives halfLives,
        string? callerProjectId = null,
        double foreignProjectFactor = 1)
    {
        var age = now - record.LastAccessedAt;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        var halfLifeDays = Math.Max(halfLives.For(record.Scope), double.Epsilon);
        var recencyDecay = Math.Pow(0.5, age.TotalDays / halfLifeDays);
        var importanceWeight = 0.5 + 0.5 * Clamp(record.Importance);
        var foreignDemotion = ForeignProjectDemotion(record, callerProjectId, foreignProjectFactor);

        return similarity * recencyDecay * importanceWeight * foreignDemotion;
    }

    private static double ForeignProjectDemotion(MemoryRecord record, string? callerProjectId, double foreignProjectFactor)
    {
        return record.ProjectId is { } project && callerProjectId is not null && project != callerProjectId
            ? foreignProjectFactor
            : 1;
    }

    private static double Clamp(double importance) => Math.Clamp(importance, 0, 1);
}
