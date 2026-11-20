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
`mcp` argument, pass the two identity variables. Registration mechanics for
Codex and Kimi live in their own docs — this repo verifies the teaching block
for their homes (above), not their client configuration.

## Who am I: the actor/tome chain

Every write carries actor provenance (alias+vendor, or `human`). Your tome
resolves by a chain, first hit wins:

1. explicit `tome` parameter on the tool call;
2. `GRIMOIRE_TOME` environment variable;
3. `GRIMOIRE_ACTOR` alias → the actor's tome;
4. the AGENTS.md claim block written by `grimoire init --agent <alias> [--tome key]`;
5. the MCP client's reported identity (clientInfo);
6. `defaultTome` in config.

`GRIMOIRE_ACTOR`/`GRIMOIRE_TOME`/`GRIMOIRE_HOME` are reserved identity
variables, not config overlays — the config loader carves them out of the
`GRIMOIRE_*` namespace (see `docs/configuration.md`).

## SessionStart hook (Qwen Code)

The repo ships the two scripts a Qwen Code `SessionStart` hook needs:
`deploy/hooks/grimoire-session-start.sh` (Linux/macOS) and
`deploy/hooks/grimoire-session-start.ps1` (Windows). Each runs
`grimoire brief --format hook --daily` and forwards the CLI's envelope line
untouched, so the model boots with exactly what the store answered.

Register it **user-scope** (`~/.qwen/settings.json`) — project-scope hooks sit
behind folder trust and a block awaiting its first decision looks exactly like
a hook that never fired. The full annotated snippet, the timeout sizing rule
(`timeout ≥ 2 × HOOK_GRIMOIRE_TIMEOUT + 2`), the two knobs
(`HOOK_GRIMOIRE_BIN`, `HOOK_GRIMOIRE_TIMEOUT` — deliberately outside the
`GRIMOIRE_*` config namespace) and the fail-open contract (broken Grimoire
costs a session nothing but the brief: empty envelope, exit 0, one stderr
line) are documented in README → SessionStart hook, with live-fire receipts in
`audits/t7-live-firing-receipt.md`.

## Per-agent launchers in one line each

- **Qwen Code**: `qwen mcp add` as above + the SessionStart hook for boot briefs.
- **Claude Code**: `claude mcp add` as above; the teaching block lands in `CLAUDE.md`.
- **Codex / Kimi**: teaching block lands in their `AGENTS.md` when the file exists;
  MCP client config is theirs to own.
- **Anything else speaking MCP stdio**: spawn `<binary> mcp`, set the two
  identity env vars, done.
