using System.Text.Json;
using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class ListToolTests
{
    private static readonly string[] AllowedFields =
    [
        "content", "scope", "kind", "status", "actor", "tags", "projectId", "sessionId",
        "importance", "accessCount", "supersededBy", "tombstoneReason", "expiresAt",
        "createdAt", "lastAccessedAt", "bodyChars",
    ];

    [Fact]
    public async Task List_DefaultsToActiveRecordsOnly()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("kept", new float[] { 1, 0, 0 });
        session.Embeddings.Register("retracted", new float[] { 0, 1, 0 });
        var kept = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "kept",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        var retracted = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "retracted",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = retracted.Payload().GetProperty("memoryId").GetGuid().ToString(),
            ["reason"] = "superseded by newer decision",
        });

        var active = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>());
        var tombstoned = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["status"] = "Tombstoned",
        });

        var activePayload = active.Payload();
        activePayload.GetProperty("count").GetInt32().ShouldBe(1);
        activePayload.GetProperty("memories")[0].GetProperty("memoryId").GetGuid()
            .ShouldBe(kept.Payload().GetProperty("memoryId").GetGuid());

        var tombstonedPayload = tombstoned.Payload();
        tombstonedPayload.GetProperty("count").GetInt32().ShouldBe(1);
        tombstonedPayload.GetProperty("memories")[0].GetProperty("tombstoneReason").GetString()
            .ShouldBe("superseded by newer decision");
    }

    [Fact]
    public async Task List_ReturnsNewestFirst()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("older", new float[] { 1, 0, 0 });
        session.Embeddings.Register("newer", new float[] { 0, 1, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "older",
            ["scope"] = "Global",
        });
        session.Clock.UtcNow = session.Clock.UtcNow.AddDays(1);
        var newer = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "newer",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>());

        var memories = result.Payload().GetProperty("memories");
        memories.GetArrayLength().ShouldBe(2);
        memories[0].GetProperty("memoryId").GetGuid().ShouldBe(newer.Payload().GetProperty("memoryId").GetGuid());
    }

    [Fact]
    public async Task List_ProjectFilter_NarrowsToThatProject()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("alpha note", new float[] { 1, 0, 0 });
        session.Embeddings.Register("beta note", new float[] { 0, 1, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "alpha note",
            ["scope"] = "Project",
            ["projectId"] = "alpha",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "beta note",
            ["scope"] = "Project",
            ["projectId"] = "beta",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["projectId"] = "alpha",
        });

        var memories = result.Payload().GetProperty("memories");
        memories.GetArrayLength().ShouldBe(1);
        memories[0].GetProperty("content").GetString().ShouldBe("alpha note");
    }

    [Fact]
    public async Task List_RespectsLimit()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("one", new float[] { 1, 0, 0 });
        session.Embeddings.Register("two", new float[] { 0, 1, 0 });
        session.Embeddings.Register("three", new float[] { 0, 0, 1 });
        foreach (var content in new[] { "one", "two", "three" })
        {
            await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
            {
                ["content"] = content,
                ["scope"] = "Global",
            });
            session.Clock.UtcNow = session.Clock.UtcNow.AddMinutes(1);
        }

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["limit"] = 2,
        });

        result.Payload().GetProperty("count").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task List_CarriesBodiesStatusAndBodyChars()
    {
        await using var session = await McpTestSession.StartAsync();
        const string content = "The deploy pipeline gates on green tests";
        session.Embeddings.Register(content, new float[] { 1, 0, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = content,
            ["scope"] = "Global",
            ["kind"] = "Fact",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>());

        var entry = result.Payload().GetProperty("memories")[0];
        entry.GetProperty("content").GetString().ShouldBe(content);
        entry.GetProperty("status").GetString().ShouldBe("Active");
        entry.GetProperty("kind").GetString().ShouldBe("Fact");
        entry.GetProperty("actor").GetString().ShouldBe(McpTestSession.ClientActor);
        entry.GetProperty("bodyChars").GetInt32().ShouldBe(content.Length);
    }

    [Fact]
    public async Task List_Fields_ProjectEntriesToExactlyTheRequestedKeys()
    {
        await using var session = await McpTestSession.StartAsync();
        const string content = "The deploy pipeline gates on green tests";
        session.Embeddings.Register(content, new float[] { 1, 0, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = content,
            ["scope"] = "Global",
            ["kind"] = "Fact",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["fields"] = new[] { "content", "createdAt" },
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(1);
        payload.GetProperty("tome").GetString().ShouldBe(McpTestSession.ClientActor);
        var entry = payload.GetProperty("memories")[0];
        KeysOf(entry).ShouldBe(["memoryId", "content", "createdAt"], ignoreOrder: true);
        entry.GetProperty("content").GetString().ShouldBe(content);
        entry.GetProperty("createdAt").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task List_Compact_ReturnsTheDigestKeySet()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("digest probe", new float[] { 1, 0, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "digest probe",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["compact"] = true,
        });

        result.ShouldSucceed();
        var entry = result.Payload().GetProperty("memories")[0];
        KeysOf(entry).ShouldBe(["memoryId", "content", "tags", "createdAt"], ignoreOrder: true);
    }

    [Fact]
    public async Task List_EmptyFieldsArray_BehavesAsOmitted()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("full shape probe", new float[] { 1, 0, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "full shape probe",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["fields"] = Array.Empty<string>(),
        });

        result.ShouldSucceed();
        var entry = result.Payload().GetProperty("memories")[0];
        entry.TryGetProperty("status", out _).ShouldBeTrue();
        entry.TryGetProperty("bodyChars", out _).ShouldBeTrue();
        entry.TryGetProperty("importance", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task List_UnknownField_ReturnsInvalidRequestNamingTheAllowedSet()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["fields"] = new[] { "content", "body" },
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("fields");
        message.ShouldContain("body");
        message.ShouldContain("Allowed fields:");
        foreach (var field in AllowedFields)
        {
            message.ShouldContain(field);
        }
    }

    [Fact]
    public async Task List_FieldsWithCompact_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["fields"] = new[] { "content" },
            ["compact"] = true,
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("fields");
        message.ShouldContain("mutually exclusive");
    }

    [Fact]
    public async Task List_CompactPayload_StaysWithinSeventyPercentOfFull()
    {
        await using var session = await McpTestSession.StartAsync();
        for (var i = 0; i < FixtureContents.Length; i++)
        {
            session.Embeddings.Register(FixtureContents[i], FixtureVectors[i]);
            await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
            {
                ["content"] = FixtureContents[i],
                ["scope"] = "Project",
                ["projectId"] = "grimoire",
                ["kind"] = "Fact",
                ["tags"] = new[] { "periodic-scan" },
            });
            session.Clock.UtcNow = session.Clock.UtcNow.AddMinutes(1);
        }

        var full = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>());
        var compact = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["compact"] = true,
        });

        full.ShouldSucceed();
        compact.ShouldSucceed();
        full.Payload().GetProperty("count").GetInt32().ShouldBe(FixtureContents.Length);

        var fullSize = full.Text().Length;
        var compactSize = compact.Text().Length;
        compactSize.ShouldBeLessThanOrEqualTo((int)(fullSize * 0.7),
            $"compact payload was {compactSize} chars against {fullSize} for the full payload");
    }

    [Fact]
    public async Task List_NonPositiveLimit_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["limit"] = -3,
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("limit");
    }

    [Fact]
    public async Task List_TagsFilter_ReturnsTheBoardLinkTagRecord_AndMissesUnknownTags()
    {
        const string boardLinkTag = "overmind://entry/dbd64079-ea55-4b90-82a9-842aca5f3a28";
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("board decision provenance", new float[] { 1, 0, 0 });
        session.Embeddings.Register("unrelated grocery note", new float[] { 0, 1, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "board decision provenance",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tags"] = new[] { boardLinkTag },
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "unrelated grocery note",
            ["scope"] = "Global",
        });

        var hit = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["tags"] = new[] { boardLinkTag },
        });

        hit.ShouldSucceed();
        var payload = hit.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(1);
        payload.GetProperty("memories")[0].GetProperty("content").GetString().ShouldBe("board decision provenance");
        payload.GetProperty("memories")[0].GetProperty("tags").EnumerateArray()
            .Select(tag => tag.GetString()).ShouldBe([boardLinkTag]);

        var miss = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["tags"] = new[] { "overmind://entry/00000000-0000-0000-0000-000000000000" },
        });

        miss.ShouldSucceed();
        miss.Payload().GetProperty("count").GetInt32().ShouldBe(0);
    }

    private static string[] KeysOf(JsonElement entry)
    {
        return [.. entry.EnumerateObject().Select(property => property.Name)];
    }

    private static readonly float[][] FixtureVectors =
    [
        [1f, 0f, 0f],
        [0f, 1f, 0f],
        [0f, 0f, 1f],
        [-1f, 0f, 0f],
        [0f, -1f, 0f],
        [0f, 0f, -1f],
        [0.7f, 0.7f, 0f],
        [0.7f, -0.7f, 0f],
        [-0.7f, 0.7f, 0f],
        [0f, 0.7f, 0.7f],
        [0.7f, 0f, 0.7f],
        [0f, -0.7f, 0.7f],
    ];

    private static readonly string[] FixtureContents =
    [
        "We ship the indexer behind a feature flag and flip it per environment; a rollback is a flag flip, never " +
        "a redeploy. The flag lives in the deploy manifest so no hotfix PR is needed, and reviews still apply.",
        "Decision on the retry policy: idempotent handlers retry three times with jittered backoff, everything " +
        "else fails fast and surfaces the error to the caller. Poison messages move to the parking lot queue.",
        "The vector store keeps one collection per tome; cross-tome search is a deliberate scry call, never an " +
        "accident. Embedding dimensions are pinned per model, and a dimension mismatch fails the upsert loudly.",
        "Journal: migrated the staging cluster to the new storage node tonight. Latency stayed flat, the snapshot " +
        "restore took eleven minutes, and the old node stays warm for a week in case the numbers drift.",
        "Fact: the API gateway rejects payloads above one megabyte with a 413 and logs the caller alias, so " +
        "oversized writes stay traceable without storing the body. Clients are expected to chunk large documents.",
        "We agreed to keep migrations in a separate assembly so the runtime image never ships migration code. The " +
        "runner is invoked explicitly during deploys, and every migration carries a reviewed down script.",
        "Preference: error messages name the parameter that failed and the allowed set, never a bare validation " +
        "string. Callers should fix their request from the message alone, and the log line mirrors the wire error.",
        "Decision: the scheduler owns all timers and handlers stay passive. A handler that needs a delay asks the " +
        "scheduler instead of spawning threads, and every job publishes a heartbeat so a stuck loop is visible.",
        "The onboarding checklist now includes a smoke read of each face: list, recall and brief must answer on a " +
        "fresh install before release. A face that errors blocks the release until the failure is triaged.",
        "Fact: clock drift across the three workers stays under fifty milliseconds after the NTP fix, and the " +
        "compaction sweep relies on that budget. If drift exceeds it the sweep logs a warning and defers.",
        "Journal: traced the timeout spike to a cold embedding provider; the first call after an idle period paid " +
        "a four second model load. We now send a warmup ping during service start, and the p99 fell back.",
        "We keep the read path cache-free by contract: every read hits the store and bumps access stats, so the " +
        "popularity signals stay honest. A cached face must bypass the stats bump and say so in its description.",
    ];
}
