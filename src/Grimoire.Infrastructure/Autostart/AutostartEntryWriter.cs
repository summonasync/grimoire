using System.Text;

namespace Grimoire.Infrastructure.Autostart;

public sealed class AutostartEntryWriter(string? entryPath = null, string? binaryPath = null) : IAutostartEntryWriter
{
    public const string FileName = "grimoire-ui.desktop";

    public static string DefaultEntryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "autostart",
        FileName);

    private readonly string? _binaryPath = binaryPath ?? Environment.ProcessPath;

    public string EntryPath { get; } = Path.GetFullPath(entryPath ?? DefaultEntryPath);

    public async Task<AutostartEntryChange> EnableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var desired = Encoding.UTF8.GetBytes(Render());
        var current = File.Exists(EntryPath) ? await File.ReadAllBytesAsync(EntryPath, cancellationToken) : null;

        if (current is not null && current.AsSpan().SequenceEqual(desired.AsSpan()))
        {
            return AutostartEntryChange.UpToDate;
        }

        var directory = Path.GetDirectoryName(EntryPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{FileName}.tmp");
        await File.WriteAllBytesAsync(tempPath, desired, cancellationToken);
        File.Move(tempPath, EntryPath, true);
        return AutostartEntryChange.Written;
    }

    public Task<AutostartEntryChange> DisableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(EntryPath))
        {
            return Task.FromResult(AutostartEntryChange.Absent);
        }

        File.Delete(EntryPath);
        return Task.FromResult(AutostartEntryChange.Removed);
    }

    // LF-forced and BOM-free: the desktop-entry bytes have to be stable across runs for the compare above to be honest.
    private string Render() => string.Join('\n',
        "[Desktop Entry]",
        "Type=Application",
        "Name=Grimoire",
        $"Exec={ExecTarget()} ui",
        "X-GNOME-Autostart-enabled=true") + "\n";

    private string ExecTarget()
    {
        var binary = Path.GetFullPath(_binaryPath
            ?? throw new InvalidOperationException("The running binary path is unavailable, so no login entry can name it."));

        // The Exec field splits on unquoted spaces, so a path carrying one survives only quoted.
        return binary.Contains(' ', StringComparison.Ordinal) ? $"\"{binary}\"" : binary;
    }
}
