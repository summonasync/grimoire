# STRUCTURE — Grimoire

> The working title "Deepmemo" was fully renamed to **Grimoire** on 2026-10-17 (ADR-006):
> solution, board project (GRR prefix) and the on-disk directory.

## Repository layout

```
Grimoire.sln
├── src/
│   ├── Grimoire.Core/             Domain + application: MemoryRecord, Scope/Kind,
│   │                              RecallQuery, scoring; ports: IMemoryStore,
│   │                              IEmbeddingProvider, IClock; MemoryService
│   │                              (remember/recall/forget/list/gc/stats); PersonaService;
│   │                              BriefService (startup brief); tome partitioning:
│   │                              TomeKey, TomeResolver, TomeStats. Zero external
│   │                              service dependencies.
│   ├── Grimoire.Infrastructure/   Adapters: QdrantMemoryStore, OllamaEmbeddingProvider,
│   │                              config loading (~/.grimoire/config.json + GRIMOIRE_* env).
│   ├── Grimoire.Mcp/              MCP face: stdio server, agent tools over Core services.
│   ├── Grimoire.Api/              HTTP face: Kestrel loopback host, minimal API endpoints,
│   │                              static SPA hosting, background GC sweep.
│   └── Grimoire.Cli/              ENTRY POINT (exe): command routing; `grimoire mcp` hosts
│                                  the MCP face, `grimoire serve` hosts the HTTP face.
├── web/                           Next.js 16 static-export SPA (management UI); build output
│                                  consumed by Grimoire.Api as static files.
├── tests/                         Mirrors src/; each test project references only its target.
├── deploy/
│   └── docker-compose.yml         qdrant + ollama for local run.
├── audits/                        Skill-produced reports (audit-*, review-*, naming-*).
├── BACKLOG/                       Read-only Overmind mirror — never hand-edit.
├── CONTEXT.md                     System shape (this repo's architecture overview).
├── STRUCTURE.md                   This file.
├── TECHNICAL-SOLUTIONS.md         ADRs.
└── AGENTS.md                      Agent entry point.
```

## Dependency direction

`Core ← Infrastructure`, `Core ← Mcp`, `Core ← Api`, `Cli → all`.
`Mcp` and `Api` never reference each other. Reverse or skip-level references are forbidden.

## Conventions

- File-scoped namespaces, nullable enabled, implicit usings.
- `record` for DTOs and value objects, `class` for services.
- API endpoints, DTOs and services live in separate files; no God services.
- No comments except non-obvious decisions; code and logs in English.
- Tests: xUnit + Shouldly + NSubstitute; Core tested against in-memory fakes, adapters
  against contract fakes; live-service integration tests are opt-in.
- Central package versioning in `Directory.Packages.props`; every new dependency goes
  through OSI license vetting (nuget-licensing-guard rules).
- Frontend (`web/`): FSD layering, React Query 5 for all server state, no `useEffect`
  fetching, RSC-first — crew web rules apply.
