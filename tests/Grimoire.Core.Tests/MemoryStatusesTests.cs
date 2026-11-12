using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryStatusesTests
{
    [Fact]
    public void All_IsTheKnownSetAndGoesRedWhenTheEnumGrows() =>
        MemoryStatuses.All.ShouldBe([MemoryStatus.Active, MemoryStatus.Superseded, MemoryStatus.Tombstoned]);
}
