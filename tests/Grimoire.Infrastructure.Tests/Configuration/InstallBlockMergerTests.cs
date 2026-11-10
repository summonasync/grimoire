using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class InstallBlockMergerTests
{
    [Fact]
    public void Merge_EmptyDocument_ReturnsBlockWithTrailingNewline()
    {
        InstallBlockMerger.Merge(string.Empty, InstallBlock.Text).ShouldBe(InstallBlock.Text + "\n");
    }

    [Fact]
    public void Merge_FreshContent_AppendsBlockAfterABlankLine()
    {
        InstallBlockMerger.Merge("intro\n", InstallBlock.Text).ShouldBe($"intro\n\n{InstallBlock.Text}\n");
    }

    [Fact]
    public void Merge_ContentWithoutTrailingNewline_SeparatesWithABlankLine()
    {
        InstallBlockMerger.Merge("intro", InstallBlock.Text).ShouldBe($"intro\n\n{InstallBlock.Text}\n");
    }

    [Fact]
    public void Merge_ExistingBlock_ReplacesInPlaceAndKeepsSurroundings()
    {
        var stale = InstallBlock.Text.Replace("## Grimoire — agent memory", "## Stale heading", StringComparison.Ordinal);
        var document = $"head\n\n{stale}\n\nfoot\n";

        var merged = InstallBlockMerger.Merge(document, InstallBlock.Text);

        merged.ShouldBe($"head\n\n{InstallBlock.Text}\n\nfoot\n");
    }

    [Fact]
    public void Merge_AlreadyUpToDate_ReturnsIdenticalText()
    {
        var document = $"intro\n\n{InstallBlock.Text}\n";

        InstallBlockMerger.Merge(document, InstallBlock.Text).ShouldBe(document);
    }

    [Fact]
    public void Merge_StartMarkerWithoutEndMarker_AppendsAtTail()
    {
        var document = $"head\n{InstallBlock.BeginMarker}\n";

        InstallBlockMerger.Merge(document, InstallBlock.Text).ShouldBe($"head\n{InstallBlock.BeginMarker}\n\n{InstallBlock.Text}\n");
    }

    [Fact]
    public void Strip_RemovesBlockAndMarkersCleanly()
    {
        var document = $"head\n\n{InstallBlock.Text}\nfoot\n";

        InstallBlockMerger.Strip(document).ShouldBe("head\nfoot\n");
    }

    [Fact]
    public void Strip_DocumentWithoutBlock_ReturnsContentUnchanged()
    {
        const string document = "plain\n";

        InstallBlockMerger.Strip(document).ShouldBe(document);
    }

    [Fact]
    public void Strip_StartMarkerWithoutEndMarker_ReturnsContentUnchanged()
    {
        var document = $"head\n{InstallBlock.BeginMarker}\n";

        InstallBlockMerger.Strip(document).ShouldBe(document);
    }
}
