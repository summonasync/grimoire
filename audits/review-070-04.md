# review-070-04 — T1 Origin defense (architect-review Light)

> Provenance: architect-review sub-agent (qwen3.8-flash, Light mode), dispatched by kelthuzad (lane api-hardening), 2026-11-18.
> Carrier reviewed: 7de0a69 on feat/070-public-security-hardening (prev tip fa76dcf).
> Post-review: carried WARNING re-stated into the catch-all task (board T2) log with the OriginGuard precedent wording; the RawHttpProbe arbitrary-headers suggestion pinned there too.
> Report text below is the reviewer's verbatim output.

verdict: PASS

**Architect-review Light — lane api-hardening, feature 070, task T1 (Origin defense on mutations)**
Carrier: `7de0a69` on `feat/070-public-security-hardening` (prev tip `fa76dcf`). Verification run: `dotnet test tests/Grimoire.Api.Tests` — **424/424 passed**, matches the executor's claim (the two CS8604 warnings shown are pre-existing in `ApiRouteMatcher.cs`/`TomeEndpoints.cs`, untouched by this commit).

**1. Seam — middleware between HostFiltering and StructuredErrors: right, with one recorded WARNING**
The composition `UseHostFiltering → UseOriginGuard → UseStructuredErrors` (`src/Grimoire.Api/ApiHostBuilder.cs:95-97`) is correct: both guards refuse *before* the endpoint pipeline, and OriginGuard being our own code can afford to sit inside our own error-shaping reach. The shape choice — OriginGuard writes the structured envelope itself via `ApiResponses.Payload("forbidden_origin", ...)` (`OriginGuard.cs:22-25`) rather than throwing and letting StructuredErrors shape it — is defensible and arguably better: refusal is deterministic regardless of error-middleware ordering, and the envelope *shape* still has one home (`Endpoints/ApiResponses.cs:12`), so there is no second contract invented.
The asymmetry vs HostFiltering's bare 400 is a framework constraint (built-in middleware can't be made to speak our envelope), not an executor whim — but per the T0 handoff the decision must be made **explicit** in the catch-all task.
**WARNING (carried, not new):** `src/Grimoire.Api/ApiHostBuilder.cs` pipeline head. Record in the catch-all task's plan: *"refusals that live before UseStructuredErrors shape their own envelope when they are ours (OriginGuard precedent); the framework's HostFiltering 400 stays bare as an accepted artifact"* — and the forced-exception test must pin which side of the boundary it lands on. Do not silently "unify" the two shapes later.

**2. Placement — matches STRUCTURE.md conventions**
`OriginGuard.cs` as its own file beside `LoopbackGuard.cs` / `StructuredErrors.cs` at the `src/Grimoire.Api/` root follows the established one-cross-cutting-middleware-per-file pattern (root already holds `LoopbackGuard.cs`, `StructuredErrors.cs`, `SpaHosting.cs`, `LoopbackBinding.cs`). `internal static` + `UseOriginGuard` extension mirrors `UseStructuredErrors` exactly. Folding it into StructuredErrors would merge two unrelated responsibilities (tenant origin policy vs error shaping) — correctly avoided. No God-service, no layer leak: reads only `Microsoft.AspNetCore.*`, `Microsoft.Extensions.Primitives`, `System` — nothing from Core contracts beyond the existing `ApiResponses` seam.

**3. Contour + packages — clean**
Diff is exactly 5 files, all inside `src/Grimoire.Api` + `tests/Grimoire.Api.Tests`. No `web/`, no Cli, no Core, no csproj or `Directory.Packages.props` changes. `Microsoft.Extensions.Primitives.StringValues` and `Uri` are in the ASP.NET Core / .NET shared framework — **zero new packages**, licensing guard not triggered.

**4. Test architecture — right shape, no premature abstraction, one forward-looking note**
`RawHttpProbe.cs` extraction is justified the moment a second consumer appeared (OriginGuardTests uses it; HostGuardTests now delegates through a thin `RawGetAsync` wrapper — callers untouched). The shared record (`Status`, `Raw`) keeps the full response text, so body/header assertions are possible without a second accessor. The 5s CTS and `Connection: close` semantics carried over unchanged; the ASCII→UTF8 switch is a superset and is needed for JSON bodies. Body support (method, path, host, optional origin, optional body) covers exactly what a header-regression probe needs — this is the natural generalization of the T0 private helper, not invented infrastructure.
**Suggestion:** the next consumer (catch-all task's forced-exception test) will likely need **arbitrary extra headers** (e.g. a custom header to trigger the exception path) — `SendAsync`'s fixed `origin`/`body` parameters will pinch there. When that task lands, add an optional `IReadOnlyDictionary<string,string>? headers` rather than a new overload-per-header; not worth touching now.
Also style-minor: no trailing newline at EOF in `OriginGuard.cs` and the rewritten tail of `HostGuardTests.cs` — consistent with the repo's existing state, no action.

**5. Faces parity — confirmed, no breakage**
- **MCP face:** `src/Grimoire.Mcp` has no HttpClient (grep-verified) — stdio calls the service layer in-process, never traverses the HTTP pipeline. Unaffected by construction.
- **CLI face:** the two HttpClient consumers (`src/Grimoire.Cli/Commands/UiCommand.cs`, `CliServices.cs`) never set an `Origin` header — `HttpClient` does not send Origin by default, so CLI/native mutations pass the "no Origin stays allowed" branch (`OriginGuard.cs:18`). The `Origin` hits in `ActorResolver.cs` are the tome-key word, not a header.
- **SPA:** browser PATCH from `grimoire.localhost` sends `Origin: http://grimoire.localhost:<port>` — in the allowlist, any port accepted (`LoopbackHosts` + scheme/host check, port deliberately not constrained). Pinned by the `[InlineData("http://grimoire.localhost:4770")]` case.
- **`tests/Grimoire.Cli.Tests/StatsParityContractTests.cs`:** hosts the API in-process and drives GET reads over HttpClient — reads are untouched by the guard and no Origin is sent; green path unaffected.
- Adversarial cases are fail-closed and tested: `null` Origin (opaque/file origins) fails `Uri.TryCreate`, `https://localhost` refused (TLS is out of the loopback contract), `localhost.evil.example` refused via exact-host match, multi-value refused via `Count == 1`.

**Bottom line:** structure intact on all five questions; one carried WARNING that is a *documentation duty for the catch-all task*, not a defect in T1. No blockers, no rework.