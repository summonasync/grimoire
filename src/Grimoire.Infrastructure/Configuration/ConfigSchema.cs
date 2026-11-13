namespace Grimoire.Infrastructure.Configuration;

public enum ConfigValueKind
{
    Text,
    Number,
    Boolean,
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
}
