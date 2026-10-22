namespace Grimoire.Infrastructure.Autostart;

public interface IAutostartEntryWriter
{
    string EntryPath { get; }

    Task<AutostartEntryChange> EnableAsync(CancellationToken cancellationToken);

    Task<AutostartEntryChange> DisableAsync(CancellationToken cancellationToken);
}
