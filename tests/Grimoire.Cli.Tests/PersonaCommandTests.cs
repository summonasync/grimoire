using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class PersonaCommandTests : IDisposable
{
    private readonly TestHost _host = new(new StubEmbeddings(new Dictionary<string, float[]>
    {
        ["Senior .NET developer"] = [1f, 0f],
        ["dry humor"] = [1f, 0f],
        ["no sudo"] = [1f, 0f],
    }));

    [Fact]
    public async Task Persona_SetSectionThenShowRoundTrips()
    {
        var set = await _host.RunAsync("persona", "set-section", "identity", "Senior .NET developer");
        var show = await _host.RunAsync("persona", "show");

        set.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        show.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("Senior .NET developer");
    }

    [Fact]
    public async Task Persona_SetSectionStoresPersonalFactWithHumanActor()
    {
        var code = await _host.RunAsync("persona", "set-section", "tone", "dry humor");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var record = _host.Store.Upserted.ShouldHaveSingleItem();
        record.Scope.ShouldBe(MemoryScope.Personal);
        record.Kind.ShouldBe(MemoryKind.Fact);
        record.Actor.ShouldBe(MemoryActor.Human);
        record.Tags.ShouldContain("persona:tone");
    }

    [Fact]
    public async Task Persona_SetSectionCarriesActorFlag()
    {
        var code = await _host.RunAsync("persona", "set-section", "rules", "no sudo", "--actor", "junior+claude");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Upserted.ShouldHaveSingleItem().Actor.ShouldBe(new MemoryActor("junior", "claude"));
    }

    [Fact]
    public async Task Persona_SetSectionUnknownName_ExitsUsage()
    {
        var code = await _host.RunAsync("persona", "set-section", "hobbies", "chess");

        code.ShouldBe(ExitCodes.Usage);
    }

    [Fact]
    public async Task Persona_SetSectionMissingText_ExitsUsage()
    {
        var code = await _host.RunAsync("persona", "set-section", "identity");

        code.ShouldBe(ExitCodes.Usage);
    }

    [Fact]
    public async Task Persona_UnknownAction_ExitsUsage()
    {
        var code = await _host.RunAsync("persona", "rename");

        code.ShouldBe(ExitCodes.Usage);
    }

    public void Dispose() => _host.Dispose();
}
