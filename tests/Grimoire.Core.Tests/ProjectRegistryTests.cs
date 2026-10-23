using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class ProjectRegistryTests
{
    [Fact]
    public void List_ReturnsConfiguredRecords()
    {
        var grimoire = NewRecord("grimoire", "/home/user/grimoire");
        var squeezr = NewRecord("squeezr", "/home/user/squeezr");

        var registry = new ProjectRegistry([grimoire, squeezr]);

        registry.List().ShouldBe([grimoire, squeezr]);
    }

    [Fact]
    public void List_OnEmptyRegistry_ReturnsEmpty()
    {
        new ProjectRegistry([]).List().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ReturnsRecordForExactAlias()
    {
        var grimoire = NewRecord("grimoire", "/home/user/grimoire");

        var registry = new ProjectRegistry([grimoire]);

        registry.Validate("grimoire").ShouldBe(grimoire);
    }

    [Fact]
    public void Validate_IsCaseSensitive()
    {
        var registry = new ProjectRegistry([NewRecord("grimoire", "/home/user/grimoire")]);

        registry.Validate("Grimoire").ShouldBeNull();
    }

    [Fact]
    public void Validate_ReturnsNullForUnknownAlias()
    {
        var registry = new ProjectRegistry([NewRecord("grimoire", "/home/user/grimoire")]);

        registry.Validate("slurpy-ai").ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsMissingProjectIds(string? projectId)
    {
        var registry = new ProjectRegistry([NewRecord("grimoire", "/home/user/grimoire")]);

        registry.Validate(projectId).ShouldBeNull();
    }

    [Fact]
    public void ResolveByPath_MatchesExactRegisteredPath()
    {
        var grimoire = NewRecord("grimoire", "/home/user/grimoire");

        var registry = new ProjectRegistry([grimoire]);

        registry.ResolveByPath("/home/user/grimoire").ShouldBe(grimoire);
    }

    [Fact]
    public void ResolveByPath_MatchesSubdirectoryOfRegisteredPath()
    {
        var grimoire = NewRecord("grimoire", "/home/user/grimoire");

        var registry = new ProjectRegistry([grimoire]);

        registry.ResolveByPath("/home/user/grimoire/src/Grimoire.Core").ShouldBe(grimoire);
    }

    [Fact]
    public void ResolveByPath_PrefersLongestRegisteredPrefix()
    {
        var grimoire = NewRecord("grimoire", "/home/user/grimoire");
        var worktree = NewRecord("grimoire-worktree", "/home/user/grimoire/.claude/worktrees/015");
        var registry = new ProjectRegistry([grimoire, worktree]);

        registry.ResolveByPath("/home/user/grimoire/.claude/worktrees/015/src").ShouldBe(worktree);
        registry.ResolveByPath("/home/user/grimoire/src").ShouldBe(grimoire);
    }

    [Fact]
    public void ResolveByPath_DoesNotMatchSiblingPath()
    {
        var registry = new ProjectRegistry([NewRecord("grimoire", "/home/user/grimoire")]);

        registry.ResolveByPath("/home/user/grimoire-old").ShouldBeNull();
    }

    [Fact]
    public void ResolveByPath_IgnoresTrailingSeparator()
    {
        var grimoire = NewRecord("grimoire", "/home/user/grimoire/");

        var registry = new ProjectRegistry([grimoire]);

        registry.ResolveByPath("/home/user/grimoire/").ShouldBe(grimoire);
        registry.ResolveByPath("/home/user/grimoire/src").ShouldBe(grimoire);
    }

    [Fact]
    public void ResolveByPath_MatchesFilesystemRoot()
    {
        var everything = NewRecord("everything", "/");

        var registry = new ProjectRegistry([everything]);

        registry.ResolveByPath("/home/user/anywhere").ShouldBe(everything);
    }

    [Fact]
    public void ResolveByPath_ReturnsNullWhenNoPathMatches()
    {
        var registry = new ProjectRegistry([NewRecord("grimoire", "/home/user/grimoire")]);

        registry.ResolveByPath("/opt/other").ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveByPath_RejectsMissingPaths(string? path)
    {
        var registry = new ProjectRegistry([NewRecord("grimoire", "/home/user/grimoire")]);

        registry.ResolveByPath(path).ShouldBeNull();
    }

    [Fact]
    public void Constructor_ThrowsWithAllValidationErrors()
    {
        var invalid = NewRecord("Grimoire", "/home/user/grimoire");
        var first = NewRecord("grimoire", "/home/user/grimoire");
        var duplicate = NewRecord("grimoire", "/home/user/grimoire-fork");

        var exception = Should.Throw<ProjectRegistryValidationException>(
            () => new ProjectRegistry([invalid, first, duplicate]));

        exception.Message.ShouldContain("'Grimoire'");
        exception.Message.ShouldContain("alias 'grimoire' is already registered");
        exception.Message.ShouldContain("projects[2]");
    }

    private static ProjectAliasRecord NewRecord(string alias, params string[] paths) => new()
    {
        Alias = alias,
        Paths = paths,
    };
}
