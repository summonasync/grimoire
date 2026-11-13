namespace Grimoire.Infrastructure.Configuration;

public static class QdrantCollectionsValidator
{
    // Qdrant reads collection names case-sensitively, so names are compared exactly as configured — no trimming, no casing rule.
    public static IReadOnlyList<string> Validate(QdrantOptions options)
    {
        (string Knob, string Name)[] collections =
        [
            ("qdrant.collection", options.Collection),
            ("qdrant.manifestCollection", options.ManifestCollectionOrDefault),
            ("qdrant.sessionCollection", options.SessionCollectionOrDefault),
        ];

        var errors = new List<string>();
        foreach (var (knob, name) in collections)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"{knob} must not be empty.");
            }
        }

        for (var first = 0; first < collections.Length; first++)
        {
            for (var second = first + 1; second < collections.Length; second++)
            {
                if (string.IsNullOrWhiteSpace(collections[first].Name) || string.IsNullOrWhiteSpace(collections[second].Name))
                {
                    continue;
                }

                if (string.Equals(collections[first].Name, collections[second].Name, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"{collections[second].Knob} '{collections[second].Name}' is the same collection as {collections[first].Knob}.");
                }
            }
        }

        return errors;
    }
}
