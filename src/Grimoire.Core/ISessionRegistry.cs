namespace Grimoire.Core;

/// <summary>
/// Writer-facing contract for the session registry: the MCP child records handshakes
/// (<see cref="WriteAsync"/>), the API projects recent ones (<see cref="ListRecentAsync"/>),
/// the serve-side sweep retires aged rows (<see cref="PruneAsync"/>). Reads degrade instead
/// of throwing: when the store is unreachable the list comes back empty and
/// <see cref="DegradationReason"/> carries the honest reason; writes surface failures as
/// UpstreamUnavailableException so the caller decides what a lost row costs.
/// <see cref="WriteAsync"/> hands back the id of the point it stored, so the actor columns
/// can be refined on that exact row by <see cref="RefineActorAsync"/> instead of a second
/// write minting a second row. The refinement is one-shot by contract: the MCP face calls
/// it at most once per row, right after session_identity_set. Against an id the registry
/// does not hold it is a documented no-op — it never creates a row — and an unreachable
/// store surfaces UpstreamUnavailableException, the same failure shape as the write path.
/// It patches only the actor fields; every other field of the row stays byte-equal.
/// A null vendor means the caller does not know one: the stored vendor field is left
/// exactly as it is — neither overwritten nor erased, and never replaced with a
/// placeholder. Only a non-null vendor updates the field; alias is always required.
/// </summary>
public interface ISessionRegistry
{
    string? DegradationReason { get; }

    Task<Guid> WriteAsync(SessionRegistryEntry entry, CancellationToken cancellationToken);

    Task RefineActorAsync(Guid id, string alias, string? vendor, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionRegistryEntry>> ListRecentAsync(
        int limit,
        DateTimeOffset? notBefore,
        CancellationToken cancellationToken);

    Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
