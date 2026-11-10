# CONTEXT — Grimoire

> Working title was "Deepmemo"; renamed to **Grimoire** everywhere on 2026-10-17 (ADR-006) —
> repo, board project (GRR number prefix) and on-disk directory included.

## What this is

Grimoire is a local agent memory service: short-term (session), long-term (project/global)
and personal memory for AI coding agents, plus the tooling around it. One binary, three faces:

- **MCP server** (`grimoire mcp`, stdio) — the agent-facing surface: `memory_remember`,
  `memory_recall`, `memory_brief`, `memory_scry`, `memory_forget`, `memory_history`,
  `memory_restore`, `memory_list`, `memory_persona_get`, `memory_compact_candidates`,
  `memory_stats`, `memory_tome_create`. Every tool takes a `tome` param; omit it to work
  in your resolved tome (see Tomes below).
- **Local web server** (`grimoire serve`, loopback HTTP + static SPA) — the human-facing
  surface: browse/search/edit memories, manage the tome shelf (create manifests, edit
  descriptions, purge a tome behind a typed confirmation), edit persona, scry across
  tomes, view settings and stats; also runs the background GC sweep.
- **CLI** (`grimoire <command>`) — management and scripting: `init` (with `--agent` to claim
  a directory), `remember`, `recall`, `brief`, `scry`, `list`, `forget`, `gc`, `persona`,
  `stats`, `config`, `projects` (the project registry behind project tags).

The MCP tool surface is frozen by contract snapshots: `tests/Grimoire.Mcp.Tests/TestData/snapshots/<tool>.json`
pins each tool's title, description, annotations, input schema and the output shape observed
through the real transport, and `McpToolContractTests` turns any drift red — including a new
tool without a snapshot and a stale snapshot for a removed tool. To change a tool
intentionally: change the tool and update its snapshot in the same change (rerun the
contract tests with `GRIMOIRE_UPDATE_TOOL_SNAPSHOTS=1` to regenerate), so review sees the
contract diff in that JSON. The teaching-keyword pins in the same test class stay — they
guard teaching quality the snapshots cannot express.

All three faces are independent clients of one Qdrant instance (vectors + payload metadata).
There is no internal hub and no IPC between faces — Qdrant is the single source of truth.
`serve` is optional: the MCP and CLI faces work with only Qdrant running.

## Solution shape

One deployable (self-contained .NET 10 binary). Projects:

```
Grimoire.Cli (entry exe) ── hosts `mcp` and `serve` faces, routes management commands
├── Grimoire.Mcp           stdio MCP server (ModelContextProtocol SDK)
├── Grimoire.Api           Kestrel loopback host: minimal API + static SPA + GC sweep
├── Grimoire.Infrastructure Qdrant store, Ollama embeddings, config
└── Grimoire.Core          domain + ports (IMemoryStore, IEmbeddingProvider, IClock)
web/                       Next.js static-export SPA, served by Grimoire.Api
```

Dependency direction: `Core ← Infrastructure / Mcp / Api ← Cli`. `Mcp` and `Api` never
reference each other.

## Memory model

Every memory is a point in one Qdrant collection: a vector (embedding of the content) plus
a payload. Two orthogonal dimensions:

| | `journal` — subjective, append-heavy, in-the-voice-of-the-writer | `fact` — curated, edit-with-provenance |
|---|---|---|
| `session` (TTL, short-term) | session observations | — |
| `project` (long-term) | agent notes on a project | standing project decisions |
| `global` (long-term) | cross-project "the human said X" | cross-project encyclopedia |
| `personal` | user habits/tastes | **persona** (identity / tone / preferences / rules) |

- Every write carries **actor provenance** (agent alias+vendor, or `human`).
- **Recall is hybrid**: vector similarity × recency decay × importance, filtered by
  scope/kind/tags, active records only.
- **Tomes partition records** (ADR-007): a tome is a persona/tenant container keyed by the
  writer's actor alias. Each tome owns its persona document, facts and journal; several
  agents may share one tome (crew case), product assistants get one each. The default tome
  is `default`; keys are kebab-case. An agent's tome resolves by chain: explicit param →
  `GRIMOIRE_TOME` → `GRIMOIRE_ACTOR` alias → the AGENTS.md claim block (`grimoire init
  --agent`) → clientInfo actor → configured default.
- **Manifests name what a tome is for** (ADR-008): a light registry in a separate
  vector-less Qdrant collection (`grimoire-manifests`) holding `key`, `description`
  (1..280 chars, the routing hint) and `createdBy`. Agents create a tome via
  `memory_tome_create` only when nothing on the brief's tome table fits; records without
  a manifest stay first-class (a tome exists with a manifest or ≥1 record). Manifest
  failures degrade reads (brief carries a note) and never block memory paths.
- **Persona** is a first-class structured document in the personal scope of a tome, edited
  by humans (UI/CLI), read by agents via `memory_persona_get`.
- **Scry is the one cross-tome door** (ADR-007): `memory_scry` reads other tomes at query
  time — facts by default, journal opt-in, session never, anonymous by default
  (`light`/`full` attribution opt-in); the store always keeps full provenance.

## Standing rules

- **No silent deletes.** `forget` writes a tombstone (status + reason + timestamp). Hard
  delete is GC's job only: expired session records and aged tombstones — plus the
  human-only console purge of a whole tome (records in every status + manifest), gated
  by a typed confirmation of the tome key (ADR-005 amendment). Agents stay
  tombstone-only.
- **Loopback only.** The HTTP face binds `127.0.0.1` — that is the security boundary.
  Never bind a public interface without a new ADR.
- **Tome isolation.** Writes are tome-bound and never cross tomes; reading another tome you
  own and `memory_scry` are the deliberate doors (ADR-007). Session scope never crosses
  tomes at all.
- **Explicit writes only** in v1 — no auto-extraction from transcripts (ADR-003).
- **The core never runs an LLM.** Journal → knowledge compaction works as: the core detects
  candidates (`memory_compact_candidates` — clustering + staleness over stored embeddings,
  pure math), an agent client distills them into `kind=fact` records (`sources` provenance)
  and tombstones the consumed journal entries; `memory_stats` reports `pendingCompaction`.
  A dedicated memo-agent process is a v2 option, not v1.
- **Payload schema is additive-only** — it is a contract for stored data (ADR-005).

## Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / C# 14, single solution `Grimoire.sln` |
| Store | Qdrant (docker compose in `deploy/`) — sole store, no SQL database |
| Embeddings | Ollama (default adapter) behind `IEmbeddingProvider`; ONNX in-proc adapter planned |
| MCP | ModelContextProtocol C# SDK (stdio) |
| HTTP | ASP.NET Core minimal APIs, loopback bind |
| SPA | Next.js 16 static export, React 19, TypeScript strict, Tailwind 4, React Query 5, FSD |
| Tests | xUnit + Shouldly + NSubstitute, `tests/` mirrors `src/` |

## Origin

Extracted from the Overmind board feature `132-deep-memories` (cross-project agent memory:
journal + encyclopedia) into a standalone product. Feature 132 remains on the Overmind board
as the Overmind-side integration point (startup recall in briefing tools); whether Overmind
later consumes Grimoire via MCP is the Overmind project's decision. Named via a 7-round
naming tournament (see `audits/naming-tournament-2026-10-17.md`, ADR-006).

## Docs

- `STRUCTURE.md` — repo layout and conventions
- `TECHNICAL-SOLUTIONS.md` — ADRs (decision history)
- `AGENTS.md` — agent entry point
- `BACKLOG/` — read-only mirror of the Overmind board (never hand-edit)
