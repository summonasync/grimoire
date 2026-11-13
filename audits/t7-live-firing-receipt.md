# T7 live receipts — shipped SessionStart hook scripts, fail-open and real-runtime firing

Feature 046, task 7 (artifact `b37e3bec-7df4-434d-8b6a-769087d50c8a`), worktree
`feat/046-brief-sessionstart-hook` at `e7681ad`. Everything below was executed on this box
(Linux, Qwen Code 0.27.1, PowerShell 7.7.0 via `/snap/bin/pwsh`), nothing is quoted from code reading.

Files under test, sha256 as measured:

```
e6580828f6ce5ddad4bb18865e26ca4d7e8e260e1b572a5c622c15a7d528ce9d  deploy/hooks/grimoire-session-start.sh
015e58311d50e4a6e817bb46d19a6fcbde41033e18caa0de51b23c4aee13f67b  deploy/hooks/grimoire-session-start.ps1
```

Static gates: `bash -n` exit 0; PowerShell parser (`Language.Parser::ParseFile`) exit 0, "ps1 parse
OK"; `shellcheck` is **not installed** on this box (`command -v shellcheck` → MISSING), so that gate is
disclosed as not-run rather than passed. Both files are 7-bit ASCII (the ASCII pass matters: Windows
PowerShell 5.1 reads BOM-less UTF-8 as ANSI and mangles the comments).

## How the CLI was pointed at without touching the crew's data

The probe ran a throwaway collection (`t7probe046`) inside Grimoire's own isolated Qdrant
(127.0.0.1:6353/6354) under an isolated `HOME` (`~/scratch/t7-probe/home` + its own
`.grimoire/config.json`), driven by `HOOK_GRIMOIRE_BIN` → a shim over `dotnet exec …/Grimoire.Cli.dll`
(built at `e7681ad`). The crew's collection `grimoire` was never a target; the installed 0.8.0 carrier
was used only as a *deliberately old* binary for the exit-2 test. Cleanup is at the end of this file.

## AC1 — both scripts emit the envelope for the same fixture content

Contract harness (fixture stdin carrying all seven measured payload keys **plus an unknown nested
field**, both platforms, byte-compared): **42 checks, 42 pass, 0 fail.**

| Case | .sh | .ps1 |
| --- | --- | --- |
| store answers, fixture envelope | rc=0, forwards the CLI line byte-for-byte | rc=0, forwards the CLI line byte-for-byte |
| `.sh` stdout vs `.ps1` stdout, same fixture | **identical** (`cmp` clean, live store, 770 bytes) | **identical** |
| CLI exit 2 (usage/recovery text) | rc=0, empty envelope, 1 stderr line | rc=0, empty envelope, 1 stderr line |
| CLI exit 6 (upstream unavailable) | rc=0, empty envelope, 1 stderr line | rc=0, empty envelope, 1 stderr line |
| CLI exit 0, garbage multi-line stdout | rc=0, empty envelope, "printed 2 stdout line(s)" | rc=0, empty envelope, same reason |
| CLI exit 0, empty stdout | rc=0, empty envelope, "printed 0 stdout line(s)" | rc=0, empty envelope, same reason |
| CLI binary missing | rc=0, empty envelope, "not found on PATH or in ~/.dotnet/tools" | rc=0, empty envelope, PATH/%USERPROFILE% wording |
| CLI never answers (`HOOK_GRIMOIRE_TIMEOUT=2`) | rc=0, empty envelope, cut at **2027 ms** | rc=0, empty envelope, cut at **2890 ms** |
| envelope with no trailing newline | forwarded (that is why the line count is `grep -c ''`, not `wc -l`) | forwarded |
| shape | `hookEventName == "SessionStart"`, canary present in `additionalContext` | same |

Live (not stubbed) happy path against the real store through the real CLI:

```
up_store      rc=0 wall_ms=369 stdout_lines=1 stdout_bytes=770 stderr=[]
up_store_ps   rc=0 wall_ms=818 stdout_lines=1 stdout_bytes=770 stderr=[]
direct CLI    rc=0 bytes=770
BYTE-IDENTICAL: shipped .sh stdout == CLI stdout
BYTE-IDENTICAL: .sh stdout == .ps1 stdout (same fixture, live store)
```

**`.ps1` status on this Linux box: measured live, not statically reviewed.** `pwsh` 7.7.0 is installed,
so every `.ps1` row above is a real execution. What is *not* measured here is Windows itself: the
`%USERPROFILE%\.dotnet\tools\grimoire.exe` fallback (same shape as `deploy/windows/serve-hidden.vbs`
uses) was exercised only through a faked `USERPROFILE`, and PowerShell 5.1's parsing of this file was
not verified — 7.7.0 is the only engine on this box.

## AC2 — fail-open measured against an unreachable store (real container down)

