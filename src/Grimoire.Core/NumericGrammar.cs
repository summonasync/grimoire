using System.Globalization;

namespace Grimoire.Core;

/// <summary>
/// The one text→number rule every face reads through: invariant culture, sign, digits, optional
/// fraction and exponent, no group separators. Finiteness is part of the rule, not an afterthought
/// of the caller, because <c>double</c> happily parses <c>NaN</c> and <c>1e999</c>.
/// Error channels stay per-face; only the grammar is shared.
/// </summary>
public static class NumericGrammar
{
    public static NumberVerdict Classify(string? value, out double number)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return NumberVerdict.NotNumber;
        }

        return double.IsFinite(number) ? NumberVerdict.Finite : NumberVerdict.NonFinite;
    }

    /// <summary>
    /// Whole-number rule for the integer doors. Measured in <c>decimal</c> rather than
    /// <c>double</c> so an integral literal keeps its exact digits on the way to <see cref="int"/>:
    /// through <c>double</c> anything past 2^53 would silently land on a neighbour before the
    /// range check ever sees it.
    /// </summary>
    public static bool TryWholeNumber(string? value, out int whole)
    {
        whole = 0;

        if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact)
            || decimal.Truncate(exact) != exact
            || exact < int.MinValue
            || exact > int.MaxValue)
        {
            return false;
        }

        whole = (int)exact;
        return true;
    }
}
