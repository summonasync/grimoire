# review-070-01 — T0 Host filtering (hard, Security elevated)

> Provenance: dotnet-code-reviewer sub-agent (qwen3.8-flash), dispatched by kelthuzad (lane api-hardening), 2026-11-18.
> Carrier reviewed: 6df65c0 on feat/070-public-security-hardening (base main 052762a).
> Post-review: the three MINORs were fixed in-slice by kelthuzad, commit 5b752cc (Api.Tests 414/414 green).
> Report text below is the reviewer's verbatim output.

verdict: PASS

Review report — feature 070-public-security-hardening, task T0 (Host filtering), carrier 6df65c0 on `feat/070-public-security-hardening` (base main 052762a). Reviewer: hard, elevated Security priority per wave rule.

## What I verified by running
- `dotnet test tests/Grimoire.Api.Tests --filter HostGuardTests` → **6 passed / 0 failed** (exit 0).
- `dotnet test tests/Grimoire.Api.Tests` (full scoped) → **412 passed / 0 failed** (exit 0) — executor's green claim reproduced, AC-4 holds, existing loopback bind-guard tests green and untouched by the diff (AC-3).
- `git show 6df65c0` — exactly 2 files, +72 lines, no drive-by edits.
- Did NOT re-run the red state (requires `git stash` — mutating; forbidden for reviewer). The red story is internally consistent: with the filter absent, `AllowedHosts` empty means allow-all, so exactly the 2 hostile theories + 1 pre-endpoint test fail and the 3 valid-host theories pass — matching the claimed 3 failed / 3 passed / 6 total.

## What I verified by reading
`src/Grimoire.Api/ApiHostBuilder.cs`, `tests/Grimoire.Api.Tests/HostGuardTests.cs`, `GrimoireApiFactory.cs`, `LoopbackBinding.cs`, `MemoryEndpoints.cs:25`, `Querying/MemoryQueryRunner.cs`, `tests/Grimoire.Cli.Tests/StatsParityContractTests.cs:192`, `src/Grimoire.Infrastructure/Processes/LoopbackConsoleProbe.cs`, `src/Grimoire.Cli/Commands/UiCommand.cs`.

## Security assessment (scope item 1 — DNS-rebinding defense)
- **Placement is complete.** `app.UseHostFiltering()` at ApiHostBuilder.cs:95 is the first user middleware, ahead of `UseStructuredErrors`, every endpoint map, and `UseSpaFrontend` (app:118) — so every path (api + SPA fallback) is gated before endpoint execution. The explicit call supersedes the framework's implicit default (which would run later in the chain); position is strictly better than the implicit one.
- **Missing/empty Host**: rejected twice over — Kestrel itself 400s HTTP/1.1 requests without Host; `AllowEmptyHostHeader` defaults to false anyway. Not tested (see MINOR-1 cluster) but defense-in-depth holds.
- **IPv6 `[::1]`**: absent from the allowlist, and that is consistent, not a gap — `LoopbackBinding.Endpoint` binds `IPAddress.Loopback` (IPv4) only, so an `[::1]` Host can never legitimately reach this socket (a request that claims `[::1]` on a socket bound to 127.0.0.1 is by definition hostile). The in-repo comment at LoopbackConsoleProbe.cs:8 confirms the product deliberately avoids the v6 surface.
- **Suffix/prefix tricks** (`127.0.0.1.evil.example`, `localhost.evil.example`): allowlist entries carry no `*` wildcards, so HostFiltering matching is exact-host (case-insensitive, port stripped for port-less entries) — these are refused. Framework-verified semantics; pinned only weakly by tests (MINOR-1).
- **Port-less entries accept any port**: verified empirically by the test design itself — harness binds ephemeral (Port=0) while Host says `:4770` and gets 200. This is the "verified, not assumed" the contract demanded; AC-2 holds.

## Scope item 2 — configuration interaction
No config path can empty the allowlist in any shipping surface. The allowlist is written via `Configure<HostFilteringOptions>` (DI, ApiHostBuilder.cs:61), immune to `builder.Configuration.Sources.Clear()`. Conversely, the framework's config-section binding can only *add* from a `HostFiltering` config section — but both the production path (`ServeCommand.BuildHostOptions` leaves `DisableConfigSourceClear=false`, sources cleared) and the test factory (`GrimoireApiFactory` never sets it — checked) clear sources, so no section can exist. The single non-clearing host (ApiHostTests:29) is an intentional hostile-Kestrel-config fixture; even if an external `HostFiltering` key existed there, a bind only overwrites present keys — the DI list is the only writer in practice. Verdict: sound.

