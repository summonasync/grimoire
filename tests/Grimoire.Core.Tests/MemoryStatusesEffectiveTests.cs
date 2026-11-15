using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryStatusesEffectiveTests
{
    [Fact]
    public void Effective_WithNothingSet_MeansActiveOnly()
    {
        MemoryStatuses.Effective(new MemoryFilter()).ShouldBe([MemoryStatus.Active]);
    }

    [Fact]
    public void Effective_WithStatus_MeansThatStatus()
    {
        MemoryStatuses.Effective(new MemoryFilter { Status = MemoryStatus.Tombstoned })
            .ShouldBe([MemoryStatus.Tombstoned]);
    }

    [Fact]
    public void Effective_WithStatuses_MeansThoseStatuses()
    {
        MemoryStatuses.Effective(new MemoryFilter { Statuses = MemoryStatuses.All })
            .ShouldBe(MemoryStatuses.All);
    }

    [Fact]
    public void Effective_WithBothClauses_StatusListWins()
    {
        var statuses = MemoryStatuses.Effective(
            new MemoryFilter { Status = MemoryStatus.Superseded, Statuses = [MemoryStatus.Active] });

        statuses.ShouldBe([MemoryStatus.Active]);
    }
}
