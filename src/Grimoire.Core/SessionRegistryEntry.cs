namespace Grimoire.Core;

/// <summary>
/// One observed MCP handshake in the session registry. Field names are the pinned 054
/// cross-task contract (writer, reader and UI share them); <see cref="ProtocolVersion"/>
/// is null when the negotiated version was not observable — never a guess or a placeholder.
/// This record is storage-facing: it must not surface on the frozen MCP tool schema.
/// </summary>
public sealed record SessionRegistryEntry
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required DateTimeOffset OccurredAt { get; init; }

    public string? ClientName { get; init; }

    public string? ClientVersion { get; init; }

    public string? ProtocolVersion { get; init; }

    public string? ActorAlias { get; init; }

    public string? ActorVendor { get; init; }

    public required string Transport { get; init; }

    public required string Host { get; init; }

    public string? SessionId { get; init; }
}
