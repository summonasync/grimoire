using Grimoire.Core;

namespace Grimoire.Api.Sessions;

internal static class SessionsQueryValues
{
    private const string NumberForm = "[-]digits[.digits][eN]";

    // An absent knob is null and stays the projection's default; an empty knob arrives as "" and a
    // repeated one as the framework's comma-joined text, so both are refused here rather than
    // silently becoming a default or a joined number. That is the chosen contract, not a leftover.
    public static (int? Value, string? Rejection) Limit(string? raw)
    {
        if (raw is null)
        {
            return (null, null);
        }

        if (!NumericGrammar.TryWholeNumber(raw, out var rows))
        {
            return (null, $"limit must be a whole number of rows written as {NumberForm} with no group separators; " +
                          $"'{raw}' is not valid.");
        }

        return (rows, null);
    }

    public static (double? Value, string? Rejection) WindowHours(string? raw)
    {
        if (raw is null)
        {
            return (null, null);
        }

        return NumericGrammar.Classify(raw, out var hours) == NumberVerdict.Finite
            ? (hours, null)
            : (null, $"windowHours must be a number of hours written as {NumberForm} with no group separators; " +
                     $"'{raw}' is not valid.");
    }
}
