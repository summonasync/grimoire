namespace Grimoire.Core.Tests.Fakes;

public sealed class FakeTomeManifestStore : ITomeManifestStore
{
    private readonly Dictionary<string, TomeManifest> _manifests = [];

    public List<string> CallLog { get; } = [];

    public Exception? Failure { get; set; }

    public string? DegradationReason { get; set; }

    public IReadOnlyCollection<TomeManifest> Manifests => _manifests.Values;

    public void Seed(TomeManifest manifest) => _manifests[manifest.Key] = manifest;

    public Task CreateAsync(TomeManifest manifest, CancellationToken cancellationToken)
    {
        ThrowIfConfigured();
        CallLog.Add($"manifest:create:{manifest.Key}");
        _manifests[manifest.Key] = manifest;
        return Task.CompletedTask;
    }

    public Task<TomeManifest?> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (DegradationReason is not null)
        {
            return Task.FromResult<TomeManifest?>(null);
        }

        ThrowIfConfigured();
        CallLog.Add($"manifest:get:{key}");
        return Task.FromResult(_manifests.TryGetValue(key, out var manifest) ? manifest : null);
    }

    public Task UpdateAsync(string key, string description, MemoryActor actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ThrowIfConfigured();
        CallLog.Add($"manifest:update:{key}");

        if (!_manifests.TryGetValue(key, out var manifest))
        {
            throw new KeyNotFoundException($"No manifest stored for '{key}'.");
        }

        _manifests[key] = manifest with { Description = description, UpdatedAt = now };
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ThrowIfConfigured();
        CallLog.Add($"manifest:delete:{key}");
        _manifests.Remove(key);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TomeManifest>> ListAsync(CancellationToken cancellationToken)
    {
        if (DegradationReason is not null)
        {
            return Task.FromResult<IReadOnlyList<TomeManifest>>([]);
        }

        ThrowIfConfigured();
        IReadOnlyList<TomeManifest> manifests = [.. _manifests.Values.OrderBy(manifest => manifest.Key)];
        return Task.FromResult(manifests);
    }

    private void ThrowIfConfigured()
    {
        if (Failure is { } failure)
        {
            throw failure;
        }
    }
}
