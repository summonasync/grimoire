using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class ActorClaimBlockParserTests
{
    [Fact]
    public void Parse_WithCompleteBlock_ReturnsClaim()
    {
        var document = """
            # Team instructions

            <!-- grimoire:actor:start -->
            alias: dan
            tome: platform-work
            <!-- grimoire:actor:end -->

            More prose below.
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
        claim.Tome.ShouldBe("platform-work");
    }

    [Fact]
    public void Parse_WithoutBlock_ReturnsNull()
    {
        ActorClaimBlockParser.Parse("# Just instructions\n\nDo the thing.\n").ShouldBeNull();
    }

    [Fact]
    public void Parse_WithUnterminatedBlock_ReturnsNull()
    {
        var document = """
            <!-- grimoire:actor:start -->
            alias: dan
            """;

        ActorClaimBlockParser.Parse(document).ShouldBeNull();
    }

    [Fact]
    public void Parse_WithAliasOnly_ReturnsPartialClaim()
    {
        var document = """
            <!-- grimoire:actor:start -->
            alias: dan
            <!-- grimoire:actor:end -->
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
        claim.Tome.ShouldBeNull();
    }

    [Fact]
    public void Parse_WithTomeOnly_ReturnsPartialClaim()
    {
        var document = """
            <!-- grimoire:actor:start -->
            tome: platform-work
            <!-- grimoire:actor:end -->
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBeNull();
        claim.Tome.ShouldBe("platform-work");
    }

    [Fact]
    public void Parse_WithEmptyValues_TreatsKeysAsAbsent()
    {
        var document = """
            <!-- grimoire:actor:start -->
            alias:
            tome:
            <!-- grimoire:actor:end -->
            """;

        ActorClaimBlockParser.Parse(document).ShouldBeNull();
    }

    [Fact]
    public void Parse_WithUnknownKeys_IgnoresThem()
    {
        var document = """
            <!-- grimoire:actor:start -->
            crew: platform
            alias: dan
            some prose line without meaning
            tome: platform-work
            <!-- grimoire:actor:end -->
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
        claim.Tome.ShouldBe("platform-work");
    }

    [Fact]
    public void Parse_WithAVendorLine_CarriesItIntoTheClaim()
    {
        var document = """
            <!-- grimoire:actor:start -->
            alias: dan
            vendor: claude
            tome: platform-work
            <!-- grimoire:actor:end -->
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
        claim.Vendor.ShouldBe("claude");
        claim.Tome.ShouldBe("platform-work");
    }

    [Fact]
    public void Parse_WithoutAVendorLine_LeavesTheVendorNull()
    {
        var document = """
            <!-- grimoire:actor:start -->
            alias: dan
            tome: platform-work
            <!-- grimoire:actor:end -->
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Vendor.ShouldBeNull();
    }

    [Fact]
    public void Parse_WithAnEmptyVendorLine_TreatsTheVendorAsAbsent()
    {
        var document = """
            <!-- grimoire:actor:start -->
            alias: dan
            vendor:
            <!-- grimoire:actor:end -->
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
        claim.Vendor.ShouldBeNull();
    }

    [Fact]
    public void Parse_WithRepeatedKeys_LastValueWins()
    {
        var document = """
            <!-- grimoire:actor:start -->
            alias: old
            alias: dan
            tome: platform-work
            <!-- grimoire:actor:end -->
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
    }

    [Fact]
    public void Parse_WithMultipleBlocks_ReadsTheFirstCompleteBlock()
    {
        var document = """
            <!-- grimoire:actor:start -->
            alias: dan
            tome: platform-work
            <!-- grimoire:actor:end -->

            <!-- grimoire:actor:start -->
            alias: other
            tome: other-tome
            <!-- grimoire:actor:end -->
            """;

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
    }

    [Fact]
    public void Parse_WithCarrigeReturnLineEndings_TrimsValues()
    {
        var document = "<!-- grimoire:actor:start -->\r\nalias: dan\r\ntome: platform-work\r\n<!-- grimoire:actor:end -->\r\n";

        var claim = ActorClaimBlockParser.Parse(document);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
        claim.Tome.ShouldBe("platform-work");
    }

    [Fact]
    public void Parse_BlockMarkers_AreTheDocumentedOnes()
    {
        ActorClaimBlockParser.StartMarker.ShouldBe("<!-- grimoire:actor:start -->");
        ActorClaimBlockParser.EndMarker.ShouldBe("<!-- grimoire:actor:end -->");
    }
}
