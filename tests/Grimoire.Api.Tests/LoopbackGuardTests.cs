using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class LoopbackGuardTests
{
    [Fact]
    public void EnsureLoopbackOnly_WithLoopbackAddresses_Passes()
    {
        LoopbackGuard.EnsureLoopbackOnly(
        [
            "http://127.0.0.1:4770",
            "http://127.5.5.5:80",
            "http://[::1]:4770",
        ]);
    }

    [Fact]
    public void EnsureLoopbackOnly_WithNoAddresses_Passes()
    {
        LoopbackGuard.EnsureLoopbackOnly([]);
    }

    [Theory]
    [InlineData("http://0.0.0.0:5999")]
    [InlineData("http://192.168.1.10:5999")]
    [InlineData("http://localhost:5999")]
    [InlineData("https://[fe80::1]:4770")]
    [InlineData("127.0.0.1:4770")]
    public void EnsureLoopbackOnly_WithNonLoopbackAddress_ThrowsNamingIt(string address)
    {
        var error = Should.Throw<InvalidOperationException>(() =>
            LoopbackGuard.EnsureLoopbackOnly([address]));

        error.Message.ShouldContain(address);
    }
}
