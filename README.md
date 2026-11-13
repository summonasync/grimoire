```
{  ·  ·  ·  ·  ·  }
 |   .-------.   |
 ·   | {:·:} |   ·
 |   '-------'   |
 ·      |·|      ·
{  ·  ·  '·'  ·  }

 ██████╗ ██████╗ ██╗███╗   ███╗ ██████╗ ██╗██████╗ ███████╗
██╔════╝ ██╔══██╗██║████╗ ████║██╔═══██╗██║██╔══██╗██╔════╝
██║  ███╗██████╔╝██║██╔████╔██║██║   ██║██║██████╔╝█████╗
██║   ██║██╔══██╗██║██║╚██╔╝██║██║   ██║██║██╔══██╗██╔══╝
╚██████╔╝██║  ██║██║██║ ╚═╝ ██║╚██████╔╝██║██║  ██║███████╗
 ╚═════╝ ╚═╝  ╚═╝╚═╝╚═╝     ╚═╝ ╚═════╝ ╚═╝╚═╝  ╚═╝╚══════╝
```

**One shared memory. Every agent. Every project.**  
*The book the summoner writes results into.*

`await SummonAsync();` — part of the summoner's kit  
overmind · **▸ grimoire** · squeezr · mindlash studio

---

## What it is

Persistent memory for AI coding agents. Every decision, preference and
"the human said X" — written down, searchable, and remembered across
sessions, projects and agents. Local-first. No cloud. No LLM in the core:
it stores, scores and clusters; the agents do the thinking.

One binary, three faces:

| Face | Command | Who uses it |
|---|---|---|
| MCP server | `grimoire mcp` (stdio) | AI agents — Claude Code, Qwen Code, Kimi, anything speaking MCP |
| Web console | `grimoire serve` (loopback HTTP + SPA), opened by `grimoire ui` | the human — browse, edit, tune |
| CLI | `grimoire <command>` | scripts and quick queries |

**Status**: the MVP plus honest-writes, startup-brief and the tome model are
landed and acceptance-verified end to end — all three faces (CLI, MCP stdio,
loopback web console) round-trip against a live Qdrant + Ollama, 2041 tests
green. Live work tracking happens on the Overmind board; this README
describes the shipped shape plus the frozen contracts.

## Why it exists

A single developer with an agent crew can hold more context in flight than
a single developer alone — but only if the crew carries what it learns
between sessions and between projects. Otherwise you spend the day
re-explaining the same preferences and decisions to four different CLIs,
and the explanation is the work. Overmind holds what one project knows;
Grimoire is the layer that survives the session — and every other project.

## The name

Seven rounds, seven themed universes, a pool of eight finalists, one user
vote. [The bracket](audits/naming-tournament-2026-10-17.md) is part of the
repo history. A grimoire is the agent's book of spells — everything it has
learned about you, written down where the next session can find it.

## Trade-offs

Read these before you adopt. They are the same weight as the feature list —
that is the deal.

- **Semantic recall needs Qdrant + an embedding provider.** That is Docker
  and Ollama on the machine before the memory gets smart.
- **Loopback-only HTTP by design**: no remote console, no phone, no team
  view.
- **Young: 0.x.** Contracts are described as frozen in this README, but a
  0.x is a 0.x.
- **Value depends on discipline** — a memory store nobody writes to is an
  empty room. The crew contract (write the moment a decision becomes known)
  is part of the product, not an afterthought.

## Quickstart

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com), Docker.

```bash
# 1. Local infrastructure (Qdrant + Ollama, loopback only)
docker compose -f deploy/docker-compose.yml up -d

# 2. Build and test
dotnet build Grimoire.sln
dotnet test Grimoire.sln

# 3. First run
grimoire init     # writes ~/.grimoire/config.json, creates the collection
grimoire serve    # web console on http://127.0.0.1:4770

# 4. Or let the console come to you
grimoire ui       # opens http://grimoire.localhost:4770 in the default browser,
                  # starting the systemd user unit first when nothing is serving

# 5. Or read the crew's day back
grimoire report           # evening report of daily notes: today's active records, grouped by actor
grimoire brief --daily    # the same day folded into the startup brief
```

`grimoire ui` probes `http://127.0.0.1:<serve.port>/health`; when nothing
answers it runs `systemctl --user start grimoire-serve` once and waits (10s
budget) before opening. A console that never answers is a failure, not a
blank tab: the command reports the remedy and exits 6. The address it shows
is `grimoire.localhost` when that name resolves to loopback, otherwise the
literal `127.0.0.1` — the health probe itself always uses the literal,
because the server binds IPv4 loopback only.