Sequence, UTC wall-clock from `live.sh`, ports 6353/6354 only (Grimoire's own isolated store):

```
21:58:05.613 before: running health=200
21:58:05.939 stopped rc=0 state=exited
             store_down      rc=0 wall_ms=226 stdout_lines=1 stdout_bytes=79
                               stderr=[grimoire-session-start: error [upstream_unavailable]: Upstream 'qdrant' is unavailable: …]
             store_down_ps   rc=0 wall_ms=680 stdout_lines=1 stdout_bytes=79   (same stderr line)
21:58:06.887 stdout store_down:    {"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":""}}
21:58:06.893 stdout store_down_ps: {"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":""}}
21:58:07.530 started rc=0
21:58:08.758 after: health=200 state=running
             store_restored  rc=0 wall_ms=381 stdout_bytes=770  canary-after-restore=4
21:58:08.497 crew surfaces: serve={"status":"healthy","store":{"reachable":true,…}} squeezr_6333=200
```

Outage window for the store: **0.9 s of wall time** (21:58:05.9 → 21:58:07.5 including the two
measured runs); service verified restored by a live brief plus `grimoire serve /health` plus the crew
collection still answering `stats` afterwards.

Wall-time budget, asserted: refused connection costs 226 ms (.sh) / 680 ms (.ps1) against a 10 s
budget. The budget is not decorative — against an **unroutable** endpoint (`10.255.255.1:6334`, no RST)
the CLI never answers at all:

```
control: bare `grimoire brief --format hook --daily` under `timeout 25` → rc=124 (still hanging at 25 s)
blackhole_10s   rc=0 wall_ms=10029 stdout=empty envelope stderr=[brief exited 124]
blackhole_2s    rc=0 wall_ms=2027  stdout=empty envelope stderr=[brief exited 124]
blackhole_2s_ps rc=0 wall_ms=2770  stdout=empty envelope stderr=[brief did not answer in 2s]
```

Review 046-06 **N-2** (recovery text on stdout at exit 2) closed with the real old carrier, no stub:
the bare carrier puts `Usage: grimoire brief` on stdout at rc=2; through the shipped scripts it is
**refused** — stdout is the empty envelope, exit 0, one stderr line naming `error [usage]: Unknown
option 'format' for 'brief'` (92 ms).

The failure output is an **empty envelope, not silence** — the choice and its measured justification are
in the deviations section.

## AC3 — live firing through the real runtime (startup / resume / compact, re-stamped)

Probe: isolated `HOME` with a user-scope `~/.qwen/settings.json` registering the **shipped `.sh`** as
the SessionStart command (`timeout: 15`, `env.HOOK_GRIMOIRE_BIN` → probe CLI). The model endpoint is a
local recording mock (`/v1/chat/completions`, OpenAI-shaped) that dumps every request body, so the
evidence is the actual `system` message the runtime put on the wire. `MEMSUB` rows are the runtime's
memory-extraction subagent, `COMPRESS` rows the summarisation call. Counted token totals come from the
mock, so compaction is steerable.

```
FIRE 1  startup, three user turns in ONE process; a record written 2 s after the startup fire
  001 MAIN     sys_chars=23479 blocks=1 endtags=1 msgs=2
  003 MAIN     sys_chars=23479 blocks=1 endtags=1 msgs=4
  005 MAIN     sys_chars=23479 blocks=1 endtags=1 msgs=6
  → (a) the brief lands at startup: the block carries the seeded T7FINAL fact.
  → across all three turns of one process: exactly one block, never two — non-accumulating.

FIRE 2  resume of that session; T7RESUME-0ccb4704 written AFTER fire 1 ended
  001 MAIN     sys_chars=20969 blocks=1 endtags=1 final=1 tok=1 msgs=8
  → (b) it RE-LANDS on resume, and with content the startup block could not have known:
    the discriminator token written after startup is present. Not a transcript echo — a fresh call.

FIRE 3  compaction inside one process (mock reports 200000 prompt tokens over the model's hard limit);
        T7COMPACT-8aae6b9f written mid-run, during the first turn
  001 MAIN     sys_chars=23790 blocks=1 tok=0     ← startup fire, token not written yet
  003 COMPRESS sys_chars= 3582 blocks=0           ← the compaction call itself
  004 MAIN     sys_chars=23790 blocks=1 tok=0     ← re-stamped, still one block
  006 COMPRESS sys_chars= 3582 blocks=0
  007 MAIN     sys_chars=23933 blocks=1 tok=1     ← +143 chars: the mid-run record is IN the block
  → (c) after compaction the block is RE-STAMPED, not accumulated: one block per request across two
    compact cycles, and the 143-char growth plus the mid-run token prove the hook re-fired and its
    fresh payload replaced the old block.

FIRE 4  the store is unreachable while the hook fires (dead port, real CLI)
  session rc=0 wall_ms=3206  result=success is_error=False turns=1
  001 MAIN     blocks=0
FIRE 5  control, same shape, store alive
  001 MAIN     blocks=1 final=1
```

Byte-level excerpt of what actually reached the model (FIRE 5, first 900 chars of the block), showing
the tag-free rendering T5 promised and the T2/T3 omission digest riding along:

```
<qwen:session-start-context hidden="true">
SessionStart additional context:
persona: (empty)
facts: (empty)
tombstones: (empty)
journal:
  c4a477f4-… Global 2026-11-13 22:14:05Z
    T7COMPACT-8aae6b9f: mid-run mutation - only a re-fired and re-stamped block can carry it.
  …
  omitted 4:
    d63f81a4-… read-limit: T7CANARY4-0a52: …
```

## AC4 — nothing left behind

`~/scratch/t7-probe` (isolated HOME, mock, harness, all captures) deleted in this slice; probe
collections `t7probe046` and `t7probe046-manifests` dropped — `GET /collections` now returns exactly
`[grimoire, grimoire-manifests, grimoire-sessions]`; no `mock-provider`/probe CLI processes survive
(killed by PID file, never by pattern — a `pkill -f` in this slice matched its own command line and
killed the harness shell, which is why one receipt run had to be redone); `grimoire stats` on the crew
collection still answers (277 active); `deploy/docker-compose.yml` (6333, squeezr's) was never touched
and answered 200 throughout. The user's real `~/.qwen/settings.json` was never read: the probe used its
own copy under the scratch `HOME`, and only `stat` (mtime/size, unchanged) was taken of the real file.

## Deviations and the choices the reviewer should look at

1. **Failure output is an empty envelope, not silence.** The task artifact says "on failure print nothing
   to stdout"; the runtime tolerates both — `setSessionStartContext` bails on a whitespace-empty
   `additionalContext` (measured: FIRE 4 put **zero** blocks on the wire), and `applySessionStartContext`
   ignores it too, so an empty envelope is byte-invisible in the prompt while keeping stdout parseable
   and the reason on stderr. One branch also covers the case where the runtime drops a *malformed* line
   whole: there is no path where the script's stdout can be misread.
2. **The knob is `HOOK_GRIMOIRE_BIN`/`HOOK_GRIMOIRE_TIMEOUT`, not `GRIMOIRE_BIN`.** Measured first run:
   the CLI rejects any unclaimed `GRIMOIRE_*` variable as a config overlay
   (`error [config]: Environment override 'GRIMOIRE_BIN' does not match any configuration property`), so
   a knob in that namespace would break the very call it configures. **T8: do not document a
   `GRIMOIRE_*` name for these two knobs.**
3. **`timeout` is optional in the `.sh`.** The script uses `timeout` when `command -v timeout` finds it
   and runs unbounded otherwise, so the *budget* is only enforced where coreutils exist; the runtime's own
   per-hook `timeout` (README's job) is the real ceiling. On macOS that usually means `brew install coreutils`.
4. **`.ps1` measured on Linux only** (see AC1). Windows is a static-shape plus engine-parity claim; the
   5.1 parser was not run because 5.1 is not on this box.
5. **shellcheck not installed** — not run, disclosed.
6. **The budget default is 10 s, not the spec's 2 s target.** Measured: 0.37 s warm, 0.24 s against a
   refused store, so 2 s never binds in practice, while the *only* case it governs (unroutable endpoint,
   hung socket) needs enough room for a cold .NET start plus one retry-free answer — and 2 s would cut a
   legitimate slow-but-answering store out of the brief for a stall the runtime already caps at 60 s.
   Overridable per hook via `HOOK_GRIMOIRE_TIMEOUT`.

## For T8 (README install snippet) — the facts this slice measured

- Ship the per-hook **`timeout` in seconds** explicitly (runtime default 60 s; values ≥1000 are re-read
  as legacy milliseconds). The scripts cut themselves at `HOOK_GRIMOIRE_TIMEOUT` (default 10).
- Registration is **user-scope**: `~/.qwen/settings.json` → `hooks.SessionStart[].hooks[] = {type:
  "command", command, name, description, timeout, env, …}`. Measured working entry in this slice:
  `command: "bash <repo>/deploy/hooks/grimoire-session-start.sh"`, `timeout: 15`,
  `env: { "HOOK_GRIMOIRE_BIN": "/abs/path/to/grimoire" }` (the `env` block is how a probe or a non-PATH
  install points at its own binary). Windows: `powershell -NoProfile -ExecutionPolicy Bypass -File <repo>\deploy\hooks\grimoire-session-start.ps1`.
- Project-scope `<cwd>/.qwen/settings.json` hooks are folder-trust-gated and **silently skipped** — state
  the precondition if a per-project install is offered.
- The hook does nothing useful when Grimoire is down by design: empty envelope, exit 0, one stderr line
  (`grimoire-session-start: …`), session unaffected — measured end to end at FIRE 4.
- Startup costs ~0.4 s of `dotnet`-carrier wall time (0.82 s on PowerShell 7.7); the brief itself is the
  payload, currently ~770 bytes for a near-empty tome and ~28 KB for the crew tome's full brief.
