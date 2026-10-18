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
        }, MemoryActor.Human, CancellationToken.None);

        var result = await session.Client.CallToolAsync("memory_persona_get", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("identity").GetString().ShouldBe("Senior .NET developer");
        payload.GetProperty("tone").GetString().ShouldBe("informal");
        payload.GetProperty("preferences").GetString().ShouldBe("dark mode, Vim bindings");
        payload.GetProperty("rules").GetString().ShouldBe("never commit without being asked");
    }
}
