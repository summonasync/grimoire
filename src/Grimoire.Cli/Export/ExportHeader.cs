using Grimoire.Infrastructure.Memory;

namespace Grimoire.Cli.Export;

internal sealed record ExportHeader
{
    public required int FormatVersion { get; init; }

    public required EmbeddingFingerprint Fingerprint { get; init; }

    public required DateTimeOffset ExportedAt { get; init; }
}
