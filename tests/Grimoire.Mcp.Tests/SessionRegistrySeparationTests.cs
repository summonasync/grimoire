using Grimoire.Core;
using ModelContextProtocol.Client;

namespace Grimoire.Mcp.Tests;

public sealed class SessionRegistrySeparationTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private static readonly float[] SharedVector = [1f, 0f, 0f];

    private const string MemoryTome = "alpha";

    private const string MemoryContent =
        "The handshake registry keeps one row per observed client connection and no vectors.";

    private const string RegistryWords = "handshake registry observed client connection";

    private static readonly string[] RegistrySentinels =
    [
        "zephyr-client",
        "0.9.9-zephyr",
        "2099-01-01",
        "zephyr-agent",
        "zephyr-box-07",
    ];

    private static readonly string[] HandshakeFields =
    [
        "clientName",
        "clientVersion",
        "protocolVersion",
        "transport",
        "occurredAt",
        "ageHours",
    ];

    [Fact]
    public async Task RegistryHoldsTheRowsThatEveryFactSurfaceMustNotSee()
    {
        await using var session = await StartAsync();

        session.Sessions.Written.Count.ShouldBe(3);
        var carried = string.Join(' ', (await session.Sessions.ListRecentAsync(
            10, null, CancellationToken.None)).Select(row =>
            $"{row.ClientName} {row.ClientVersion} {row.ProtocolVersion} {row.ActorAlias} {row.Host}"));

        foreach (var sentinel in RegistrySentinels)
        {
            carried.ShouldContain(sentinel);
        }

        session.Store.Points.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Recall_AskedWithTheRegistryWords_ReturnsTheMemoryAndNoHandshakeRow()
    {
        await using var session = await StartAsync();

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = RegistryWords,
            ["tome"] = MemoryTome,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("count").GetInt32().ShouldBe(1);
        result.Payload().GetProperty("matches")[0].GetProperty("content").GetString().ShouldBe(MemoryContent);
        AssertNoRegistryRow(result.Text());
    }

    [Fact]
    public async Task List_InTheTomeHoldingTheMemory_ReturnsTheMemoryAndNoHandshakeRow()
    {
        await using var session = await StartAsync();

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["tome"] = MemoryTome,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("count").GetInt32().ShouldBe(1);
        result.Payload().GetProperty("memories")[0].GetProperty("content").GetString().ShouldBe(MemoryContent);
        AssertNoRegistryRow(result.Text());
    }

    [Fact]
    public async Task Digest_OnTheHandshakeTopic_ReturnsTheMemoryAndNoHandshakeRow()
    {
        await using var session = await StartAsync();

        var result = await session.Client.CallToolAsync("memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "handshake",
            ["tome"] = MemoryTome,
        });

        result.ShouldSucceed();
        var facts = result.Payload().GetProperty("longMemo").GetProperty("items");
        facts.GetArrayLength().ShouldBe(1);
        facts[0].GetProperty("content").GetString().ShouldBe(MemoryContent);
        AssertNoRegistryRow(result.Text());
    }

    [Fact]
    public async Task Brief_ForTheTomeHoldingTheMemory_ReturnsTheMemoryAndNoHandshakeRow()
    {
        await using var session = await StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["tome"] = MemoryTome,
        });

        result.ShouldSucceed();
        var facts = result.Payload().GetProperty("facts").GetProperty("items");
        facts.GetArrayLength().ShouldBe(1);
        facts[0].GetProperty("content").GetString().ShouldBe(MemoryContent);
        AssertNoRegistryRow(result.Text());
    }

    [Fact]
    public async Task Scry_AcrossTomes_ReachesTheMemoryAndNoHandshakeRow()
    {
        await using var session = await StartAsync();

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = RegistryWords,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("count").GetInt32().ShouldBe(1);
        result.Payload().GetProperty("hits")[0].GetProperty("content").GetString().ShouldBe(MemoryContent);
        AssertNoRegistryRow(result.Text());
    }

    private static async Task<McpTestSession> StartAsync()
    {
        var session = await McpTestSession.StartAsync();

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        await session.Sessions.AttemptedAsync.WaitAsync(Wait);
        await session.Sessions.WriteAsync(HandshakeRow(session.Clock.UtcNow.AddMinutes(-2)), CancellationToken.None);
        await session.Sessions.WriteAsync(HandshakeRow(session.Clock.UtcNow.AddMinutes(-1)), CancellationToken.None);

        session.Embeddings.Register(MemoryContent, SharedVector);
        session.Embeddings.Register(RegistryWords, SharedVector);
        session.Embeddings.Register("handshake", SharedVector);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = MemoryContent,
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tags"] = new[] { "topic:handshake" },
            ["tome"] = MemoryTome,
        });

        return session;
    }

    private static SessionRegistryEntry HandshakeRow(DateTimeOffset occurredAt) => new()
    {
        OccurredAt = occurredAt,
        ClientName = "zephyr-client",
        ClientVersion = "0.9.9-zephyr",
        ProtocolVersion = "2099-01-01",
        ActorAlias = "zephyr-agent",
        ActorVendor = "zephyr",
        Transport = "stdio",
        Host = "zephyr-box-07",
    };

    private static void AssertNoRegistryRow(string payload)
    {
        foreach (var sentinel in RegistrySentinels)
        {
            payload.Contains(sentinel, StringComparison.Ordinal)
                .ShouldBeFalse($"a fact surface carried the registry value '{sentinel}': {payload}");
        }

        foreach (var field in HandshakeFields)
        {
            payload.Contains($"\"{field}\"", StringComparison.Ordinal)
                .ShouldBeFalse($"a fact surface carried the handshake field '{field}': {payload}");
        }
    }
}
