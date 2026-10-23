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

    private static readonly FlagSpec ProjectFilter = new(
        "project", "id", "Restrict the brief to one project's memories.");

    private static readonly FlagSpec FactsPerScope = new(
        "facts-per-scope", "n", "Top facts per scope. Default: 5.");

    private static readonly FlagSpec JournalPerScope = new(
        "journal-per-scope", "n", "Recent journal entries per scope. Default: 5.");

    private static readonly FlagSpec TombstoneLimit = new(
        "tombstone-limit", "n", "Tombstoned records in the digest. Default: 10.");

    private static readonly FlagSpec Reason = new(
        "reason", "text", "Tombstone reason. Default: 'forgotten via grimoire cli'.");

    private static readonly FlagSpec WebRoot = new(
        "web-root", "path", "Static SPA export directory. Default: ~/.grimoire/web");

    private static readonly FlagSpec Gzip = FlagSpec.Switch(
        "gzip", "Write the export through gzip; the default export path gets .jsonl.gz.");

    private static readonly FlagSpec Agent = new(
        "agent", "alias", "Claim this directory for an agent: rewrite the managed grimoire:actor block in AGENTS.md.");

    private static readonly FlagSpec ClaimTome = new(
        "tome", "key", "Tome key for the claim block. Default: the alias as a tome key.");

    private static readonly FlagSpec Tome = new(
        "tome", "key",
        "Tome key for this operation. Default: GRIMOIRE_TOME, then the GRIMOIRE_ACTOR alias, then the AGENTS.md claim, then the configured default tome.");

    private static readonly FlagSpec Tomes = new(
        "tomes", "a,b", "Comma-separated tome keys to search. Default: every tome except your own.");

    private static readonly FlagSpec IncludeJournal = FlagSpec.Switch(
        "include-journal", "Opt into other tomes' journal entries; facts alone are searched by default.");

    private static readonly FlagSpec Attribution = new(
        "attribution", "none|light|full", "Actor fields on results: none, light (alias) or full (alias+vendor). Default: none.");

    private static readonly FlagSpec EnableAtLogin = FlagSpec.Switch(
        "enable-at-login", "Add the console to the desktop login session and exit; the entry names this binary.");

    private static readonly FlagSpec DisableAtLogin = FlagSpec.Switch(
        "disable-at-login", "Remove the console from the desktop login session and exit.");

    private static readonly FlagSpec Yes = FlagSpec.Switch(
        "yes", "Confirm the drop. Without it the command refuses and changes nothing.");

    private static readonly FlagSpec ProjectDisplayName = new(
        "display-name", "text", "Human-readable project name. Pass an empty value to clear it.");

    private static readonly FlagSpec ProjectBoardId = new(
        "board-project-id", "id", "Overmind board project id linked to the alias. Pass an empty value to clear it.");

    private static readonly CommandSpec[] Commands =
    [
        new CommandSpec
        {
            Name = "init",
            Summary = "Write the default config when missing, prepare the Qdrant collection, and with --agent claim this directory in AGENTS.md.",
            Syntax = "init",
            Flags = [Agent, ClaimTome],
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
            Name = "ui",
            Summary = "Open the console in the default browser, starting the systemd user unit first when it is down. " +
                      "With --enable-at-login or --disable-at-login it only manages the desktop login entry and exits.",
            Syntax = "ui",
            Flags = [EnableAtLogin, DisableAtLogin],
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
            Flags = [Scope, Kind, Tags, Importance, Ttl, Session, Project, Actor, Tome],
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
            Name = "brief",
            Summary = "Render the agent startup brief: persona, top facts, recent journal, tombstones.",
            Syntax = "brief",
            Flags = [ProjectFilter, Scope, FactsPerScope, JournalPerScope, TombstoneLimit],
        },
        new CommandSpec
        {
            Name = "scry",
            Summary = "Search other tomes anonymously: facts by default, journal opt-in, session never.",
            Syntax = "scry <query>",
            MinPositional = 1,
            Flags = [Tomes, IncludeJournal, Attribution, Limit],
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
            Name = "reindex",
            Summary = "Re-embed every active memory with the configured embedding model. Resumable: an interrupted run leaves the store read-refused until reindex completes.",
            Syntax = "reindex",
        },
        new CommandSpec
        {
            Name = "drop-collection",
            Summary = "Delete the whole Qdrant collection — every memory, tombstones included. Destructive and irreversible; operations stay refused until the store is recreated.",
            Syntax = "drop-collection",
            Flags = [Yes],
        },
        new CommandSpec
        {
            Name = "export",
            Summary = "Dump every memory — all statuses and tomes, vectors included — to a JSONL file stamped with the embedding fingerprint.",
            Syntax = "export [path]",
            Flags = [Gzip],
        },
        new CommandSpec
        {
            Name = "import",
            Summary = "Restore a store export: validates the embedding fingerprint against the configured model, then upserts records with ids, statuses, timestamps and vectors preserved. Re-running an import is safe.",
            Syntax = "import <path>",
            MinPositional = 1,
        },
        new CommandSpec
        {
            Name = "persona",
            Summary = "Show or edit the persona document read by agents.",
            Syntax = "persona [show | set-section <identity|tone|preferences|rules> <text>]",
            Flags = [Actor, Tome],
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
        new CommandSpec
        {
            Name = "projects",
            Summary = "Manage the project registry: aliases, local paths, board ids and display names. " +
                      "Sync pulls the project list from the local Overmind host, matching by board id; " +
                      "it adds new projects with a generated alias and marks vanished ones, never deleting.",
            Syntax = "projects [list | add <alias> <path>... | update <alias> [path]... | remove <alias> | sync]",
            Flags = [ProjectDisplayName, ProjectBoardId],
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
