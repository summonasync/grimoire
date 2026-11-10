namespace Grimoire.Core;

public interface ITomeManifestStore
{
    string? DegradationReason { get; }

    Task CreateAsync(TomeManifest manifest, CancellationToken cancellationToken);

    Task<TomeManifest?> GetAsync(string key, CancellationToken cancellationToken);

    Task UpdateAsync(string key, string description, MemoryActor actor, DateTimeOffset now, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    Task<IReadOnlyList<TomeManifest>> ListAsync(CancellationToken cancellationToken);
}
