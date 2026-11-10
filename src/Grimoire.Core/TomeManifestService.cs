namespace Grimoire.Core;

public sealed class TomeManifestService(ITomeManifestStore store, IClock clock)
{
    public const int MaxDescriptionLength = 280;

    public async Task<TomeManifestWriteResult> CreateAsync(
        string key,
        string description,
        MemoryActor actor,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeKey(key);
        var trimmed = ValidateDescription(description);
        RequireActor(actor);
        var now = clock.UtcNow;

        if (await store.GetAsync(normalized, cancellationToken) is not null)
        {
            return new TomeManifestWriteResult { Outcome = TomeManifestOutcome.AlreadyExists };
        }

        var manifest = new TomeManifest
        {
            Key = normalized,
            Description = trimmed,
            CreatedBy = actor.ToString(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        await store.CreateAsync(manifest, cancellationToken);

        return new TomeManifestWriteResult { Outcome = TomeManifestOutcome.Created, Manifest = manifest };
    }

    public async Task<TomeManifestWriteResult> UpdateAsync(
        string key,
        string description,
        MemoryActor actor,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeKey(key);
        var trimmed = ValidateDescription(description);
        RequireActor(actor);
        var now = clock.UtcNow;

        var existing = await store.GetAsync(normalized, cancellationToken);
        if (existing is null)
        {
            return new TomeManifestWriteResult { Outcome = TomeManifestOutcome.NotFound };
        }

        await store.UpdateAsync(normalized, trimmed, actor, now, cancellationToken);

        return new TomeManifestWriteResult
        {
            Outcome = TomeManifestOutcome.Updated,
            Manifest = existing with { Description = trimmed, UpdatedAt = now },
        };
    }

    public async Task<TomeManifest?> GetAsync(string key, CancellationToken cancellationToken) =>
        await store.GetAsync(NormalizeKey(key), cancellationToken);

    public Task<IReadOnlyList<TomeManifest>> ListAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);

    private static string NormalizeKey(string key) =>
        TomeKey.TryNormalize(key, out var normalized)
            ? normalized
            : throw new ArgumentException(
                "A tome key must be lowercase kebab-case with 1..64 characters.", nameof(key));

    private static string ValidateDescription(string description)
    {
        var trimmed = description?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("A manifest description is required.", nameof(description));
        }

        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"A manifest description must not exceed {MaxDescriptionLength} characters after trim.", nameof(description));
        }

        return trimmed;
    }

    private static void RequireActor(MemoryActor actor)
    {
        if (string.IsNullOrWhiteSpace(actor.Alias))
        {
            throw new ArgumentException("A manifest write requires actor provenance.", nameof(actor));
        }
    }
}
