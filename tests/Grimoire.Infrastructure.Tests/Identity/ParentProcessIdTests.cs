using Grimoire.Infrastructure.Identity;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Identity;

public sealed class ParentProcessIdTests
{
    [Fact]
    public void Resolve_OfTheCurrentProcess_MatchesProcStatOnLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var stat = File.ReadAllText($"/proc/{Environment.ProcessId}/stat");
        var commEnd = stat.LastIndexOf(')');
        var fields = stat[(commEnd + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        ParentProcessId.Resolve().ShouldBe(int.Parse(fields[1]));
    }

    [Fact]
    public void Resolve_OfAnUnknownProcess_ReturnsNull()
    {
        ParentProcessId.Resolve(int.MaxValue - 1).ShouldBeNull();
    }
}
