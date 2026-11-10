namespace Grimoire.Core;

public sealed class TomePurgeService(IMemoryStore store, ITomeManifestStore manifests)
{
    public async Task<TomePurgeResult> PurgeTomeAsync(string tome, MemoryActor actor, CancellationToken cancellationToken)
    {
        if (!actor.IsHuman)
        {
            throw new UnauthorizedAccessException("Tome purge is a hard delete and is reserved for the human operator.");
        }

        if (!TomeKey.TryNormalize(tome, out var key))
        {
            throw new ArgumentException(
                "A tome key must be lowercase kebab-case with 1..64 characters.", nameof(tome));
        }

        var records = await store.DeleteByTomeAsync(key, cancellationToken);

        bool manifestPurged;
        try
        {
            await manifests.DeleteAsync(key, cancellationToken);
            manifestPurged = true;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            manifestPurged = false;
        }

        return new TomePurgeResult { Records = records, ManifestPurged = manifestPurged };
    }
}
