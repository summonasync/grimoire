namespace Grimoire.Core;

public static class ProjectSyncPlanner
{
    public static ProjectSyncPlan Plan(IReadOnlyList<ProjectAliasRecord> records, IReadOnlyList<BoardProject> board)
    {
        var byId = BoardById(board);
        var paths = ExistingPaths(records);
        var added = new List<ProjectAliasRecord>();
        var updated = new List<ProjectAliasRecord>();
        var goneAliases = new List<string>();
        var addedChanges = new List<ProjectSyncChange>();
        var updatedChanges = new List<ProjectSyncChange>();
        var goneChanges = new List<ProjectSyncChange>();
        var skippedChanges = new List<ProjectSyncChange>();

        foreach (var record in records)
        {
            if (record.BoardProjectId is not { } boardId)
            {
                continue;
            }

            if (byId.TryGetValue(boardId, out var project))
            {
                Refresh(record, project, updated, updatedChanges);
            }
            else
            {
                if (!record.BoardGone)
                {
                    goneAliases.Add(record.Alias);
                }

                goneChanges.Add(new ProjectSyncChange(
                    record.Alias,
                    $"board project {boardId} is gone; the alias and its memories stay - " +
                    $"remove it explicitly with 'grimoire projects remove {record.Alias}' if unwanted"));
            }
        }

        foreach (var project in board)
        {
            if (byId[project.Id] != project)
            {
                continue;
            }

            if (records.Any(record => record.BoardProjectId == project.Id))
            {
                continue;
            }

            Introduce(project, records, added, paths, addedChanges, skippedChanges);
        }

        return new ProjectSyncPlan
        {
            Added = added,
            Updated = updated,
            GoneAliases = goneAliases,
            Report = new ProjectSyncReport
            {
                Added = addedChanges,
                Updated = updatedChanges,
                Gone = goneChanges,
                Skipped = skippedChanges,
            },
        };
    }

    private static IReadOnlyDictionary<string, BoardProject> BoardById(IReadOnlyList<BoardProject> board) => board
        .GroupBy(project => project.Id, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static HashSet<string> ExistingPaths(IReadOnlyList<ProjectAliasRecord> records) => [..
        records.SelectMany(record => record.Paths)
            .Select(ProjectRegistryValidator.NormalizePath)];

    private static void Refresh(
        ProjectAliasRecord record,
        BoardProject project,
        ICollection<ProjectAliasRecord> updated,
        ICollection<ProjectSyncChange> changes)
    {
        var next = record;
        var details = new List<string>();
        if (record.DisplayName != project.Name)
        {
            details.Add(NameChange(record.DisplayName, project.Name));
            next = next with { DisplayName = project.Name };
        }

        if (record.Description != project.Description)
        {
            details.Add(ValueChange("description", record.Description, project.Description));
            next = next with { Description = project.Description };
        }

        if (record.BoardGone)
        {
            details.Add("board-gone flag cleared");
            next = next with { BoardGone = false };
        }

        if (details.Count == 0)
        {
            return;
        }

        updated.Add(next);
        changes.Add(new ProjectSyncChange(record.Alias, string.Join("; ", details)));
    }

    private static void Introduce(
        BoardProject project,
        IReadOnlyList<ProjectAliasRecord> records,
        ICollection<ProjectAliasRecord> added,
        ISet<string> paths,
        ICollection<ProjectSyncChange> changes,
        ICollection<ProjectSyncChange> skipped)
    {
        var subject = string.IsNullOrWhiteSpace(project.Name) ? project.Id : project.Name;
        if (string.IsNullOrWhiteSpace(project.DiskPath))
        {
            skipped.Add(new ProjectSyncChange(
                subject,
                $"board project {project.Id} has no disk path on the board - register it manually with " +
                $"'grimoire projects add <alias> <path> --board-project-id {project.Id}'"));
            return;
        }

        var path = ProjectRegistryValidator.NormalizePath(project.DiskPath);
        if (!paths.Add(path))
        {
            skipped.Add(new ProjectSyncChange(
                subject,
                $"board project {project.Id} carries path '{path}' which is already registered locally - " +
                "link it by hand with 'grimoire projects update <alias> --board-project-id " +
                $"{project.Id}' after sorting the paths out"));
            return;
        }

        var alias = ProjectAlias.Propose(subject, [.. records.Select(record => record.Alias), .. added.Select(record => record.Alias)]);
        added.Add(new ProjectAliasRecord
        {
            Alias = alias,
            Paths = [path],
            BoardProjectId = project.Id,
            DisplayName = project.Name,
            Description = project.Description,
        });
        changes.Add(new ProjectSyncChange(
            alias,
            $"board project {project.Id} ('{project.Name}') at {path}"));
    }

    private static string NameChange(string? before, string after) =>
        before is null ? $"display name set to '{after}'" : $"display name '{before}' -> '{after}'";

    private static string ValueChange(string label, string? before, string? after) => (before, after) switch
    {
        (null, { } next) => $"{label} set to '{next}'",
        ({ } previous, null) => $"{label} '{previous}' cleared",
        ({ } previous, { } next) => $"{label} '{previous}' -> '{next}'",
        _ => $"{label} cleared",
    };
}
