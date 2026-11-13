using System.Globalization;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grpc.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Sessions;

/// <summary>
/// Qdrant-backed <see cref="ISessionRegistry"/> over a dedicated vector-less collection,
/// a sibling of the tome-manifest store: same boot shape (ensure the collection on first
/// use, retried until it succeeds), same degradation honesty (reads return empty with a
/// reason, writes surface <see cref="UpstreamUnavailableException"/>). Point ids are random
/// UUIDv7 values generated per write and handed back to the caller — one point per handshake
/// is the unit of observation, so two identical handshakes are two rows and no field-derived
/// hash may collapse them. The actor refinement patches only actorAlias and actorVendor on
/// the addressed point via SetPayload and fails the same way a write does; an unknown id is
/// acknowledged without creating a row.
/// No payload indexes: Qdrant scroll has no server-side ordering, so newest-first retrieval
/// and the window filter are done reader-side over a collection bounded by retention.
/// Null contract fields are omitted from the payload and decode back to null — a missing
/// protocolVersion stays null, never a placeholder.
/// </summary>
public sealed class QdrantSessionStore : ISessionRegistry
{
    private const uint ScrollPageSize = 256;

    private const string UpstreamName = "qdrant";

    private readonly IQdrantVectorDb _db;
    private readonly string _collection;
    private readonly SemaphoreSlim _readyGate = new(1, 1);
    private bool _ready;

    public QdrantSessionStore(IQdrantVectorDb db, QdrantOptions qdrant)
    {
        _db = db;
        _collection = qdrant.SessionCollectionOrDefault;
    }

    public string? DegradationReason { get; private set; }

    public async Task<Guid> WriteAsync(SessionRegistryEntry entry, CancellationToken cancellationToken) =>
        await GuardAsync(async () =>
        {
            await EnsureReadyAsync(cancellationToken);
            var id = Guid.CreateVersion7();
            await _db.UpsertAsync(_collection, [Point(id, entry)], cancellationToken);
            return id;
        });

    public async Task RefineActorAsync(Guid id, string alias, string vendor, CancellationToken cancellationToken) =>
        await GuardAsync(async () =>
        {
            await EnsureReadyAsync(cancellationToken);

            // SetPayload addresses one existing point: on an unknown id Qdrant acknowledges
            // the update without creating a row, so the no-op policy is the server's own.
            await _db.SetPayloadAsync(
                _collection,
                new Dictionary<string, Value>
                {
                    [SessionPayloadFields.ActorAlias] = alias,
                    [SessionPayloadFields.ActorVendor] = vendor,
                },
                id,
                cancellationToken);
        });

    private async Task GuardAsync(Func<Task> call) =>
        await GuardAsync<object?>(async () =>
        {
            await call();
            return null;
        });

