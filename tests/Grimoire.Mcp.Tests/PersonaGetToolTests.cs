using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class PersonaGetToolTests
{
    [Fact]
    public async Task PersonaGet_WithoutPersonaData_ReturnsEmptySections()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_persona_get", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("identity").GetString().ShouldBe("");
        payload.GetProperty("tone").GetString().ShouldBe("");
        payload.GetProperty("preferences").GetString().ShouldBe("");
        payload.GetProperty("rules").GetString().ShouldBe("");
    }

    [Fact]
    public async Task PersonaGet_ReturnsTheHumanCuratedDocument()
    {
        await using var session = await McpTestSession.StartAsync();
        var persona = new PersonaService(session.Store, session.Embeddings, session.Clock);
        await persona.SetAsync(new PersonaDocument
        {
            Identity = "Senior .NET developer",
            Tone = "informal",
            Preferences = "dark mode, Vim bindings",
            Rules = "never commit without being asked",
        }, MemoryActor.Human, CancellationToken.None, McpTestSession.ClientActor);

        var result = await session.Client.CallToolAsync("memory_persona_get", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("identity").GetString().ShouldBe("Senior .NET developer");
        payload.GetProperty("tone").GetString().ShouldBe("informal");
        payload.GetProperty("preferences").GetString().ShouldBe("dark mode, Vim bindings");
        payload.GetProperty("rules").GetString().ShouldBe("never commit without being asked");
    }


    [Fact]
    public async Task PersonaGet_UnknownExplicitTome_CarriesTheNoneYetWarning()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_persona_get", new Dictionary<string, object?>
        {
            ["tome"] = "nonesuch-tome",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("warnings")[0].GetString()
            .ShouldBe("tome 'nonesuch-tome' is empty or does not exist. Known tome keys: (none yet)");
    }

    [Fact]
    public async Task PersonaGet_FilledPersonaInExplicitTome_StaysSilent()
    {
        await using var session = await McpTestSession.StartAsync();
        var persona = new PersonaService(session.Store, session.Embeddings, session.Clock);
        await persona.SetAsync(new PersonaDocument
        {
            Identity = "Senior .NET developer",
            Tone = "informal",
            Preferences = "dark mode, Vim bindings",
            Rules = "never commit without being asked",
        }, MemoryActor.Human, CancellationToken.None, McpTestSession.ClientActor);

        var result = await session.Client.CallToolAsync("memory_persona_get", new Dictionary<string, object?>
        {
            ["tome"] = McpTestSession.ClientActor,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("identity").GetString().ShouldBe("Senior .NET developer");
        result.Payload().TryGetProperty("warnings", out _).ShouldBeFalse();
    }
}
