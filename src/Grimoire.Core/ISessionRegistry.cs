namespace Grimoire.Core;

/// <summary>
/// Writer-facing contract for the session registry: the MCP child records handshakes
/// (<see cref="WriteAsync"/>), the API projects recent ones (<see cref="ListRecentAsync"/>),
/// the serve-side sweep retires aged rows (<see cref="PruneAsync"/>). Reads degrade instead
/// of throwing: when the store is unreachable the list comes back empty and
/// <see cref="DegradationReason"/> carries the honest reason; writes surface failures as
/// UpstreamUnavailableException so the caller decides what a lost row costs.
/// </summary>
public interface ISessionRegistry
{
    string? DegradationReason { get; }

    Task WriteAsync(SessionRegistryEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionRegistryEntry>> ListRecentAsync(
        int limit,
        DateTimeOffset? notBefore,
        CancellationToken cancellationToken);

    Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
