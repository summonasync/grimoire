# Grimoire

> **Persistent memory for AI coding agents.** Every decision, preference and
> "the human said X" — written down, searchable, and remembered across sessions,
> projects and agents. Local-first. No cloud. No LLM in the core.

One binary, three faces:

| Face | Command | Who uses it |
|---|---|---|
| MCP server | `grimoire mcp` (stdio) | AI agents — Claude Code, Qwen Code, Kimi, anything speaking MCP |
| Web console | `grimoire serve` (loopback HTTP + SPA) | the human — browse, edit, tune |
| CLI | `grimoire <command>` | scripts and quick queries |

**Status**: the MVP plus honest-writes, startup-brief and the tome model are landed
and acceptance-verified end to end — all three faces (CLI, MCP stdio, loopback web
console) round-trip against a live Qdrant + Ollama, 656 tests green. Live work
tracking happens on the Overmind board; this README describes the shipped shape plus
the frozen contracts.

## The name

Seven rounds, seven themed universes, a pool of eight finalists, one user
vote. [The bracket](audits/naming-tournament-2026-10-17.md) is part of the
repo history. A grimoire is the agent's book of spells — everything it has
learned about you, written down where the next session can find it.

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
```

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

## How memory works

Every memory is one point in a Qdrant collection: a vector (embedding of the
content) plus a payload. Two orthogonal dimensions:

| | `journal` — subjective, append-heavy | `fact` — curated, provenance-tracked |
|---|---|---|
| `session` (TTL) | session observations | — |
| `project` | agent notes on a project | standing project decisions |
| `global` | cross-project "the human said X" | cross-project encyclopedia |
| `personal` | user habits and tastes | **persona** (identity / tone / rules) |

Memories are partitioned into **tomes** (ADR-007) — persona/tenant containers keyed by
the writer's actor alias. Each tome owns its persona, facts and journal; a coding crew
shares one tome, product assistants get one each. An agent's tome resolves from an
explicit param, `GRIMOIRE_TOME`, `GRIMOIRE_ACTOR`, the AGENTS.md claim block
(`grimoire init --agent`) or clientInfo, falling back to the `default` tome. Isolation
is the rule; `memory_scry` is the one deliberate cross-tome read — facts by default,
journal opt-in, session never, anonymous by default.

Recall is a hybrid score — similarity alone is not memory:

```
score = similarity × 0.5 ^ (age_days / halfLife(scope)) × (0.5 + 0.5 × importance)
```

Half-lives per scope: session 0.5d, project 30d, global 90d, personal 180d.
All defaults live in `~/.grimoire/config.json`, overridable with `GRIMOIRE_*`
env vars.

Standing rules, enforced from day one:

- **No silent deletes.** `forget` writes a tombstone (status + reason +
  timestamp); only the GC hard-deletes — expired session records and aged
  tombstones.
- **Every write carries provenance** — agent alias+vendor, or `human`.
- **Loopback only.** The web face binds `127.0.0.1`, period — the bind guard
  is tested against hostile environments.
- **No LLM in the core.** The core stores, scores and clusters; agents do the
  thinking. Journal → knowledge compaction: the core finds candidate clusters,
  an agent distills them into facts and tombstones the consumed entries.

## MCP tools

| Tool | Teaches the agent to… |
|---|---|
| `memory_brief` | load the startup brief in one call — persona, top facts, recent journal, tombstone digest |
| `memory_compact_candidates` | get journal clusters worth distilling into facts |
| `memory_forget` | retract with a reason (tombstone, never delete) |
| `memory_list` | browse recent active memories |
| `memory_persona_get` | read the tome's persona document |
| `memory_recall` | search before assuming — hybrid-scored, scope-filtered |
| `memory_remember` | write a memory (journal vs fact routing, scope choice) |
| `memory_scry` | read into other tomes — the one deliberate cross-tome surface |
| `memory_stats` | counts, `pendingCompaction`, provider health |

Tool descriptions are the extraction substitute — they are pinned by wire-level
contract tests.

## Configuration

`~/.grimoire/config.json`, every key overridable via `GRIMOIRE_<SECTION>__<PROP>`:

| Section | Keys (defaults) |
|---|---|
| `qdrant` | `endpoint` (`http://localhost:6334`), `apiKey?`, `collection` (`grimoire`) |
| `embedding` | `provider` (`ollama`), `model` (`nomic-embed-text`), `endpoint` (`http://localhost:11434`), `dimensions` (768) |
| `memory` | `defaultTome` (`default`), `sessionTtlHours` (24), `dedupSimilarityThreshold` (0.92), `recencyHalfLifeDays` per scope (0.5/30/90/180), `tombstoneRetentionDays` (30) |
| `serve` | `port` (4770), `gcIntervalMinutes` (30) |

Unknown keys are rejected — typos in a hand-edited file fail loudly.

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
[TECHNICAL-SOLUTIONS.md](TECHNICAL-SOLUTIONS.md) (ADR-001..007) — including
the tome tenant model (ADR-007): persona-isolated tomes with one deliberate
cross-tome read, `memory_scry`.
