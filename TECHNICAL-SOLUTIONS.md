# TECHNICAL-SOLUTIONS — Grimoire (ADR log)

> ADR-001..005 were drafted when the project ran under the working title "Deepmemo"
> (renamed to Grimoire by ADR-006); the reasoning is unaffected.

## ADR-001: Single binary, three faces, no internal hub — 2026-10-17

Status: Accepted

Context: the product must serve AI agents (MCP), the human (browse/edit UI) and scripts
(CLI) against one local memory store. Personal scale: one user, a few machines, thousands of
memories. Forces: zero-ops, "agent just works", minimal distribution surface.

Decision: one self-contained binary with three faces — `grimoire mcp` (stdio MCP server),
`grimoire serve` (loopback HTTP + static SPA + GC sweep), `grimoire <command>` (management
CLI). All faces are independent clients of Qdrant; there is no internal hub or IPC.
`serve` is optional for the agent and CLI faces.

Alternatives considered:
- Two binaries (mcp + cli) — lost: two artifacts to build, version and distribute for zero
  gain at personal scale.
- HTTP server as the hub with MCP/CLI proxying through it — lost: makes `serve` a hard
  prerequisite for the agent surface; the "only Qdrant must be up" property dies.
- Separate service per face — lost: no named driver (no independent deploy cadence, scaling
  or failure domain); layering mistaken for decomposition.

Consequences:
+ One artifact to distribute (dotnet tool or self-contained).
+ MCP and CLI work without the HTTP face; the UI is a pure addition.
− No single writer: correctness leans on Qdrant per-point atomicity and payload design
  (accepted at personal scale).

## ADR-002: Qdrant as the sole store — 2026-10-17

Status: Accepted (user decision in-session)

Context: memory needs semantic search plus rich filterable metadata. A Qdrant instance
already runs on the target machine (squeezr stack), so the ops cost is already paid.

Decision: one Qdrant collection holds vectors and the full payload (scope, kind, actor,
tags, timestamps, status). No SQL database anywhere in the solution.

Alternatives considered:
- SQLite + in-proc cosine — lost: user explicitly chose a real vector engine; it would be
  zero-dep but caps filtered ANN and diverges from already-running infra.
- Markdown files + separate index — lost: two sources of truth for one memory.
- Postgres + pgvector — lost: heavier ops footprint for personal scale.

Consequences:
+ Filtered ANN search and payload indexes; no ORM/EF layer at all.
− Qdrant becomes a required service (docker compose shipped in `deploy/`); a fully embedded
  mode does not exist in v1.

## ADR-003: Explicit write model — 2026-10-17

Status: Accepted (user decision in-session)

Context: two ways memory can appear — agents write deliberately, or the system watches
transcripts and extracts facts (mem0-style).

Decision: v1 is explicit-only. Agents call `remember`/`recall`/`forget` deliberately;
humans write via UI/CLI. No transcript mining. MCP tool descriptions carry the "when to
remember" guidance instead of an extraction pipeline.

Alternatives considered:
- LLM extraction pipeline in v1 — lost: pulls an LLM dependency into the core, +30% scope,
  nondeterministic core behavior.
- Both in v1 — lost: MVP bloat; extraction is a clean v2 increment over the same store.

Consequences:
+ Deterministic core with zero LLM dependencies.
− Memory quality depends on agent discipline (mitigated by tool descriptions and recall
  surface framing).

## ADR-004: Ollama-first embeddings behind a port — 2026-10-17

Status: Accepted (user decision in-session)

Decision: `IEmbeddingProvider` port in Core; v1 ships the Ollama HTTP adapter (model
configurable, default `nomic-embed-text`). An ONNX in-proc adapter is the next iteration
and gets its own ADR when it lands.

Alternatives considered:
- ONNX in-proc first — lost: bundles a ~30–90MB model into the release and adds tokenizer
  plumbing; delays v1 for a property (no service beyond Qdrant) nobody needs on day one.
- Both adapters in v1 — lost: MVP bloat.

Consequences:
+ Fast v1; provider swapped by config; vector dimension carried in collection config.
− Ollama becomes a required service (shipped in compose); fully self-contained offline mode
  waits for the ONNX adapter.

## ADR-005: Memory taxonomy and delete discipline — 2026-10-17

Status: Accepted

