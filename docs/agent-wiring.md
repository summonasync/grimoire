# Agent wiring

How coding agents reach the memory: the teaching block, the MCP registration
shapes, the actor/tome identity chain, and the SessionStart hook.

## Teach the agent first: `grimoire install`

An agent that never learns about the memory never calls it. The verb stamps a
short managed block — what the memory is, `memory_brief` at session start,
write-as-you-go, the tome routing table — into every agent home memory file it
finds:

```bash
grimoire install                 # upsert into ~/.claude/CLAUDE.md, ~/.qwen/QWEN.md,
                                 # ~/.codex/AGENTS.md, ~/.kimi-code/AGENTS.md
grimoire install --print-block   # print the block, touch nothing
grimoire install --remove        # strip the managed range only
```

The block lives between `<!-- grimoire:begin -->` and `<!-- grimoire:end -->`
markers: reruns replace the marked range in place (your own prose around it
survives), append where markers are absent, and report one outcome line per
file — `created` / `updated` / `unchanged` / `removed` / `skipped (not
found)`. Missing files are skipped, never created; a home is detected only
when it already exists. `GRIMOIRE_HOME` redirects the home scan (test rigs,
sandboxes); it does not move the config file.

Kit coexistence: where the OvermindCrewKit mirrors already carry the same
block, reruns report `unchanged` — the verb only manages what it stamped.

## MCP registration

The MCP face is `grimoire mcp` over stdio. The two runtimes this crew runs
live with, registered user-scope with identity in the environment:

```bash
# Qwen Code
qwen mcp add grimoire /path/to/grimoire mcp --scope user \
  --env GRIMOIRE_TOME=crew --env GRIMOIRE_ACTOR=<alias>+qwen --trust

# Claude Code
claude mcp add grimoire --scope user \
  -e GRIMOIRE_TOME=crew -e GRIMOIRE_ACTOR=<alias>+claude -- /path/to/grimoire mcp
```

Any other MCP-capable runtime works the same way: spawn the binary with the
`mcp` argument, pass the two identity variables. The raw shape:

```json
{
  "grimoire": {
    "command": "grimoire",
    "args": ["mcp"],
    "env": { "GRIMOIRE_ACTOR": "dan+claude" }
  }
}
```

Registration mechanics for Codex and Kimi live in their own docs — this repo
verifies the teaching block for their homes (above), not their client
configuration.

## Who am I: the actor and tome chains

Every write carries actor provenance (alias+vendor, or `human`). The actor
resolves through one chain, first match wins:

1. the explicit `--actor` flag;
2. `session_identity_set(alias, vendor)` — the MCP tool an agent calls once at
   session start when its runtime hands it a stable alias; the pair lives for
   the connection only, is never persisted, and the latest pair wins;
3. the identity file `~/.overmind/identity/parent-<ppid>.json` — written by
   the Overmind adapter when the agent session registers, keyed by the parent
   PID; Grimoire is a read-only consumer and skips it silently when absent;
4. `GRIMOIRE_ACTOR` (`<alias>` or `<alias>+<vendor>`);
5. the AGENTS.md claim block (`grimoire init --agent` writes it: alias,
   optional vendor, tome);
6. the MCP client name; plain `human` as the last resort.

The four flag-less CLI reads (`brief`, `list`, `recall`, `report`) walk the
same chain minus the `--actor`/`--tome` flags (they have none) and never reach
the clientInfo step — their parity with the MCP face runs exactly to that
boundary.

**Do not set `GRIMOIRE_ACTOR` at machine scope** (shell profile, system-wide
environment): a session launched outside the wrapper meant to own the value
silently writes under that shared actor — exactly how the LUCA deployment
misattributed a whole crew. Keep it inside a per-agent launcher, one wrapper
per crew member:

```bat
@echo off
set GRIMOIRE_ACTOR=Gandalf+claude
set GRIMOIRE_TOME=crew
claude
```

Your tome resolves by its own chain, first hit wins:

1. explicit `tome` parameter on the tool call;
2. `GRIMOIRE_TOME`;
3. `GRIMOIRE_ACTOR` alias → the actor's tome;
4. the AGENTS.md claim block;
5. the MCP client's reported identity (clientInfo);
6. `defaultTome` in config.

Set `GRIMOIRE_TOME` explicitly for a crew — one kebab-case key for everyone.
Without it the tome resolves per actor alias and the crew forks into one tome
per agent. A session started outside its launcher falls to the shared
attribution; restarting through the launcher (or calling
`session_identity_set`) fixes everything written afterwards.

`GRIMOIRE_ACTOR`/`GRIMOIRE_TOME`/`GRIMOIRE_HOME` are reserved identity
variables, not config overlays — the config loader carves them out of the
`GRIMOIRE_*` namespace (see `docs/configuration.md`).

## SessionStart hook (Qwen Code)

