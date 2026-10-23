using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class ProjectsTool(IProjectRegistryStore registry)
{
    public const string Name = "projects";

    public const string Title = "Project registry";

    public const string Description =
        "Read and update the project registry: owner config that maps kebab-case aliases to absolute workspace " +
        "paths (optional boardProjectId and displayName ride along) - the lookup behind project tags on memory " +
        "writes. One strict-case action enum: List, Add, Update, Remove; a wrong or misspelled action is rejected " +
        "with the allowed set spelled out. Add needs an alias plus at least one absolute path; Update replaces the " +
        "whole path list and sets or clears the optional fields; Remove is explicit only. Rare door: the registry " +
        "is owner config - confirm with the human before changing it, and a running MCP server picks up registry " +
        "edits on its next start.";

    public Task<CallToolResult> Projects(
        [Description("The registry operation: List, Add, Update or Remove, passed exactly in this casing.")]
        [AllowedValues("List", "Add", "Update", "Remove")]
        string action,
        [Description("Kebab-case alias (a-z, 0-9, single dashes): the record to add, update or remove.")]
        string? alias = null,
        [Description("Absolute paths for Add, or the replacement path list for Update (whole-list replace).")]
        string[]? paths = null,
        [Description("Optional board project id on Add or Update; an empty string clears it on Update.")]
        string? boardProjectId = null,
        [Description("Optional display name on Add or Update; an empty string clears it on Update.")]
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var (parsedAction, actionError) = EnumParams.ParseRequired<ProjectAction>(action, nameof(action));
        if (actionError is not null)
        {
            return Task.FromResult(ToolResults.Failure(actionError));
        }

        return parsedAction switch
        {
            ProjectAction.List => List(alias, paths, boardProjectId, displayName, cancellationToken),
            ProjectAction.Add => Add(alias, paths, boardProjectId, displayName, cancellationToken),
            ProjectAction.Update => Update(alias, paths, boardProjectId, displayName, cancellationToken),
            _ => Remove(alias, cancellationToken),
        };
    }

    private Task<CallToolResult> List(
        string? alias,
        string[]? paths,
        string? boardProjectId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        if (ExtraArguments(alias, paths, boardProjectId, displayName) is { } extra)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ParamConflict,
                $"action List takes no other arguments: drop {string.Join(", ", extra)}.",
                extra[0])));
        }

        return ToolResults.ExecuteAsync(ct =>
        {
            var records = registry.Read();
            return Task.FromResult(ToolResults.Success(new ProjectsView
            {
                Projects = [.. records.Select(ToEntry)],
                Count = records.Count,
            }));
        }, cancellationToken);
    }

    private Task<CallToolResult> Add(
        string? alias,
        string[]? paths,
        string? boardProjectId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return Task.FromResult(ToolResults.Failure(Required(nameof(alias))));
        }

        if (!ProjectAlias.IsValid(alias))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"alias: '{alias}' is invalid: use lowercase kebab-case (a-z, 0-9, single dashes, at most {ProjectAlias.MaxLength} characters).",
                nameof(alias)).NothingApplied()));
        }

        if (paths is null || paths.Length == 0)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "paths: Add needs at least one absolute workspace path.",
                nameof(paths)).NothingApplied()));
        }

        if (EnsureAbsolute(paths) is { } pathError)
        {
            return Task.FromResult(ToolResults.Failure(pathError));
        }

        var created = new ProjectAliasRecord
        {
            Alias = alias,
            Paths = paths,
            BoardProjectId = TrimToNull(boardProjectId),
            DisplayName = TrimToNull(displayName),
        };

        return ToolResults.ExecuteAsync(ct => Task.FromResult(Mutate(records =>
        {
            if (records.Any(record => record.Alias.Equals(alias, StringComparison.Ordinal)))
            {
                throw new ProjectRegistryMutationException($"Project alias '{alias}' is already registered.");
            }

            records.Add(created);
            return $"Added project '{alias}'.";
        }, nameof(ProjectAction.Add), () => created)), cancellationToken);
    }

    private Task<CallToolResult> Update(
        string? alias,
        string[]? paths,
        string? boardProjectId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return Task.FromResult(ToolResults.Failure(Required(nameof(alias))));
        }

        if (paths is not null && paths.Length == 0)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "paths: pass at least one absolute path, or omit the argument to keep the current list.",
                nameof(paths)).NothingApplied()));
        }

        if (paths is null && boardProjectId is null && displayName is null)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "Nothing to update: pass replacement paths, displayName or boardProjectId.",
                nameof(paths)).NothingApplied()));
        }

        if (paths is not null && EnsureAbsolute(paths) is { } pathError)
        {
            return Task.FromResult(ToolResults.Failure(pathError));
        }

        ProjectAliasRecord updated = null!;
        return ToolResults.ExecuteAsync(ct => Task.FromResult(Mutate(records =>
        {
            var index = FindRecord(records, alias);
            var next = records[index];
            if (paths is not null)
            {
                next = next with { Paths = paths };
            }

            if (displayName is not null)
            {
                next = next with { DisplayName = TrimToNull(displayName) };
            }

            if (boardProjectId is not null)
            {
                next = next with { BoardProjectId = TrimToNull(boardProjectId) };
            }

            records[index] = next;
            updated = next;
            return $"Updated project '{alias}'.";
        }, nameof(ProjectAction.Update), () => updated)), cancellationToken);
    }

    private Task<CallToolResult> Remove(string? alias, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return Task.FromResult(ToolResults.Failure(Required(nameof(alias))));
        }

        ProjectAliasRecord removed = null!;
        return ToolResults.ExecuteAsync(ct => Task.FromResult(Mutate(records =>
        {
            var index = FindRecord(records, alias);
            removed = records[index];
            records.RemoveAt(index);
            return $"Removed project '{alias}'.";
        }, nameof(ProjectAction.Remove), () => removed)), cancellationToken);
    }

    private CallToolResult Mutate(
        Func<List<ProjectAliasRecord>, string> mutate,
        string action,
        Func<ProjectAliasRecord> outcome)
    {
        registry.Mutate(mutate);
        return ToolResults.Success(new ProjectChangeView { Action = action, Project = ToEntry(outcome()) });
    }

    private static int FindRecord(List<ProjectAliasRecord> records, string alias)
    {
        var index = records.FindIndex(record => record.Alias.Equals(alias, StringComparison.Ordinal));
        return index >= 0
            ? index
            : throw new ProjectRegistryMutationException($"No registered project '{alias}'.");
    }

    private static ToolError Required(string param) =>
        new ToolError(
            ToolErrorCodes.InvalidRequest,
            $"{param} is required for this action.",
            param).NothingApplied();

    private static ToolError? EnsureAbsolute(string[] paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                return new ToolError(
                    ToolErrorCodes.InvalidRequest,
                    $"paths: '{path}' is not an absolute path; the registry stores absolute workspace paths only.",
                    nameof(paths));
            }
        }

        return null;
    }

    private static string[]? ExtraArguments(
        string? alias,
        string[]? paths,
        string? boardProjectId,
        string? displayName)
    {
        List<string>? extra = null;
        if (alias is not null)
        {
            (extra ??= []).Add(nameof(alias));
        }

        if (paths is not null)
        {
            (extra ??= []).Add(nameof(paths));
        }

        if (boardProjectId is not null)
        {
            (extra ??= []).Add(nameof(boardProjectId));
        }

        if (displayName is not null)
        {
            (extra ??= []).Add(nameof(displayName));
        }

        return extra?.ToArray();
    }

    private static ProjectEntryView ToEntry(ProjectAliasRecord record) => new()
    {
        Alias = record.Alias,
        Paths = record.Paths,
        BoardProjectId = record.BoardProjectId,
        DisplayName = record.DisplayName,
    };

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
