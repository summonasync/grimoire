# Contributing to Grimoire

Grimoire is a local agent memory service — .NET 10 backend, Next.js static-export
SPA, one Qdrant store. This file is the working contract for changes; the deeper
references are `CONTEXT.md` (system shape), `STRUCTURE.md` (layout and
conventions) and `TECHNICAL-SOLUTIONS.md` (decision history, ADRs).

## Prerequisites

- .NET 10 SDK
- Docker — Qdrant and Ollama run from `deploy/docker-compose.yml`
- Node.js + npm — only for work under `web/`

## Local setup

```bash
docker compose -f deploy/docker-compose.yml up -d   # Qdrant + Ollama, loopback only
ollama pull qwen3-embedding                         # the default embedding model
dotnet build Grimoire.sln
dotnet test Grimoire.sln
```

Run the faces from source with `dotnet run --project src/Grimoire.Cli -- <command>`
(`init` first — it writes `~/.grimoire/config.json` and creates the collection —
then `serve`, `mcp`, or any management verb). The README quickstart shows the
installed-binary flow.

## Verification — there is no CI

By explicit project decision, Grimoire has **no CI pipelines**. All verification
is local, and it is the contributor's job to run it before opening a PR:

```bash
dotnet build Grimoire.sln
dotnet test Grimoire.sln
dotnet format --verify-no-changes
# when web/ is touched:
(cd web && npm run lint && npx tsc --noEmit && npm run build)
```

Releases run the same gates in Release configuration, and the evidence is
recorded in `audits/release-<version>.md`. A PR without verification output is
not reviewable — paste the counts, not just "tests pass".

## Conventions

- English everywhere: code, comments, commit messages, docs, issue and PR text.
- Dependency direction: `Core ← Infrastructure / Mcp / Api ← Cli`. `Mcp` and
  `Api` never reference each other.
- The Qdrant payload schema is an **additive-only contract** — stored data must
  keep reading correctly across versions.
- The HTTP face binds loopback only. Widening the bind requires a new ADR in
  `TECHNICAL-SOLUTIONS.md`, not a config change.
- No silent deletes: `forget` writes a tombstone; hard delete belongs to GC and
  to the human-only console purge.
- Tests: xUnit + Shouldly + NSubstitute; `tests/` mirrors `src/`. Behavior
  changes ship with tests.
- New NuGet/npm dependencies need an OSI-approved license (MIT/Apache-2.0/BSD)
  and central version pinning (`Directory.Packages.props` for .NET). Propose the
  dependency in the PR description; do not smuggle it in.

### The frozen MCP surface

The MCP tool surface is pinned by contract snapshots in
`tests/Grimoire.Mcp.Tests/TestData/snapshots/` — title, description, annotations,
input schema and output shape per tool. Any intentional tool change updates its
snapshot **in the same PR**: rerun the contract tests with
`GRIMOIRE_UPDATE_TOOL_SNAPSHOTS=1` to regenerate, and let review see the contract
diff in that JSON. A new tool without a snapshot, or a stale snapshot for a
removed tool, fails the suite.

## Branches, commits, PRs

- One feature per branch: `feat/<short-slug>`; fixes `fix/<slug>`, docs
  `docs/<slug>`.
- Conventional-style commits: `feat(scope): …`, `fix(scope): …`, `docs(scope): …`,
  `test(scope): …`, `build(release): …`.
- Open a PR into `main`. Describe what shipped and why, paste the verification
  output, and call out contract impact (MCP snapshots, payload schema, bind).
- A maintainer reviews and merges. Nothing auto-merges; force-pushes to shared
  branches are not part of the flow.

## Reporting bugs and proposing features

Use the issue templates. For security problems, follow `SECURITY.md` — private
reporting, never a public issue.
