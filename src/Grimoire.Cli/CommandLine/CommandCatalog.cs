namespace Grimoire.Cli.CommandLine;

internal static class CommandCatalog
{
    private static readonly FlagSpec Actor = new(
        "actor", "alias[+vendor]", "Actor attribution for the write. Default: GRIMOIRE_ACTOR, then 'human'.");

    private static readonly FlagSpec Scope = new(
        "scope", "scope", "Memory scope: session, project, global or personal. Comma-separate to filter several.");

    private static readonly FlagSpec Kind = new(
        "kind", "kind", "Memory kind: journal or fact. Default: journal.");

    private static readonly FlagSpec Tags = new(
        "tags", "a,b,c", "Comma-separated tags.");

    private static readonly FlagSpec Importance = new(
        "importance", "0..1", "Importance weight. Default: 0.5.");

    private static readonly FlagSpec Ttl = new(
        "ttl", "hours", "Session expiry override in hours. Requires --scope session.");

    private static readonly FlagSpec Session = new(
        "session", "id", "Session id for session-scope memories.");

    private static readonly FlagSpec Project = new(
        "project", "id", "Project id for project-scope memories.");

    private static readonly FlagSpec Tag = new(
        "tag", "a,b,c", "Comma-separated tags to filter by.");

    private static readonly FlagSpec Status = new(
        "status", "status", "Record status: active (default), superseded or tombstoned.");

    private static readonly FlagSpec Limit = new(
        "limit", "n", "Maximum number of results.");

    private static readonly FlagSpec Reason = new(
        "reason", "text", "Tombstone reason. Default: 'forgotten via grimoire cli'.");

    private static readonly FlagSpec WebRoot = new(
        "web-root", "path", "Static SPA export directory. Default: ~/.grimoire/web");

    private static readonly CommandSpec[] Commands =
    [
        new CommandSpec
        {
            Name = "init",
            Summary = "Write the default config when missing and prepare the Qdrant collection.",
            Syntax = "init",
        },
        new CommandSpec
        {
            Name = "serve",
            Summary = "Run the loopback web server with the management SPA and the GC sweep.",
            Syntax = "serve",
            Flags = [WebRoot],
        },
        new CommandSpec
        {
            Name = "mcp",
            Summary = "Run the stdio MCP server for agent clients.",
            Syntax = "mcp",
            Flags = [Actor],
        },
        new CommandSpec
        {
            Name = "remember",
            Summary = "Store a memory.",
            Syntax = "remember <text>",
            MinPositional = 1,
            Flags = [Scope, Kind, Tags, Importance, Ttl, Session, Project, Actor],
        },
        new CommandSpec
        {
            Name = "recall",
            Summary = "Semantic recall over memories.",
            Syntax = "recall <query>",
            MinPositional = 1,
            Flags = [Scope, Limit],
        },
        new CommandSpec
        {
            Name = "list",
            Summary = "List memories. Defaults to active records.",
            Syntax = "list",
            Flags = [Scope, Tag, Status, Limit],
        },
        new CommandSpec
        {
            Name = "forget",
            Summary = "Tombstone a memory by id or by its best query match.",
            Syntax = "forget <id|query>",
            MinPositional = 1,
            Flags = [Reason],
        },
        new CommandSpec
        {
            Name = "gc",
            Summary = "Hard-delete expired session memories and aged tombstones.",
            Syntax = "gc",
        },
        new CommandSpec
        {
            Name = "persona",
            Summary = "Show or edit the persona document read by agents.",
            Syntax = "persona [show | set-section <identity|tone|preferences|rules> <text>]",
            Flags = [Actor],
        },
        new CommandSpec
        {
            Name = "stats",
            Summary = "Show memory statistics.",
            Syntax = "stats",
        },
        new CommandSpec
        {
            Name = "config",
            Summary = "Show or change configuration.",
            Syntax = "config [get | set <section.property> <value> | path]",
        },
    ];

    public static IReadOnlyList<CommandSpec> All { get; } = Commands;

    public static CommandSpec? Find(string name)
    {
        foreach (var spec in Commands)
        {
            if (spec.Name.Equals(name, StringComparison.Ordinal))
            {
                return spec;
            }
        }

        return null;
    }
}
