using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ProjectsCommand(CliEnvironment environment)
{
    public Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IProjectRegistryStore>();
        return Task.FromResult(invocation.Positional.FirstOrDefault() switch
        {
            null or "list" => List(store),
            "add" => Add(store, invocation),
            "update" => Update(store, invocation),
            "remove" => Remove(store, invocation),
            var action => throw new UsageException(
                $"Unknown projects action '{action}' (expected: list, add, update, remove).", invocation.Spec),
        });
    }

    private int List(IProjectRegistryStore store)
    {
        var records = store.Read();
        if (records.Count == 0)
        {
            environment.Output.WriteLine("No projects registered.");
            return ExitCodes.Ok;
        }

        var aliasWidth = records.Max(record => record.Alias.Length);
        var boardWidth = Math.Max("board".Length, records.Max(record => Optional(record.BoardProjectId).Length));
        var nameWidth = Math.Max("display name".Length, records.Max(record => Optional(record.DisplayName).Length));
        environment.Output.WriteLine(
            $"{"alias".PadRight(aliasWidth)}  {"board".PadRight(boardWidth)}  {"display name".PadRight(nameWidth)}  paths");
        foreach (var record in records)
        {
            environment.Output.WriteLine(
                $"{record.Alias.PadRight(aliasWidth)}  {Optional(record.BoardProjectId).PadRight(boardWidth)}  " +
                $"{Optional(record.DisplayName).PadRight(nameWidth)}  {string.Join(", ", record.Paths)}");
        }

        return ExitCodes.Ok;
    }

    private int Add(IProjectRegistryStore store, CliInvocation invocation)
    {
        if (invocation.Positional.Count < 3)
        {
            throw new UsageException("Usage: grimoire projects add <alias> <path>...", invocation.Spec);
        }

        var alias = invocation.Positional[1];
        if (!ProjectAlias.IsValid(alias))
        {
            throw new UsageException(
                $"Invalid project alias '{alias}': use lowercase kebab-case (a-z, 0-9, single dashes, at most {ProjectAlias.MaxLength} characters).",
                invocation.Spec);
        }

        var paths = invocation.Positional.Skip(2).ToArray();
        EnsureAbsolutePaths(invocation.Spec, paths);
        var record = new ProjectAliasRecord
        {
            Alias = alias,
            Paths = paths,
            BoardProjectId = TrimToNull(invocation.Value("board-project-id")),
            DisplayName = TrimToNull(invocation.Value("display-name")),
        };

        return Mutate(store, records =>
        {
            if (records.Any(existing => existing.Alias.Equals(alias, StringComparison.Ordinal)))
            {
                throw new CommandException("config", $"Project alias '{alias}' is already registered.", ExitCodes.Config);
            }

            records.Add(record);
            return $"Added project '{alias}' ({DescribePaths(record.Paths)}{DescribeOptional(record)})";
        });
    }

    private int Update(IProjectRegistryStore store, CliInvocation invocation)
    {
        if (invocation.Positional.Count < 2)
        {
            throw new UsageException(
                "Usage: grimoire projects update <alias> [path...] [--display-name <text>] [--board-project-id <id>]",
                invocation.Spec);
        }

        var alias = invocation.Positional[1];
        var paths = invocation.Positional.Skip(2).ToArray();
        var displayName = invocation.Value("display-name");
        var boardProjectId = invocation.Value("board-project-id");
        if (paths.Length == 0 && displayName is null && boardProjectId is null)
        {
            throw new UsageException(
                "Nothing to update: pass replacement paths, --display-name or --board-project-id.", invocation.Spec);
        }

        EnsureAbsolutePaths(invocation.Spec, paths);
        return Mutate(store, records =>
        {
            var index = records.FindIndex(record => record.Alias.Equals(alias, StringComparison.Ordinal));
            if (index < 0)
            {
                throw new CommandException("not_found", $"No registered project '{alias}'.", ExitCodes.NotFound);
            }

            var current = records[index];
            var changes = new List<string>();
            var updated = current;
            if (paths.Length > 0 && !paths.SequenceEqual(current.Paths))
            {
                changes.Add(PathChange(current.Paths, paths));
                updated = updated with { Paths = paths };
            }

            if (displayName is not null)
            {
                var nextDisplayName = TrimToNull(displayName);
                if (nextDisplayName != current.DisplayName)
                {
                    changes.Add(ValueChange("display name", current.DisplayName, nextDisplayName));
                    updated = updated with { DisplayName = nextDisplayName };
                }
            }

            if (boardProjectId is not null)
            {
                var nextBoardId = TrimToNull(boardProjectId);
                if (nextBoardId != current.BoardProjectId)
                {
                    changes.Add(ValueChange("board project", current.BoardProjectId, nextBoardId));
                    updated = updated with { BoardProjectId = nextBoardId };
                }
            }

            records[index] = updated;
            var delta = changes.Count == 0 ? "no changes" : string.Join("; ", changes);
            return $"Updated project '{alias}': {delta}";
        });
    }

    private int Remove(IProjectRegistryStore store, CliInvocation invocation)
    {
        if (invocation.Positional.Count < 2)
        {
            throw new UsageException("Usage: grimoire projects remove <alias>", invocation.Spec);
        }

        var alias = invocation.Positional[1];
        return Mutate(store, records =>
        {
            var index = records.FindIndex(record => record.Alias.Equals(alias, StringComparison.Ordinal));
            if (index < 0)
            {
                throw new CommandException("not_found", $"No registered project '{alias}'.", ExitCodes.NotFound);
            }

            var removed = records[index];
            records.RemoveAt(index);
            return $"Removed project '{alias}' ({DescribePaths(removed.Paths)})";
        });
    }

    private int Mutate(IProjectRegistryStore store, Func<List<ProjectAliasRecord>, string> mutate)
    {
        string report;
        try
        {
            report = store.Mutate(mutate);
        }
        catch (ProjectRegistryMutationException ex)
        {
            throw new CommandException("config", ex.Message, ExitCodes.Config);
        }
        catch (ConfigException ex)
        {
            throw new CommandException("config", ex.Message, ExitCodes.Config);
        }

        environment.Output.WriteLine(report);
        return ExitCodes.Ok;
    }

    private static void EnsureAbsolutePaths(CommandSpec spec, IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                throw new UsageException($"Project path '{path}' must be an absolute path.", spec);
            }
        }
    }

    private static string DescribePaths(IReadOnlyList<string> paths) =>
        paths.Count == 0 ? "no paths" : $"paths: {string.Join(", ", paths)}";

    private static string DescribeOptional(ProjectAliasRecord record)
    {
        var parts = new List<string>();
        if (record.BoardProjectId is { } board)
        {
            parts.Add($"board: {board}");
        }

        if (record.DisplayName is { } name)
        {
            parts.Add($"name: '{name}'");
        }

        return parts.Count == 0 ? string.Empty : $"; {string.Join(", ", parts)}";
    }

    private static string PathChange(IReadOnlyList<string> before, IReadOnlyList<string> after) =>
        before.Count == 0 ? $"paths set to {string.Join(", ", after)}" : $"paths: {string.Join(", ", before)} -> {string.Join(", ", after)}";

    private static string ValueChange(string label, string? before, string? after) => (before, after) switch
    {
        (null, { } next) => $"{label} set to '{next}'",
        ({ } previous, null) => $"{label} '{previous}' cleared",
        ({ } previous, { } next) => $"{label} '{previous}' -> '{next}'",
        _ => $"{label} cleared",
    };

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Optional(string? value) => value ?? "-";
}
