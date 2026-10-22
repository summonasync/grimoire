using System.Reflection;

namespace Grimoire.Cli;

internal static class ProductVersion
{
    public static string Text => Read() ?? "0.0.0";

    private static string? Read()
    {
        var assembly = typeof(ProductVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var separator = informational.IndexOf('+');
            return separator < 0 ? informational : informational[..separator];
        }

        return assembly.GetName().Version is { } version ? version.ToString(3) : null;
    }
}
