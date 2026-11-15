# Grimoire on Windows

Field notes from the 0.3.0 install (2026-10-26/27). The shipped channel is the dotnet tool
from `README.md` → Distribution; this folder adds what Windows needs on top of it: a hidden
console launcher, a logon task, and the config edits a fresh box required.

## Install or update

```powershell
.\deploy\windows\install-tool.ps1
```

Packs `src/Grimoire.Cli`, installs or updates the global tool from `build/packages`,
builds the SPA and copies `web/out` to `%USERPROFILE%\.grimoire\web`. Then `grimoire init`
on a first run (writes `%USERPROFILE%\.grimoire\config.json`, creates the Qdrant collection).

A running `grimoire.exe` locks the tool store, so the script first stops the
`serve-hidden.vbs` launcher (it would respawn serve) and every `grimoire` process — serve
and the MCP servers of open agent sessions. The launcher is restarted at the end, even
when the update fails; agent sessions need an MCP reconnect (`/mcp` in Claude Code).

## Config edits a fresh box needed

| Key | Value | Why |
|---|---|---|
| `embedding.model` | `qwen3-embedding:0.6b` | the bare `qwen3-embedding` tag is the 8B model (4096 dims) and fails the strict 1024-dim check (D1 in `audits/install-smoke-2026-10-26.md`) |
| `embedding.endpoint` | `http://127.0.0.1:11434` | pin the loopback literal; `localhost` can resolve to `::1` and a different listener |
| `defaultTome` | your crew tome (`inno-crew` here) | CLI `list`/`recall`/`forget` read this tome, not the actor's (D2); top-level key, so hand-edit — `config set` only takes `section.property` (D3) |

Pull the model into the Ollama the config points at: `ollama pull qwen3-embedding:0.6b`
(or `curl -X POST http://127.0.0.1:11434/api/pull -d '{"name":"qwen3-embedding:0.6b"}'`).
After changing the model run `grimoire reindex`.

## Console at logon

`grimoire ui` does not work on Windows: its liveness probe gives the deep `/health`
2 s while that endpoint round-trips Ollama (3–4 s), and the fallback is systemd (D4).
Use the scripts instead:

```powershell
.\deploy\windows\install-autostart.ps1     # copies serve-hidden.vbs to ~\.grimoire, registers "Grimoire Serve" (onlogon), starts it now
.\deploy\windows\uninstall-autostart.ps1   # removes the task and stops the console
```

`serve-hidden.vbs` runs `grimoire serve` with no window; output lands in
`%USERPROFILE%\.grimoire\serve.log`. Console: `http://127.0.0.1:4770` (loopback only).

The task fires at logon together with Docker Desktop, so Qdrant is not up yet and the
first `serve` dies on `actively refused it` (seen 2026-10-28: task result 0, console dead).
The launcher therefore waits on `serve` and restarts it every 15 s; a run that lived over a
minute resets the budget, 30 short runs in a row stop it. The task is registered with no
time limit and runs on battery — the old defaults (72 h, AC only) killed or skipped it.

Gotchas:
- A task created from an elevated console can only be replaced from an elevated one
  (`Access is denied` from both `schtasks` and `Register-ScheduledTask`). Run the installer
  as admin once; the task it writes runs non-elevated anyway.
- Run `schtasks` from PowerShell or cmd. Git Bash rewrites `/create` into
  `C:/Program Files/Git/create`; if you must, prefix with `MSYS_NO_PATHCONV=1`.
- `cmd /c` strips the first and last quote when the line starts with a quote and has
  more than two of them — the vbs wraps the whole command in an extra pair for that reason.
- Manual start without the task: `Start-Process grimoire.exe serve -WindowStyle Hidden`.

## Claude Code MCP

```powershell
claude mcp add grimoire -s user -e GRIMOIRE_ACTOR=<alias>+claude -e GRIMOIRE_TOME=<tome> -- "$env:USERPROFILE\.dotnet\tools\grimoire.exe" mcp
```

Put the name first: `-e` is variadic and swallows a trailing name. `GRIMOIRE_TOME` keeps
every agent of the crew in one tome; without it the tome derives from the actor alias.

## Two Ollamas on one port

A native Windows Ollama and a docker one can both listen on 11434 (the native one binds
`127.0.0.1` explicitly, docker's proxy binds the wildcard). A model pulled into one is
404 on the other, which looks like a Grimoire bug. `netstat -ano | findstr 11434` first.
