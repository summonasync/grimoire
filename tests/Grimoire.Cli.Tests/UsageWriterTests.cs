using Grimoire.Cli.CommandLine;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class UsageWriterTests
{
    [Fact]
    public void WriteGeneral_ListsEveryCatalogCommand()
    {
        var output = new StringWriter();

        UsageWriter.WriteGeneral(output);

        var text = output.ToString();
        foreach (var spec in CommandCatalog.All)
        {
            text.ShouldContain(spec.Name);
        }

        text.ShouldContain("6 upstream unavailable");
    }

    [Fact]
    public void WriteCommand_ListsFlagsWithDescriptions()
    {
        var output = new StringWriter();
        var spec = CommandCatalog.Find("remember")!;

        UsageWriter.WriteCommand(output, spec);

        var text = output.ToString();
        text.ShouldContain("remember <text>");
        text.ShouldContain("--ttl");
        text.ShouldContain("GRIMOIRE_ACTOR");
    }
}
