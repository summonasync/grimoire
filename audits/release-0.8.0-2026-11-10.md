# Release 0.8.0 — 2026-11-10

Feature: `042-agent-onboarding-routing` (Overmind board, Grimoire project).
Source commits on `feat/042-agent-onboarding-routing`: `0e84291` — `feat(042): MCP
instructions teach tome routing` (T0), `c9b4583` — `feat(042): grimoire install verb,
managed home-file block writer` (T1), `224ed17` — review record, `a57d154` — `fix(042):
install block test usings, BOM-preserving write` (both review blockers from
`audits/review-042-01.md` closed). This docs/bump commit completes the release; the
annotated-tag and merge mechanics follow the usual PR flow (release/0.7.0 precedent,
PR #9).

## What shipped

The onboarding chain: an agent that never learns about the memory never calls it.
Two gaps, both closed on the agent-facing surfaces.

1. **The MCP server never told an agent the memory existed, let alone where to write.**
   The `initialize` handshake now carries server instructions (T0, `0e84291`): a short
   routing preamble — call `memory_brief` at session start, record durable facts with
   `memory_remember` the moment they appear, tombstone wrong ones with `memory_forget`,
   check `memory_recall` before asserting anything on record, route by the brief's tome
   table, open a tome with `memory_tome_create` only when nothing fits. Generated from
   the installed tool surface (one source of truth, no hand-maintained duplicate), pinned
   by contract tests.
2. **No machine-level onboarding verb.** A CLI agent home carries a memory file
   (`~/.claude/CLAUDE.md`, `~/.qwen/QWEN.md`, `~/.codex/AGENTS.md`,
   `~/.kimi-code/AGENTS.md`), and nothing kept them taught. `grimoire install` (T1,
   `c9b4583` + `a57d154`) upserts the gate-approved block — bounded by
   `<!-- grimoire:begin -->` / `<!-- grimoire:end -->` markers — into every detected
   home file: replace-in-place when marked, append when not, one outcome line per file
   (`created` / `updated` / `unchanged` / `removed` / `skipped (not found)`), missing
   files skipped and never created, original encoding and BOM preserved on write
   (UTF-8 with and without BOM, UTF-16 LE/BE), stamped `.bak` next to the target,
   atomic rename. `--print-block` prints without touching files; `--remove` strips only
   the managed range. Kit coexistence: on machines where the OvermindCrewKit mirrors
   already carry the same block, the verb is idempotent — reruns report `unchanged`
   (kit leg deployed via Overmind PR 65; the live `unchanged x4` proof is a post-merge
   check, see Pending).

README documents the verb (`Teaching agents the memory` section), CONTEXT.md lists
`install` among the CLI verbs. Docs-only drift fixed on the way: the Distribution
install example still printed `grimoire 0.2.0`.

## Version mechanics

One bump location, as in every release since 0.3.0:

- `Directory.Build.props` `<Version>`: `0.7.0` → **`0.8.0`**. This feeds the CLI tool
  package (`src/Grimoire.Cli` packs `Grimoire.0.8.0.nupkg`), every assembly version and
  — through the existing runtime chains — both version faces: the MCP handshake
  `serverInfo.version` via `McpCommand.VersionText()` and `/api/version` via the Api
  host assembly. `McpHostOptions.ServerVersion` keeps its `"0.1.0"` default on purpose:
  it is the DI fallback, both entry points override it from the assembly version, and no
  literal in code pins the release number. Version-bearing tests assert
  assembly-vs-assembly, so the bump carries no test edits.
- `README.md`: test count `1860` → `1889` (own run below, not carried over).

## Verification chain

- `dotnet build Grimoire.sln -c Release`: 0 errors, 10 warnings — all CS8604/CS8602
  nullable notes, 8 in tests, 2 in `src/Grimoire.Api` (`ApiRouteMatcher.cs:88`,
  `TomeEndpoints.cs:72`); both files last touched on `origin/main` (041), feature 042
  does not touch `src/Grimoire.Api` at all. Pre-existing, none in the 042 diff.
- `dotnet test Grimoire.sln -c Release --no-build`, own run on this tree
  (`audits/release-0.8.0-test.log`): **Core 619 + Infrastructure 253 + Mcp 455 +
  Api 274 + Cli 288 = 1889 passed, 0 failed, 0 skipped.** Per-project counts listed
  explicitly — the T1 task log that claimed a green suite while the Infrastructure
  project did not compile (review-042-01, BLOCKER 1) is exactly the failure mode this
  form exists to prevent. The 1889 total matches the reviewer-reconciled expectation:
  review-042-01 projected 252 Infrastructure tests once the missing usings compiled
  (230 pre-042 + 22 new), and the fix commit added the UTF-16 LE roundtrip pair for 253.
- Review gate: `audits/review-042-01.md` found 2 blockers / 3 minors / 2 nits on
  `c9b4583`; `a57d154` closed both blockers (the three missing `using` lines; the BOM
  preamble write in `InstallBlockFileWriter.WriteChanged` plus the UTF-16 roundtrip
  test). The minors (newline normalization on remove, orphan-marker semantics, empty-file
  `created`) are recorded in the review, not silently dropped; the orphan-marker
  behavior is documented as matching the existing managed-block precedent.
- Block-text fidelity: `--print-block` output diffed byte-identical against the
  gate-approved spec Fix 2 block (review-042-01 live check), and
  `InstallBlockTests.Text_MatchesTheGateApprovedBlockVerbatim` pins it in-suite.
- Frozen MCP surface: the tool-snapshot contract tests are green in the run above —
  instructions changed, tool descriptions and schemas did not.

## Carrier and pending steps (honest state)

Nothing is installed yet — this release is staged, not shipped:

- The PR (`feat/042-agent-onboarding-routing` → `main`) is open; the merge is the
  human's door.
- After merge: `dotnet pack src/Grimoire.Cli -c Release -o build/packages` +
  `dotnet tool update -g Grimoire --add-source build/packages` (or reinstall per the
  checklist) puts 0.8.0 on PATH; `grimoire --version` must answer `grimoire 0.8.0`.
- Then the live chain, strictly ordered: the kit leg is already deployed (Overmind
  PR 65), `grimoire install` must report `unchanged` on the four kit-carried homes
  (coexistence proof), `grimoire brief` must show the tomes section, a fresh MCP
  session must receive the routing instructions, `grimoire stats` census clean.
  Evidence goes into the task log — owned by the orchestrator + human, not this
  staging commit.

## Rollback

The tool is the carrier, the channel is the local packages folder:

```
dotnet tool update -g Grimoire --add-source build/packages --version 0.7.0
```

(and `dotnet tool uninstall -g Grimoire` + reinstall when the folder feed no longer
holds the 0.7.0 package). The release touches no stored data, no schema and no config
shape — the instructions string and the install verb are additive surfaces, so there is
no migration to unwind. `grimoire install --remove` undoes the managed block on any
home file independently of the tool version.

## Left open by this release

- Live verification evidence (install `unchanged` x4, version, brief tomes,
  fresh-session instructions, census) lands post-merge in the task log.
- The `-g` tool update needs the merged main and the owner's go — sequenced in Pending
  above, deliberately not run from the release branch.
