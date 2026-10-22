namespace Grimoire.Mcp;

internal static class EnumParams
{
    public static (TEnum? Value, ToolError? Error) Parse<TEnum>(string? raw, string paramName)
        where TEnum : struct, Enum
    {
        if (raw is null)
        {
            return (null, null);
        }

        var names = Enum.GetNames<TEnum>();
        foreach (var name in names)
        {
            if (string.Equals(name, raw, StringComparison.Ordinal))
            {
                return ((TEnum)Enum.Parse(typeof(TEnum), name), null);
            }
        }

        return (null, Reject(paramName, raw, names));
    }

    public static (TEnum Value, ToolError? Error) ParseRequired<TEnum>(string? raw, string paramName)
        where TEnum : struct, Enum
    {
        if (raw is null)
        {
            var names = Enum.GetNames<TEnum>();
            return (default, new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"{paramName} is required. Allowed values (case-sensitive, pass one exactly as advertised): " +
                $"{string.Join(", ", names)}.",
                paramName));
        }

        var (value, error) = Parse<TEnum>(raw, paramName);
        return error is not null ? (default, error) : (value!.Value, null);
    }

    public static (IReadOnlyList<TEnum>? Values, ToolError? Error) ParseEntries<TEnum>(string[]? raw, string paramName)
        where TEnum : struct, Enum
    {
        if (raw is null)
        {
            return (null, null);
        }

        var names = Enum.GetNames<TEnum>();
        var values = new List<TEnum>(raw.Length);
        foreach (var entry in raw)
        {
            var matched = Array.IndexOf(names, entry) >= 0 ? entry : null;
            if (matched is null)
            {
                return (null, Reject($"{paramName} entry", entry, names) with { Param = paramName });
            }

            values.Add((TEnum)Enum.Parse(typeof(TEnum), matched));
        }

        return (values, null);
    }

    private static ToolError Reject(string paramName, string raw, string[] names)
        => new(
            ToolErrorCodes.InvalidRequest,
            $"{paramName}: '{raw}' is not an allowed value. Allowed values (case-sensitive, pass one exactly as " +
            $"advertised): {string.Join(", ", names)}.",
            paramName);
}
