using System.Globalization;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Tests.Fakes;

public static class QdrantFilterEvaluator
{
    public static bool Matches(Filter filter, IReadOnlyDictionary<string, Value> payload) =>
        filter.Must.All(condition => Matches(condition, payload))
        && filter.MustNot.All(condition => !Matches(condition, payload));

    private static bool Matches(Condition condition, IReadOnlyDictionary<string, Value> payload) =>
        condition.Field is { } field && Matches(field, payload);

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
        if (!payload.TryGetValue(key, out var value))
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
        if (!payload.TryGetValue(key, out var value) || value.KindCase != Value.KindOneofCase.StringValue)
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
}
