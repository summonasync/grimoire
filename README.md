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
loopback web console) round-trip against a live Qdrant + Ollama. Test counts
live in git, the CI run and the board — not in this paragraph, because a number
here goes stale on every merge. Live work tracking happens on the Overmind
board; this README describes the shipped shape plus the frozen contracts.

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
also the answer to the other question an empty report raises: `report` has no
`--tome` flag, so the tome it reads comes from the chain below (`GRIMOIRE_TOME`,
then the `GRIMOIRE_ACTOR` alias, then the AGENTS.md claim, then the configured
default) — a day that looks empty may simply live in another tome, and the echo
names the one it actually read. `--project` shares its help
text with `brief` (where the same filter is described as "the brief"); for
`report` it filters the reported day's records to that project.

The same notes have a second read, folded into the startup brief: `grimoire
brief --daily` and `memory_brief` with `daily: true` add a `daily` section —
active journal records tagged `daily-notes` within the brief's scope filter
(`Session` is never briefed), in two buckets: today and the first day behind
it that holds a note (up to a week back), grouped by actor,
oldest entry first. The tag is the write side's convention, not this read's
invention: daily notes are written once, tagged `daily-notes` by the session
that wrote them (the rule in `CONTEXT.md`), and the daily section is the other
end of that deal. A record that lands in the daily section is dropped from the
brief's `journal` section, so the same note is never paid for twice. `report`
and `--daily` differ in shape — and, when `memory.dailyTimeZone` names a zone
other than the machine's, in which calendar day each one calls today: `report`
is always machine-local, `--daily` cuts its day in the configured zone.

Two windows and a purse decide what the section shows. The day is cut at
local midnight in the zone `memory.dailyTimeZone` names, so "today" means
today in that zone — the machine's own day while the knob is unset. Entries are
budgeted: one longer than 1500 characters is cut and flagged `truncated`, and
once 6000 characters are spent the rest is only counted, as a number in `more`.
Either outcome names `daily` in `omitted`: a capped body or a counted tail is
content the section knows about and does not print. Both numbers are code
defaults, not config keys; the two knobs listed under `memory` are all the
daily section exposes.

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

### The brief's four knobs

`grimoire brief` renders the startup brief in one of two shapes and under two
optional bounds:

| Flag | Behavior |
|---|---|
| `--format text` (default) | the human brief on stdout; with `--daily`, `Warning:` lines trail the sections |
| `--format hook` | one JSON `SessionStart` envelope on stdout and nothing else — the same `Warning:` lines go to stderr instead. This is the shape the [SessionStart hook](#sessionstart-hook-the-agent-boots-with-the-brief) forwards |
| `--max-chars <n>` | Total character budget for the brief; anything that does not fit is named, not silently dropped. Positive integer only (`0`, `1.5` and `wide` all exit usage naming the flag and the value); unset means the engine's own budget. The cap goes into Core's budget walk before anything renders, and the daily purse tightens with it (`min(cap, configured)`) — a small `--max-chars` cannot leave a 6000-character daily flood behind |
| `--since` / `--until <time>` | Only count memories created inside `[since, until)`. `yyyy-MM-dd` resolves to midnight in the machine's local zone; an ISO-8601 instant carries its own offset. `since` is inclusive, `until` is **exclusive** — a record standing exactly on the upper bound is out. An unparsable value exits usage naming the flag and the value; `since` after `until` exits usage naming `'since'` |

The window bounds **content**, not the lifecycle counters: `tombstones` and
`compactionDue` stay all-time whatever `--since`/`--until` say. Deliberately —
`compactionDue` is store health, and graves are dated by when they were
tombstoned, which a created-at window cannot cut without inventing a second
windowing mechanism. `memory_brief` on the MCP face takes no window at all; the
bounded read is the CLI's.

### What fell out of the brief

Sections fill in priority order under the budget, and when something does not
fit the payload says so twice: `omitted` names the sections that lost content,
`omissionDigest` names the records. Four record-carrying sections produce a
digest — `facts`, `tombstones`, `journal`, `daily` — and a section key appears
only when that section really lost something, always in that order. Each line is
an id, a reason and a one-line excerpt (`read-limit` and friends are the text
face; the wire carries the enum names verbatim):

| Loss | Text face | Wire | What happened |
|---|---|---|---|
| read limit | `read-limit` | `ReadLimit` | more matched than the per-scope count asked for (`--facts-per-scope`, `--journal-per-scope`, `--tombstone-limit`) |
| section budget | `over-budget` | `SectionBudget` | the section fell out of the character budget — the heading still prints as `persona: (omitted: over budget)` and its records become digest lines |
| daily purse | `daily-purse` | `DailyPurse` | the daily section spent its character purse and only counted the rest |
| excerpt cut | ` [cut]` | `"excerptCut": true` | the digest line itself is shorter than the record |

`persona` and `tomes` can fall out of budget too, and they keep their older
face: the section name in `omitted`, and for the tome table its own
`(+N more)` line. They are not sets of memory records with ids, so no digest
line can name what fell out of them.

An excerpt at or under 300 characters prints whole; above it, it is cut to 80
and flagged. Both numbers are Core defaults (`BriefNeverTruncateChars`,
`BriefDigestExcerptChars`) shared by both faces — chosen, not measured, and not
config keys. The whole map is absent from the payload when nothing fell out:
never an empty object, never `[]`. Nothing is deleted by any of this — the id on
a digest line is the record's handle, taken directly by `memory_history` and by
`grimoire forget <id|query>`, and the excerpt is what a targeted `memory_recall`
query finds again.

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
match wins. The four flag-less CLI reads (`brief`, `list`, `recall`, `report`)
walk the same chain and the tome it resolves is the tome they read; they just
never consult `--actor`/`--tome` (they have no such flags) and never reach the
clientInfo step below, so their parity with the MCP face runs exactly to that
boundary and no further. The steps, write face first:

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

### SessionStart hook: the agent boots with the brief

The repo ships the two scripts a Qwen Code `SessionStart` hook needs —
`deploy/hooks/grimoire-session-start.sh` (Linux/macOS) and
`deploy/hooks/grimoire-session-start.ps1` (Windows). Each runs
`grimoire brief --format hook --daily` and forwards the CLI's envelope line
untouched, so what the model gets is exactly what the store answered.

The runtime does not append the payload to the conversation: it places
`hookSpecificOutput.additionalContext` inside the system instruction, wrapped in
its own `<qwen:session-start-context>` block, HTML-escapes `<` and `>` inside
that block, and re-stamps it per source (`startup`, `resume`, `compact`) instead
of accumulating one block per fire — all measured on Qwen Code 0.27.1 against a
recording model endpoint. That is why the brief arrives tag-free: the CLI's hook
face rewrites every angle bracket to its square-bracket twin, so board XML
markers, `<removed …>` tombstones and `alias+vendor` placeholders survive the
trip readable instead of being mangled by a second escaping pass.

Register the hook **user-scope** — `~/.qwen/settings.json` (or
`$QWEN_HOME/settings.json`). The measured install uses these fields; the
annotations describe the 0.27.1 behavior this hook relies on — receipts in
`audits/t7-live-firing-receipt.md` cover the firing, escaping and re-stamp,
while `type`/`command`/`name`/`description`/`timeout`/`env` are what the shipped
snippet ran with end to end:

```jsonc
{
  "hooks": {
    "SessionStart": [
      {
        // regex against the event source: startup | resume | clear | compact; omit to match every source
        "matcher": "startup|resume|compact",
        // hooks inside one group run in parallel unless this is true
        "sequential": false,
        "hooks": [
          {
            "type": "command",
            "command": "bash /path/to/Grimoire/deploy/hooks/grimoire-session-start.sh",
            "name": "grimoire-brief",
            "description": "Start the session with the Grimoire brief",
            // SECONDS (default 60; a value of 1000 or more is still read as legacy milliseconds)
            "timeout": 24,
            "env": { "HOOK_GRIMOIRE_BIN": "/home/you/.dotnet/tools/grimoire" },
            "shell": "bash"
          }
        ]
      }
    ]
  }
}
```

Windows is the same block with
`"command": "powershell -NoProfile -ExecutionPolicy Bypass -File C:\\path\\to\\Grimoire\\deploy\\hooks\\grimoire-session-start.ps1"`
and `"shell": "powershell"`. The `env` block is how a non-PATH install (or a
test rig) points the hook at its own binary.

A project-scope `<project>/.qwen/settings.json` hook is a different door: it runs
only after you approve that exact configuration or trust the folder
(`security.folderTrust.enabled` plus a trust decision), and a block still
awaiting its first decision is withheld — which looks exactly like "the hook
never fired". User scope has no such gate; that is why this install is
user-scope. Hook definitions are read at startup, so after editing the file run
`/hooks` to reload or restart the session (`/hooks list` in a non-interactive run
only prints what that process already loaded; there is no `qwen hooks`
subcommand).

**Sizing `timeout`.** The script spends its budget twice — once draining the
hook payload from stdin, once calling the CLI — so the worst case is
`2 × HOOK_GRIMOIRE_TIMEOUT` plus a second of teardown. Set the per-hook `timeout`
to at least `2 × HOOK_GRIMOIRE_TIMEOUT + 2`: at the default budget of 10 that is
22; the snippet carries 24 for slack, which is what the twins' worst-case
measurement (two budgets plus the kill grace) leaves room for. The per-hook
`timeout` is the runtime's hard ceiling and the knob's range exists to keep it
meaningful — a budget of 60 or more could never finish before the runtime cut it.

**Knobs.** Both scripts read the same two variables and both validate before
using them:

| Variable | Meaning |
|---|---|
| `HOOK_GRIMOIRE_BIN` | Absolute path to the `grimoire` binary. Unset: `PATH`, then `~/.dotnet/tools/grimoire` (on Windows `%USERPROFILE%\.dotnet\tools\grimoire.exe`). |
| `HOOK_GRIMOIRE_TIMEOUT` | Seconds of budget per leg. Integers `1..59` only — `0`, `-3`, `1.5`, `abc`, `60`, `2147484` and `99999999999` are all rejected and the default `10` is used instead. A leading zero is accepted as the number it reads (`07` is 7), identically on both platforms. |

The `HOOK_` prefix is not decoration: the CLI claims every `GRIMOIRE_*` variable
as a config overlay and refuses a name it does not own, so a knob called
`GRIMOIRE_BIN` would break the very call it is meant to configure (measured:
`error [config]: Environment override 'GRIMOIRE_BIN' does not match any
configuration property`).

**When Grimoire is down, the session is fine.** Fail-open is the whole contract:
store unreachable, binary missing, CLI silent, CLI confused — every path prints a
parseable empty envelope
(`{"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":""}}`),
exits 0 and leaves one `grimoire-session-start: <reason>` line on stderr. The
runtime drops a whitespace-empty `additionalContext`, so the session starts
clean, with no brief and no trace of the failure in the prompt. A broken Grimoire
costs a session nothing but the brief.

Measured through the shipped `.sh` on this tree, for scale: a live brief arrives
in 0.43 s as one 31 KB envelope line; a refused store returns the 79-byte empty
envelope in 0.26 s with one stderr line; a missing binary does it in 14 ms; and
against an unroutable endpoint the script cuts itself at 10.03 s (2.03 s with
`HOOK_GRIMOIRE_TIMEOUT=2`), still exiting 0 with the empty envelope. Only the
last case ever spends the budget. `bash -n` and the PowerShell language parser
pass on the shipped bytes, and both twins were byte-compared against each other
and against the direct CLI call on the same live store. Surfaces this feature did
not exercise: Windows itself (the `.ps1` ran under PowerShell 7.7 on Linux),
PowerShell 5.1's parser, and `shellcheck` (not installed here). On macOS,
`timeout` is coreutils — the `.sh` uses it when present and runs unbounded when it
is not, leaving the per-hook `timeout` as the only ceiling;
`brew install coreutils` closes that.

## Distribution

The CLI ships as a dotnet tool: `src/Grimoire.Cli` packs
`Grimoire.<version>.nupkg` (version from `Directory.Build.props`, tool
command `grimoire`). The public feed (nuget.org) is an owner decision still
pending, so the shipped channel today is a local folder feed built from
this repo:

```bash
dotnet pack src/Grimoire.Cli -c Release -o build/packages
dotnet tool install -g Grimoire --add-source build/packages --no-cache
grimoire --version    # -> grimoire 0.9.0
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
Warning: Deployed SPA export reports version 0.1.0 while this binary is 0.9.0; the served UI was built for a different release. Rebuild web/ and redeploy the export.
Warning: Deployed SPA export version is unknown (grimoire-web.json is absent); it was never compared against this binary (0.9.0).
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
  timestamp); only the GC hard-deletes — expired **Active** session records and
  aged tombstones. A session record past its TTL is not reaped by that rule once
  it changes status: a Superseded one stays for good (the correction chain reads
  through it), a Tombstoned one stays until retention retires it as an aged
  tombstone. Either way GC reports the leftover (`past TTL kept by policy`)
  instead of pretending it is zero — superseded rows are what the correction
  chain reads through (ADR-005, 2026-11-13 amendment). The one extra hard-delete
  door is the human's: the console tome purge, gated by a typed confirmation
  (ADR-005 amendment).
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

## Recent MCP handshakes

`grimoire mcp` records what it observed at the protocol level. The first request of a
connection writes one row into a vector-less `grimoire-sessions` collection — client name
and version, negotiated protocol version, the actor the chain resolved, transport, host,
timestamp. `grimoire serve` projects those rows over `GET /api/sessions` (`limit` 1..500,
default 50; `windowHours` 0..8760, default the configured retention; anything unusable is
refused with a 400 that names the parameter before the registry is read) and the console's
**Recent MCP handshakes** panel renders them under the heading "Clients the server saw at
initialize".

The panel reports observed handshakes. That is the whole claim, and these are the things it
deliberately does not say:

- **No presence, no "online", no connection count.** A row is one handshake at one
  timestamp; it never means the client is connected now. The same client reconnecting four
  times is four rows, and aged rows simply leave the window — nothing marks them dead.
- **A connection that never sends a request is never recorded.** The write happens on the
  first request through the transport, not at socket open. An empty panel means "no
  handshake observed in the window", and the panel says exactly that.
- **`sessionId` is null on stdio.** A stdio connection has no MCP session id to report, so
  the field stays null there rather than carrying a stand-in value.
- **A client that reports no identity is stored as `unknown`.** When the actor chain
  resolves nothing, the row carries the chain's `unknown` alias — the same provenance the
  memory-write path uses, not a placeholder invented for the console.

Ownership is split one way: the MCP face writes and never prunes, `serve` reads and prunes,
and a retention sweep runs in the serve process only. Reads degrade instead of failing — an
unreachable registry answers an empty list plus a `degradationReason`, and the panel shows
"Handshake registry unavailable" rather than pretending the window is empty.

## MCP tools

| Tool | Teaches the agent to… |
|---|---|
| `memory_brief` | load the startup brief in one call — persona, top facts, recent journal, tombstone digest; `daily: true` folds in the day's `daily-notes` section; what the budget cut is named twice, the sections in `omitted` and the records in `omissionDigest` |
| `memory_compact_candidates` | get journal clusters worth distilling into facts |
| `memory_digest` | pull everything one tome knows about a topic in a single read — the standing facts, the journal entries that are still news, and the tombstones that used to be true; a `topic:` tag matches in both shapes (the `topic:<value>` stamp and the bare slug), so asking by topic needs no tag archaeology; what the budget cut off is named in `omitted` |
| `memory_forget` | retract with a reason (tombstone, never delete) |
| `memory_history` | walk a memory's full supersede chain before trusting or overwriting it |
| `memory_restore` | reverse a correction that was itself a mistake — the record returns, the superseder is tombstoned |
| `memory_list` | browse recent active memories |
| `memory_note` | write a journal note; without an explicit `topic` the embedding neighbors vote — two agreeing at ≥ 0.75 nominate their topic, but the nomination is stamped only when the winner's best similarity also reaches `TopicConfidenceThreshold` (0.8), otherwise nothing is tagged — the response says which (`topicDecision`, `topicRationale`), so pass an explicit topic for anything you will later ask about by topic |
| `memory_persona_get` | read the tome's persona document |
| `memory_recall` | search before assuming — hybrid-scored, scope-filtered |
| `memory_remember` | write a memory (journal vs fact routing, scope choice); Project scope demands a registered projectId or auto-tags one from the working directory |
| `memory_scry` | read into other tomes — the one deliberate cross-tome surface |
| `memory_stats` | counts, `pendingCompaction`, provider health |
| `memory_tome_create` | open a new tome when nothing fits — the description is the routing hint other agents read in the brief's tome table |
| `projects` | read and amend the alias→workspace registry — the kebab-case aliases with absolute paths that a `Project`-scope write is auto-tagged from, plus the optional board link; an alias the registry does not know is rejected with the registered set spelled out, and `Sync` pulls the project list from the local Overmind host, marking what the board dropped as gone rather than deleting it |
| `session_identity_set` | say who is talking — this session's actor alias and vendor — so every later write carries real provenance instead of the shared placeholder; the latest pair wins, it lives for the connection only and is never persisted, and a `--actor` flag still outranks it |

Tool descriptions are the extraction substitute — they are pinned by
wire-level contract tests.

## Configuration

`~/.grimoire/config.json`, every key overridable via
`GRIMOIRE_<SECTION>__<PROP>`; a root-level key such as `defaultTome` overrides
as `GRIMOIRE_DEFAULTTOME`:

| Section | Keys (defaults) |
|---|---|
| *(root)* | `defaultTome` (`default`) |
| `qdrant` | `endpoint` (`http://localhost:6334`), `apiKey?`, `collection` (`grimoire`), `sessionCollection?` (`<collection>-sessions`) |
| `embedding` | `provider` (`ollama`), `model` (`qwen3-embedding`), `endpoint` (`http://localhost:11434`), `dimensions` (1024) |
| `memory` | `sessionTtlHours` (24), `dedupSimilarityThreshold` (0.92), `recencyHalfLifeDays` per scope (0.5/30/90/180), `tombstoneRetentionDays` (30), `dailyTag` (`daily-notes`), `dailyTimeZone` (unset = machine-local day) |
| `sessions` | `retentionHours?` (unset follows `memory.sessionTtlHours`) |
| `serve` | `port` (4770), `gcIntervalMinutes` (30) |

Unknown keys are rejected — typos in a hand-edited file fail loudly. The
same strictness guards the environment: the daily knobs are overridable as
`GRIMOIRE_MEMORY__DAILYTAG` and `GRIMOIRE_MEMORY__DAILYTIMEZONE`, and a name
that matches no property — `GRIMOIRE_MEMORY__DAILYZONETIMEZONE`, say — fails
the load instead of being silently ignored.
`grimoire config set` addresses `section.property` pairs, so a root-level key
like `defaultTome` is edited directly in the file. That key is the tail of the
tome chain, not its head: `GRIMOIRE_TOME` outranks it on the MCP and web faces,
on the `--tome` flag of the verbs that have it, and on the four flag-less CLI
reads (`brief`, `list`, `recall`, `report`), which resolve the tome they read
through `GRIMOIRE_TOME` → the `GRIMOIRE_ACTOR` alias → the AGENTS.md claim →
this default.
An unusable handshake window — `sessions.retentionHours` (or its
`memory.sessionTtlHours` fallback) that is not finite or not positive — stops `serve`
at startup with a config error naming the knob, because a zero window would retire
live handshake rows instead of aged ones.

The topic vote has three knobs and none of them is a config key:
`NearTopicSimilarityThreshold` (0.75) decides which neighbors are near enough to
cast a vote, `TopicAgreementCount` (2) how many must name the same topic, and
`TopicConfidenceThreshold` (0.8) the winner's own similarity required before the
stamp lands. They are defaults on `MemoryEngineOptions` with no `memory` section
entry — `EngineOptionsMapper` never reads them — so
`GRIMOIRE_MEMORY__TOPICCONFIDENCETHRESHOLD` fails the load like any other unknown
name, and retuning one means editing the default where it is declared.

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