Login autostart is opt-in and reversible, and neither flag opens anything:

```bash
grimoire ui --enable-at-login    # writes ~/.config/autostart/grimoire-ui.desktop (one report line, exit 0)
grimoire ui --disable-at-login   # removes it; reports cleanly when there is nothing to remove
```

The entry names the binary you ran, so re-enabling after a release
refreshes it. Running either flag twice is a no-op: a matching entry
reports "already up to date" and is not rewritten.

`grimoire report` is the evening read of a day's work: active records of one
local day grouped by actor, newest first inside each group. `--date
<yyyy-MM-dd>` picks the day (default today, resolved in the local zone),
`--tag` overrides the `daily-notes` default, `--project <id>` narrows the
day to one project, and `--json` prints the same report as data. An empty
day says so in one explicit line naming the window, tags and tome it
actually read — `No daily notes in 2026-11-12 (window
2026-11-11T21:00:00Z..2026-11-12T21:00:00Z, tags daily-notes, tome
default).` — never silence, never a header implying content. That line is
also the answer to the other question an empty report raises: `report`
reads the engine's default tome and has no `--tome` flag, so a day that
looks empty may simply live in another tome. `--project` shares its help
text with `brief` (where the same filter is described as "the brief"); for
`report` it filters the reported day's records to that project.

The same day has a second read, folded into the startup brief: `grimoire
brief --daily` and `memory_brief` with `daily: true` add a `daily` section —
active journal records tagged `daily-notes` within the brief's scope filter
(`Session` is never briefed), in two buckets: today and the first day behind
it that holds a note (up to a week back), grouped by actor,
oldest entry first. The tag is the write side's convention, not this read's
invention: daily notes are written once, tagged `daily-notes` by the session
that wrote them (the rule in `CONTEXT.md`), and the daily section is the other
end of that deal. A record that lands in the daily section is dropped from the
brief's `journal` section, so the same note is never paid for twice. `report`
and `--daily` differ in shape, not in truth: `report` reads one named local
day, `--daily` reads today plus the last day that actually had a note.

Two windows and a purse decide what the section shows. The day is cut at
local midnight in the zone `memory.dailyTimeZone` names, so "today" means
today where the machine's owner lives. Entries are budgeted: one longer than
1500 characters is cut and flagged `truncated`, and once 6000 characters are
spent the rest is only counted — a number in `more`, and `daily` named in
`omitted`. Both numbers are code defaults, not config keys; the two knobs
listed under `memory` are all the daily section exposes.

A degraded config says so instead of rendering an empty room, and it says so
only when the daily section was asked for. A blank `memory.dailyTag` disables
the section and warns naming the key: an empty value is used as given and
matches no record, so the payload shows an empty section plus that warning —
it is not a reset to `daily-notes`. An unset or blank
`memory.dailyTimeZone` means the machine-local day and stays silent, because
that is the default, not a failure; only an id that does not resolve falls
back to UTC, and then it warns naming the id. Both lines arrive in the
payload's `warnings` field, disabled-section first and zone second, and the
CLI prints the same two after the section as `Warning:` lines.

What does not cross to the CLI is the tome-level warning: `grimoire brief`
never prints `tome '…' is empty or does not exist` while `memory_brief` does.
That asymmetry predates the daily section and is not something to align. The
tome warning is narrower than the hint beside it, too: it fires only for a
tome requested by name that holds no record at all anywhere in the store —
`KnownTomeKeys` collects the tome of every Active and Tombstoned record — so a
tome with a single daily note in it is known and never warned about, and since
the daily section counts toward emptiness, a daily-only tome no longer gets a
`hint` preaching "tome is empty". Hint and warning are two signals with
different reach, not one switch.

Wire an agent to the memory (stdio MCP):

```json
{
  "grimoire": {
    "command": "grimoire",
    "args": ["mcp"],
    "env": { "GRIMOIRE_ACTOR": "dan+claude" }
  }
}
```

When a crew of agents shares one memory, give every session its own actor.
Every write is stamped with one, resolved through a single chain — first
match wins:

1. the explicit `--actor` flag,
2. `session_identity_set(alias, vendor)` — the MCP tool an agent calls once at
   session start when its runtime hands it a stable alias; the pair lives for
   the connection only and is never persisted,
3. the identity file `~/.overmind/identity/parent-<ppid>.json` — the fallback
   channel: the Overmind adapter writes it when the agent session registers,
   keyed by the parent PID of the server process; Grimoire is a read-only
   consumer and skips it silently when the file is absent,
