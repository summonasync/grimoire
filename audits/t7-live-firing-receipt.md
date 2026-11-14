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
| `.sh` stdout vs `.ps1` stdout, same fixture | **identical** (`cmp` clean; live Grimoire store, throwaway collection `t7probe046`, tome `t7probe` holding one seeded fact → 770 bytes) | **identical** |
| CLI exit 2 (usage/recovery text) | rc=0, empty envelope, 1 stderr line | rc=0, empty envelope, 1 stderr line |
| CLI exit 6 (upstream unavailable) | rc=0, empty envelope, 1 stderr line | rc=0, empty envelope, 1 stderr line |
| CLI exit 0, garbage multi-line stdout | rc=0, empty envelope, "printed 2 stdout line(s)" | rc=0, empty envelope, same reason |
| CLI exit 0, empty stdout | rc=0, empty envelope, "printed 0 stdout line(s)" | rc=0, empty envelope, same reason |
| CLI binary missing | rc=0, empty envelope, "not found on PATH or in ~/.dotnet/tools" | rc=0, empty envelope, PATH/%USERPROFILE% wording |
| CLI never answers (`HOOK_GRIMOIRE_TIMEOUT=2`) | rc=0, empty envelope, cut at **2027 ms** | rc=0, empty envelope, cut at **2890 ms** |
| envelope with no trailing newline | forwarded (that is why the line count is `grep -c ''`, not `wc -l`) | forwarded |
| shape | `hookEventName == "SessionStart"`, canary present in `additionalContext` | same |

Live (not stubbed) happy path against the real store through the real CLI: (subject of every byte count in the block below: the **throwaway collection
`t7probe046`, tome `t7probe`**, seeded with one canary fact, on Grimoire's own isolated Qdrant at
6353/6354 - `770 bytes` is that brief, `79 bytes` is the empty envelope, never the crew collection)

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
  payload: measured 770 bytes for the throwaway probe collection `t7probe046` / tome `t7probe` with a
  single seeded fact, and 28 627 bytes for the real crew collection `grimoire` / tome `crew`.

---

# FIX-RECEIPT — reopen after review 046-08 (SF-1 / SF-2)

Same task, two acceptance items carried in from the light review (AC5 = SF-1 `.ps1` waits stdin EOF,
AC6 = SF-2 `.sh` passes `HOOK_GRIMOIRE_TIMEOUT` raw into `timeout`). Both fixed, nothing else changed.
Commit lineage (corrected here: an earlier draft of this section had it backwards): `b056a81` "T7 shipped
hook scripts with live receipts" is the shipped pair, `036775e` is its **child** (the T7 light review
record), `3206aae` carries this reopen's fix, and `cce7ce8` is the current HEAD (the fix-round
reverification record). The clamp round below is uncommitted working-tree state on top of `cce7ce8`.

## The second pair (SF-1/SF-2 fix, commit 3206aae) — historical; T8 cites the final pair in "Third pair"

```
36f2aa8c6005a02e20d0988ec33a3a943a94b99bf2e787a2f98905a1430dfd73  deploy/hooks/grimoire-session-start.sh
08f69fed6e100592870ba08004b5f3e61d690f697e7c69f038157237e5e63d01  deploy/hooks/grimoire-session-start.ps1
```

This block recorded the *second* pair, which commit `3206aae` now carries
(`36f2aa8c…` .sh / `08f69fed…` .ps1). A third and final round followed it - see "Third pair" at the end of
this file - so read this as history, not as the number to cite.
Lineage of the script content: **shipped `b056a81`** (`e6580828…` / `015e5831…`) → **fix `3206aae`**
(`36f2aa8c…` / `08f69fed…`) → **clamp final, working tree** (`87f7246c…` / `517cef25…`). Inside that last
round the `.sh` also passed through content `d79f07ac…`, which one comment tightening superseded before
verification: no number in the "Third pair" section below was taken on it, and it should not be cited.

