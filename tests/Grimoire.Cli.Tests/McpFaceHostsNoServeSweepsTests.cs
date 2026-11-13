using System.Xml.Linq;
using Grimoire.Api;
using Grimoire.Mcp;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class McpFaceHostsNoServeSweepsTests
{
    [Fact]
    public void McpFace_CannotSeeTheRetentionSweep_ItDoesNotEvenLoadTheApiAssembly()
    {
        // The sweep type and its AddHostedService registration both live in Grimoire.Api, which
        // only the serve command builds. An assembly reference is the load boundary: without it
        // the mcp child cannot run a prune, whatever its configuration or clock says.
        typeof(SessionRetentionSweeper).Assembly.ShouldBe(typeof(ApiHostBuilder).Assembly);

        typeof(McpHost).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ShouldNotContain("Grimoire.Api");
    }

    [Fact]
    public void McpFace_CannotReferenceTheApiProject_ItsCsprojDeclaresNoSuchReference()
    {
        // GetReferencedAssemblies reads IL, and the compiler omits an assembly reference no code
        // uses — a bare <ProjectReference> to Grimoire.Api leaves the IL assertion green while
        // the child could reach the sweep the moment anyone writes a line against it. The project
        // file is the boundary that a reference is actually added at, so it is pinned where it is
        // declared.
        var document = XDocument.Load(FindRepositoryFile("src", "Grimoire.Mcp", "Grimoire.Mcp.csproj"));

        ReferencedProjectFiles(document).ShouldNotContain("Grimoire.Api.csproj");
    }

    [Fact]
    public void McpFace_CannotRunASweepOfItsOwn_NoHostedServiceTypeInItsAssembly()
    {
        // A prune loop copied into Grimoire.Mcp would never reference Grimoire.Api and would
        // therefore pass both reference arms above. What makes it a sweep is that the host runs
        // it: no hosted-service type in the child's own assembly means no loop the mcp face can
        // start, whatever its code does.
        var hosted = typeof(McpHost).Assembly.GetTypes()
            .Where(type => typeof(IHostedService).IsAssignableFrom(type) || typeof(BackgroundService).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .ToArray();

        hosted.ShouldBeEmpty();
    }

    private static IReadOnlyList<string> ReferencedProjectFiles(XDocument document) =>
        document.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty)
            .Select(include => Path.GetFileName(include.Replace('\\', '/')))
            .ToArray();

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
