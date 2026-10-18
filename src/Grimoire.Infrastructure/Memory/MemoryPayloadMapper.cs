using System.Globalization;
using Grimoire.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

public static class MemoryPayloadMapper
{
    public static IReadOnlyDictionary<string, Value> ToPayload(MemoryRecord record)
    {
        var payload = new Dictionary<string, Value>
        {
            [MemoryPayloadFields.Content] = record.Content,
            [MemoryPayloadFields.Scope] = record.Scope.ToString(),
            [MemoryPayloadFields.Kind] = record.Kind.ToString(),
            [MemoryPayloadFields.Status] = record.Status.ToString(),
            [MemoryPayloadFields.Actor] = ToValue(record.Actor),
            [MemoryPayloadFields.Tome] = record.Tome,
            [MemoryPayloadFields.Tags] = record.Tags.ToArray(),
            [MemoryPayloadFields.Importance] = record.Importance,
            [MemoryPayloadFields.CreatedAt] = Store(record.CreatedAt),
            [MemoryPayloadFields.LastAccessedAt] = Store(record.LastAccessedAt),
            [MemoryPayloadFields.AccessCount] = record.AccessCount,
            [MemoryPayloadFields.Sources] = record.Sources.Select(source => source.ToString()).ToArray(),
            [MemoryPayloadFields.Corrections] = ToCorrectionsValue(record.Corrections),
        };

        if (record.ProjectId is { } projectId)
        {
            payload[MemoryPayloadFields.ProjectId] = projectId;
        }

        if (record.SessionId is { } sessionId)
        {
            payload[MemoryPayloadFields.SessionId] = sessionId;
        }

        if (record.ExpiresAt is { } expiresAt)
        {
            payload[MemoryPayloadFields.ExpiresAt] = Store(expiresAt);
        }

        if (record.SupersededBy is { } supersededBy)
        {
            payload[MemoryPayloadFields.SupersededBy] = supersededBy.ToString();
        }

        if (record.TombstoneReason is { } tombstoneReason)
        {
            payload[MemoryPayloadFields.TombstoneReason] = tombstoneReason;
        }

        if (record.TombstonedAt is { } tombstonedAt)
        {
            payload[MemoryPayloadFields.TombstonedAt] = Store(tombstonedAt);
        }

        if (record.IdempotencyKey is { } idempotencyKey)
        {
            payload[MemoryPayloadFields.IdempotencyKey] = idempotencyKey;
        }

        return payload;
    }

    public static MemoryRecord ToRecord(Guid id, IDictionary<string, Value> payload)
    {
        try
        {
            return new MemoryRecord
            {
                Id = id,
                Content = RequiredString(payload, MemoryPayloadFields.Content),
                Scope = RequiredEnum<MemoryScope>(payload, MemoryPayloadFields.Scope),
                Kind = RequiredEnum<MemoryKind>(payload, MemoryPayloadFields.Kind),
                Status = RequiredEnum<MemoryStatus>(payload, MemoryPayloadFields.Status),
                Actor = RequiredActor(payload, MemoryPayloadFields.Actor),
                Tome = DecodeTome(payload),
                ProjectId = OptionalString(payload, MemoryPayloadFields.ProjectId),
                SessionId = OptionalString(payload, MemoryPayloadFields.SessionId),
                Tags = OptionalStrings(payload, MemoryPayloadFields.Tags),
                Importance = RequiredDouble(payload, MemoryPayloadFields.Importance),
                CreatedAt = RequiredTimestamp(payload, MemoryPayloadFields.CreatedAt),
                LastAccessedAt = RequiredTimestamp(payload, MemoryPayloadFields.LastAccessedAt),
                AccessCount = OptionalLong(payload, MemoryPayloadFields.AccessCount),
                ExpiresAt = OptionalTimestamp(payload, MemoryPayloadFields.ExpiresAt),
                SupersededBy = OptionalGuid(payload, MemoryPayloadFields.SupersededBy),
                TombstoneReason = OptionalString(payload, MemoryPayloadFields.TombstoneReason),
                TombstonedAt = OptionalTimestamp(payload, MemoryPayloadFields.TombstonedAt),
                Sources = [.. OptionalStrings(payload, MemoryPayloadFields.Sources).Select(Guid.Parse)],
                Corrections = OptionalCorrections(payload, MemoryPayloadFields.Corrections),
                IdempotencyKey = OptionalString(payload, MemoryPayloadFields.IdempotencyKey),
            };
        }
        catch (PayloadDecodeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PayloadDecodeException($"Memory point '{id}' carries a payload that does not match the schema: {ex.Message}", ex);
        }
    }

    private static Value ToValue(MemoryActor actor)
    {
        var fields = new Struct();
        fields.Fields.Add("alias", actor.Alias);

        if (actor.Vendor is { } vendor)
        {
            fields.Fields.Add("vendor", vendor);
        }

        return new Value { StructValue = fields };
    }

    private static Value ToCorrectionsValue(IReadOnlyList<CorrectionEntry> corrections)
    {
        var list = new ListValue();
        foreach (var correction in corrections)
        {
            var fields = new Struct();
            fields.Fields.Add("at", Store(correction.At));
            fields.Fields.Add("actor", ToValue(correction.Actor));
            fields.Fields.Add("action", correction.Action.ToString());
            fields.Fields.Add("reason", correction.Reason);
            list.Values.Add(new Value { StructValue = fields });
        }

        return new Value { ListValue = list };
    }