Numstat against the ranges that matter (both twins only; the receipt's own growth is separate):

```
b056a81 -> 3206aae      (what the reopen's fix commit carried)
  8   0   deploy/hooks/grimoire-session-start.sh
 35   5   deploy/hooks/grimoire-session-start.ps1

cce7ce8 -> working tree (this clamp round, uncommitted)
  6   3   deploy/hooks/grimoire-session-start.sh
  6   2   deploy/hooks/grimoire-session-start.ps1
```

## The two changes

**.sh — one guard line, right after the knob is read (SF-2):**

```bash
[[ "$budget" =~ ^[0-9]+$ ]] && ((10#$budget > 0)) || budget=10
```

Acceptance set is deliberately identical to the `.ps1`'s existing `TryParse` + `-gt 0`: digits only, not
zero, `10#` so a leading zero reads as 7 rather than as octal or as a rejected typo. Deviation from the
suggested `^[1-9][0-9]*$` shape: that pattern rejects `07` while the `.ps1` accepts it, which would have
put the twins out of step on one value — the whole point of the item.

**.ps1 — the drain no longer runs on `[Console]::In` (SF-1).** Root cause, measured not guessed:
`[Console]::In.ReadToEndAsync()` does not hand control back while a writer holds stdin open, so the
script never reached the brief at all, and `[Console]::In.Dispose()` cannot help because the script
thread is already inside the call. The shipped drain now reads the raw `[Console]::OpenStandardInput()`
stream in a loop bounded by the budget (`ReadAsync` + `Wait(left)` against a `Stopwatch` deadline) and
the **drain releases stdin before any exit path runs**: `Release-Stdin` is called inline the moment the
read stops being waited on, so by the time any later branch (brief, bail, outer trap) is reached there is
no stream left to own.

## SF-1 wedge matrix — writer sends the payload plus 200 KB, marks its own flush, then never closes

Pipe buffer is 64 KB, so `drained=YES` proves the read consumed everything rather than erroring out, and
`envelope` distinguishes forwarded-from-empty-from-nothing (a first version of this fixture truncated
stdout to 44 chars and could not tell the two envelopes apart — discarded).

| target | twin | budget | rc | wall_ms | within budget+3s | drained | envelope |
| --- | --- | --- | --- | --- | --- | --- | --- |
| OLD `b056a81` | sh | 3 | 0 | 3041 | yes | YES | FORWARDED |
| OLD | ps | 3 | **124** | **35020** (cap) | **NO** | YES | **NONE** |
| OLD | sh | 2 | 0 | 2038 | yes | YES | FORWARDED |
| OLD | ps | 2 | **124** | **35020** (cap) | **NO** | YES | **NONE** |
| OLD | sh | 5 | 0 | 5039 | yes | YES | FORWARDED |
| OLD | ps | 5 | **124** | **35020** (cap) | **NO** | YES | **NONE** |
| NEW | sh | 3 | 0 | 3037 | yes | YES | FORWARDED |
| NEW | ps | 3 | 0 | **3426** | yes | YES | **FORWARDED** |
| NEW | sh | 2 | 0 | 2039 | yes | YES | FORWARDED |
| NEW | ps | 2 | 0 | **2413** | yes | YES | **FORWARDED** |
| NEW | sh | 5 | 0 | 5039 | yes | YES | FORWARDED |
| NEW | ps | 5 | 0 | **5422** | yes | YES | **FORWARDED** |

The `.ps1` now costs its budget plus ~400 ms of teardown, at every budget, and the brief still gets
through. Control rows under the same wedged writer: trivial `pwsh` 285 ms, trivial `bash` 8 ms.

## SF-2 budget-value matrix — CLI stub hangs 25 s, stdin=/dev/null so the drain cannot colour the wall

| value | OLD sh | OLD ps | NEW sh | NEW ps |
| --- | --- | --- | --- | --- |
| `3` | 3026ms, cut ✓ | 3787ms, cut ✓ | 3028ms, cut ✓ | 3827ms, cut ✓ |
| `abc` | **24ms, CLI never reached**, stderr `timeout: invalid tim…` | 10816ms ✓ | 10026ms, cut at the default ✓ | 10808ms ✓ |
| `0` | **25043ms — unbounded, brief forwarded after the CLI's 25s hang** | 10792ms ✓ | 10026ms ✓ | 10799ms ✓ |
| `-3` | **22ms, CLI never reached**, stderr `error: unexpected ar…` | 10775ms ✓ | 10029ms ✓ | 10814ms ✓ |
| `1.5` | 1526ms (GNU read it as 1.5 s — silently a different budget than asked) | 10768ms ✓ | 10026ms ✓ | 10804ms ✓ |
| `''` / unset | 10027 / 10027ms ✓ | 10792 / 10771ms ✓ | 10028 / 10026ms ✓ | 10817 / 10807ms ✓ |

And with a healthy (instant) CLI, every one of those values still **forwards the brief** on both twins
(`FORWARDED` in all 14 NEW rows, 37-45 ms on `.sh`, 415-450 ms on `.ps1`): the guard rescues the knob, it
does not spend the brief. rc stayed 0 everywhere, as the contract requires.

## Nothing else moved

* Contract harness on the new bytes: **42/42 pass** — parity, all four failure shapes, missing binary,
  no-trailing-newline, budget cut (`sh` 2026 ms / `ps` 2808 ms at budget 2), envelope shape.
* Live byte parity, read-only brief through the real CLI: `up sh` rc=0 528 ms / `up ps` rc=0 910 ms,
  334 bytes each (live Grimoire store, throwaway collection `t7fix046`, tome `t7fix`, one seeded fact),
  `cmp` clean against the direct CLI stdout **and** against each other, canary present in
  both envelopes.
* Fail-open re-measured without stopping any container: refused port `127.0.0.1:59999` → both twins rc=0,
  identical empty envelope, one stderr line (`upstream_unavailable`), 243 ms / 749 ms; unroutable
  `10.255.255.1:6334` → rc=0, empty envelope, cut at 10031/10721 ms (default) and 2032/2755 ms
  (`HOOK_GRIMOIRE_TIMEOUT=2`).
* After the sweep: qdrant 6353 healthz=200, `grimoire brief` still answers, `grimoire serve /health`
  healthy, squeezr 6333 untouched at 200.
* Static gates: `bash -n` 0, PowerShell language parser 0; both files still ASCII-only; `shellcheck` still
  not installed on this box (disclosed, not passed).

## Method honesty — three of my own fixtures were wrong before they were right

Recorded because a receipt that hides its own re-dos is worth less:
1. First wedge fixture exported `HOOK_GRIMOIRE_BIN` but never `HOOK_GRIMOIRE_TIMEOUT`, so the `.sh` rows
   all read 10029 ms (the default) instead of the budget under test.
2. First budget matrix recreated the "hanging" stub without its `sleep` and fed stdin from a here-string,
   which keeps the write end open — every row then measured the drain eating the budget, not the CLI cut.
   Both runs discarded; the tables above are the corrected fixtures, re-measured against `b056a81` and the
   working tree in one sequential pass (never concurrent: overlapping wall-time measurements lie).
3. The B3 "both hazards" rows are **not** wedge evidence — that arm's stdin closed at EOF. The wedge
   evidence is the matrix above, which holds the pipe open through a `mkfifo` writer that never exits.
4. `pkill -f` is still banned here for a reason: killed by PID file throughout, one runaway probe stub
   chased down by PID.

### Why my OLD-column walls differ from the review's (same bug, same direction)

Review 046-08 measured the `.ps1` at 20 187 / 15 884 ms under budgets 3 / 2; my re-run of the same
`b056a81` bytes hit the 35 s cap instead. The fixture is what differs, and it pins the mechanism down:
this fixture pushes the payload plus 200 KB and then holds the pipe open, so `[Console]::In`'s pump keeps
finding bytes and never returns control to `Wait` at all; with a writer that hands over a small payload
and then merely stops closing, `Wait` does return at the budget and the abandoned pump is what costs the
extra ~17 s at process exit. Both shapes are the same un-detachable pump on the same line, both blow the
budget by an order of magnitude, and the fix closes both - which is why the NEW column is now
budget + ~400 ms rather than either number.

## Hash correction and the final-bytes re-verification (SUPERSEDED by "Third pair" below - read that)

The `.ps1` hash above is `08f69fed…`, not the `c2b6d755…` this section first recorded: after the matrices
ran, one comment line inside the new drain was itself wrong — it quoted `25 018 ms` as if it were a
measurement of the bug, when that number was a test-harness *timeout*. Behavior-neutral (comment only),
but the receipt cannot cite a hash whose comment misstates its own evidence, so it was corrected and
everything that matters was re-measured on the final bytes. At the time of writing that pair was
`36f2aa8c…` / `08f69fed…`, and `c2b6d755…` / `e6580828…` were already dead - but a third round followed
(the oversized-knob clamp), so **cite the "Third pair" section at the end of this file instead**.

Verification of that (second) pair, one pass, 20/20:

```
gates     bash -n rc=0 · PowerShell parser rc=0 ("ps1 parse OK") · both files ASCII-only
wedge b3  sh rc=0 3035 ms · ps rc=0 3570 ms (budget 3 + 3 s grace) · fixture drained in both
parity    .sh == .ps1 == direct CLI stdout, 28 627 bytes each (READ-ONLY brief of the crew collection
          `grimoire`, tome `crew` - the real crew store, nothing written), one line each
knob      HOOK_GRIMOIRE_TIMEOUT = abc / 0 / -3 / 1.5 / '' → rc=0 and the brief still forwarded on .sh
fail-open refused port 59999: .sh and .ps1 both rc=0, byte-identical EMPTY envelope, one stderr line
```

Review-quoted numbers stay quoted as the review's own measurements (20 187 / 15 884 ms for SF-1,
the rc=125 and `0`-means-no-limit findings for SF-2); the numbers in the tables above are mine, and the
`35 020 ms` cap row in the OLD column is my harness's timeout firing, which is exactly the distinction the
corrected comment now makes.


