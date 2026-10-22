using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class DistributionChannelTests
{
    [Fact]
    public void CliProject_PacksAsDotnetTool_GrimoireOnPath()
    {
        var tool = ReadCliProjectProperties();

        tool["PackAsTool"].ShouldBe("true");
        tool["ToolCommandName"].ShouldBe("grimoire");
    }

    [Fact]
    public void CliProject_OverridesTheSolutionPackDisable_WithoutForkingTheVersion()
    {
        var tool = ReadCliProjectProperties();
        tool["IsPackable"].ShouldBe("true");

        tool.ContainsKey("Version").ShouldBe(false,
            "package version must ride Directory.Build.props, a forked Version is a second version source");
    }

    [Fact]
    public void ToolCommandName_MatchesTheDetectionProbePrefix_ShellsOutFindsTheSameBinaryName()
    {
        var command = ReadCliProjectProperties()["ToolCommandName"];

        var probe = $"grimoire {ProductVersion.Text}";

        command.ShouldBe("grimoire");
        probe.ShouldStartWith(command);
    }

    private static Dictionary<string, string> ReadCliProjectProperties()
    {
        var csproj = FindRepositoryFile("src", "Grimoire.Cli", "Grimoire.Cli.csproj");
        var document = XDocument.Load(csproj);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.Descendants("PropertyGroup").SelectMany(group => group.Elements()))
        {
            values[property.Name.LocalName] = property.Value;
        }

        return values;
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Grimoire.sln")))
            {
                var path = Path.Combine([directory.FullName, .. segments]);
                if (File.Exists(path))
                {
                    return path;
                }

                break;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Repository file {string.Join('/', segments)} not found walking up from {AppContext.BaseDirectory}.");
    }
}
