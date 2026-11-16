# Release 0.9.1 — 2026-11-16

Everything merged since 0.9.0 ships as a patch bump: features 063, 064 and 065
(PR #35 `516efba`, #36 `a1894f0`, #37 `2264a5e`), no breaking contract change —
snapshots additive, wire pins intact, one new optional request field on the REST
PATCH contract and one new output field on `memory_stats`.

## What shipped

1. **063 — tome guard on REST memory PATCH** (audit B1, BLOCKERS): PATCH
   resolves the target record's tome and rejects foreign-tome edits fail-closed
   (400 `invalid_tome` → 404 `not_found` naming the tome) before any store
   write or embedding call, following the restore/history precedent in the same
   file. `PatchMemoryRequest` gains the optional `tome` for parity. Review PASS
   (`audits/review-063-01.md`); the residual tome-move race is tracked as board
   issue 92ff1f3b.
2. **064 — serverVersion in memory_stats** (board issue 3dfc1cdc): `StatsView`
   carries the handshake's `ServerVersion` from `McpHostOptions` — one source,
   no new literal; contract test pins stats-vs-handshake equality with a
   non-default probe version. Review PASS (`audits/review-064-01.md`).
3. **065 — UX polish** (issues de87f125, 8d014bee, 8647e1f3): brief facts take
   whole entries in score order while the budget holds (the wholesale drop that
   let persona starve a fresh session of its standing facts is gone — verified
   live before and after); the CLI help column sizes from the longest verb;
   README version examples refreshed. The fourth scoped item, digest plain-tag
   matching, was verified already fixed by feature 047 — the red probe had run
   through a stale 0.8.0 in-memory binary; board issue fbb52f29 closed with the
   correction. Review PASS (`audits/review-065-01-t0-ux-polish-PASS-6408094.md`).

## Version mechanics

One bump location, as in every release: `Directory.Build.props` `<Version>`
`0.9.0` → **`0.9.1`** — feeding the CLI tool package, assembly versions, the
MCP handshake `serverInfo.version` and `/api/version`. The README carries no
test counts by design (057 convention).

## Verification chain

- `dotnet build Grimoire.sln -c Release`: 0 errors, 10 warnings (the pre-existing
  nullability set, none in the release diff).
- `dotnet test Grimoire.sln -c Release --no-build`, own run on the merged tree:
  **Core 861 + Infrastructure 368 + Mcp 519 + Cli 437 + Api 406 = 2591 passed,
  0 failed, 0 skipped.**
- Review gates: PASS on all three features (records under `audits/review-06[3-5]-*`),
  suggestion-level pins applied in-branch before merge.
- Board state at release: features 063/064/065 done by owner tap; issue cards
  64c386f6, 3dfc1cdc, de87f125, 8d014bee, 8647e1f3 close with this landing.

## Deploy (post-merge, this release)

```bash
dotnet pack src/Grimoire.Cli -c Release -o build/packages
dotnet tool update -g Grimoire --add-source build/packages --no-cache
systemctl --user restart grimoire-serve.service
grimoire --version                                   # -> grimoire 0.9.1
curl -s http://127.0.0.1:4770/api/version            # -> {"version":"0.9.1"}
```

Note for agents: running stdio MCP sessions keep their previous binary in
memory until the host respawns them — version-skew checks against a live
session must use `memory_stats.serverVersion` (this release) or a fresh
process, not the session's in-memory tool behavior.

## Rollback

```
dotnet tool update -g Grimoire --add-source build/packages --version 0.9.0
```

(and uninstall + reinstall when the folder feed no longer holds the 0.9.0
package — it does, `build/packages/Grimoire.0.9.0.nupkg`). The release touches
no stored data, no schema and no config shape; the PATCH tome field and the
stats version field are additive surfaces, so there is no migration to unwind.

## Left open

- W1 (Host filtering), W2 (MemoryService split), W3 (scroll-before-limit),
  W5 (adoption telemetry), the NaN parser bug, the export fingerprint
  omission, the purge door, tome-key strictness, the tray icon and the
  agent-write API gaps remain tracked as open board issues — none block
  this patch release.