Decision: two orthogonal dimensions — **Scope** (`session` / `project` / `global` /
`personal`) × **Kind** (`journal` / `fact`); persona is a structured personal-scope fact.
`forget` always writes a tombstone (status + reason + timestamp); hard delete is GC's job
only (expired session records, aged tombstones). Every write carries actor provenance.

Origin: Overmind feature `132-deep-memories` (journal vs encyclopedia split, tombstone
discipline), generalized to a standalone product.

Alternatives considered:
- Single kind distinguished by tags — lost: journal and fact have different contracts
  (append vs curate), retention and UI affordances; the 132 analysis already split them.
- Hard delete on forget — lost: context-clash defense — agents resurrect deleted things
  unless a visible "this is dead" marker exists (rule proven in Overmind).

Consequences:
+ One uniform point schema; the UI renders kinds differently; full auditability.
− The payload schema is a contract from day one: additive-only evolution.

## ADR-006: Product name "Grimoire" — 2026-10-17

Status: Accepted (user decision, naming tournament final ballot)

Context: the working title "Deepmemo" was a first-idea placeholder. A 7-round naming
tournament (themed universes, heat-ordered spectrums, user-picked winners accumulating into
a pool of 8) ran per `audits/naming-tournament-2026-10-17.md`.

Decision: the product is named **Grimoire** («книга заклинаний агента»); runner-up Mneme is
recorded as the fallback. Solution, projects, namespaces, binary and config paths use the
Grimoire name (`Grimoire.sln`, `grimoire` CLI, `~/.grimoire/config.json`, `GRIMOIRE_*` env).
The Overmind board project row and the on-disk directory keep the Deepmemo name until the
human renames them (UI / `mv` + re-register).

Alternatives considered:
- Mneme (runner-up) — best ergonomics of the pool; lost the final ballot on vibe.
- Engram, Memex, Compost, Codex, Mycelium — eliminated across rounds on collision or vibe
  grounds (details in the tournament record).

Consequences:
+ A name with the exact vibe the user asked for («агент-маг, который помнит всё»).
− Two renames were manual: the Overmind board project row (renamed with the **GRR** number
  prefix) and the disk directory — both **completed 2026-10-17**.

## ADR-007: Books and Universe — tenant model over actor attribution — 2026-10-17

Status: Accepted (user decision in-session)

Context: Grimoire serves two scenarios — a coding crew where several agents (claude/qwen/
kimi) share one project's memory with attribution, and product assistants (e.g. Ainora)
where each persona must keep an isolated memory. Which memory is shared and which is not
needs an explicit model.

Decision: **Book** = a persona/tenant container keyed by the writer's actor alias. A book
holds the persona document (per-book, not global), the owner facts, and all memory records;
multiple actors may share one book (crew case), product assistants get one book each.
**Universe** = the only cross-book surface: a read-time query over designated scopes —
facts by default, journal opt-in, session never. Provenance (actor) is always stored in
payloads (ADR-005 discipline); anonymity of Universe answers is a presentation mode
(default: no attribution; param: light/full). Onboarding binding lives in the runtime's own
instruction files: `grimoire init --agent <alias>` writes a managed claim block into
AGENTS.md (precedent: squeezr's managed block), product assistants carry the name in their
system prompt. Every record already carries `actor` (ADR-005), so Books require **zero data
migration** — partitioning is read/write logic over the existing schema. Entity names
"Book"/"Universe" are working titles; a naming-sensei DOJO round runs inside feature 008,
and the winning names are renamed across backend code, MCP/REST contracts and UI **within
that feature** (user requirement: naming parity in code and UI, never docs-only).

Alternatives considered:
- Agent = book (partition by every writer) — lost: breaks the crew-sharing spirit of
  Overmind 132; attribution would fragment project memory per agent.
- Server-side agent registry + claim locks (Overmind 099 style) — lost: memory needs
  attribution and presence, not exclusivity; claim files stay a post-MVP option.
- env-only identity (no instruction-file binding) — lost: zero-config binding and
  visibility win; env stays step 1 of the resolution chain
  (param → env → instruction-file claim → clientInfo).

Consequences:
+ Both scenarios (crew / product assistants) run on one mechanic: isolation by default
  with a deliberate sharing door (Universe).
+ Zero migration — the schema already carries the book key.
− `memory_brief` and recall gain book-awareness (filters, per-book persona) — that is
  feature 008's scope, post-MVP.
− Session scope never crosses books; cross-agent serendipity is opt-in (journal) —
  accepted.
