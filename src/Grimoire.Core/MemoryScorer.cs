namespace Grimoire.Core;

public static class MemoryScorer
{
    public static double Score(double similarity, MemoryRecord record, DateTimeOffset now, RecencyHalfLives halfLives)
    {
        var age = now - record.LastAccessedAt;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        var halfLifeDays = Math.Max(halfLives.For(record.Scope), double.Epsilon);
        var recencyDecay = Math.Pow(0.5, age.TotalDays / halfLifeDays);
        var importanceWeight = 0.5 + 0.5 * Clamp(record.Importance);

        return similarity * recencyDecay * importanceWeight;
    }

    private static double Clamp(double importance) => Math.Clamp(importance, 0, 1);
}
