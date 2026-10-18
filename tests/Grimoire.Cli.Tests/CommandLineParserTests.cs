using Grimoire.Cli.CommandLine;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_CommandWithPositionalAndFlags_PreservesEverything()
    {
        var invocation = CommandLineParser.Parse(["remember", "deploy", "on", "friday", "--scope", "session", "--importance=0.9"]);

        invocation.Spec.Name.ShouldBe("remember");
        invocation.Positional.ShouldBe(["deploy", "on", "friday"]);
        invocation.Value("scope").ShouldBe("session");
        invocation.Value("importance").ShouldBe("0.9");
        invocation.JoinedPositional().ShouldBe("deploy on friday");
    }

    [Fact]
    public void Parse_UnknownCommand_ThrowsWithoutSpec()
    {
        var exception = Should.Throw<UsageException>(() => CommandLineParser.Parse(["frobnicate"]));

        exception.Spec.ShouldBeNull();
        exception.Message.ShouldContain("frobnicate");
    }

    [Fact]
    public void Parse_UnknownFlag_ThrowsWithCommandSpec()
    {
        var exception = Should.Throw<UsageException>(() => CommandLineParser.Parse(["list", "--wat", "1"]));

        exception.Spec.ShouldNotBeNull();
        exception.Spec!.Name.ShouldBe("list");
    }

    [Fact]
    public void Parse_MissingFlagValue_Throws()
    {
        Should.Throw<UsageException>(() => CommandLineParser.Parse(["recall", "query", "--limit"]));
    }

    [Fact]
    public void Parse_MissingPositional_Throws()
    {
        Should.Throw<UsageException>(() => CommandLineParser.Parse(["remember"]));
    }

    [Fact]
    public void Parse_NoArguments_Throws()
    {
        Should.Throw<UsageException>(() => CommandLineParser.Parse([]));
    }

    [Fact]
    public void Parse_ExactFlagNamesOnly()
    {
        Should.Throw<UsageException>(() => CommandLineParser.Parse(["list", "--Scope", "project"]));
    }
}
