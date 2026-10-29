using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class CompactCandidatesToolTests
{
    [Fact]
    public async Task CompactCandidates_EmptyStore_ReturnsNoGroups()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_compact_candidates", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("groups").GetArrayLength().ShouldBe(0);
        payload.GetProperty("candidateCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task CompactCandidates_GroupsStaleSimilarJournals_WithTheirContents()
    {
        await using var session = await McpTestSession.StartAsync();
        var stale = session.Clock.UtcNow.AddDays(-30);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        var contents = new Dictionary<Guid, string>
        {
            [firstId] = "Tried Mediator, hit a wall with generic dispatch",
            [secondId] = "Mediator dispatch generics keep biting us",
            [thirdId] = "Generic dispatch issues with Mediator again today",
        };
        var similar = new float[][] { [1, 0.05f, 0], [1, 0, 0.05f], [1, 0.05f, 0.05f] };
        for (var i = 0; i < similar.Length; i++)
        {
            var id = new[] { firstId, secondId, thirdId }[i];
            await session.Store.UpsertAsync(Journal(id, contents[id], stale), similar[i], CancellationToken.None);
        }

        var result = await session.Client.CallToolAsync("memory_compact_candidates", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        var groups = payload.GetProperty("groups");
        groups.GetArrayLength().ShouldBe(1);
        var group = groups[0];
        var ids = group.GetProperty("memoryIds").EnumerateArray().Select(value => value.GetGuid()).ToArray();
        ids.Length.ShouldBe(3);
        ids.ShouldContain(firstId);
        ids.ShouldContain(secondId);
        ids.ShouldContain(thirdId);
        group.GetProperty("similarity").GetDouble().ShouldBeGreaterThanOrEqualTo(0.85);
        var entries = group.GetProperty("entries");
        entries.GetArrayLength().ShouldBe(3);
        var entryContents = entries.EnumerateArray().Select(entry => entry.GetProperty("content").GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        entryContents.ShouldBe(contents.Values.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        payload.GetProperty("candidateCount").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task CompactCandidates_IgnoresFreshEntriesAndFacts()
    {
        await using var session = await McpTestSession.StartAsync();
        var stale = session.Clock.UtcNow.AddDays(-30);
        await session.Store.UpsertAsync(
            Journal(Guid.NewGuid(), "fresh journal note", session.Clock.UtcNow),
            new float[] { 1, 0, 0 },
            CancellationToken.None);
        await session.Store.UpsertAsync(
            Journal(Guid.NewGuid(), "stale standing fact", stale) with { Kind = MemoryKind.Fact },
            new float[] { 1, 0.05f, 0 },
            CancellationToken.None);

        var result = await session.Client.CallToolAsync("memory_compact_candidates", new Dictionary<string, object?>());

        result.Payload().GetProperty("groups").GetArrayLength().ShouldBe(0);
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
            Tome = McpTestSession.ClientActor,
            ProjectId = "grimoire",
            CreatedAt = accessed,
            LastAccessedAt = accessed,
        };
    }


    [Fact]
    public async Task CompactCandidates_UnknownExplicitTome_CarriesTheNoneYetWarning()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_compact_candidates", new Dictionary<string, object?>
        {
            ["tome"] = "nonesuch-tome",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("groups").GetArrayLength().ShouldBe(0);
        payload.GetProperty("warnings")[0].GetString()
            .ShouldBe("tome 'nonesuch-tome' is empty or does not exist. Known tome keys: (none yet)");
    }

    [Fact]
    public async Task CompactCandidates_KnownTomeWithoutDueCandidates_StaysSilent()
    {
        await using var session = await McpTestSession.StartAsync();
        var stale = session.Clock.UtcNow.AddDays(-30);
        float[] vector = [1f, 0f, 0f];
        await session.Store.UpsertAsync(
            Journal(Guid.NewGuid(), "Lone stale journal, no cluster mates", stale), vector, CancellationToken.None);

        var result = await session.Client.CallToolAsync("memory_compact_candidates", new Dictionary<string, object?>
        {
            ["tome"] = McpTestSession.ClientActor,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("groups").GetArrayLength().ShouldBe(0);
        payload.TryGetProperty("warnings", out _).ShouldBeFalse();
    }
}
