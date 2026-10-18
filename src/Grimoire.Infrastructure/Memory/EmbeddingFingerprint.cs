using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

public readonly record struct EmbeddingFingerprint(string Model, int Dimensions)
{
    public static readonly Guid MarkerPointId = Guid.Parse("7a1c9f3e-5b2d-4c8e-9f60-3d8a1b5c7e92");

    public const string ModelField = "__grimoire_embedding_model";

    public const string DimensionsField = "__grimoire_embedding_dimensions";

    public static EmbeddingFingerprint From(string model, int dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);

        return new EmbeddingFingerprint(model, dimensions);
    }

    public IReadOnlyDictionary<string, Value> ToPayload() => new Dictionary<string, Value>
    {
        [ModelField] = Model,
        [DimensionsField] = Dimensions,
    };

    public static EmbeddingFingerprint? FromPayload(IReadOnlyDictionary<string, Value> payload)
    {
        if (!payload.TryGetValue(ModelField, out var model) || !payload.TryGetValue(DimensionsField, out var dimensions))
        {
            if (!payload.ContainsKey(ModelField) && !payload.ContainsKey(DimensionsField))
            {
                return null;
            }

            throw new EmbeddingFingerprintException("The embedding fingerprint marker carries an incomplete payload.");
        }

        if (model.KindCase != Value.KindOneofCase.StringValue
            || dimensions.KindCase != Value.KindOneofCase.IntegerValue)
        {
            throw new EmbeddingFingerprintException("The embedding fingerprint marker carries malformed values.");
        }

        return new EmbeddingFingerprint(model.StringValue, (int)dimensions.IntegerValue);
    }
}