4. the `GRIMOIRE_ACTOR` environment variable (`<alias>` or `<alias>+<vendor>`),
5. the claim block in AGENTS.md (`alias:`, optional `vendor:`, `tome:`),
6. the MCP client name; plain `human` as the last resort.

Do not set `GRIMOIRE_ACTOR` at machine scope (shell profile, system-wide
environment). A session launched outside the wrapper that was meant to own the
value then silently writes under that shared actor — exactly how the LUCA
deployment misattributed a whole crew. Keep `GRIMOIRE_ACTOR` inside the
per-agent launcher that sets it, one wrapper per crew member — `Gandalf.cmd`
sets `GRIMOIRE_ACTOR=Gandalf+claude`:

```bat
@echo off
set GRIMOIRE_ACTOR=Gandalf+claude
set GRIMOIRE_TOME=crew
claude
```

Set `GRIMOIRE_TOME` too — one kebab-case tome key for the whole crew. Without
it the tome resolves from each actor's alias and the crew forks into one tome
per agent; the explicit key keeps every agent in one tome with distinct
actors. A session started outside its launcher falls down the chain to the
shared attribution; restarting it through the launcher (or letting it call
`session_identity_set`) fixes attribution for everything written afterwards.

### Teaching agents the memory

An agent that never learns about the memory never calls it. `grimoire install`
stamps a short managed block — what the memory is, `memory_brief` at session
start, write-as-you-go, and the tome routing table — into every agent home
memory file it finds on this machine:

```bash
grimoire install            # upsert the block into ~/.claude/CLAUDE.md,
                            # ~/.qwen/QWEN.md, ~/.codex/AGENTS.md,
                            # ~/.kimi-code/AGENTS.md
grimoire install --print-block   # print the block without touching any file
grimoire install --remove        # strip the block, leave the rest alone
```

The block lives between `<!-- grimoire:begin -->` and `<!-- grimoire:end -->`
markers: rerunning the verb replaces the marked range in place (so your own
prose around it stays untouched), appends to files without it, and reports one
outcome line per file — `created`, `updated`, `unchanged`, `removed` or
`skipped (not found)`. Missing files are skipped, never created; a file is
detected only when it already exists, and homes resolve under the user profile
or `GRIMOIRE_HOME` when set (the flag the test rigs and sandboxes use).

Kit coexistence: on machines where the OvermindCrewKit mirrors already carry
the same block, the verb is idempotent — reruns report `unchanged`, and
`--remove` followed by the kit's own deploy restores the same text. The kit
owns its copies; this verb only manages what it stamped.

## Distribution

The CLI ships as a dotnet tool: `src/Grimoire.Cli` packs
`Grimoire.<version>.nupkg` (version from `Directory.Build.props`, tool
command `grimoire`). The public feed (nuget.org) is an owner decision still
pending, so the shipped channel today is a local folder feed built from
this repo:

```bash
dotnet pack src/Grimoire.Cli -c Release -o build/packages
dotnet tool install -g Grimoire --add-source build/packages --no-cache
grimoire --version    # -> grimoire 0.8.0
```

