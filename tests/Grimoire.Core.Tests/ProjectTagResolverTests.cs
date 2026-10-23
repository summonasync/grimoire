using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class ProjectTagResolverTests
{
    private const string CallerPath = "/home/user/worktrees/grimoire";

    private readonly IProjectRegistry _registry =
        new ProjectRegistry(
        [
            new ProjectAliasRecord { Alias = "grimoire", Paths = ["/home/user/grimoire", "/home/user/worktrees/grimoire"] },
            new ProjectAliasRecord { Alias = "squeezr", Paths = ["/home/user/squeezr"] },
        ]);

    [Fact]
    public void Resolve_ProjectWithRegisteredAlias_ReturnsItWithoutAutotag()
    {
        var resolution = new ProjectTagResolver(_registry).Resolve(MemoryScope.Project, "squeezr", CallerPath);

        resolution.Rejection.ShouldBeNull();
        resolution.AutoTagged.ShouldBeFalse();
        resolution.ProjectId.ShouldBe("squeezr");
    }

    [Theory]
    [InlineData("Grimoire")]
    [InlineData("squeez")]
    [InlineData("slurpy-ai")]
    public void Resolve_ProjectWithUnregisteredAlias_RejectsUnknownWithTheRegisteredSet(string projectId)
    {
        var resolution = new ProjectTagResolver(_registry).Resolve(MemoryScope.Project, projectId, CallerPath);

        resolution.Rejection.ShouldBe(ProjectTagRejection.Unknown);
        resolution.ProjectId.ShouldBeNull();
        resolution.AllowedAliases.ShouldBe(["grimoire", "squeezr"]);
    }

    [Fact]
    public void Resolve_ProjectWithoutAlias_AutotagsFromTheCallerPath()
    {
        var resolution = new ProjectTagResolver(_registry).Resolve(MemoryScope.Project, null, CallerPath);

        resolution.Rejection.ShouldBeNull();
        resolution.AutoTagged.ShouldBeTrue();
        resolution.ProjectId.ShouldBe("grimoire");
    }

    [Fact]
    public void Resolve_ProjectWithoutAliasAndNoPathMatch_RejectsMissingWithTheRegisteredSet()
    {
        var resolution = new ProjectTagResolver(_registry).Resolve(MemoryScope.Project, null, "/opt/somewhere-else");

        resolution.Rejection.ShouldBe(ProjectTagRejection.Missing);
        resolution.ProjectId.ShouldBeNull();
        resolution.AutoTagged.ShouldBeFalse();
        resolution.AllowedAliases.ShouldBe(["grimoire", "squeezr"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Resolve_ProjectWithBlankAlias_TakesTheAutotagPath(string? projectId)
    {
        var resolution = new ProjectTagResolver(_registry).Resolve(MemoryScope.Project, projectId, CallerPath);

        resolution.Rejection.ShouldBeNull();
        resolution.AutoTagged.ShouldBeTrue();
        resolution.ProjectId.ShouldBe("grimoire");
    }

    [Fact]
    public void Resolve_LongTermScopeWithoutAlias_StaysNull()
    {
        foreach (var scope in new[] { MemoryScope.Global, MemoryScope.Personal })
        {
            var resolution = new ProjectTagResolver(_registry).Resolve(scope, null, CallerPath);

            resolution.Rejection.ShouldBeNull();
            resolution.ProjectId.ShouldBeNull();
            resolution.AutoTagged.ShouldBeFalse();
            resolution.AllowedAliases.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(MemoryScope.Global)]
    [InlineData(MemoryScope.Personal)]
    public void Resolve_LongTermScopeWithRegisteredAlias_KeepsItAsProvenance(MemoryScope scope)
    {
        var resolution = new ProjectTagResolver(_registry).Resolve(scope, "squeezr", CallerPath);

        resolution.Rejection.ShouldBeNull();
        resolution.ProjectId.ShouldBe("squeezr");
    }

    [Theory]
    [InlineData(MemoryScope.Global)]
    [InlineData(MemoryScope.Personal)]
    public void Resolve_LongTermScopeWithUnregisteredAlias_RejectsUnknown(MemoryScope scope)
    {
        var resolution = new ProjectTagResolver(_registry).Resolve(scope, "Squeezr", CallerPath);

        resolution.Rejection.ShouldBe(ProjectTagRejection.Unknown);
        resolution.AllowedAliases.ShouldBe(["grimoire", "squeezr"]);
    }

    [Fact]
    public void Resolve_EmptyRegistry_ProjectWithoutAlias_RejectsMissingWithoutAliases()
    {
        var resolution = new ProjectTagResolver(new ProjectRegistry([])).Resolve(MemoryScope.Project, null, CallerPath);

        resolution.Rejection.ShouldBe(ProjectTagRejection.Missing);
        resolution.AllowedAliases.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_EmptyRegistry_LongTermScopeWithAlias_RejectsUnknown()
    {
        var resolution = new ProjectTagResolver(new ProjectRegistry([])).Resolve(MemoryScope.Global, "grimoire", CallerPath);

        resolution.Rejection.ShouldBe(ProjectTagRejection.Unknown);
        resolution.AllowedAliases.ShouldBeEmpty();
    }
}
