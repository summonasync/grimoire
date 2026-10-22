using System.Net;
using Grimoire.Infrastructure.Network;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Network;

public sealed class SystemHostNameResolverTests
{
    [Fact]
    public async Task ResolveAsync_HostsNameFromTheResolverAnswers()
    {
        var resolver = new SystemHostNameResolver();

        var addresses = await resolver.ResolveAsync("localhost", CancellationToken.None);

        addresses.ShouldNotBeEmpty();
        addresses.ShouldAllBe(address => IPAddress.IsLoopback(address));
    }

    [Fact]
    public async Task ResolveAsync_UnresolvableName_AnswersEmptyInsteadOfThrowing()
    {
        var resolver = new SystemHostNameResolver();

        var addresses = await resolver.ResolveAsync("grimoire-does-not-exist.invalid", CancellationToken.None);

        addresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_CallerCancellation_Propagates()
    {
        using var timeout = new CancellationTokenSource();
        await timeout.CancelAsync();
        var resolver = new SystemHostNameResolver();

        await Should.ThrowAsync<OperationCanceledException>(
            () => resolver.ResolveAsync("localhost", timeout.Token));
    }
}
