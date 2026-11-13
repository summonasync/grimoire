using Grimoire.Api;
using Grimoire.Mcp;
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
}
