# Grimoire product strategy — agent-UX lessons from Overmind

- Author: product-strategist
- Date: 2026-10-17
- Inputs: Overmind board project `ff313f29-8144-488c-94e6-5ea5dde57bfd` (entries + features 013, 020, 029, 032, 036, 037, 044, 052, 063, 068, 084, 099, 104, 106, 109, 114, 121, 132), Grimoire docs (CONTEXT.md, STRUCTURE.md, TECHNICAL-SOLUTIONS.md ADR-001..006, AGENTS.md), Grimoire board `7450aeab-137e-446d-88ed-5ffc9b624a1b` feature 001-agent-memory-mvp + validated spec `7684c2ac`, audits/review-001-01.md.
- Method: every claim below cites the Overmind record it comes from. This is a proposal doc — no board writes, no code touched.

---

## 1. Lessons from Overmind

Each lesson: what the record said → what it means for Grimoire (tool set, MCP descriptions, memory scopes, human–agent interaction).

### L1 — Tool names and schemas are load-bearing product surface; churn silently breaks agents

**Record:** Friction log `500f8197` ("Agent-side friction log (Qwen Code, Bob.AI sessions, 2026-10-03/04)"), item 1: agent runtimes pin hot tools by NAME (`tools.visible`); Overmind kept tool names and schemas stable so the pins survive releases — "churn silently un-pins tools".

**For Grimoire:** the 7 tool names (`memory_remember`, `memory_recall`, `memory_forget`, `memory_list`, `memory_persona_get`, `memory_compact_candidates`, `memory_stats`) and their parameter shapes are a contract from day one, not implementation detail. Rename = migration event, never a refactor side effect. Additive parameter evolution only (mirrors the additive-only payload rule in ADR-005).

### L2 — Inconsistent parameter naming taxes every session

**Record:** Friction log `500f8197` item 5 (`note` vs `logNote`; artifact UUID-only addressing); entry `22440990` (schema papercuts: `slug` vs `featureSlug`, `isCurrent` undocumented, `read_entry` requiring `entryId` while every response says `id`).

**For Grimoire:** one naming convention across the tool family, stated once and enforced by contract tests: `memoryId` everywhere (never `id` in one tool and `memoryId` in another), `query` for search text, `scope`/`kind` as the same enum spellings as the payload. Human-locating selectors (exact text match, tag) alongside UUIDs, because agents quote memory text far more often than they carry UUIDs across turns.

### L3 — Selectors must match what the agent SEES, and writes must echo back what landed

**Record:** Friction log `500f8197` item 2: `acIndex` was 0-based while every rendered list was 1-based — a silent wrong tick (a successful write of the wrong thing). The fix set: accept exact-text as the primary selector, echo the ticked item text in the response. Entry `73add129` adds: a tool that cannot undo its smallest mutation "invites leaving the lie in place".

**For Grimoire:** `recall`/`forget`/`list` responses must carry the exact content text an agent would quote. A follow-up `memory_forget` by id must be answerable from what `recall` just returned. Any dedup/supersede decision made server-side during `remember` must be echoed (`superseded: <id>`), so the agent sees what happened to its write — the 068 "mutation transparency" principle applied to memory.

### L4 — A write that times out but still commits is the worst failure mode for trust

**Record:** Friction log `500f8197` item 3 → feature `104-materialization-off-write-path`: `planning_patch_artifact` timed out at 10s while the write HAD committed; blind retry creates double versions, no retry risks a lost write. Fix: honest commit-state in timeout errors + a verify-probe keyed by an idempotency token (logNote).

**For Grimoire:** every `remember` performs a network embedding call (Ollama) before the Qdrant upsert — two timeout windows per write. The spec says "errors structured, never raw exceptions" but does not define commit-state semantics. Needed: content-hash idempotency key on writes (safe retry), and error payloads that state whether the record landed (`committed: true/false`), so an agent's retry logic is deterministic, not hopeful.

### L5 — Token budget is a product resource; mutation tools must not echo bodies back

**Record:** Feature `037-mcp-echo-slimming` (user session 2026-09-20: "planning_upsert_artifact эхом возвращает всё тело — тратить огромное количество токенов"; ~30 mutations × KB bodies per night). Compact adapter-level projection: `{id, kind, status, version}` + bodyChars + log tail.

