using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class StatsToolTests
{
    [Fact]
    public async Task Stats_EmptyStore_ReportsZeroesAndNoPendingCompaction()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("active").GetInt64().ShouldBe(0);
        payload.GetProperty("superseded").GetInt64().ShouldBe(0);
        payload.GetProperty("tombstoned").GetInt64().ShouldBe(0);
        payload.GetProperty("pendingCompaction").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Stats_ReflectsCountsPerStatusScopeAndPendingCompaction()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("first", new float[] { 1, 0, 0 });
        session.Embeddings.Register("second", new float[] { 0, 1, 0 });
        session.Embeddings.Register("third", new float[] { 0, 0, 1 });
        foreach (var content in new[] { "first", "second", "third" })
        {
            await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
            {
                ["content"] = content,
                ["scope"] = "Project",
                ["projectId"] = "grimoire",
            });
        }

        var third = session.Store.Points.Single(point => point.Record.Content == "third").Record;
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = third.Id.ToString(),
            ["reason"] = "wrong assumption",
        });

        var stale = session.Clock.UtcNow.AddDays(-30);
        var similar = new float[][] { [1, 0.05f, 0], [1, 0, 0.05f], [1, 0.05f, 0.05f] };
        foreach (var vector in similar)
        {
            await session.Store.UpsertAsync(
                Journal(Guid.NewGuid(), $"stale journal {vector[1]}{vector[2]}", stale),
                vector,
                CancellationToken.None);
        }

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        var payload = result.Payload();
        payload.GetProperty("active").GetInt64().ShouldBe(5);
        payload.GetProperty("superseded").GetInt64().ShouldBe(0);
        payload.GetProperty("tombstoned").GetInt64().ShouldBe(1);
        payload.GetProperty("pendingCompaction").GetInt64().ShouldBe(3);
        payload.GetProperty("activeByScope").GetProperty("Project").GetInt64().ShouldBe(5);
        payload.GetProperty("activeByScope").GetProperty("Session").GetInt64().ShouldBe(0);
    }

    private static MemoryRecord Journal(Guid id, string content, DateTimeOffset accessed)
    {
        return new MemoryRecord
        {
            Id = id,
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = MemoryActor.Human,
            ProjectId = "grimoire",
            CreatedAt = accessed,
            LastAccessedAt = accessed,
        };
    }
}
