# Platform support

What runs where, measured against the shipped code — not aspirational.

## Release binaries

Built by `scripts/release-pack.sh` from the single solution; self-contained,
single-file, compressed. Three RIDs ship today:

| RID | OS | Notes |
|---|---|---|
| `linux-x64` | Linux x86-64 | glibc-based distros |
| `osx-arm64` | macOS on Apple silicon | no Intel build ships |
| `win-x64` | Windows 10/11 x86-64 | binary named `grimoire.exe` |

The dotnet-tool dev channel is framework-dependent and one package covers all
platforms — it rides an installed .NET 10 runtime. The release binaries above
do not need any runtime.

## The loopback boundary (all platforms)

The HTTP face binds `127.0.0.1` only — `LoopbackBinding.Endpoint` constructs
the endpoint from `IPAddress.Loopback`, and `LoopbackGuard.EnsureLoopbackOnly`
refuses any wider address set at startup. Default port 4770
(`serve.port` in config). This is the security boundary, not a convenience:
widening the bind requires a new ADR (see `SECURITY.md`). Qdrant and Ollama are
expected on loopback of the same machine; the compose file maps their ports to
`127.0.0.1` only.

## `grimoire ui`

The command probes `http://127.0.0.1:<serve.port>/health` and opens the
console in your default browser — that part works on all platforms.

The rescue step is Linux-only: when nothing answers, `grimoire ui` runs
`systemctl --user start grimoire-serve` and waits (10s budget) before opening.
Where systemd does not exist the command fails honestly — exit 6, a message
naming that `systemctl` is not available to start the unit. On macOS and
Windows, start `grimoire serve` yourself first, then `grimoire ui` (or just
open the URL).

Login autostart (`grimoire ui --enable-at-login` / `--disable-at-login`)
writes or removes a user-level XDG desktop entry
(`~/.config/autostart/grimoire-ui.desktop`) — the Linux user-autostart
mechanism; macOS and Windows do not consume that file.

## macOS specifics

- Apple silicon only (`osx-arm64`); no Intel build ships.
- The SessionStart hook script uses `timeout` when present and runs unbounded
  when it is not — on macOS `timeout` is coreutils, so `brew install
  coreutils` gives the hook its wall-clock ceiling; without it the per-hook
  `timeout` in the runtime's config is the only bound.

## Windows specifics

- `install.ps1` requires PowerShell 7 (`pwsh`); the shipped `.ps1` was
  verified under PowerShell 7 on Linux, Windows itself and Windows PowerShell
  5.1 are untested surfaces — say so when it matters to you.
- The binary is `grimoire.exe`; PATH is a user-environment change, the
  installer only prints the note.

## What is NOT platform-dependent

The MCP stdio face, the CLI verbs, the store model and the config format are
identical everywhere the binary runs. Tomes, actors, tombstones and scry
behave the same on all three platforms.
