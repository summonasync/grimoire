namespace Grimoire.Core.Consolidation;

public sealed class ExtractiveSummarizer : IClusterSummarizer
{
    public const int SourceTextChars = 200;

    public string Summarize(IReadOnlyList<MemoryRecord> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count == 0)
        {
            throw new ArgumentException("At least one source record is required.", nameof(sources));
        }

        var ordered = sources
            .OrderByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .ToArray();
        var range = $"{ordered[^1].CreatedAt:yyyy-MM-dd}..{ordered[0].CreatedAt:yyyy-MM-dd}";
        var topic = MajorityTopic(ordered);

        var header = $"Consolidated from {ordered.Length} records"
            + (topic is null ? " . " : $" . {topic} . ")
            + range;

        return header + "\n" + string.Join(
            '\n',
            ordered.Select(record =>
                $"- {record.CreatedAt:yyyy-MM-dd} [{record.Actor}] {SourceText(record.Content)} (grimoire://memory/{record.Id})"));
    }

    public static string? MajorityTopic(IReadOnlyList<MemoryRecord> records)
    {
        var half = records.Count / 2d;

        return records
            .SelectMany(record => record.Tags.Where(tag => tag.StartsWith(ITopicClassifier.TopicPrefix, StringComparison.Ordinal)))
            .GroupBy(tag => tag, StringComparer.Ordinal)
            .Where(group => group.Count() >= half)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string SourceText(string content) => Trim(content.Replace('\r', ' ').Replace('\n', ' ').Trim());

    private static string Trim(string text) => text.Length <= SourceTextChars ? text : text[..SourceTextChars];
}
