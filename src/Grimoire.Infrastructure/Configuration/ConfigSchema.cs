using System.Globalization;

namespace Grimoire.Infrastructure.Configuration;

public enum ConfigValueKind
{
    Text,
    Number,
    Boolean,
}

/// <summary>
/// Outcome of reading override text against the numeric grammar both config doors share.
/// Only <see cref="Finite"/> may reach the options tree: a value that parses but is not
/// finite aborts the later deserialization as an unclassified runtime failure.
/// </summary>
public enum NumberVerdict
{
    Finite,
    NotNumber,
    NonFinite,
}

/// <summary>
/// Resolves the CLR kind of a configuration leaf by walking <see cref="GrimoireOptions"/>
/// properties by name. Used where the JSON node carries no type hint — an override or a
/// <c>config set</c> targeting a property whose default is null (a nullable number looks
/// exactly like a nullable string in the node tree, so guessing by value shape would
/// coerce a numeric API key into a number).
/// </summary>
public static class ConfigSchema
{
    public static ConfigValueKind KindOf(IReadOnlyList<string> segments)
    {
        var current = typeof(GrimoireOptions);

        foreach (var segment in segments)
        {
            var property = current.GetProperty(segment, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (property is null)
            {
                return ConfigValueKind.Text;
            }

            current = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        return current switch
        {
            not null when current == typeof(double) || current == typeof(float) || current == typeof(decimal)
                || current == typeof(int) || current == typeof(long) || current == typeof(short) => ConfigValueKind.Number,
            not null when current == typeof(bool) => ConfigValueKind.Boolean,
            _ => ConfigValueKind.Text,
        };
    }

    /// <summary>
    /// The numeric grammar both config doors read through: invariant-culture parse, then finite.
    /// <paramref name="number"/> is only worth reading when the verdict is
    /// <see cref="NumberVerdict.Finite"/>.
    /// </summary>
    public static NumberVerdict ClassifyNumber(string value, out double number)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return NumberVerdict.NotNumber;
        }

        return double.IsFinite(number) ? NumberVerdict.Finite : NumberVerdict.NonFinite;
    }
}
