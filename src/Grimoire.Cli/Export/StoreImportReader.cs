using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Grimoire.Cli.Export;

internal sealed class StoreImportReader(TextReader reader)
{
    private int _lineNumber;

    public async Task<ExportHeader> ReadHeaderAsync(CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken);

        if (line is null)
        {
            throw new CommandException("export", "The export file holds no header line.", ExitCodes.Runtime);
        }

        ExportHeader? header;

        try
        {
            header = JsonSerializer.Deserialize<ExportHeader>(line, ExportFormat.Json);
        }
        catch (JsonException ex)
        {
            throw new CommandException("export", $"Export header (line 1) is not valid JSON: {ex.Message}", ExitCodes.Runtime);
        }

        if (header is null)
        {
            throw new CommandException("export", "Export header (line 1) is empty.", ExitCodes.Runtime);
        }

        if (header.FormatVersion != ExportFormat.Version)
        {
            throw new CommandException(
                "export",
                $"Unsupported export format version {header.FormatVersion}; this build reads version {ExportFormat.Version}.",
                ExitCodes.Runtime);
        }

        return header;
    }

    public async IAsyncEnumerable<ExportEntry> ReadEntriesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _lineNumber = 1;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            _lineNumber++;

            if (line.Length == 0)
            {
                continue;
            }

            ExportEntry? entry;

            try
            {
                entry = JsonSerializer.Deserialize<ExportEntry>(line, ExportFormat.Json);
            }
            catch (JsonException ex)
            {
                throw new CommandException("export", $"Export line {_lineNumber} is not a valid record: {ex.Message}", ExitCodes.Runtime);
            }

            if (entry?.Record is null)
            {
                throw new CommandException("export", $"Export line {_lineNumber} carries no record.", ExitCodes.Runtime);
            }

            yield return entry;
        }
    }

    public static async Task<int> CountEntriesAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var afterHeader = false;
        var count = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!afterHeader)
            {
                afterHeader = true;
                continue;
            }

            if (line.Length > 0)
            {
                count++;
            }
        }

        return count;
    }
}