    public async Task<IReadOnlyList<SessionRegistryEntry>> ListRecentAsync(
        int limit,
        DateTimeOffset? notBefore,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureReadyAsync(cancellationToken);
            var rows = await ScrollAllAsync(cancellationToken);

            var recent = rows
                .Select(row => row.Entry)
                .Where(entry => notBefore is null || entry.OccurredAt >= notBefore)
                .OrderByDescending(entry => entry.OccurredAt)
                .Take(limit)
                .ToArray();

            DegradationReason = null;
            return recent;
        }
        catch (Exception failure) when (IsDegradable(failure))
        {
            DegradationReason = Reason(failure);
            return [];
        }
    }

    public async Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        await GuardAsync(async () =>
        {
            await EnsureReadyAsync(cancellationToken);
            var expired = (await ScrollAllAsync(cancellationToken))
                .Where(row => row.Entry.OccurredAt < cutoff)
                .ToArray();

            if (expired.Length == 0)
            {
                return 0;
            }

            await _db.DeleteAsync(_collection, [.. expired.Select(row => row.Id)], cancellationToken);

            return expired.Length;
        });

    private async Task<IReadOnlyList<StoredRow>> ScrollAllAsync(CancellationToken cancellationToken)
    {
        var rows = new List<StoredRow>();
        PointId? offset = null;

        do
        {
            var page = await _db.ScrollAsync(
                _collection,
                new Filter(),
                ScrollPageSize,
                offset,
                withVectors: false,
                cancellationToken);

            foreach (var point in page.Result)
            {
                rows.Add(new StoredRow(Guid.Parse(point.Id.Uuid), Decode(point)));
            }

            offset = page.NextPageOffset;
        }
        while (offset is not null);

        return rows;
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return;
        }

        await _readyGate.WaitAsync(cancellationToken);

        try
        {
            if (_ready)
            {
                return;
            }

            if (!await _db.CollectionExistsAsync(_collection, cancellationToken))
            {
                await _db.CreateCollectionWithoutVectorsAsync(_collection, cancellationToken);
            }

            _ready = true;
        }
        finally
        {
            _readyGate.Release();
        }
    }

    private async Task<T> GuardAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (RpcException ex) when (IsAvailabilityFailure(ex))
        {
            throw new UpstreamUnavailableException(UpstreamName, ex);
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.NotFound)
        {
            _ready = false;
            throw new UpstreamUnavailableException(UpstreamName, ex);
        }
    }

    private static bool IsDegradable(Exception failure) =>
        failure is RpcException or UpstreamUnavailableException;

    private static string Reason(Exception failure) =>
        $"Session registry is unreachable — recent handshakes may be missing: {failure.Message}";

    private static bool IsAvailabilityFailure(RpcException ex) =>
        ex.Status.StatusCode is StatusCode.Unavailable
            or StatusCode.DeadlineExceeded
            or StatusCode.Unauthenticated;

    private static PointStruct Point(Guid id, SessionRegistryEntry entry)
    {
        var point = new PointStruct
        {
            Id = new PointId { Uuid = id.ToString() },
            Vectors = new Vectors { Vectors_ = new NamedVectors() },
        };

        point.Payload[SessionPayloadFields.SchemaVersion] = entry.SchemaVersion;
        point.Payload[SessionPayloadFields.OccurredAt] = Store(entry.OccurredAt);
        point.Payload[SessionPayloadFields.Transport] = entry.Transport;
        point.Payload[SessionPayloadFields.Host] = entry.Host;

        if (entry.ClientName is { } clientName)
        {
            point.Payload[SessionPayloadFields.ClientName] = clientName;
        }

        if (entry.ClientVersion is { } clientVersion)
        {
            point.Payload[SessionPayloadFields.ClientVersion] = clientVersion;
        }

        if (entry.ProtocolVersion is { } protocolVersion)
        {
            point.Payload[SessionPayloadFields.ProtocolVersion] = protocolVersion;
        }

        if (entry.ActorAlias is { } actorAlias)
        {
            point.Payload[SessionPayloadFields.ActorAlias] = actorAlias;
        }

        if (entry.ActorVendor is { } actorVendor)
        {
            point.Payload[SessionPayloadFields.ActorVendor] = actorVendor;
        }

        if (entry.SessionId is { } sessionId)
        {
            point.Payload[SessionPayloadFields.SessionId] = sessionId;
        }

        return point;
    }

    private static SessionRegistryEntry Decode(RetrievedPoint point)
    {
        var id = point.Id.Uuid;

        try
        {
            return new SessionRegistryEntry
            {
                SchemaVersion = (int)RequiredInteger(point.Payload, SessionPayloadFields.SchemaVersion),
                OccurredAt = RequiredTimestamp(point.Payload, SessionPayloadFields.OccurredAt),
                ClientName = OptionalString(point.Payload, SessionPayloadFields.ClientName),
                ClientVersion = OptionalString(point.Payload, SessionPayloadFields.ClientVersion),
                ProtocolVersion = OptionalString(point.Payload, SessionPayloadFields.ProtocolVersion),
                ActorAlias = OptionalString(point.Payload, SessionPayloadFields.ActorAlias),
                ActorVendor = OptionalString(point.Payload, SessionPayloadFields.ActorVendor),
                Transport = RequiredString(point.Payload, SessionPayloadFields.Transport),
                Host = RequiredString(point.Payload, SessionPayloadFields.Host),
                SessionId = OptionalString(point.Payload, SessionPayloadFields.SessionId),
            };
        }
        catch (PayloadDecodeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PayloadDecodeException($"Session registry point '{id}' carries a payload that does not match the schema: {ex.Message}", ex);
        }
    }

    private static string RequiredString(IDictionary<string, Value> payload, string field) =>
        payload.TryGetValue(field, out var value) && value.KindCase == Value.KindOneofCase.StringValue
            ? value.StringValue
            : throw new PayloadDecodeException($"Payload field '{field}' is missing or is not a string.");

    private static string? OptionalString(IDictionary<string, Value> payload, string field) =>
        payload.TryGetValue(field, out var value) && value.KindCase == Value.KindOneofCase.StringValue
            ? value.StringValue
            : null;

    private static long RequiredInteger(IDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out var value) || value.KindCase != Value.KindOneofCase.IntegerValue)
        {
            throw new PayloadDecodeException($"Payload field '{field}' is missing or is not an integer.");
        }

        return value.IntegerValue;
    }

    private static DateTimeOffset RequiredTimestamp(IDictionary<string, Value> payload, string field) =>
        DateTimeOffset.Parse(
            RequiredString(payload, field),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    private static string Store(DateTimeOffset moment) => moment.ToString("O", CultureInfo.InvariantCulture);

    private sealed record StoredRow(Guid Id, SessionRegistryEntry Entry);
}
