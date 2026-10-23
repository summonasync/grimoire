using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class ProjectAliasTests
{
    [Theory]
    [InlineData("grimoire")]
    [InlineData("squeezr")]
    [InlineData("slurpy-ai")]
    [InlineData("summonasync")]
    [InlineData("feature-015")]
    [InlineData("a1")]
    public void IsValid_AcceptsKebabCaseSlugs(string alias)
    {
        ProjectAlias.IsValid(alias).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Grimoire")]
    [InlineData("slurpy_ai")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--dash")]
    [InlineData("spaced name")]
    [InlineData("том")]
    [InlineData("proj!")]
    public void IsValid_RejectsNonCanonicalAliases(string? alias)
    {
        ProjectAlias.IsValid(alias).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_RejectsAliasesOverMaxLength()
    {
        ProjectAlias.IsValid(new string('a', ProjectAlias.MaxLength + 1)).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_AcceptsAliasesAtMaxLength()
    {
        ProjectAlias.IsValid(new string('a', ProjectAlias.MaxLength)).ShouldBeTrue();
    }
}
