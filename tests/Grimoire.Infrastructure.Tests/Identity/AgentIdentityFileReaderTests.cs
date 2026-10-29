using Grimoire.Core;
using Grimoire.Infrastructure.Identity;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Identity;

public sealed class AgentIdentityFileReaderTests : IDisposable
{
    private const int ParentPid = 4242;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "grimoire-infra-tests", Guid.NewGuid().ToString("N"));

    private string IdentityDirectory => Path.Combine(_home, ".overmind", "identity");

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    [Fact]
    public void Get_WithAWellFormedFile_ReturnsAliasAndVendor()
    {
        WriteIdentity("""{ "alias": "guldan", "vendor": "claude", "sessionId": "6f1c2f5e-9a4b-4c3d-8d2e-1f0a9b8c7d6e", "registeredAt": "2026-10-29T09:00:00Z" }""");

        var actor = Reader().Get();

        actor.ShouldBe(new MemoryActor("guldan", "claude"));
    }

    [Fact]
    public void Get_WithoutAVendor_CarriesNullVendor()
    {
        WriteIdentity("""{ "alias": "guldan", "vendor": "" }""");

        var actor = Reader().Get();

        actor.ShouldBe(new MemoryActor("guldan", null));
    }

    [Fact]
    public void Get_TrimsSurroundingWhitespaceOffBothFields()
    {
        WriteIdentity("""{ "alias": "  guldan  ", "vendor": " claude " }""");

        var actor = Reader().Get();

        actor.ShouldBe(new MemoryActor("guldan", "claude"));
    }

    [Fact]
    public void Get_WithAMissingFile_ReturnsNull()
    {
        Reader().Get().ShouldBeNull();
    }

    [Fact]
    public void Get_WithAnUnreadableHome_ReturnsNull()
    {
        new AgentIdentityFileReader(string.Empty, ParentPid).Get().ShouldBeNull();
    }

    [Fact]
    public void Get_WithCorruptJson_ReturnsNull()
    {
        WriteIdentity("""{ "alias": "guldan", """);

        Reader().Get().ShouldBeNull();
    }

    [Fact]
    public void Get_WithANonObjectPayload_ReturnsNull()
    {
        WriteIdentity("""[42]""");

        Reader().Get().ShouldBeNull();
    }

    [Fact]
    public void Get_WithAnEmptyAlias_ReturnsNull()
    {
        WriteIdentity("""{ "alias": "   ", "vendor": "claude" }""");

        Reader().Get().ShouldBeNull();
    }

    [Fact]
    public void Get_WithAnOverlongAlias_ReturnsNull()
    {
        WriteIdentity("""{ "alias": "abcdefghijklmnopqrstuvwxyz0123456", "vendor": "claude" }""");

        Reader().Get().ShouldBeNull();
    }

    [Fact]
    public void Get_WithWhitespaceInsideTheAlias_ReturnsNull()
    {
        WriteIdentity("""{ "alias": "gul dan", "vendor": "claude" }""");

        Reader().Get().ShouldBeNull();
    }

    [Fact]
    public void Get_WithoutAParentPid_ReturnsNull()
    {
        WriteIdentity("""{ "alias": "guldan", "vendor": "claude" }""");

        new AgentIdentityFileReader(_home, null).Get().ShouldBeNull();
    }

    private AgentIdentityFileReader Reader() => new(_home, ParentPid);

    private void WriteIdentity(string json)
    {
        Directory.CreateDirectory(IdentityDirectory);
        File.WriteAllText(Path.Combine(IdentityDirectory, $"parent-{ParentPid}.json"), json);
    }
}
