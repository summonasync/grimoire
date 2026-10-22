namespace Grimoire.Mcp;

public enum BootReason
{
    UnknownEnvKey,
    ConfigInvalid,
    DependencyUnresolved,
    TransportUnavailable,
    HostFault,
}

public static class BootMarkers
{
    public const string MountOkPrefix = "mount_ok";
    public const string MountFailPrefix = "mount_fail:";

    private static readonly (BootReason Reason, string Code)[] ClosedSet =
    [
        (BootReason.UnknownEnvKey, "unknown_env_key"),
        (BootReason.ConfigInvalid, "config_invalid"),
        (BootReason.DependencyUnresolved, "dependency_unresolved"),
        (BootReason.TransportUnavailable, "transport_unavailable"),
        (BootReason.HostFault, "host_fault"),
    ];

    public static IReadOnlyList<string> Codes { get; } = ClosedSet.Select(entry => entry.Code).ToList();

    public static string CodeOf(BootReason reason) =>
        ClosedSet.First(entry => entry.Reason == reason).Code;

    public static BootReason ReasonOf(string code)
    {
        foreach (var (reason, value) in ClosedSet)
        {
            if (value == code)
            {
                return reason;
            }
        }

        throw new ArgumentException(
            $"'{code}' is not a mount_fail reason code. Known codes: {string.Join(", ", Codes)}.", nameof(code));
    }

    public static string MountOkLine(string serverName, string version) =>
        $"{MountOkPrefix} {serverName}/{version}";

    public static string MountFailLine(BootReason reason, string detail) =>
        $"{MountFailPrefix}{CodeOf(reason)} {Flatten(detail)}";

    public static BootReason ClassifyConfigException(string message) =>
        message.Contains("does not match any configuration property", StringComparison.Ordinal) ||
        message.Contains("carries no property path", StringComparison.Ordinal) ||
        message.Contains("which is not a section", StringComparison.Ordinal)
            ? BootReason.UnknownEnvKey
            : BootReason.ConfigInvalid;

    public static BootReason ClassifyHostException(Exception exception) => exception switch
    {
        ObjectDisposedException => BootReason.TransportUnavailable,
        IOException => BootReason.TransportUnavailable,
        InvalidOperationException => BootReason.DependencyUnresolved,
        _ => BootReason.HostFault,
    };

    private static string Flatten(string detail) => detail
        .Replace('\r', ' ')
        .Replace('\n', ' ');
}
