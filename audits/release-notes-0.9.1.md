# Grimoire 0.9.1 — first public release

Grimoire is a local agent memory service: one binary, three faces — an MCP
stdio server for your coding agents, a loopback web console, and a management
CLI — over a single Qdrant store on your machine. No cloud, no telemetry.
License: Apache-2.0 (see `LICENSE`).

## Install

Pick the archive for your platform (each carries the binary **and** the web
console export, plus internal `SHA256SUMS`):

- Linux x64: `grimoire-0.9.1-linux-x64.tar.gz`
- macOS arm64: `grimoire-0.9.1-osx-arm64.tar.gz`
- Windows x64: `grimoire-0.9.1-win-x64.zip`

Linux / macOS:

```bash
bash install.sh grimoire-0.9.1-linux-x64.tar.gz
```

Windows (PowerShell):

```powershell
pwsh -File install.ps1 -Archive grimoire-0.9.1-win-x64.zip
```

The script lands `grimoire` on PATH and the console export into
`~/.grimoire/web`. No sudo, no .NET SDK, no repo clone; re-running is idempotent.

## First run

```bash
docker compose -f deploy/docker-compose.yml up -d   # Qdrant + Ollama, loopback only
ollama pull qwen3-embedding                        # the default embedding model
grimoire init                                      # probes the model, names the remedy when missing
grimoire serve                                     # console on http://127.0.0.1:4770
```

`deploy/docker-compose.yml` ships in the repo; copy that one file or run your
own Qdrant and Ollama on loopback. The HTTP face binds 127.0.0.1 only — that is
the security boundary; see SECURITY.md for the threat model and private
vulnerability reporting.

## Checksums (sha256)

```
0f6f4ded08e8ea2d9569a77ad7c117580e370a0dd57c9b92bcba3e438559084c  grimoire-0.9.1-linux-x64.tar.gz
132337642bc1eb4a0f1636fc34a4ce21a40d48a20e4b67d8e6664b977f9380e4  grimoire-0.9.1-osx-arm64.tar.gz
e6016fd704968dfff0d378f0b9fc5c690a388f8bf503a9522c60fe5402cdad81  grimoire-0.9.1-win-x64.zip
```

## What is in 0.9.1

Everything from the 0.9.x line (see CHANGELOG.md): tome-isolated memory with
persona documents and tombstone corrections, hybrid recall, daily notes and
the evening report, live MCP session registry, guided first-run init, and the
public distribution channel itself — self-contained per-RID archives with the
stamped SPA console and install scripts.

Verification: 2620 tests green in Release (plain and TZ=UTC), format and web
lint/tsc/build clean; install smoke from the archive without SDK or clone.
Full receipt: `audits/release-0.9.1-public-2026-11-19.md` in the repo.
