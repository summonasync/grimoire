using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryStatusesTests
{
    [Fact]
    public void All_CoversEveryDeclaredStatus() =>
        MemoryStatuses.All.ShouldBe(Enum.GetValues<MemoryStatus>());
}
