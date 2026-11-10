using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

internal static class ToolSurfaceProbes
{
    public static async Task<IReadOnlyDictionary<string, CallToolResult>> RunAsync(McpTestSession session)
    {
        var probes = new Dictionary<string, CallToolResult>(StringComparer.Ordinal);

        var personaIdentity = new string('i', 9000);
        session.Embeddings.Register(personaIdentity, [1f, 1, 1]);
        await new PersonaService(session.Store, session.Embeddings, session.Clock).SetAsync(new PersonaDocument
        {
            Identity = personaIdentity,
            Tone = "",
            Preferences = "",
            Rules = "",
        }, MemoryActor.Human, CancellationToken.None, McpTestSession.ClientActor);

        session.Embeddings.Register("snapshot probe standing fact", [1f, 0, 0]);
        session.Embeddings.Register("snapshot probe superseded fact", [0f, 1, 0]);
        session.Embeddings.Register("snapshot probe superseded fact v2", [0f, 1, 0.05f]);
        session.Embeddings.Register("snapshot probe fresh fact", [1f, 0.7f, 0]);
        session.Embeddings.Register("snapshot probe journal note", [0f, 1, 1]);
        session.Embeddings.Register("snapshot probe disposable note", [1f, 1, 0]);

        await RememberAsync(session, new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe standing fact",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["kind"] = "Fact",
            ["tags"] = new[] { "probe", "topic:probe" },
        });

        await RememberAsync(session, new Dictionary<string, object?>
        {
            ["content"] = "alpha tome probe fact",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tags"] = new[] { "alpha" },
            ["tome"] = "alpha",
        });

        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        var supersededId = await RememberAsync(session, new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe superseded fact",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["kind"] = "Fact",
            ["tags"] = new[] { "topic:probe" },
        });

        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        await RememberAsync(session, new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe superseded fact v2",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["kind"] = "Fact",
        });

        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        await RememberAsync(session, new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe journal note",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["kind"] = "Journal",
        });

        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        var disposableId = await RememberAsync(session, new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe disposable note",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["kind"] = "Journal",
            ["tags"] = new[] { "topic:probe" },
        });

        var stale = session.Clock.UtcNow.AddDays(-30);

        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        await Probe(session, probes, "memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe fresh fact",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["kind"] = "Fact",
            ["tags"] = new[] { "probe", "topic:probe" },
        });

        await Probe(session, probes, "memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "snapshot probe fresh fact",
        });

        await Probe(session, probes, "memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "alpha tome probe fact",
            ["tomes"] = new[] { "alpha" },
        });

        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        await Probe(session, probes, "memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = disposableId.ToString(),
            ["reason"] = "probe retraction",
        });

        await Probe(session, probes, "memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = disposableId.ToString(),
        });

        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        await Probe(session, probes, "memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = supersededId.ToString(),
            ["reason"] = "probe revival",
        });

        await Probe(session, probes, "memory_list", new Dictionary<string, object?>());
        await Probe(session, probes, "memory_persona_get", new Dictionary<string, object?>());

        float[] firstCluster = [1f, 0.05f, 0];
        float[] secondCluster = [1f, 0.05f, 0.05f];
        await session.Store.UpsertAsync(
            StaleJournal(Guid.NewGuid(), "snapshot probe stale journal one", stale), firstCluster, CancellationToken.None);
        await session.Store.UpsertAsync(
            StaleJournal(Guid.NewGuid(), "snapshot probe stale journal two", stale), secondCluster, CancellationToken.None);

        await Probe(session, probes, "memory_brief", new Dictionary<string, object?>());

        await Probe(session, probes, "memory_compact_candidates", new Dictionary<string, object?>());
        await Probe(session, probes, "memory_stats", new Dictionary<string, object?>());

        await Probe(session, probes, "projects", new Dictionary<string, object?>
        {
            ["action"] = "List",
        });

        session.Embeddings.Register("snapshot probe deploy journal one", [1f, 0.7f, 0]);
        session.Embeddings.Register("snapshot probe deploy journal two", [1f, 0, 0.7f]);
        session.Embeddings.Register("snapshot probe deploy journal three", [1f, 0, 0]);

        await Probe(session, probes, "memory_note", new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe deploy journal one",
            ["projectId"] = "grimoire",
            ["topic"] = "deploy",
        });

        await Probe(session, probes, "memory_note", new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe deploy journal two",
            ["projectId"] = "grimoire",
            ["topic"] = "deploy",
        });

        await Probe(session, probes, "memory_note", new Dictionary<string, object?>
        {
            ["content"] = "snapshot probe deploy journal three",
            ["projectId"] = "grimoire",
            ["tags"] = new[] { "probe" },
        });

        session.Embeddings.Register("probe", [1f, 0, 0]);
        await Probe(session, probes, "memory_digest", new Dictionary<string, object?>
        {
            ["topic"] = "probe",
        });

        await Probe(session, probes, "session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "probe-alias",
            ["vendor"] = "probe-vendor",
        });

        await Probe(session, probes, "memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "probe-tome",
            ["description"] = "snapshot probe tome manifests",
        });

        return probes;
    }

    private static async Task Probe(
        McpTestSession session,
        Dictionary<string, CallToolResult> probes,
        string tool,
        Dictionary<string, object?> arguments)
    {
        var result = await session.Client.CallToolAsync(tool, arguments);
        result.ShouldSucceed();
        probes[tool] = result;
    }

    private static async Task<Guid> RememberAsync(McpTestSession session, Dictionary<string, object?> arguments)
    {
        var result = await session.Client.CallToolAsync("memory_remember", arguments);
        result.ShouldSucceed();
        return result.Payload().GetProperty("memoryId").GetGuid();
    }

    private static MemoryRecord StaleJournal(Guid id, string content, DateTimeOffset accessed)
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
}
