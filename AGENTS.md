# AGENTS — Grimoire

Project-level rules for AI coding agents. System shape: `CONTEXT.md`. Conventions and repo
layout: `STRUCTURE.md`. Decision history: `TECHNICAL-SOLUTIONS.md`. Work tracking: the
Overmind board (MCP) + `BACKLOG/` (read-only materialized mirror). Read them before
changing anything — this file is the entry point, not a replacement.

> Working title was "Deepmemo"; renamed to **Grimoire** everywhere on 2026-10-17 (ADR-006) —
> repo, board project (GRR number prefix) and on-disk directory included.

## What this is

Grimoire — a local agent memory service (short-term session, long-term project/global,
personal) with three faces of one binary: MCP stdio server (`grimoire mcp`), loopback web
server + SPA (`grimoire serve`), management CLI. Store: Qdrant. Embeddings: Ollama behind a
pluggable port. Full topology: `CONTEXT.md`.

Current state: the MVP (board feature `001-agent-memory-mvp`) is in flight — live status
lives on the Overmind board, never in these files. Post-MVP pipeline: `003-honest-writes`,
`004-agent-startup-brief`, `008-books-and-universe` (the Books & Universe tenant model,
ADR-007), then `005`–`007`. Work planning happens ONLY through the Overmind MCP tools.

## Stack

- .NET 10 / C# 14, ASP.NET Core minimal APIs, no ORM (Qdrant client only)
- ModelContextProtocol C# SDK (MCP face)
- Next.js 16 static export, React 19, TypeScript strict, Tailwind 4, React Query 5 (SPA)
- Tests: xUnit + Shouldly + NSubstitute; `tests/` mirrors `src/`

## Commands

- Build: `dotnet build Grimoire.sln`
- Test: `dotnet test Grimoire.sln`
- Format: `dotnet format --verify-no-changes`
- Web (inside `web/`): `npm run lint && npx tsc --noEmit`; `npm run build` (static export,
  served by `Grimoire.Api` — no Next server in production)
- Local infra: `docker compose -f deploy/docker-compose.yml up -d` (qdrant + ollama)
- First run: `grimoire init` (config + collection) → `grimoire serve` (UI) or agent config:
  command `grimoire`, args `["mcp"]`

## Hard rules

- **Loopback-only bind** for the HTTP face — that is the security boundary. Never bind a
  public interface without a new ADR.
- **No silent deletes**: `forget` = tombstone (status + reason + timestamp); GC owns hard
  delete (expired session records, aged tombstones).
- **Every memory write carries actor provenance** (agent alias+vendor, or `human`).
- The Qdrant payload schema is an **additive-only contract**.
- Dependency direction `Core ← Infrastructure / Mcp / Api ← Cli`; `Mcp` and `Api` never
  reference each other.
- No new NuGet/npm packages without OSI license vetting (nuget-licensing-guard rules) and
  central pinning in `Directory.Packages.props`.
- No git commits or pushes unless the user explicitly asked in the current session.
- `BACKLOG/` is a read-only mirror — all planning writes go through the Overmind MCP.
- Removed requirements/decisions get `<removed YYYY-MM-DD — reason>` tombstones, never
  silent deletion.

## Product principles

- Target user: the owner — a senior .NET developer running multiple AI agents locally;
  power-user defaults, zero setup beyond `grimoire init`.
- UX budgets: two-click rule in the SPA; destructive actions (forget/purge) confirmed or
  reversible; settings editable both by hand (config file) and via UI.
- Quality bar: build + tests green; loopback-only assertion covered by a test; SPA works
  from static export without a server runtime.

## References

- `CONTEXT.md` — system shape: three faces, memory model (scope × kind), standing rules
- `STRUCTURE.md` — repository layout, dependency direction, code conventions
- `TECHNICAL-SOLUTIONS.md` — ADR-001..007 (decision history incl. ADR-006 naming, ADR-007
  Books & Universe tenant model)
- `audits/` — review reports and research artifacts (`review-001-*.md`,
  `architect-review-*.md`, `naming-tournament-*.md`, `strategy-grimoire-*.md`)
- Overmind board (MCP tools / web UI) — the ONLY live source of feature/task status;
  `BACKLOG/` is its read-only materialized mirror, never hand-edited
