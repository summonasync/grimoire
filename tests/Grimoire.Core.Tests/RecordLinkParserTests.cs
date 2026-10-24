using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class RecordLinkParserTests
{
    [Fact]
    public void Parse_ValidToken_ReturnsTargetId()
    {
        var target = Guid.NewGuid();

        RecordLinkParser.Parse($"see [[{target}]] for the details").ShouldBe([target]);
    }

    [Fact]
    public void Parse_MultipleTokens_ReturnsEveryTargetInOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        RecordLinkParser.Parse($"[[{first}]] then [[{second}]]").ShouldBe([first, second]);
    }

    [Fact]
    public void Parse_MalformedToken_ReturnsNothing()
    {
        RecordLinkParser.Parse("see [[not-a-guid]] for the details").ShouldBeEmpty();
    }

    [Fact]
    public void Parse_UnclosedToken_ReturnsNothing()
    {
        RecordLinkParser.Parse($"see [[{Guid.NewGuid()}").ShouldBeEmpty();
    }

    [Fact]
    public void Parse_PlainContent_ReturnsNothing()
    {
        RecordLinkParser.Parse("no links in here").ShouldBeEmpty();
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsNothing()
    {
        RecordLinkParser.Parse(string.Empty).ShouldBeEmpty();
        RecordLinkParser.Parse(null).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_RepeatedToken_ReturnsEachOccurrence()
    {
        var target = Guid.NewGuid();

        RecordLinkParser.Parse($"[[{target}]] and again [[{target}]]").ShouldBe([target, target]);
    }

    [Fact]
    public void ParseReason_BareId_ReturnsId()
    {
        var restored = Guid.NewGuid();

        RecordLinkParser.ParseReason($"restored {restored}").ShouldBe([restored]);
    }

    [Fact]
    public void ParseReason_BracketedId_ReturnsId()
    {
        var target = Guid.NewGuid();

        RecordLinkParser.ParseReason($"superseded by [[{target}]]").ShouldBe([target]);
    }

    [Fact]
    public void ParseReason_MultipleIds_ReturnsBoth()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        RecordLinkParser.ParseReason($"restored {first}, was superseded by {second}")
            .ShouldBe([first, second]);
    }

    [Fact]
    public void ParseReason_FreeTextWithoutIds_ReturnsNothing()
    {
        RecordLinkParser.ParseReason("stale deployment notes").ShouldBeEmpty();
    }

    [Fact]
    public void ParseReason_EmptyReason_ReturnsNothing()
    {
        RecordLinkParser.ParseReason(null).ShouldBeEmpty();
        RecordLinkParser.ParseReason(string.Empty).ShouldBeEmpty();
    }
}
