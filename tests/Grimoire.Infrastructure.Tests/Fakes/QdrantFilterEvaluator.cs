using System.Globalization;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Tests.Fakes;

public static class QdrantFilterEvaluator
{
    public static bool Matches(Filter filter, IReadOnlyDictionary<string, Value> payload) =>
        filter.Must.All(condition => Matches(condition, payload))
        && (filter.Should.Count == 0 || filter.Should.Any(condition => Matches(condition, payload)))
        && filter.MustNot.All(condition => !Matches(condition, payload));

    private static bool Matches(Condition condition, IReadOnlyDictionary<string, Value> payload) =>
        condition.Field is { } field ? Matches(field, payload)
        : condition.Filter is { } nested ? Matches(nested, payload)
        : condition.IsEmpty is { } empty ? IsEmpty(payload, empty.Key)
        : throw new NotSupportedException($"Condition kind on '{condition.Field?.Key}' is not supported by the fake.");

    private static bool IsEmpty(IReadOnlyDictionary<string, Value> payload, string key) =>
        !TryResolve(payload, key, out var value) || value.KindCase == Value.KindOneofCase.NullValue;

    private static bool Matches(FieldCondition field, IReadOnlyDictionary<string, Value> payload)
    {
        if (field.Match is { } match)
        {
            return Matches(match, payload, field.Key);
        }

        if (field.DatetimeRange is { } range)
        {
            return Matches(range, payload, field.Key);
        }

        throw new NotSupportedException($"Filter condition on '{field.Key}' is not supported by the fake.");
    }

    private static bool Matches(Match match, IReadOnlyDictionary<string, Value> payload, string key)
    {
        if (!TryResolve(payload, key, out var value))
        {
            return false;
        }

        return match.MatchValueCase switch
        {
            Match.MatchValueOneofCase.Keyword => value.KindCase == Value.KindOneofCase.StringValue
                && value.StringValue == match.Keyword,
            Match.MatchValueOneofCase.Keywords => Keywords(payload, key, value).Any(match.Keywords.Strings.Contains),
            Match.MatchValueOneofCase.ExceptKeywords => !Keywords(payload, key, value).Any(match.ExceptKeywords.Strings.Contains),
            _ => throw new NotSupportedException($"Match kind '{match.MatchValueCase}' on '{key}' is not supported by the fake."),
        };
    }

    private static bool Matches(DatetimeRange range, IReadOnlyDictionary<string, Value> payload, string key)
    {
        if (!TryResolve(payload, key, out var value) || value.KindCase != Value.KindOneofCase.StringValue)
        {
            return false;
        }

        var moment = DateTimeOffset.Parse(value.StringValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).UtcDateTime;

        if (range.Lte is not null && moment > range.Lte.ToDateTime())
        {
            return false;
        }

        if (range.Lt is not null && moment >= range.Lt.ToDateTime())
        {
            return false;
        }

        if (range.Gte is not null && moment < range.Gte.ToDateTime())
        {
            return false;
        }

        if (range.Gt is not null && moment <= range.Gt.ToDateTime())
        {
            return false;
        }

        return true;
    }

    private static string[] Keywords(IReadOnlyDictionary<string, Value> payload, string key, Value value) =>
        value.KindCase switch
        {
            Value.KindOneofCase.StringValue => [value.StringValue],
            Value.KindOneofCase.ListValue => [.. value.ListValue.Values
                .Where(item => item.KindCase == Value.KindOneofCase.StringValue)
                .Select(item => item.StringValue)],
            _ => throw new NotSupportedException($"Payload field '{key}' does not carry keyword values."),
        };

    // Real Qdrant resolves dotted keys into struct payloads; this double only ever looked at the
    // top level, which made it answer "no match" for anything aimed inside a struct — an actor
    // condition among them. Absent segment and null value both resolve to "not there", matching
    // what the server reported when probed directly.
    //
    // The grammar is deliberately no wider than the server's, measured on 1.19 against a throwaway
    // collection: a dotted key walks into structs only. "status.x" (a scalar prefix), "tags.1"
    // (a positional index) and "items.name" / "items.0.name" (a list of structs) all answered with
    // no rows there, so they answer with no rows here. A malformed path ("actor.") was refused by
    // the server as a bad request rather than matched silently, so it throws here — a test double
    // that answers a query the server would never run is the looseness this feature exists to kill.
    private static bool TryResolve(IReadOnlyDictionary<string, Value> payload, string key, out Value value)
    {
        var segments = key.Split('.');

        if (Array.IndexOf(segments, string.Empty) >= 0)
        {
            throw new NotSupportedException(
                $"Key '{key}' is not a valid json path. Real Qdrant refuses the whole request with " +
                "\"Invalid json path\", so the fake refuses to answer it instead of matching nothing.");
        }

        var current = new Value();
        var fields = (IReadOnlyDictionary<string, Value>?)payload;

        for (var index = 0; index < segments.Length; index++)
        {
            if (fields is null || !fields.TryGetValue(segments[index], out current))
            {
                value = new Value();
                return false;
            }

            // The last segment resolves to whatever it holds, container included: a plain keyword on
            // "actor" is a real query shape and the server answers it with no match, not with an error.
            if (index == segments.Length - 1)
            {
                break;
            }

            if (current.KindCase != Value.KindOneofCase.StructValue)
            {
                value = new Value();
                return false;
            }

            fields = current.StructValue.Fields;
        }

        value = current;
        return true;
    }
}
