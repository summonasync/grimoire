using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
}
