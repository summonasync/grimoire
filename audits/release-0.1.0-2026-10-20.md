# Release 0.1.0 — first install on this machine + agent onboarding

Date: 2026-10-20 · commit `a59ae55` · board decision `4223d6af-fddd-4b03-a144-6cfc898e0f3c`

## Carrier

| Item | Value |
|---|---|
| Build | `dotnet publish src/Grimoire.Cli -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true` |
| Installed | `~/.local/bin/grimoire` — 53 263 856 B, ELF x86-64, stripped |
| sha256 | `f4252dbed40d26e12fa812a92c0fbc8eb8b2c7bf89bd95963b0ce0c2474b92f4` |
| Self-reported | MCP `serverInfo` = `grimoire` / "Grimoire Memory" / `0.1.0` |
| SPA | `~/.grimoire/web` (static export of `web/out`, rebuilt fresh: lint/tsc/next build rc=0) |
| Store | isolated Qdrant `grimoire-qdrant` → `http://localhost:6354`, collection `grimoire` |
| Embeddings | shared Ollama `nomic-embed-text`, 768 dims |

Framework-dependent publish and `dotnet tool` packaging were considered and rejected — owner
wants one file that moves between machines with no SDK dependency (matches how `squeezr` ships here).

## Gates (all measured, not assumed)

- `dotnet build Grimoire.sln -c Release` → rc=0.
- `dotnet test Grimoire.sln -c Release --no-build` → **814 passed / 0 failed / 0 skipped**
  (Core 208 · Mcp 170 · Cli 167 · Api 135 · Infrastructure 134).
- Web face: `/health` = healthy with store+embeddings reachable; `/` = 200/13 588 B;
  `/index.html` = 200 text/html; `/design/hero-console.webp` = 200 image/webp; `/404.html` = 200.
  Bind confirmed `127.0.0.1:4770` (loopback rule holds against the live socket table).
- MCP face over real stdio against the installed binary: `initialize` → `tools/list` = 11 tools
  (`memory_brief`, `memory_compact_candidates`, `memory_forget`, `memory_history`, `memory_list`,
  `memory_persona_get`, `memory_recall`, `memory_remember`, `memory_restore`, `memory_scry`,
  `memory_stats`) → `memory_remember` wrote `36c123d8-ca1d-4c09-b03e-0bd550c17c1f`
  (Global/Fact, tome `crew`, actor `tucker+qwen`) → `memory_recall` hit it at score 0.662 →
  `memory_brief` returned it under facts with persona state=Populated → `memory_stats` crew active=5.
- Client registration health: `qwen mcp list` → `✓ grimoire … Connected`, `qwen mcp reconnect grimoire`
  → `✓ Reconnected successfully`; `claude mcp list` → `grimoire … ✔ Connected`.

## Onboarding contract chosen

One shared tome `crew` for the coding crew (ADR-007), identity carried in the **actor**, not in per-tool tomes:

| Client | Registration | GRIMOIRE_ACTOR | GRIMOIRE_TOME |
|---|---|---|---|
| Qwen Code | `qwen mcp add grimoire ~/.local/bin/grimoire mcp --scope user --env … --trust` | `tucker+qwen` | `crew` |
| Claude Code | `claude mcp add grimoire --scope user -e … -- ~/.local/bin/grimoire mcp` | `guldan+claude` | `crew` |

`~/.grimoire/config.json`: `defaultTome` = `crew`. Persona of tome `crew` seeded from the global
crew contract — all four sections (identity / tone / preferences / rules), written by `tucker+qwen`.
Codex and kimi were deliberately left out of this pass.

## Pre-images (rollback path)

Outside the repo, because `settings.json` and `.claude.json` carry third-party tokens:

- `~/.grimoire/pre-onboard-2026-10-20/qwen-settings.json` md5 `19961220a7f787ddddafc7c5c018cd6e`
- `~/.grimoire/pre-onboard-2026-10-20/claude.json` md5 `3b20936d320c0ab2a50fac270e6e18c7`
- `~/.grimoire/pre-onboard-2026-10-20/grimoire-config.json` md5 `7663c1c07453893e548f067e1acdde68`
- store snapshot: `~/.grimoire/backups/pre-crew-onboard-2026-10-20.jsonl` (`grimoire export --gzip`, rc=0)

Rollback: restore the three files, `qwen mcp remove grimoire`, `claude mcp remove grimoire -s user`,
and `grimoire import` the snapshot if the store must go back.

## Notes found while verifying (no code changed)

- `grimoire config set` accepts only `section.property` (two+ segments, `ConfigFile.ApplyValue`),
  so the root-level `defaultTome` cannot be set through the CLI — the file had to be edited by hand.
- `GC stale: true` after install is correct, not a regression: only `GcSweeper` writes the heartbeat,
  `grimoire gc` (CLI) never touches `~/.grimoire/gc-state.json`. `RemainingDelay()` evaluates
  `now + interval - now`, so the first in-server sweep lands one full interval (30 min) after
  `serve` starts, and `stale` clears there.
- Harness lesson (mine, not the product's): an MCP stdio probe must treat `readline() == ""` as
  server death — `continue` on EOF spins forever; and never pass `GRIMOIRE_*` env keys that are not
  in the schema, the config loader rejects unknown keys and the server dies before it answers.
  Also `memory_remember` takes `content`, not `text` — a wrong key returns a clean
  `isError` result, so an empty `structuredContent` is a contract miss, not a store failure.