**For Grimoire:** `remember` returning the full stored content it just received is the same waste. Mutation responses should be compact projections (`id`, `scope`, `kind`, `deduped`, `supersededBy?`, `bodyChars`, score/health when relevant). Read tools (`recall`, `list`, `persona_get`) keep full bodies — there the body IS the payload.

### L6 — One cheap startup call beats N category reads — and agents adopt a service only if it is load-bearing at startup

**Record:** Feature `052-agent-briefing-tools` (the whole point: "агенту на старте нужны все категории разом"); friction log closing paragraph: Overmind is adopted BECAUSE it is load-bearing ("single source beats low friction"); feature `132-deep-memories` requirement: "Startup recall surface: one cheap call to load the encyclopedia (and optionally recent journal)".

**For Grimoire:** agent startup recall is the single most important flow of the product, and the spec currently needs 2–3 round trips for it (`memory_persona_get` + `memory_recall` global + `memory_recall` project). A one-call `memory_brief` (persona + top standing facts by scope + recent journal + tombstone digest) is the Grimoire equivalent of `overmind_briefing` — and the tombstone digest is the anti-resurrect guard (L8).

### L7 — Teaching happens on the tool's own surfaces: descriptions, quickstart, teaching-errors — pinned by contract tests

**Record:** Features `013-mcp-agent-onboarding`, `032-status-model-self-documentation`, `036-agent-workflow-onboarding` (p0: "всё нужное должно приезжать с самим Overmind" — teach the flow in tool descriptions, quickstart text, and 400s that name the next step; `WorkflowTeachingContractTests` + `PromptAndFailureModeTests` pin the texts against drift, including an anti-drift guard against live `ListTools`).

**For Grimoire:** ADR-003 makes tool descriptions the extraction substitute — which makes them load-bearing product surface, exactly like Overmind's quickstart. They need: when-to-remember / when-to-recall guidance, scope-choice rules (session vs project vs global vs personal), the compaction loop walkthrough, and contract tests that fail the build when a description drifts. Teaching-errors: `remember` with an ambiguous scope → 400-ish structured error teaching the scope model, not a generic validation message.

### L8 — Visible death beats absent trace — and the tombstone list must be IN the startup surface

**Record:** Feature `029-entry-tombstones` + quickstart rule "check tombstones before proposing anything that smells like a resurrect"; 132 requirement "tombstone on retract (never silent deletes)"; brief 052 puts tombstones into the briefing output.

**For Grimoire:** ADR-005 already adopts tombstone-not-delete — the record proves the decision right. The part the MVP spec misses: recalled/listed/briefed output must make tombstones visible enough that an agent does not re-assert a forgotten fact. A recent-forgets digest in `memory_brief` (L6) is the cheap, proven shape.

### L9 — Multi-agent reality: identity, liveness and provenance are product features, not metadata

**Record:** Feature `099-agent-claims` (alias+vendor identity "Dan by qwen", 409 naming the live holder, claim age as ghost control, alias naming rules surfaced in onboarding — `121-alias-rules-editor`, `114-claims-onboarding-docs`); features `043-status-provenance` / `068-mutation-transparency` (every log line carries the door marker `[agent]/[ui]/[auto]`).

**For Grimoire:** the `actor` provenance field exists in the spec (good — it came from 132), but it is a free string with no convention and no session concept. The Overmind-proven shape: alias rules surfaced at onboarding/first call, vendor+alias discrimination, and provenance markers that let a HUMAN reading the UI see who wrote what and when. Memory written by two agents in one project without identity discipline degrades into anonymous noise exactly like an unclaimed board did.

### L10 — Human–agent gates must be readable from ONE place, and the cheapest approval channel must have a deterministic mechanism

**Record:** Entries `22440990` (onboard said `humanGate=off`, validate returned 409 "Human approval required" — two sources of truth; the autonomous wave stalled on the first gate) and `74eb89d8` (same class: chat approval existed but had no deterministic proof path; the agent retried validate in a loop). Feature `109-done-gate-human-tap` shows the working version: gate mechanics (proof newer than artifact) pinned in one place and taught in the 409 text.

