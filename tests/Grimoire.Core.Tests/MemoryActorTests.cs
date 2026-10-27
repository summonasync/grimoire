using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryActorTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("nerzhul", "nerzhul")]
    [InlineData("nerzhul+claude", "nerzhul")]
    [InlineData("nerzhul+claude+extra", "nerzhul")]
    public void AliasPart_SplitsBeforeFirstVendorSeparator(string? actor, string? expected) =>
        MemoryActor.AliasPart(actor).ShouldBe(expected);
}
