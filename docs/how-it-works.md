# How it works

The engine: what a memory is, how it is found, how it is corrected, and what
the server observed. Product behavior, not philosophy — every claim here is
enforced somewhere in the code.

## The memory model

Every memory is one point in a Qdrant collection: a vector (embedding of the
content) plus a payload. Two orthogonal dimensions:

| | `journal` — subjective, append-heavy | `fact` — curated, provenance-tracked |
|---|---|---|
| `session` (TTL) | session observations | — |
| `project` | agent notes on a project | standing project decisions |
| `global` | cross-project "the human said X" | cross-project encyclopedia |
| `personal` | user habits and tastes | **persona** (identity / tone / rules) |

## Tomes: tenant isolation with one deliberate door

Memories are partitioned into **tomes** (ADR-007) — persona/tenant containers
keyed by the writer's actor alias. Each tome owns its persona, facts and
journal; a coding crew shares one tome, product assistants get one each. Tome
resolution chain: explicit `tome` param → `GRIMOIRE_TOME` → `GRIMOIRE_ACTOR`
alias → AGENTS.md claim block (`grimoire init --agent`) → clientInfo →
`default`.

Isolation is the rule; `memory_scry` is the one deliberate cross-tome read —
facts by default, journal opt-in, session never, anonymous by default (the
store keeps full provenance regardless).

A tome can carry a **manifest** (ADR-008): a `description` naming what belongs
there plus `createdBy`, stored in a separate vector-less collection. The
manifest is the routing hint — agents read the tome table in `memory_brief`,
write to the fitting tome, and call `memory_tome_create` only when nothing
fits. A tome exists once it has a manifest or at least one record; records
without a manifest stay first-class. Manifest failures degrade reads, never
block memory paths.

## Recall: similarity alone is not memory

```
score = similarity × 0.5 ^ (age_days / halfLife(scope)) × (0.5 + 0.5 × importance)
```

Half-lives per scope: session 0.5 days, project 30, global 90, personal 180
(config `memory.recencyHalfLifeDays`). All knobs live in
`~/.grimoire/config.json` with `GRIMOIRE_*` env overrides — see
`docs/configuration.md`.

Reads narrow with `since`/`until` and `actor` on `memory_list`,
`memory_recall` and the loopback `GET /api/memories`. Timestamps are ISO-8601;
a date-only value means midnight local time; `until` is **exclusive**. The
`actor` filter matches the full string when it carries a vendor
(`Radim+claude` pins one writer) and the alias part otherwise (`Radim` also
matches `Radim+claude`, case-insensitive).

## Corrections: tombstones, chains, and the GC's honest leftovers

- **No silent deletes.** `memory_forget` writes a tombstone (status + reason +
  timestamp). Only GC hard-deletes: expired **Active** session records and aged
  tombstones. A session record past its TTL is not reaped once it changes
  status — a Superseded one stays for good (the correction chain reads through
  it), a Tombstoned one stays until `memory.tombstoneRetentionDays` retires it.
  GC reports those leftovers (`RetainedPastTtl`) instead of pretending zero
  (ADR-005, 2026-11-13 amendment).
- **`memory_history`** walks a memory's full supersede chain; **
  **`memory_restore`** reverses a correction that was itself a mistake — the
  record returns to active, the correction gets tombstoned on the correction
  record.
- The one extra hard-delete door is the human's: the console's tome purge
  removes every record plus the manifest behind a typed confirmation of the
  exact tome key. No undo. Agents never hard-delete.

## Provenance and project identity

Every write carries actor provenance (alias+vendor, or `human`). Project-scope
writes demand a registered `projectId`: scope=`project` is rejected at the
write door unless the id names an entry of the `projects` registry; unknown
ones are refused with the registered set spelled out. Omit it and the working
directory picks the project (longest-prefix match over registered paths —
worktrees resolve too; the MCP server inherits the client's cwd, so start
`grimoire mcp` from the project root). On `global`/`personal` the projectId is
optional provenance.

## Compaction without an LLM in the core

The core stores, scores and clusters; agents do the thinking. Journal →
knowledge compaction: `memory_compact_candidates` detects clusters of old
similar journal entries (pure math over stored embeddings), the agent distills
them into `kind=fact` records with `sources` provenance and tombstones the
consumed entries; `memory_stats` reports `pendingCompaction`.

## What the server saw: the handshake registry

`grimoire mcp` records one row per connection on its **first request** —
client name/version, negotiated protocol version, resolved actor, transport,
host, timestamp — into a vector-less `grimoire-sessions` collection. `serve`
projects rows over `GET /api/sessions` (`limit` 1..500 default 50;
`windowHours` 0..8760; unusable parameters get a 400 that names the parameter
before the registry is read) and the console renders the panel under
"Clients the server saw at initialize".

The claim is deliberately narrow: observed handshakes, never presence — no
"online", no connection count; a connection that sends no request is never
recorded; `sessionId` is null on stdio; a client with no reported identity is
stored with the chain's `unknown` alias. The MCP face writes and never prunes;
`serve` reads, prunes and runs the retention sweep. Reads degrade instead of
failing: an unreachable registry answers an empty list plus a
`degradationReason`.

## The MCP tool surface (contract-frozen)

Sixteen tools, pinned by wire-level contract snapshots — descriptions are the
extraction substitute, and `ReadmeToolTableTests` guards the table against
drift:

| Tool | What it is for |
|---|---|
| `memory_brief` | one-call startup brief: persona, top facts, recent journal, tombstone digest; `daily: true` folds in the day's `daily-notes`; budget cuts named in `omitted` + `omissionDigest` |
| `memory_compact_candidates` | journal clusters worth distilling into facts |
| `memory_digest` | everything one tome knows about a topic — facts, still-news journal, tombstones that used to be true; plain and `topic:`-stamped tags both match |
| `memory_forget` | retract with a reason (tombstone, never delete) |
| `memory_history` | the full supersede chain before you trust or overwrite |
| `memory_restore` | reverse a correction that was itself a mistake |
| `memory_list` | browse recent active memories by filter |
| `memory_note` | zero-ceremony journal write; neighbor-vote topic stamping with a confidence threshold — pass an explicit `topic` for anything you will ask about by topic |
| `memory_persona_get` | read the tome's persona document |
| `memory_recall` | hybrid-scored search before you assume |
| `memory_remember` | the durable write door: journal vs fact routing, scope choice, project identity rules |
| `memory_scry` | the one deliberate cross-tome read |
| `memory_stats` | counts, `pendingCompaction`, provider health, `serverVersion` |
| `memory_tome_create` | open a tome when nothing fits; the description is the routing hint |
| `projects` | read/amend the alias→workspace registry; `Sync` pulls from the local Overmind host, dropped entries marked gone, not deleted |
| `session_identity_set` | this session's actor alias+vendor for write provenance; connection-scoped, latest wins, `--actor` outranks it |

## Standing rules in one breath

Loopback-only bind (guard tested against hostile environments). Additive-only
payload schema — stored data keeps reading correctly across versions. Durable
facts in English, the human's quotes verbatim as spoken. Explicit writes only:
nothing is auto-extracted from transcripts.
