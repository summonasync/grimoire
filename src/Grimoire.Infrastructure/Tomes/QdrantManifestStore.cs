using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grpc.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Tomes;

/// <summary>
/// Qdrant-backed <see cref="ITomeManifestStore"/> over a dedicated vector-less collection.
/// Point ids are deterministic UUIDv5 values of the tome key, so create and delete converge
/// on one point per manifest and re-runs are idempotent. Boot ensures the collection exists
/// (created when missing) and the key keyword index is present; boot is retried on every
/// call until it succeeds. Manifest reads degrade instead of throwing: when the store
/// cannot be reached, <see cref="GetAsync"/> returns null and <see cref="ListAsync"/>
/// returns empty while <see cref="DegradationReason"/> carries the honest reason — memory
/// paths never depend on this store. Manifest writes surface failures as
/// UpstreamUnavailableException; a collection dropped mid-session resets boot only on the
/// next write, so reads stay degraded until a write re-ensures the collection.
/// </summary>
public sealed class QdrantManifestStore : ITomeManifestStore
{
    private const uint ScrollPageSize = 256;

    private const string UpstreamName = "qdrant";

    private static readonly (string Field, PayloadSchemaType Type)[] Indexes =
    [
        (ManifestPayloadFields.Key, PayloadSchemaType.Keyword),
    ];

    private readonly IQdrantVectorDb _db;
    private readonly string _collection;
    private readonly SemaphoreSlim _readyGate = new(1, 1);
    private bool _ready;

    public QdrantManifestStore(IQdrantVectorDb db, QdrantOptions qdrant)
    {
        _db = db;
        _collection = qdrant.ManifestCollectionOrDefault;
    }

    public string? DegradationReason { get; private set; }

    public async Task CreateAsync(TomeManifest manifest, CancellationToken cancellationToken) =>
        await GuardAsync(async () =>
        {
            await EnsureReadyAsync(cancellationToken);
            await _db.UpsertAsync(_collection, [Point(manifest)], cancellationToken);
        });

    public async Task<TomeManifest?> GetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureReadyAsync(cancellationToken);
            var points = await _db.RetrieveAsync(
                _collection, [TomeManifestKeyId.For(key)], withVectors: false, cancellationToken);
            DegradationReason = null;

            return points.Count == 0 ? null : Decode(points[0]);
        }
        catch (Exception failure) when (IsDegradable(failure))
        {
            DegradationReason = Reason(failure);
            return null;
        }
    }

    public async Task UpdateAsync(
        string key,
        string description,
        MemoryActor actor,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await GuardAsync(async () =>
        {
            await EnsureReadyAsync(cancellationToken);
            await _db.SetPayloadAsync(
                _collection,
                new Dictionary<string, Value>
                {
                    [ManifestPayloadFields.Description] = description,
                    [ManifestPayloadFields.UpdatedAt] = Store(now),
                    [ManifestPayloadFields.UpdatedBy] = actor.ToString(),
                },
                TomeManifestKeyId.For(key),
                cancellationToken);
        });

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await GuardAsync(async () =>
        {
            await EnsureReadyAsync(cancellationToken);
            await _db.DeleteAsync(_collection, [TomeManifestKeyId.For(key)], cancellationToken);
        });

    public async Task<IReadOnlyList<TomeManifest>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureReadyAsync(cancellationToken);
            var manifests = new List<TomeManifest>();
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
                    manifests.Add(Decode(point));
                }

                offset = page.NextPageOffset;
            }
            while (offset is not null);

            DegradationReason = null;
            return manifests;
        }
        catch (Exception failure) when (IsDegradable(failure))
        {
            DegradationReason = Reason(failure);
            return [];
        }
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

            await EnsureCollectionAsync(cancellationToken);
            _ready = true;
        }
        finally
        {
            _readyGate.Release();
        }
    }

    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (!await _db.CollectionExistsAsync(_collection, cancellationToken))
        {
            await _db.CreateCollectionWithoutVectorsAsync(_collection, cancellationToken);
        }

        var info = await _db.GetCollectionInfoAsync(_collection, cancellationToken);
        var present = info.PayloadSchema.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var (field, type) in Indexes)
        {
            if (!present.Contains(field))
            {
                await _db.CreatePayloadIndexAsync(_collection, field, type, cancellationToken);
            }
        }
    }

    private async Task GuardAsync(Func<Task> call)
    {
        try
        {
            await call();
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
        $"Tome manifest store is unreachable — manifest data may be stale: {failure.Message}";

    private static bool IsAvailabilityFailure(RpcException ex) =>
        ex.Status.StatusCode is StatusCode.Unavailable
            or StatusCode.DeadlineExceeded
            or StatusCode.Unauthenticated;

    private static PointStruct Point(TomeManifest manifest)
    {
        var point = new PointStruct
        {
            Id = TomeManifestKeyId.For(manifest.Key),
            Vectors = new Vectors { Vectors_ = new NamedVectors() },
        };
        point.Payload[ManifestPayloadFields.Key] = manifest.Key;
        point.Payload[ManifestPayloadFields.Description] = manifest.Description;
        point.Payload[ManifestPayloadFields.CreatedBy] = manifest.CreatedBy;
        point.Payload[ManifestPayloadFields.CreatedAt] = Store(manifest.CreatedAt);
        point.Payload[ManifestPayloadFields.UpdatedAt] = Store(manifest.UpdatedAt);

        return point;
    }

    private static TomeManifest Decode(RetrievedPoint point)
    {
        var id = Guid.Parse(point.Id.Uuid);

        try
        {
            return new TomeManifest
            {
                Key = Required(point.Payload, ManifestPayloadFields.Key),
                Description = Required(point.Payload, ManifestPayloadFields.Description),
                CreatedBy = Required(point.Payload, ManifestPayloadFields.CreatedBy),
                CreatedAt = Timestamp(point.Payload, ManifestPayloadFields.CreatedAt),
                UpdatedAt = Timestamp(point.Payload, ManifestPayloadFields.UpdatedAt),
            };
        }
        catch (Exception ex)
        {
            throw new PayloadDecodeException(
                $"Tome manifest point '{id}' carries an incomplete payload: {ex.Message}", ex);
        }
    }

    private static string Required(IDictionary<string, Value> payload, string field) =>
        payload.TryGetValue(field, out var value) && value.KindCase == Value.KindOneofCase.StringValue
            ? value.StringValue
            : throw new PayloadDecodeException($"Payload field '{field}' is missing or is not a string.");

    private static DateTimeOffset Timestamp(IDictionary<string, Value> payload, string field) =>
        DateTimeOffset.Parse(
            Required(payload, field),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);

    private static string Store(DateTimeOffset moment) => moment.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
}
