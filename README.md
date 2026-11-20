```
{  ·  ·  ·  ·  ·  }
 |   .-------.   |
 ·   | {:·:} |   ·
 |   '-------'   |
 ·      |·|      ·
{  ·  ·  '·'  ·  }

 ██████╗ ██████╗ ██╗███╗   ███╗ ██████╗ ██╗██████╗ ███████╗
██╔════╝ ██╔══██╗██║████╗ ████║██╔═══██╗██║██╔══██╗██╔════╝
██║  ███╗██████╔╝██║██╔████╔██║██║   ██║██║██████╔╝█████╗
██║   ██║██╔══██╗██║██║╚██╔╝██║██║   ██║██║██╔══██╗██╔══╝
╚██████╔╝██║  ██║██║██║ ╚═╝ ██║╚██████╔╝██║██║  ██║███████╗
 ╚═════╝ ╚═╝  ╚═╝╚═╝╚═╝     ╚═╝ ╚═════╝ ╚═╝╚═╝  ╚═╝╚══════╝
```

**One shared memory. Every agent. Every project.**
*The book the summoner writes results into.*

`await SummonAsync();` — part of the summoner's kit
overmind · **▸ grimoire** · squeezr · mindlash studio

---

## What it is

Persistent memory for AI coding agents — decisions, preferences, "the human
said X" — written down, searchable, surviving sessions, projects and agents.
Local-first, no cloud, no LLM in the core: it stores, scores and clusters;
the agents do the thinking.

One binary, three faces:

| Face | Command | Who uses it |
|---|---|---|
| MCP server | `grimoire mcp` (stdio) | AI agents — anything speaking MCP |
| Web console | `grimoire serve` (loopback HTTP + SPA), opened by `grimoire ui` | the human |
| CLI | `grimoire <command>` | scripts and quick queries |

## Why it exists

A developer with an agent crew can hold more context in flight — only if the
crew carries what it learns between sessions and projects. Otherwise the
explanation is the work. Overmind holds what one project knows; Grimoire is
the layer that survives the session — and every other project.

## The name

Seven rounds, seven themed universes, one user vote.
[The bracket](audits/naming-tournament-2026-10-17.md) is part of the repo
history. A grimoire is the agent's book of spells — everything it learned
about you, written where the next session can find it.

## Trade-offs

Read these before adopting; they weigh the same as the feature list.

- Semantic recall needs Qdrant + an embedding provider — Docker and Ollama
  before the memory gets smart.
- Loopback-only HTTP by design: no remote console, no phone, no team view.
- Young: 0.x. Contracts are described as frozen; a 0.x is a 0.x.
- Value depends on discipline — an unwritten memory store is an empty room.

## Install

Self-contained binaries with the web console inside the archive — no SDK, no
clone. Grab `install.sh` + your platform archive from
[GitHub Releases](https://github.com/summonasync/grimoire/releases/latest)
and run it (Windows: `install.ps1` under `pwsh`). Flags, defaults,
idempotence and the from-source path: **docs/installation.md**.

## Quickstart

```bash
# 1. Local infrastructure (Qdrant + Ollama, loopback only)
docker compose -f deploy/docker-compose.yml up -d
ollama pull qwen3-embedding

# 2. First run
grimoire init     # config + collection, then probes the model — loud remedy when missing
grimoire serve    # console on http://127.0.0.1:4770
grimoire ui       # opens the console in your browser

# 3. The day's read
grimoire report           # evening report of daily notes, grouped by actor
grimoire brief --daily    # the same day folded into the startup brief
```

## Docs

| Read | For |
|---|---|
| [docs/installation.md](docs/installation.md) | binary channel per OS, from source, first run |
| [docs/agent-wiring.md](docs/agent-wiring.md) | MCP registration, actor/tome chains, teaching block, SessionStart hook |
| [docs/how-it-works.md](docs/how-it-works.md) | memory model, tomes, scoring, corrections, brief budget, handshakes, tool table |
| [docs/configuration.md](docs/configuration.md) | every key, default, env override and refusal |
| [docs/troubleshooting.md](docs/troubleshooting.md) | missing model, dead stores, port fights, SPA skew |
| [docs/platform-support.md](docs/platform-support.md) | what runs where — honestly |

System shape and decisions live in `CONTEXT.md`, `STRUCTURE.md` and
`TECHNICAL-SOLUTIONS.md` (ADRs); work tracking lives on the Overmind board.

## Distribution

The public channel is **GitHub Releases**: per-RID self-contained archives
(`win-x64`, `linux-x64`, `osx-arm64`) carrying binary + stamped SPA export +
`SHA256SUMS`, built by `scripts/release-pack.sh`; install scripts ship as
release assets. The dev channel is a dotnet tool from a local folder feed
(nuget.org declined for now):

```bash
dotnet pack src/Grimoire.Cli -c Release -o build/packages
dotnet tool install -g Grimoire --add-source build/packages --no-cache
grimoire --version    # -> grimoire 0.9.1
```

The console does not ride the tool package — `serve` reads the export from
`~/.grimoire/web` and warns honestly on version skew
(`docs/troubleshooting.md`). Release verification is local: the same gates
in Release configuration, evidence in `audits/release-<version>.md`
(precedent `audits/release-0.8.0-2026-11-10.md`).

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md) — the contract, the commands, the
no-CI verification model.

```bash
dotnet build Grimoire.sln
dotnet test Grimoire.sln
dotnet format --verify-no-changes
(cd web && npm run lint && npx tsc --noEmit && npm run build)
```

## License & security

Apache-2.0 — see [LICENSE](LICENSE) and [CHANGELOG.md](CHANGELOG.md).
Security model and private reporting: [SECURITY.md](SECURITY.md).

---

*summonasync — one dev, a swarm of agents, nothing leaves the machine.*
Everything here runs on your machine: the store lives in `~/.grimoire`, the
web face binds `127.0.0.1` only. If it does not — that is a bug, file it.