    private static IReadOnlyList<CorrectionEntry> OptionalCorrections(IDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out var value) || value.KindCase == Value.KindOneofCase.NullValue)
        {
            return [];
        }

        if (value.KindCase != Value.KindOneofCase.ListValue)
        {
            throw new PayloadDecodeException($"Payload field '{field}' is not a list.");
        }

        return [.. value.ListValue.Values.Select(DecodeCorrection)];
    }

    private static CorrectionEntry DecodeCorrection(Value value)
    {
        if (value.KindCase != Value.KindOneofCase.StructValue)
        {
            throw new PayloadDecodeException($"Payload field '{MemoryPayloadFields.Corrections}' carries a non-struct entry.");
        }

        return new CorrectionEntry
        {
            At = ParseTimestamp(RequiredString(value.StructValue.Fields, "at"), MemoryPayloadFields.Corrections),
            Actor = RequiredActor(value.StructValue.Fields, "actor"),
            Action = Enum.Parse<CorrectionAction>(RequiredString(value.StructValue.Fields, "action")),
            Reason = RequiredString(value.StructValue.Fields, "reason"),
        };
    }

    private static MemoryActor RequiredActor(IDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out var value) || value.KindCase != Value.KindOneofCase.StructValue)
        {
            throw new PayloadDecodeException($"Payload field '{field}' is missing or is not a struct.");
        }

        return new MemoryActor(RequiredString(value.StructValue.Fields, "alias"), OptionalString(value.StructValue.Fields, "vendor"));
    }

    private static string DecodeTome(IDictionary<string, Value> payload)
    {
        var raw = OptionalString(payload, MemoryPayloadFields.Tome);
        if (raw is null)
        {
            return TomeKey.DefaultTome;
        }

        try
        {
            return TomeKey.Normalize(raw);
        }
        catch (ArgumentException ex)
        {
            throw new PayloadDecodeException($"Payload field '{MemoryPayloadFields.Tome}' carries an invalid tome key '{raw}'.", ex);
        }
    }

    private static string RequiredString(IDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out var value) || value.KindCase != Value.KindOneofCase.StringValue)
        {
            throw new PayloadDecodeException($"Payload field '{field}' is missing or is not a string.");
        }

        return value.StringValue;
    }

    private static string? OptionalString(IDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out var value) || value.KindCase == Value.KindOneofCase.NullValue)
        {
            return null;
        }

        if (value.KindCase != Value.KindOneofCase.StringValue)
        {
            throw new PayloadDecodeException($"Payload field '{field}' is not a string.");
        }

        return value.StringValue;
    }

    private static TEnum RequiredEnum<TEnum>(IDictionary<string, Value> payload, string field)
        where TEnum : struct, Enum
    {
        var raw = RequiredString(payload, field);

        if (!Enum.TryParse(raw, out TEnum parsed))
        {
            throw new PayloadDecodeException($"Payload field '{field}' carries unknown value '{raw}'.");
        }

        return parsed;
    }

    private static IReadOnlyList<string> OptionalStrings(IDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out var value) || value.KindCase == Value.KindOneofCase.NullValue)
        {
            return [];
        }

        if (value.KindCase != Value.KindOneofCase.ListValue)
        {
            throw new PayloadDecodeException($"Payload field '{field}' is not a list.");
        }

        return [.. value.ListValue.Values.Select(item =>
        {
            if (item.KindCase != Value.KindOneofCase.StringValue)
            {
                throw new PayloadDecodeException($"Payload field '{field}' carries a non-string entry.");
            }

            return item.StringValue;
        })];
    }

    private static double RequiredDouble(IDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out var value) || value.KindCase != Value.KindOneofCase.DoubleValue)
        {
            throw new PayloadDecodeException($"Payload field '{field}' is missing or is not a number.");
        }

        return value.DoubleValue;
    }

    private static long OptionalLong(IDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out var value) || value.KindCase == Value.KindOneofCase.NullValue)
        {
            return 0;
        }

        if (value.KindCase != Value.KindOneofCase.IntegerValue)
        {
            throw new PayloadDecodeException($"Payload field '{field}' is not an integer.");
        }

        return value.IntegerValue;
    }

    private static DateTimeOffset RequiredTimestamp(IDictionary<string, Value> payload, string field) =>
        ParseTimestamp(RequiredString(payload, field), field);

    private static DateTimeOffset? OptionalTimestamp(IDictionary<string, Value> payload, string field)
    {
        var raw = OptionalString(payload, field);
        return raw is null ? null : ParseTimestamp(raw, field);
    }

    private static DateTimeOffset ParseTimestamp(string raw, string field)
    {
        try
        {
            return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        catch (FormatException ex)
        {
            throw new PayloadDecodeException($"Payload field '{field}' carries a malformed timestamp '{raw}'.", ex);
        }
    }

    private static string Store(DateTimeOffset moment) => moment.ToString("O", CultureInfo.InvariantCulture);

    private static Guid? OptionalGuid(IDictionary<string, Value> payload, string field)
    {
        var raw = OptionalString(payload, field);
        return raw is null ? null : Guid.Parse(raw);
    }
}
