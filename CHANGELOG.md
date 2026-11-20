# Changelog

All notable changes to this project are documented in this file. The format
follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions
follow semantic versioning.

The 0.1.0–0.9.1 entries were backfilled from the tracked release records
(`audits/release-*.md`) and git history. Internal milestones 0.3.0–0.7.0 predate
this changelog; their content lives in git history.

## [Unreleased]

### Added

- Apache-2.0 license (`LICENSE`), superseding the MIT grant of 0.1.0–0.9.1
  (owner ruling 2026-11-20, carrier PR #41).
- Community health files: `SECURITY.md`, `CONTRIBUTING.md`, issue and pull
  request templates.

## [0.9.1] — 2026-11-16

### Added

- Tome guard on REST memory PATCH: foreign-tome edits are rejected fail-closed
  before any store write or embedding call; `PatchMemoryRequest` gains an
  optional `tome` field for parity (063).
- `memory_stats` reports `serverVersion` — version-skew visibility from the
  client, one source with the MCP handshake (064).

### Changed

- Brief facts take whole entries in score order while the budget holds — the
  wholesale drop that let persona starve a fresh session of its standing facts
  is gone (065).
- CLI help column sizes from the longest verb (065).

## [0.9.0] — 2026-11-16

### Added

- Live MCP session registry: a handshake observation log in its own collection,
  `GET /api/sessions`, and a retention sweep in `serve` (054).
- Daily section in `memory_brief` / `grimoire brief` — the last working day's
  and today's daily notes grouped by actor (045), with a SessionStart hook
  integration (046).
- `grimoire report` — the evening read of the day's daily notes; daily notes
  land through a single write path (048).
- Recall escalation loop (061).
- Plain-tag matching in `memory_digest` (047).
- Topic tag transparency (052).

### Changed

- Unified filter semantics across the memory read surfaces (058).
- One strict number grammar for numeric inputs across the faces (062).
- Strict config validation with honest refusals (059).
- Honest responses on API edge paths (060).
- Honest upper bounds in store reads (055).
- SPA build stamp for release parity with the backend (051).
- README documents the full MCP tool surface, guarded against drift by a
  contract test (057).

### Fixed

- Status-blind read surfaces — list/count paths that silently hid non-active
  records, including the purge guard and the GC expired count (050, 044, 056).
- Install flow no longer fails on locked tool files on Windows (#10).

## [0.8.0] — 2026-11-10

### Added

- The MCP `initialize` handshake carries server instructions that teach tome
  routing and the memory loop, generated from the installed tool surface (042).
- `grimoire install` — upserts the managed routing block into detected agent
  home files (`CLAUDE.md`, `QWEN.md`, `AGENTS.md`), with `--print-block` and
  `--remove`; encoding and BOM preserved, atomic writes (042).
- Tome manifests (`grimoire-manifests` collection) and the human-only tome
  purge door gated by a typed confirmation (041).

## [0.2.0] — 2026-10-21

### Changed

- One payload block per MCP tool result — the duplicate `structuredContent`
  carrier is gone (measured ~37% smaller frames on the probe calls).
- Non-ASCII content is no longer `\uXXXX`-escaped in tool results: the same
  Cyrillic record went from 2759 to 999 decoded characters.

## [0.1.0] — 2026-10-20

### Added

- First release: a local agent memory service with three faces — MCP stdio
  server (11 tools), loopback web console with a static SPA, management CLI.
- Qdrant-backed store with hybrid recall (vector similarity × recency decay ×
  importance) and Ollama embeddings.
- Memory model: scope (session / project / global / personal) × kind (journal /
  fact), actor provenance on every write.
- Tomes — persona-isolated containers; persona documents; tombstone-based
  corrections (`forget` / `restore` / `history`); cross-tome `scry`.
- Background GC for expired session records and aged tombstones.