---

# Third pair — the oversized-knob clamp (reviewer N-4), final bytes

Parent chose the clamp over prose. Two edits, nothing else moved. **Cite this pair; the two above it are
history.** Base of the diff: HEAD `cce7ce8`.

```
87f7246ce8573568b24e9945456982757ab9be4d40bbe7c5cb5ef79417ba0427  deploy/hooks/grimoire-session-start.sh
517cef25a1e6c1c997965e4495695874ee4ac1414fba5f94104350c8763786e5  deploy/hooks/grimoire-session-start.ps1
```

```
6   3   deploy/hooks/grimoire-session-start.sh      (cce7ce8 -> working tree)
6   2   deploy/hooks/grimoire-session-start.ps1
```

## The two guard lines, verbatim as landed

`.sh` line 41:

```bash
[[ "$budget" =~ ^[0-9]+$ ]] && ((10#$budget > 0 && 10#$budget < 60)) || budget=10
```

`.ps1` line 41:

```powershell
        if ([int]::TryParse($env:HOOK_GRIMOIRE_TIMEOUT, [ref]$parsed) -and $parsed -gt 0 -and $parsed -lt 60) {
```

Rationale carried in both comments: the runtime caps a command hook at 60s, so a value of 60 or above
cannot mean what it says - the third face of the SF-2 class. The `.ps1` one adds why the ceiling was worth
more than tidiness: `2147484` passed the old guard and then the `*1000` millisecond conversions overflowed,
so the hook dropped the brief silently at 504 ms instead of delivering it.