**For Grimoire:** v1 is explicit-write with no human gates — fine. But the moment the UI introduces any curation gate (compaction review, persona edit locks, GC confirmation), the gate state must be readable by the agent from the SAME surface that reports configuration, and every gate rejection must name the deterministic unblocking path. This is a design-time lesson: do not grow a second place where "what requires a human" lives.

### L11 — Background side-effects must surface failure loudly, or trust dies silently

**Record:** Entry `ccf52ead` (Squeezr materializer froze the mirror for days — every write re-failed the pass, nothing surfaced beyond a field in the onboard blob; the data existed, the alert did not); entry `73add129` part 2 (clobber-guard deadlock after an aborted pass — the error text misattributed the drift); feature `104` (failures surface in `lastAttempt`); feature `074` (materialization run diagnostics).

**For Grimoire:** the GC sweep inside `grimoire serve` and the compaction candidate detection are background passes over stored data. Their failure must be visible in `/health`, `memory_stats` (and the SPA) — lastRun/lastError, consecutive-failure count — not just a log line. Also per-item failure isolation: one unwritable/broken record must not abort the whole sweep (the Squeezr abort-vs-refuse lesson).

### L12 — Renames, moves and model changes are the disaster class — plan the migration path before the data exists

**Record:** Entry `b5875176` (folder rename silently orphaned the whole board; entries survived only by manual re-typing, losing createdAt/history; onboard's resolve-or-create made it worse). Grimoire itself just lived a rename (Deepmemo → Grimoire, ADR-006) with two manual renames still pending.

**For Grimoire:** the analogous silent-killers are (a) embedding provider/model change → all stored vectors invalid for the new space, (b) collection rename, (c) machine move. The spec has none of: dimension/model drift detection at `init`/`remember` (loud error, not garbage recall), a `grimoire reindex` story, or an export/import path. This is cheap to design now (config carries the model fingerprint in collection metadata) and near-impossible to retrofit after real data accumulates.

### L13 — Round-trips are prompt overhead: batch operations and cheap reads are first-class

**Record:** Feature `044-batch-planning-ops` (35 tasks = 70 round-trips vs one file-script call — "order-of-magnitude difference"); `sections=` cheap reads (praised in `73add129` closing paragraph); `037` compact echoes.

**For Grimoire:** the compaction flow is inherently batch (fetch N candidates → write M facts → tombstone N journals). If that takes 3 round-trips per cluster, agents will not do it and `pendingCompaction` grows forever. Remember should accept an array (or a dedicated batch op); candidate fetch should take limit/scope filters; the apply loop should be teachable in one description paragraph (L7).

### L14 — Corrections need a first-class path, or the system teaches agents to leave lies

**Record:** Entry `73add129` (no AC un-tick → "оставил врать" until manually noticed); feature `106-log-self-edit` (compromise: edit your OWN newest line, with version trace — auditability preserved, clutter cut); feature `045-artifact-version-diff` (history is substance: "what exactly changed"); decision `faf9aff5` (escape hatch: done from any status — simplicity beats ceremony at personal scale).

**For Grimoire:** the spec has tombstone (with reason) but no restore, no un-supersede, and `PATCH` edits content without a visible provenance trail beyond actor. Memory is human-corrected constantly ("that's wrong, forget it" / "actually the decision changed") — the correction loop must be: forget (tombstone) → restore within retention (undo), supersede chain walkable, edit-with-reason. Small surface, huge trust payoff.

### L15 — Proven the "protect it" list (friction log closing paragraph)

STOP semantics (obey-or-ask, never work around), frozen-artifact discipline with logNote, structured `<refs>` vs prose, byte-identical no-op reporting, and 400-with-diagnostics error style ("it is how the 0-based trap got caught"). **For Grimoire:** structured errors that dump the actual state (which scopes exist, which record matched) are not noise — they are the diagnostics that made Overmind debuggable BY AGENTS. Keep the error contract rich, never terse.

---

## 2. Gap analysis — MVP spec vs what the experience suggests

### What the experience validates as RIGHT (keep, do not touch)

| Spec decision | Evidence it is right |
|---|---|
| Tombstone-not-delete (ADR-005) | 029-entry-tombstones; context-clash rule "proven in Overmind" |
| Provenance on every write | 099-agent-claims, 043/068 markers |
| Explicit writes; tool descriptions as the teaching surface (ADR-003) | 036/013/032 — teaching on own surfaces, pinned by tests |
| Scope × kind taxonomy (session/project/global/personal × journal/fact) | 132-deep-memories two-kind analysis |
| Hybrid recall scoring (similarity × recency × importance) | matches journal-vs-encyclopedia contracts |
| One binary / three faces / no hub (ADR-001); Qdrant sole store (ADR-002) | single-host, zero-ops reality that made Overmind adoptable |
| Non-goals: auto-extraction, KG, memo-agent, auth | the Overmind record shows no demand signal for these at personal scale |

### What is MISSING (the experience says the spec should carry it)

1. **Honest write semantics** (L4): no idempotency key, no commit-state in timeout errors, no dedup-probe race story for concurrent agents. `104` is the exact precedent. The payload schema is additive-only — adding `idempotencyKey` now is legal; adding retry semantics after dogfooding is painful.
2. **Startup one-call brief** (L6): recall+persona is 2–3 round trips; `132` explicitly demanded one cheap startup call. No tombstone digest anywhere in read surfaces (L8).
3. **Correction affordances** (L14): no restore/un-tombstone, no un-supersede, no edit-reason trail. `73add129` shows what that costs.
4. **Actor identity conventions** (L9): `actor` is a free string; no alias rules, no session discrimination; two agents in one project can supersede each other invisibly.
5. **Description/pin contract tests** (L7): the quality bar pins build/test/loopback but not the teaching texts — the one surface ADR-003 leans on.
6. **Background failure surfacing** (L11): GC sweep has no lastRun/lastError/consecutive-failure surface in stats or /health.
7. **Migration & drift** (L12): no embedding-model fingerprint check, no reindex, no export/import.

### What is PREMATURE (rightly non-goals — do not let scope creep in)

- Auto-extraction from transcripts (ADR-003 v2) — the Overmind record shows explicit-write plus good descriptions was sufficient for weeks of dogfooding.
- Notification center / agent→human channel (Overmind 020/084/087) — Grimoire has no board state to argue about; the human door is the SPA. Revisit only if compaction review becomes gated.
- Importance auto-tuning, knowledge graph, memo-agent process — no evidence of need; keep non-goals.
- Session-identity infrastructure à la `AgentSession` (099) — v1 alias+vendor string is enough; add liveness only when two agents actually fight over one fact in practice.

---

## 3. Tool-set vision

### Current surface (spec v2, 7 tools)

`memory_remember`, `memory_recall`, `memory_forget`, `memory_list`, `memory_persona_get`, `memory_compact_candidates`, `memory_stats`.

### Target surface (post-MVP, evidence-ranked)

| Tool / change | Purpose | Grounded in |
|---|---|---|
| **`memory_brief`** (new) | One-call startup: persona + top standing facts (global→project) + recent journal + tombstone digest + store health line | L6 (052, 132 "startup recall surface"), L8 (029) |
| **`memory_restore`** (new) | Un-tombstone / un-supersede within retention window, reason required | L14 (73add129, 106) |
| **`remember` batch + `sources` param** (extend) | Array content; `sources` ids for compaction facts — closes the distillation loop without extra tools | L13 (044), 132 journal→encyclopedia |
| **Mutation echo slimming** (contract) | remember/forget/restore return compact projections + what-changed (`deduped`, `supersededBy`), never the full content echo | L5 (037), L3 (068) |
| **Structured teaching errors** (contract) | Scope-ambiguity, model-drift, dedup-conflict errors name the fix and the next call | L7/L15 (036, 032) |
| **`memory_audit`** (v1.5, candidate) | Walk one record's provenance: actor history, supersede chain, compaction sources | L14 (045 diff, 068) |

### CLI/HTTP implications

- CLI: `grimoire brief`, `grimoire restore <id> --reason`, `grimoire reindex` (embedding change), `grimoire export|import` (migration/machine move).
- HTTP: `POST /api/memories/{id}/restore`; `GET /api/brief`; `/health` extended with GC sweep lastRun/lastError + consecutive-failure count (074/104 pattern); PATCH accepts `reason` for the provenance trail.
- Config: collection metadata carries `embeddingProvider` + `model` + `dimensions` fingerprint, checked at `init`, `remember` and `serve` boot — mismatch = loud structured error naming `grimoire reindex` (L12).

### Agent-ergonomics requirements (the non-negotiables)

1. Stable names/schemas; additive evolution only (L1).
2. One parameter-naming convention across the family, contract-tested (L2).
3. Responses carry the exact text/selectors the agent needs for the next call; server-side decisions (dedup, supersede) echoed back (L3).
4. Idempotent writes; timeout errors state commit-state (L4).
5. Mutations echo compact; reads carry bodies (L5).
6. Startup = one call including the tombstone digest (L6, L8).
7. Tool descriptions teach when/what/where to remember — pinned by contract tests (L7).
8. Errors dump state + recovery path, never raw exceptions (L15).
9. Correction loop (forget → restore → edit-with-reason) is fully agent-drivable (L14).
10. Background passes surface failure in stats/health, per-item isolation (L11).

---

## 4. Feature proposals

Numbering continues the Grimoire board sequence (001 in flight). Sizes: S ≤ 1 day, M = 2–3 days, L = a week of agent waves.

### P1 — `003-honest-writes` — priority p1, size M
**Goal:** deterministic write semantics: content-hash idempotency key on `remember` (payload additive change), timeout errors state committed-or-not, dedup probe race safety for concurrent agents, compact mutation echoes (no content echo). **Why now:** every day of dogfooding before this accumulates double-writes and broken retry habits; the payload schema change is only free while the collection is young. **Evidence:** friction log `500f8197` items 1/3; features `104-materialization-off-write-path`, `037-mcp-echo-slimming`, `068-mutation-transparency`.

### P2 — `002-agent-startup-brief` — priority p1, size S/M
**Goal:** `memory_brief` MCP tool + `grimoire brief` CLI + `GET /api/brief`: persona + scoped top facts + recent journal + tombstone digest + health line, one call. **Why now:** it is THE adoption surface — agents adopt what is load-bearing at session start; also the cheapest anti-resurrect defense. **Evidence:** `052-agent-briefing-tools`, `132-deep-memories` (startup recall requirement), friction log "single source beats low friction".

### P3 — `004-memory-corrections` — priority p2, size S/M
**Goal:** correction loop: `memory_restore` (un-tombstone/un-supersede within retention, reason), supersede-chain walk in list/recall responses, PATCH-with-reason provenance trail, `sources` on remember for the compaction flow. **Why now:** humans correct memories constantly from day one; without a cheap correction path the store accretes known-lies (the exact failure 73add129 documents). **Evidence:** `73add129` (no un-tick → "оставил врать"), `106-log-self-edit`, `045-artifact-version-diff`, `029-entry-tombstones`.

### P4 — `005-teaching-contract` — priority p2, size S
**Goal:** contract tests pin tool descriptions, parameter names and error texts (anti-drift guard vs live tool list, Overmind's `WorkflowTeachingContractTests` pattern); a one-page naming convention for the tool family; scope-choice rules written into descriptions. **Why now:** descriptions are the extraction substitute per ADR-003 — unpinned, they will drift within three sprints; parameter naming is cheapest to fix before there are clients. **Evidence:** friction log items 1/5, `036-agent-workflow-onboarding`, `032-status-model-self-documentation`, entries `22440990` (schema papercuts).

### P5 — `006-ops-trust` — priority p3, size M
**Goal:** GC sweep and compaction failures surface in stats//health/SPA (lastRun/lastError, consecutive-failure loudness), per-item failure isolation in sweeps, embedding model/dimension fingerprint + `grimoire reindex`, `grimoire export|import`. **Why now:** not day-one, but must land before multi-month unattended use — the Squeezr mirror froze for days precisely because the alert did not exist. **Evidence:** entries `ccf52ead`, `b5875176`, `73add129` (clobber deadlock), features `074`, `104`.

**Sequencing note:** P1 and P2 ride the MVP wave (P1 before real data accumulates; P2 as the first post-MVP feature). P3/P4 are the second wave. P5 before any long-unattended period.

---

*Proposals only — the user gates feature creation on the board (product-strategist lane: propose, never create here).*