## Scope item 5 — blast radius
All 47 host-building callsites I found in tests + src go through `ApiHostBuilder.Build` with loopback clients: `GrimoireApiFactory.NewClient` and `StatsParityContractTests.cs:192-203` derive `BaseAddress` from the server's own `127.0.0.1:<ephemeral>` address → Host header always allowed. Internal src callers (`LoopbackConsoleProbe` → `http://127.0.0.1:<port>/health`, `UiCommand` → `grimoire.localhost`/`127.0.0.1`) are all inside the allowlist; Ollama/Qdrant endpoints are outbound, unaffected. No legitimate flow sends a non-allowlisted Host. Cli.Tests parity green signal comes via the T4 full-suite window (wave rule — not re-run here), but the callsite reads clean.

## BP checklist
- **BP-32 [S-accepted]** HostFiltering returns a bare 400 with no ProblemDetails envelope, outside `UseStructuredErrors`. Accepted: this is a middleware-boundary refusal before any request/endpoint context exists — same class as Kestrel's own malformed-request 400s. Retrofitting a structured body would mean replacing framework middleware; not worth it, and the audit probe checks status only. No action.
- **BP-60/63** [OK]: xUnit + Shouldly, `Method_Scenario_Expectation` naming, Theory for parameterized cases. **BP-62** [OK]: each test owns its factory (`using var`), no shared mutable state, no order dependence. **BP-61**: red-first recorded with the honest re-recording of the HttpClient-rewrite trap — the raw-socket re-do is exactly what the TDD contract asks for. **BP-93** [OK]: no AI-template smell; the two comment blocks are non-obvious-decision comments (security rationale, harness trap), which is the allowed category.
- Commit identity: `kelthuzad <kelthuzad@summonasync.dev>` ✓ lane alias; `Co-authored-by` trailer matches the repo's main-branch convention.

## Findings

**BLOCKERS** — none.

**MAJORS** — none.

**MINORS** (fix in T0 or fold into T2's test pass — none block the contract as written):
1. `HostGuardTests.cs:12-15` — no suffix/prefix attack cases. This exact class of Host (`127.0.0.1.evil.example`, `localhost.evil.example`) is what a rebinding attempt sends; the framework refuses them but nothing in the suite pins that. Fix: extend the hostile theory with `[InlineData("127.0.0.1.evil.example")]` and `[InlineData("localhost.evil.example")]` — zero new machinery.
2. `HostGuardTests.cs:24-35` — the pre-endpoint proof is sound today (verified by reading: `GET /api/memories` → `MemoryQueryRunner.RunAsync` → `store.ListAsync` at MemoryQueryRunner.cs:59, which invokes `OnListAsync`), but it has no positive control inside the test. A future refactor moving the list path off `ListAsync` turns the `storeListed.ShouldBeFalse()` green-vacuously. Fix: in the same test, after the hostile probe, issue a valid-Host `GET /api/memories` via `factory.NewClient()` and assert `storeListed` is true.
3. `HostGuardTests.cs:53-64` — `RawGetAsync` has no timeout; a regression where the server accepts-but-never-answers hangs the collection until the runner dies instead of failing red. Fix: `using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));` threaded into `ConnectAsync`/`WriteAsync`/`ReadLineAsync`.

**NITS**:
- `HostGuardTests.cs` — `factory.NewClient().BaseAddress!` spins up a full `HttpClient` just to read the bound address and never disposes it (harmless at this scale; a `factory.Address` accessor would be cleaner, but that touches shared harness code — not this task's scope).
- `Encoding.ASCII` in the probe turns exotic Host bytes into `?` — refusal-direction-safe, noted only.

## AC verdict
AC-1 (red recorded) — accepted on consistent executor evidence (not re-runnable read-only). AC-2 ✓ (ran). AC-3 ✓ (ran + diff touches no guard test). AC-4 ✓ (412/412, ran). AC-5 (find_references log) — recorded in the task contract text handed to me; board log entry exists per the task artifact. All five ACs hold. Task T0 may be marked done; the three MINORs are test-hardening, cheapest folded into T2 (same file, same suite) if the marshal prefers.