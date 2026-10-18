using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class ActorClaimProviderTests : IDisposable
{
    private readonly string _root;

    public ActorClaimProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "grimoire-claim-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ReadAsync_WithClaimInStartingDirectory_ReturnsClaim()
    {
        var child = CreateDirectory("child");
        await File.WriteAllTextAsync(Path.Combine(child, ActorClaimProvider.FileName), Block("dan", "platform-work"));

        var claim = await new ActorClaimProvider(child).ReadAsync(CancellationToken.None);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
        claim.Tome.ShouldBe("platform-work");
    }

    [Fact]
    public async Task ReadAsync_WithClaimOnlyInAncestor_WalksUpTheChain()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, ActorClaimProvider.FileName), Block("dan", "platform-work"));
        var child = CreateDirectory("child");

        var claim = await new ActorClaimProvider(child).ReadAsync(CancellationToken.None);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("dan");
    }

    [Fact]
    public async Task ReadAsync_WithClaimsAtEveryLevel_NearestDirectoryWins()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, ActorClaimProvider.FileName), Block("parent", "parent-tome"));
        var child = CreateDirectory("child");
        await File.WriteAllTextAsync(Path.Combine(child, ActorClaimProvider.FileName), Block("child", "child-tome"));

        var claim = await new ActorClaimProvider(child).ReadAsync(CancellationToken.None);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("child");
        claim.Tome.ShouldBe("child-tome");
    }

    [Fact]
    public async Task ReadAsync_WithUnparsableClaim_FallsThroughToAncestor()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, ActorClaimProvider.FileName), Block("parent", "parent-tome"));
        var child = CreateDirectory("child");
        await File.WriteAllTextAsync(Path.Combine(child, ActorClaimProvider.FileName), "<!-- grimoire:actor:start -->\nalias: orphan\n");

        var claim = await new ActorClaimProvider(child).ReadAsync(CancellationToken.None);

        claim.ShouldNotBeNull();
        claim.Alias.ShouldBe("parent");
    }

    [Fact]
    public async Task ReadAsync_WithoutAnyClaimFile_ReturnsNull()
    {
        var child = CreateDirectory("empty");

        (await new ActorClaimProvider(child).ReadAsync(CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_WithMalformedClaimFile_SkipsItAndReturnsNull()
    {
        var child = CreateDirectory("broken");
        await File.WriteAllTextAsync(Path.Combine(child, ActorClaimProvider.FileName), "no markers here at all");

        (await new ActorClaimProvider(child).ReadAsync(CancellationToken.None)).ShouldBeNull();
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Block(string alias, string tome) => $"""
        <!-- grimoire:actor:start -->
        alias: {alias}
        tome: {tome}
        <!-- grimoire:actor:end -->
        """;
}
