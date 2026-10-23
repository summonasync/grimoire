using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class ProjectRegistryValidatorTests
{
    [Fact]
    public void Validate_AcceptsEmptyRegistry()
    {
        ProjectRegistryValidator.Validate([]).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_AcceptsFullRecord()
    {
        var record = new ProjectAliasRecord
        {
            Alias = "grimoire",
            Paths = ["/home/user/grimoire"],
            BoardProjectId = "7450aeab-137e-446d-88ed-5ffc9b624a1b",
            DisplayName = "Grimoire",
        };

        ProjectRegistryValidator.Validate([record]).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ReportsEmptyAlias()
    {
        var errors = ProjectRegistryValidator.Validate([NewRecord("")]);

        errors.ShouldHaveSingleItem().ShouldContain("alias must not be empty");
    }

    [Fact]
    public void Validate_ReportsNonKebabAlias()
    {
        var errors = ProjectRegistryValidator.Validate([NewRecord("Slurpy.AI")]);

        errors.ShouldHaveSingleItem().ShouldContain("must be lowercase kebab-case");
    }

    [Fact]
    public void Validate_ReportsDuplicateAliases()
    {
        var errors = ProjectRegistryValidator.Validate([NewRecord("grimoire"), NewRecord("grimoire")]);

        errors.ShouldHaveSingleItem().ShouldContain("alias 'grimoire' is already registered");
    }

    [Fact]
    public void Validate_ReportsDuplicateBoardProjectIds()
    {
        var first = NewRecord("grimoire");
        first = first with { BoardProjectId = "11111111-1111-1111-1111-111111111111" };
        var second = NewRecord("squeezr");
        second = second with { BoardProjectId = "11111111-1111-1111-1111-111111111111" };

        var errors = ProjectRegistryValidator.Validate([first, second]);

        errors.ShouldHaveSingleItem().ShouldContain("boardProjectId '11111111-1111-1111-1111-111111111111' is already registered");
    }

    [Fact]
    public void Validate_ReportsDuplicatePathsAcrossRecords()
    {
        var errors = ProjectRegistryValidator.Validate(
            [NewRecord("grimoire", "/home/user/grimoire"), NewRecord("grimoire-clone", "/home/user/grimoire/")]);

        errors.ShouldHaveSingleItem().ShouldContain("already registered");
    }

    [Fact]
    public void Validate_ReportsRelativePaths()
    {
        var errors = ProjectRegistryValidator.Validate([NewRecord("grimoire", "projects/grimoire")]);

        errors.ShouldHaveSingleItem().ShouldContain("must be absolute");
    }

    [Fact]
    public void Validate_ReportsEmptyPathEntries()
    {
        var errors = ProjectRegistryValidator.Validate([NewRecord("grimoire", "  ")]);

        errors.ShouldHaveSingleItem().ShouldContain("must not contain empty entries");
    }

    [Fact]
    public void Validate_LabelsErrorsWithRecordIndex()
    {
        var errors = ProjectRegistryValidator.Validate([NewRecord("grimoire"), NewRecord("Slurpy")]);

        errors.ShouldHaveSingleItem().ShouldStartWith("projects[1]");
    }

    [Fact]
    public void Validate_CollectsAllErrorsInOnePass()
    {
        var errors = ProjectRegistryValidator.Validate(
            [NewRecord("Slurpy"), NewRecord("Slurpy", "relative/path")]);

        errors.Count.ShouldBe(3);
    }

    private static ProjectAliasRecord NewRecord(string alias, params string[] paths) => new()
    {
        Alias = alias,
        Paths = paths,
    };
}
