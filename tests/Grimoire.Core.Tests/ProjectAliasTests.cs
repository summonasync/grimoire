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

    [Theory]
    [InlineData("Ainora.Assistant", "ainora-assistant")]
    [InlineData("Slurp.AI", "slurp-ai")]
    [InlineData("Bob AI daily assistant", "bob-ai-daily-assistant")]
    [InlineData("feature--015_", "feature-015")]
    [InlineData("---", "project")]
    [InlineData("том", "project")]
    [InlineData("", "project")]
    public void Propose_SlugifiesTheSourceName(string source, string expected)
    {
        ProjectAlias.Propose(source, []).ShouldBe(expected);
    }

    [Fact]
    public void Propose_TakenAlias_AppendsCollisionSuffix()
    {
        ProjectAlias.Propose("Slurp.AI", ["slurp-ai", "slurp-ai-2", "other"]).ShouldBe("slurp-ai-3");
    }

    [Fact]
    public void Propose_LongSource_ClampsToMaxLengthWithSuffixRoom()
    {
        var alias = ProjectAlias.Propose(new string('a', ProjectAlias.MaxLength + 20), [new string('a', ProjectAlias.MaxLength - 4)]);

        ProjectAlias.IsValid(alias).ShouldBeTrue();
    }

    [Fact]
    public void Propose_LongSourceWithTakenClamp_KeepsTheAliasValid()
    {
        var stem = new string('b', ProjectAlias.MaxLength - 4);
        var alias = ProjectAlias.Propose(new string('b', ProjectAlias.MaxLength + 20), [stem]);

        ProjectAlias.IsValid(alias).ShouldBeTrue();
        alias.ShouldNotBe(stem);
    }
}
