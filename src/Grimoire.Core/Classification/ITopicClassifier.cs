namespace Grimoire.Core;

public interface ITopicClassifier
{
    const string TopicPrefix = "topic:";

    static string? Normalize(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var trimmed = topic.Trim();
        var bare = trimmed.StartsWith(TopicPrefix, StringComparison.Ordinal)
            ? trimmed[TopicPrefix.Length..]
            : trimmed;
        var slug = string.Join('-', bare.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

        return slug.Length == 0 ? null : string.Concat(TopicPrefix, slug);
    }

    Task<string?> ClassifyAsync(
        ReadOnlyMemory<float> vector,
        string tome,
        MemoryScope scope,
        string? projectId,
        string? sessionId,
        CancellationToken cancellationToken);
}
