using System.Text.RegularExpressions;

namespace Grimoire.Mcp.Tests;

internal static partial class ReadmeSurface
{
    private const string SolutionFile = "Grimoire.sln";
    private const string ToolsHeading = "## MCP tools";

    [GeneratedRegex(@"^\|\s*`(?<name>[^`]+)`\s*\|")]
    private static partial Regex ToolRow();

    public static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFile)))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"No {SolutionFile} walking up from '{AppContext.BaseDirectory}' — the README guard has no repo to read.");
    }

    public static string ReadmeText()
    {
        var readme = Path.Combine(RepoRoot(), "README.md");
        if (!File.Exists(readme))
        {
            throw new FileNotFoundException(
                $"No README.md at '{readme}' — the guard refuses to grade a document it never opened.", readme);
        }

        return File.ReadAllText(readme);
    }

    public static string[] ToolTableNames(string readme)
    {
        var lines = readme.Replace("\r\n", "\n").Split('\n');
        var heading = Array.FindIndex(lines, line => line.Trim() == ToolsHeading);
        if (heading < 0)
        {
            throw new InvalidDataException($"README.md carries no '{ToolsHeading}' heading.");
        }

        var cursor = heading + 1;
        while (cursor < lines.Length && lines[cursor].Trim().Length == 0)
        {
            cursor++;
        }

        var rows = new List<string>();
        while (cursor < lines.Length && lines[cursor].StartsWith('|'))
        {
            rows.Add(lines[cursor]);
            cursor++;
        }

        if (rows.Count < 3)
        {
            throw new InvalidDataException(
                $"The '{ToolsHeading}' table holds {rows.Count} lines — header, separator and at least one row expected.");
        }

        if (!rows[0].Contains("Tool", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The first line under '{ToolsHeading}' is not the table header: {rows[0]}");
        }

        if (!IsSeparatorRow(rows[1]))
        {
            throw new InvalidDataException($"The second line under '{ToolsHeading}' is not a table separator: {rows[1]}");
        }

        var names = new string[rows.Count - 2];
        for (var i = 2; i < rows.Count; i++)
        {
            var match = ToolRow().Match(rows[i]);
            if (!match.Success)
            {
                throw new InvalidDataException(
                    $"Row {i - 1} of the '{ToolsHeading}' table does not name its tool in a backticked first column: {rows[i]}");
            }

            names[i - 2] = match.Groups["name"].Value;
        }

        return names;
    }

    private static bool IsSeparatorRow(string line)
    {
        return line.Contains('-') && line.All(character => character is '|' or '-' or ':' or ' ');
    }
}
