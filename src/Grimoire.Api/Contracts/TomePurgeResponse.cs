namespace Grimoire.Api.Contracts;

public sealed record TomePurgeResponse(string Key, TomePurgeCounts Purged, bool ManifestPurged);
