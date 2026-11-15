using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class WarningMergeTests
{
    [Fact]
    public void Merge_NothingToSay_StaysNullInsteadOfEmpty()
    {
        WarningMerge.Merge(null, null).ShouldBeNull();
    }

    [Fact]
    public void Merge_CoreOnly_PassesTheCoreListThrough()
    {
        WarningMerge.Merge(["core"], null).ShouldBe(["core"]);
    }

    [Fact]
    public void Merge_TomeOnly_PassesTheTomeListThrough()
    {
        WarningMerge.Merge(null, ["tome"]).ShouldBe(["tome"]);
    }

    [Fact]
    public void Merge_BothSources_KeepsCoreAheadOfTome()
    {
        WarningMerge.Merge(["core"], ["tome"]).ShouldBe(["core", "tome"]);
    }

    [Fact]
    public void Merge_SameTextFromBothSides_CarriesItOnce()
    {
        WarningMerge.Merge(["shared"], ["shared"])!.ShouldBe(["shared"]);
    }

    [Fact]
    public void Merge_DifferentCasing_KeepsBothTexts()
    {
        WarningMerge.Merge(["Page short"], ["page short"])!.Count.ShouldBe(2);
    }
}
