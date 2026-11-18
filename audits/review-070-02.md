# review-070-02 — T0 Host filtering (architect-review Light)

> Provenance: architect-review sub-agent (qwen3.8-flash, Light mode), dispatched by kelthuzad (lane api-hardening), 2026-11-18.
> Carrier reviewed: 6df65c0 on feat/070-public-security-hardening (base main 052762a).
> Post-review: MINORs from the parallel code pass fixed in 5b752cc; WARNING #1 below is carried into T3's task log as an explicit decision point.
> Report text below is the reviewer's verbatim output.

verdict: PASS

# Architect Review (Light) — Feature 070-public-security-hardening, Task T0 (Host header filtering)

**Carrier:** `6df65c0` on `feat/070-public-security-hardening` (base `main` `052762a`). 2 files, +72 lines: `src/Grimoire.Api/ApiHostBuilder.cs`, `tests/Grimoire.Api.Tests/HostGuardTests.cs`.
**Independent verification:** scoped `dotnet test tests/Grimoire.Api.Tests` — **412/412 passed** (3s). The two CS8604 warnings in the build output (`ApiRouteMatcher.cs:88`, `TomeEndpoints.cs:72`) pre-date this commit and belong to T3's nullability scope — not a T0 finding.

## Q1 — Middleware ordering seam: correct, no double-run, no bypass

`app.UseHostFiltering()` placed immediately after `builder.Build()` and before `app.UseStructuredErrors()` and every `Map*` + `UseSpaFrontend` — right seam. Findings:

- **No double registration.** ASP.NET Core auto-adds host filtering via `HostFilteringStartupFilter`, but `UseHostFiltering` is marker-guarded (`ObjectIdentityMarker` in `app.Properties`) — the explicit call and the implicit startup filter cooperate; whichever runs first registers the middleware, the other is a no-op. No exception path, no duplicate check.
- **No bypass vs. implicit placement.** Even without the explicit call the auto-added filtering would wrap the user pipeline; making it explicit pins the position deterministically instead of relying on default-host magic. Good defensive style.
- **WARNING (forward-looking, T3 scope, not a T0 blocker):** the rejected-host 400 is emitted by HostFiltering *before* `UseStructuredErrors`, so refused requests get Kestrel's bare 400 text, not the structured error envelope. That matches the spec's "refuse with 400" today; when T3 lands the structured catch-all, decide explicitly whether host-refusals should (or deliberately should not) carry the structured shape. Record the decision in T3, don't let it happen by accident.

## Q2 — Layer boundaries & lane contour: clean

- Diff touches only `src/Grimoire.Api` and `tests/Grimoire.Api.Tests` — inside the lane contour (no web/, no Cli, no Core, no README/meta). Verified against `git diff 052762a..6df65c0 --stat`.
- `Microsoft.AspNetCore.HostFiltering` resolves through the existing `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in `Grimoire.Api.csproj` — **zero new NuGet packages**, `Directory.Packages.props` untouched. Complies with the licensing guard.
- Dependency direction intact (`Core ← Api`); no HTTP concern leaked into Core or Infrastructure — the allowlist lives and dies in `ApiHostBuilder`, the host-composition seam where every other pipeline decision already lives. File placement is exactly STRUCTURE.md-conformant. No new project references, no cycles.

## Q3 — Configuration authority: hardcoded DI Configure is the right call

- Consistent with the existing philosophy: `builder.Configuration.Sources.Clear()` makes `ApiHostOptions` the only bind authority; the Host allowlist continues that pattern as a `Configure<HostFilteringOptions>` lambda.
- Ordering verified: options actions apply in registration order, so the lambda runs *after* any config-sourced `HostFilteringSetup` binding and overwrites `AllowedHosts` unconditionally. Even the `DisableConfigSourceClear` escape hatch cannot let an appsettings `AllowedHosts="*"` re-open the hole. The allowlist is unforgivable-by-config, which is exactly what a security boundary should be.
- Judgment against spec: spec asks for a **fixed** three-host allowlist and explicitly no new config surface. Promoting it to an `ApiHostOptions` field would add a knob the spec forbids and weaken the boundary — correctly not done. No over- or under-engineering found. (Port-less entries matching any port is documented ASP.NET behavior and is correctly exploited by the test using `:4770` against an ephemeral bind.)

## Q4 — Test architecture: raw-socket helper placement

- The rationale (HttpClient rewrites an explicit `Host` to the request authority) is real and measured — the raw `TcpClient` probe is the only way to reproduce the audit's curl shape; the assertion that the **store is never touched** on refusal (`storeListed.ShouldBeFalse()`) is the architecturally valuable witness. Good test design.
- **SUGGESTION (recommend, don't mandate):** keep `RawGetAsync` private in `HostGuardTests` for now. T2's Origin defense will need a raw probe too (a realistic rebinding request carries hostile Host *and* Origin together, which HttpClient cannot express), but extracting a shared `tests/Grimoire.Api.Tests` helper at the moment the second consumer appears is one cheap move with no risk of premature abstraction. If kelthuzad prefers to pre-extract during T2, that's equally fine — placement is not a structural violation either way.
- Minor coverage note for the code-reviewer pass, not structure: the negative Theory exercises one hostile shape (`audit-invalid.example`); a suffix-trap case (`localhost.evil.com`) would pin the exact-match semantics. Bare "localhost" doesn't match it in HostFiltering, so it's a test-depth nicety.

## Q5 — Faces parity: confirmed unaffected

- **MCP stdio face:** never constructs `ApiHostBuilder` — untouched by construction.
- **CLI face:** `serve` goes through `ApiHost.RunAsync → ApiHostBuilder.Build` and gains the guard as intended (same hardening, one seam).
- **Test harnesses:** all callers verified — `GrimoireApiFactory`, `LoopbackBindingTests`, `SessionRetentionRegistrationTests`, `SessionsWithoutRegistryTests`, `SpaHostingConsoleFilterTests` and `Cli.Tests/StatsParityContractTests` all bind `Port = 0` through `LoopbackBinding.Endpoint` → `IPAddress.Loopback` only (IPv4), so the advertised address is `http://127.0.0.1:<port>` and every HttpClient sends `Host: 127.0.0.1:<port>` — allowlisted. Empirically green: 412/412 in Api.Tests; the executor's run plus the Cli parity suite riding the same harness shape. No IPv6 `[::1]` bind exists, so no bracketed-host surprise is possible.
- Production browser console (`grimoire.localhost` per the recorded hostname ruling) is in the allowlist — the SPA surface keeps working.

## Summary

| # | Severity | Finding |
|---|----------|---------|
| 1 | WARNING → T3 | Host-refusal 400 bypasses structured errors by position; make the envelope decision explicit when T3 lands |
| 2 | SUGGESTION | Raw-socket helper: extract to shared test infra when T2 becomes the second consumer (not before) |
| 3 | NOTE (test depth) | Add a suffix-trap hostile host case (`localhost.evil.com`) — for the code-reviewer pass to weigh |

No BLOCKERS. Structure, seams, contours and dependency direction all hold; the change is minimal, correctly placed, and config-unforgable by design. T0 structurally sound.