The repo ships the two scripts a Qwen Code `SessionStart` hook needs:
`deploy/hooks/grimoire-session-start.sh` (Linux/macOS) and
`deploy/hooks/grimoire-session-start.ps1` (Windows). Each runs
`grimoire brief --format hook --daily` and forwards the CLI's envelope line
untouched, so the model boots with exactly what the store answered.

The runtime places `hookSpecificOutput.additionalContext` inside the system
instruction, wrapped in its own `<qwen:session-start-context>` block,
HTML-escapes angle brackets inside it, and re-stamps per source (`startup`,
`resume`, `compact`) instead of accumulating — measured on Qwen Code 0.27.1.
That is why the brief arrives tag-free: the CLI's hook face rewrites angle
brackets to square-bracket twins so board XML markers and tombstone tags
survive the trip readable.

Register the hook **user-scope** (`~/.qwen/settings.json` or
`$QWEN_HOME/settings.json`). Project-scope hooks sit behind folder trust and
a block awaiting its first decision looks exactly like a hook that never
fired; user scope has no such gate. Hook definitions load at startup — after
editing, run `/hooks` to reload or restart the session. The measured snippet:

```jsonc
{
  "hooks": {
    "SessionStart": [
      {
        "matcher": "startup|resume|compact",   // regex against the event source
        "sequential": false,
        "hooks": [
          {
            "type": "command",
            "command": "bash /path/to/Grimoire/deploy/hooks/grimoire-session-start.sh",
            "name": "grimoire-brief",
            "description": "Start the session with the Grimoire brief",
            "timeout": 24,   // SECONDS (default 60; >=1000 read as legacy milliseconds)
            "env": { "HOOK_GRIMOIRE_BIN": "/home/you/.dotnet/tools/grimoire" },
            "shell": "bash"
          }
        ]
      }
    ]
  }
}
```

Windows: same block with
`"command": "powershell -NoProfile -ExecutionPolicy Bypass -File C:\\path\\to\\Grimoire\\deploy\\hooks\\grimoire-session-start.ps1"`
and `"shell": "powershell"`. The `env` block points the hook at a non-PATH
binary (or a test rig).

**Sizing `timeout`.** The script spends its budget twice — draining stdin,
then calling the CLI — so the worst case is `2 × HOOK_GRIMOIRE_TIMEOUT` plus a
second of teardown. Set the per-hook `timeout` to at least
`2 × HOOK_GRIMOIRE_TIMEOUT + 2`: at the default budget of 10 that is 22; the
snippet carries 24 for slack. A budget of 60+ could never finish before the
runtime cut.

**Knobs** (both scripts, validated before use):

| Variable | Meaning |
|---|---|
| `HOOK_GRIMOIRE_BIN` | absolute path to the binary; unset: `PATH`, then `~/.dotnet/tools/grimoire` (Windows: `%USERPROFILE%\.dotnet\tools\grimoire.exe`) |
| `HOOK_GRIMOIRE_TIMEOUT` | seconds per leg, integers `1..59` only — `0`, `-3`, `1.5`, `abc`, `60`, overflow all rejected, default `10`; a leading zero reads as the number (`07` = 7), identically on both platforms |

The `HOOK_` prefix is not decoration: the CLI claims the whole `GRIMOIRE_*`
namespace and refuses a name it does not own, so a `GRIMOIRE_BIN` knob would
break the very call it configures.

**Fail-open is the whole contract.** Store unreachable, binary missing, CLI
silent or confused — every path prints a parseable empty envelope
(`{"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":""}}`),
exits 0 and leaves one `grimoire-session-start: <reason>` line on stderr. The
runtime drops the whitespace-empty context, so a broken Grimoire costs a
session nothing but the brief.

Measured through the shipped `.sh`: live brief 0.43 s / 31 KB envelope;
refused store 0.26 s / 79-byte empty envelope + one stderr line; missing
binary 14 ms; unroutable endpoint self-cuts at 10.03 s (2.03 s with
`HOOK_GRIMOIRE_TIMEOUT=2`), still exit 0. `bash -n` and the PowerShell
language parser pass on the shipped bytes; the twins were byte-compared
against each other and against the direct CLI call. Unexercised surfaces:
Windows itself (the `.ps1` ran under PowerShell 7.7 on Linux), PowerShell
5.1's parser, `shellcheck` (not installed). On macOS `timeout` is coreutils —
the script uses it when present and runs unbounded when not; `brew install
coreutils` closes that. Live-fire receipts:
`audits/t7-live-firing-receipt.md`.

## Per-agent launchers in one line each

- **Qwen Code**: `qwen mcp add` as above + the SessionStart hook for boot briefs.
- **Claude Code**: `claude mcp add` as above; the teaching block lands in `CLAUDE.md`.
- **Codex / Kimi**: teaching block lands in their `AGENTS.md` when the file exists;
  MCP client config is theirs to own.
- **Anything else speaking MCP stdio**: spawn `<binary> mcp`, set the two
  identity env vars, done.