`-g` lands `grimoire` on PATH (`~/.dotnet/tools` on Linux/macOS,
`%USERPROFILE%\.dotnet\tools` on Windows; use `--tool-path <dir>` for
anywhere else). One package covers win/mac/linux — as a
framework-dependent tool it rides an installed .NET 10 runtime; machines
without one need the runtime or the self-contained release binary instead.
External installers (Overmind's assisted install) spawn exactly these
commands and then probe `grimoire --version`.

The console UI does not ride the package: `grimoire serve` reads the static
export off disk — `~/.grimoire/web`, or `--web-root <dir>` for anywhere else —
so packing only the CLI ships a web face built for the previous release. The
export step, from the repo root:

```bash
(cd web && npm ci && npm run build)   # postbuild emits web/out/grimoire-web.json
rsync -a --delete web/out/ ~/.grimoire/web/
```

`npm run build` exports the SPA and `web/scripts/emit-web-stamp.mjs` stamps the
export with `productVersion` (read from `Directory.Build.props`, the single
version truth), `webSha` and `builtAt`. `serve` compares that stamp against the
running binary at boot and prints what it found. Both branches, verbatim from a
boot of this branch's binary:

```
Warning: Deployed SPA export reports version 0.1.0 while this binary is 0.8.0; the served UI was built for a different release. Rebuild web/ and redeploy the export.
Warning: Deployed SPA export version is unknown (grimoire-web.json is absent); it was never compared against this binary (0.8.0).
```

The first is a mismatch and names both versions; the second is the
absent-or-unreadable branch, which says unknown and never claims stale — there
was nothing to measure. Either way the host still serves the UI; the point is
that a skipped deploy has stopped being silent.

Release record: the `audits/release-<ver>-<date>.md` audit (precedent
`audits/release-0.8.0-2026-11-10.md`) must name the deployed stamp's
`productVersion` — the value `cat ~/.grimoire/web/grimoire-web.json` answers
after the copy. An audit without that line is an incomplete release.

Windows: a hidden-console launcher, a logon task and the config edits a fresh box
needed live in [`deploy/windows/`](deploy/windows/README.md) (`grimoire ui` is
Linux-only today).

The local infrastructure story is unchanged: Qdrant + Ollama via
[`deploy/docker-compose.yml`](deploy/docker-compose.yml), isolated
per-product variant
[`deploy/docker-compose.grimoire.yml`](deploy/docker-compose.grimoire.yml).

## How memory works

Every memory is one point in a Qdrant collection: a vector (embedding of
the content) plus a payload. Two orthogonal dimensions:

| | `journal` — subjective, append-heavy | `fact` — curated, provenance-tracked |
|---|---|---|
| `session` (TTL) | session observations | — |
| `project` | agent notes on a project | standing project decisions |
| `global` | cross-project "the human said X" | cross-project encyclopedia |
| `personal` | user habits and tastes | **persona** (identity / tone / rules) |

Memories are partitioned into **tomes** (ADR-007) — persona/tenant
containers keyed by the writer's actor alias. Each tome owns its persona,
facts and journal; a coding crew shares one tome, product assistants get
one each. An agent's tome resolves from an explicit param, `GRIMOIRE_TOME`,
`GRIMOIRE_ACTOR`, the AGENTS.md claim block (`grimoire init --agent`) or
clientInfo, falling back to the `default` tome. Isolation is the rule;
`memory_scry` is the one deliberate cross-tome read — facts by default,
journal opt-in, session never, anonymous by default.

A tome can carry a **manifest** (ADR-008) — a `description` naming what
belongs there, plus `createdBy` provenance, stored in a separate
vector-less Qdrant collection. The manifest is the routing hint: agents
read the tome table in `memory_brief`, write to the fitting tome, and call
`memory_tome_create` only when no existing tome fits. A tome exists once it
has a manifest or at least one record; records without a manifest stay
first-class. The human owns the destructive door: the console's tome purge
removes all records (tombstones included) plus the manifest behind a typed
confirmation of the exact tome key — no undo. Agents never hard-delete;
`memory_forget` tombstones.

Recall is a hybrid score — similarity alone is not memory:

```
score = similarity × 0.5 ^ (age_days / halfLife(scope)) × (0.5 + 0.5 × importance)
```

Half-lives per scope: session 0.5d, project 30d, global 90d, personal 180d.
All defaults live in `~/.grimoire/config.json`, overridable with
`GRIMOIRE_*` env vars.

Reads narrow with `since`/`until` and `actor` — the same parameters on
`memory_list`, `memory_recall` and the loopback `GET /api/memories`.
Timestamps are ISO-8601; a date-only value means midnight local time, and
`until` is exclusive — `until=2026-10-27` is everything written before that
day. The `actor` filter matches the full string when it carries a vendor
(`Radim+claude` pins one writer) and the alias part otherwise (`Radim` also
matches `Radim+claude`, case-insensitive).

Standing rules, enforced from day one:

- **No silent deletes.** `forget` writes a tombstone (status + reason +
  timestamp); only the GC hard-deletes — expired session records and aged
  tombstones. The one extra hard-delete door is the human's: the console
  tome purge, gated by a typed confirmation (ADR-005 amendment).
- **Every write carries provenance** — agent alias+vendor, or `human`.
- **Project writes carry a registered projectId.** scope=`project` is
  rejected at the write door unless the projectId names an entry of the
  `projects` registry in `~/.grimoire/config.json`; an unknown one is
  rejected with the registered set. Omit it and the caller's working
  directory picks the project automatically (longest-prefix match over
  registered paths, so worktrees resolve too) — the MCP server inherits the
  cwd of the client that launched it, so start `grimoire mcp` from the
  project root. On `global`/`personal` the projectId is optional
  provenance.
- **Loopback only.** The web face binds `127.0.0.1`, period — the bind
  guard is tested against hostile environments.
- **No LLM in the core.** The core stores, scores and clusters; agents do
  the thinking. Journal → knowledge compaction: the core finds candidate
  clusters, an agent distills them into facts and tombstones the consumed
  entries.
- **Language canon.** Durable facts are written in English; the human's
  verbatim quotes stay as spoken — a convention the tool descriptions
  teach, not a store-enforced rule.

## MCP tools

| Tool | Teaches the agent to… |
|---|---|
| `memory_brief` | load the startup brief in one call — persona, top facts, recent journal, tombstone digest; `daily: true` folds in the day's `daily-notes` section |
| `memory_compact_candidates` | get journal clusters worth distilling into facts |
| `memory_forget` | retract with a reason (tombstone, never delete) |
| `memory_history` | walk a memory's full supersede chain before trusting or overwriting it |
| `memory_restore` | reverse a correction that was itself a mistake — the record returns, the superseder is tombstoned |
| `memory_list` | browse recent active memories |
| `memory_persona_get` | read the tome's persona document |
| `memory_recall` | search before assuming — hybrid-scored, scope-filtered |
| `memory_remember` | write a memory (journal vs fact routing, scope choice); Project scope demands a registered projectId or auto-tags one from the working directory |
| `memory_scry` | read into other tomes — the one deliberate cross-tome surface |
| `memory_stats` | counts, `pendingCompaction`, provider health |
| `memory_tome_create` | open a new tome when nothing fits — the description is the routing hint other agents read in the brief's tome table |

Tool descriptions are the extraction substitute — they are pinned by
wire-level contract tests.

## Configuration

`~/.grimoire/config.json`, every key overridable via
`GRIMOIRE_<SECTION>__<PROP>`; a root-level key such as `defaultTome` overrides
as `GRIMOIRE_DEFAULTTOME`:

| Section | Keys (defaults) |
|---|---|
| *(root)* | `defaultTome` (`default`) |
| `qdrant` | `endpoint` (`http://localhost:6334`), `apiKey?`, `collection` (`grimoire`) |
| `embedding` | `provider` (`ollama`), `model` (`qwen3-embedding`), `endpoint` (`http://localhost:11434`), `dimensions` (1024) |
| `memory` | `sessionTtlHours` (24), `dedupSimilarityThreshold` (0.92), `recencyHalfLifeDays` per scope (0.5/30/90/180), `tombstoneRetentionDays` (30), `dailyTag` (`daily-notes`), `dailyTimeZone` (unset = machine-local day) |
| `serve` | `port` (4770), `gcIntervalMinutes` (30) |

Unknown keys are rejected — typos in a hand-edited file fail loudly. The
same strictness guards the environment: the daily knobs are overridable as
`GRIMOIRE_MEMORY__DAILYTAG` and `GRIMOIRE_MEMORY__DAILYTIMEZONE`, and a name
that matches no property — `GRIMOIRE_MEMORY__DAILYZONETIMEZONE`, say — fails
the load instead of being silently ignored.
`grimoire config set` addresses `section.property` pairs, so a root-level key
like `defaultTome` is edited directly in the file. That key is also the tome
the CLI `report` reads: `GRIMOIRE_TOME` steers the MCP and web faces and the
`--tome` flag chain of the verbs that have it, and `report` has no such flag.

## Development

```bash
dotnet build Grimoire.sln
dotnet test Grimoire.sln
dotnet format --verify-no-changes
```

```
Grimoire.sln
├── src/
│   ├── Grimoire.Core/             domain + ports, zero external packages
│   ├── Grimoire.Infrastructure/   Qdrant store, Ollama embeddings, config
│   ├── Grimoire.Mcp/              stdio MCP face (ModelContextProtocol SDK)
│   ├── Grimoire.Api/              loopback Kestrel host, REST, GC sweep
│   └── Grimoire.Cli/              entry exe — hosts both faces, CLI commands
├── web/                           Next.js static-export console, served by Grimoire.Api
├── tests/                         mirrors src/ — xUnit + Shouldly + NSubstitute
├── deploy/docker-compose.yml      qdrant + ollama
└── audits/                        reviews, naming tournament, product strategy
```

Dependency direction: `Core ← Infrastructure / Mcp / Api ← Cli`. `Mcp` and
`Api` never reference each other. Decisions live in
[TECHNICAL-SOLUTIONS.md](TECHNICAL-SOLUTIONS.md) (ADR-001..008) — including
the tome tenant model (ADR-007): persona-isolated tomes with one deliberate
cross-tome read, `memory_scry`, and the manifest shelf they are routed by
(ADR-008).

---

*summonasync — one dev, a swarm of agents, nothing leaves the machine.*  
overmind · grimoire · squeezr · mindlash studio

Everything here runs on your machine: the store lives in `~/.grimoire`, the
web face binds `127.0.0.1` only. If it does not — that is a bug, file it.