## Knob matrix, both twins, hang stub (CLI sleeps 25s), stdin=/dev/null

| HOOK_GRIMOIRE_TIMEOUT | decided | sh wall / envelope | ps wall / envelope |
| --- | --- | --- | --- |
| `7` | accepted → cut at 7s | 7 026 ms EMPTY | 7 799 ms EMPTY |
| `07` | accepted as 7 (the `10#` parity case) | 7 029 ms EMPTY | 7 812 ms EMPTY |
| `59` | accepted → runs to the stub, brief survives | 25 043 ms FORWARDED | 25 420 ms FORWARDED |
| `60` | **rejected → fallback 10s cut** | 10 027 ms EMPTY | 10 807 ms EMPTY |
| `99` | rejected → 10s cut | 10 025 ms EMPTY | 10 793 ms EMPTY |
| `0` | rejected → 10s cut | 10 027 ms EMPTY | 10 798 ms EMPTY |
| `-3` | rejected → 10s cut | 10 030 ms EMPTY | 10 815 ms EMPTY |
| `1.5` | rejected → 10s cut | 10 027 ms EMPTY | 10 846 ms EMPTY |
| `abc` | rejected → 10s cut | 10 028 ms EMPTY | 10 837 ms EMPTY |
| `99999999999` | rejected → 10s cut (was unbounded 25 024 ms) | 10 026 ms EMPTY | 10 806 ms EMPTY |
| `2147484` | rejected → 10s cut (was the 504 ms silent drop) | 10 028 ms EMPTY | 10 814 ms EMPTY |

`cli_reached=yes` in all 22 rows - the pre-clamp `.sh` shapes that skipped the CLI entirely (`abc`, `-3`)
are gone too. rc=0 everywhere, as the fail-open contract requires.

Same value set with a healthy instant CLI: **22/22 rows rc=0 and the brief FORWARDED** (34-42 ms `.sh`,
411-443 ms `.ps1`) - no knob value, valid or not, can cost the payload.

What was deliberately not re-measured: the SF-1 wedge and the SF-2 lower bound were closed in the round
above and only sanity-checked here - `.ps1` wedge at budget 2 → rc=0, 2 428 ms, drained, FORWARDED.

## Parity and gates on these exact bytes

```
live read-only brief, crew collection `grimoire` / tome `crew` (nothing written to the store):
  sh rc=0 28 769 bytes 1 line | ps rc=0 28 769 bytes 1 line | direct CLI rc=0 28 769 bytes
  PASS .sh stdout == .ps1 stdout ; PASS .sh stdout == CLI stdout
  (28 769 vs the 28 627 of the previous round: the crew brief grew between the two runs, both times
   read-only - the number is a property of the store at the moment of the run, not of the scripts)
bash -n rc=0 · PowerShell language parser rc=0 ("ps1 parse OK") · both files 0 non-ASCII lines
shellcheck: still not installed on this box (not passed, disclosed)
```

Cleanup: `~/scratch/t7-*` deleted, no probe processes left, `docker ps` untouched (no container was
stopped this round - unreachability came from the stub and from /dev/null-bounded hangs), the user's real
`~/.qwen/settings.json` never read. No commit.
