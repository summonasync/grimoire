using System.Reflection;
using System.Text.Json;

namespace Grimoire.Api;

public enum SpaStampState
{
    Match,
    Mismatch,
    Unknown,
}

public enum SpaStampUnknownCause
{
    None,
    Missing,
    Empty,
    Invalid,
    Unreadable,
}

public sealed record SpaStampVerdict(
    SpaStampState State,
    string? ExportVersion = null,
    string? BinaryVersion = null,
    SpaStampUnknownCause Cause = SpaStampUnknownCause.None)
{
    public string? WarningLine => State switch
    {
        SpaStampState.Mismatch =>
            $"Deployed SPA export reports version {ExportVersion} while this binary is {BinaryVersion}; " +
            "the served UI was built for a different release. Rebuild web/ and redeploy the export.",
        SpaStampState.Unknown =>
            $"Deployed SPA export version is unknown ({CausePhrase()}); it was never compared against this binary ({BinaryVersion}).",
        _ => null,
    };

    private string CausePhrase() => Cause switch
    {
        SpaStampUnknownCause.Empty => "grimoire-web.json is empty",
        SpaStampUnknownCause.Invalid => "grimoire-web.json has no readable productVersion string",
        SpaStampUnknownCause.Unreadable => "grimoire-web.json could not be opened",
        _ => "grimoire-web.json is absent",
    };
}

public static class SpaStampVerifier
{
    public const string StampFileName = "grimoire-web.json";

    public static string BinaryVersion { get; } = ReadBinaryVersion();

    public static SpaStampVerdict Read(string staticRootPath)
    {
        var stampPath = Path.Combine(staticRootPath, StampFileName);
        if (!File.Exists(stampPath))
        {
            return Unknown(SpaStampUnknownCause.Missing);
        }

        string text;
        try
        {
            text = File.ReadAllText(stampPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Unknown(SpaStampUnknownCause.Unreadable);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Unknown(SpaStampUnknownCause.Empty);
        }

        if (!TryReadProductVersion(text, out var exportVersion))
        {
            return Unknown(SpaStampUnknownCause.Invalid);
        }

        return exportVersion == BinaryVersion
            ? new SpaStampVerdict(SpaStampState.Match, exportVersion, BinaryVersion)
            : new SpaStampVerdict(SpaStampState.Mismatch, exportVersion, BinaryVersion);
    }

    private static SpaStampVerdict Unknown(SpaStampUnknownCause cause) =>
        new(SpaStampState.Unknown, BinaryVersion: BinaryVersion, Cause: cause);

    private static bool TryReadProductVersion(string text, out string exportVersion)
    {
        exportVersion = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("productVersion", out var product)
                || product.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(product.GetString()))
            {
                return false;
            }

            exportVersion = StripBuildMetadata(product.GetString()!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ReadBinaryVersion()
    {
        var assembly = typeof(SpaStampVerifier).Assembly;
        // The Api face reads its own stamp: ProductVersion lives inside Cli and the
        // dependency direction forbids Api -> Cli, so the normalization is duplicated here.
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return StripBuildMetadata(informational);
        }

        return assembly.GetName().Version is { } version ? version.ToString(3) : "0.0.0";
    }

    private static string StripBuildMetadata(string version)
    {
        var trimmed = version.Trim();
        var separator = trimmed.IndexOf('+');
        return separator < 0 ? trimmed : trimmed[..separator];
    }
}
