# review-070-05 — T2 structured catch-all + nullability (light)

> Provenance: dotnet-code-reviewer sub-agent (qwen3.8-flash, LIGHT pass), dispatched by kelthuzad (lane api-hardening), 2026-11-18.
> Carrier reviewed: e269138 on feat/070-public-security-hardening (diff b0a27ba..e269138).
> Post-review: WARNINGS W1 (client-abort swallowing) + W2 (HasStarted guard) fixed in-slice by kelthuzad, commit 4f8b75e — abort rethrow clause + when(!Response.HasStarted) on all three catches; build 0 warnings, Api.Tests 431/431. Clauses reading-verified: the harness has no deterministic path for a mid-response abort or a post-start throw. MINOR (log-side correlation capture) parked as a board idea — needs a host-logging seam in GrimoireApiFactory.
> Report text below is the reviewer's verbatim output.

verdict: PASS

**Lane api-hardening · feature 070 · T2 (structured catch-all + nullability hygiene) · review LIGHT · carrier e269138 (diff b0a27ba..e269138, 4 files +57/-5)**

## Verification method
- **Verified by running** (on the worktree tip = e269138):
 - `dotnet build src/Grimoire.Api --no-incremental` → **0 Warning(s), 0 Error(s)** (AC2 confirmed — the was-2-warnings claim holds, no hidden production warnings behind incrementality).
 - `dotnet test tests/Grimoire.Api.Tests` → **431/431 passed, exit 0** (AC4 confirmed; degradation 503/409 tests included green in that denominator, AC3 confirmed).
- **Verified by reading**: full diff; pipeline order in `ApiHostBuilder.cs:95-97` (`UseHostFiltering → UseOriginGuard → UseStructuredErrors`); `ApiResponses.Payload` shape; `FakeEmbeddingProvider.cs:19` (throws the configured `Failure`, so the forced-exception test genuinely drives the catch path through the real PATCH → embed flow); `CreateTomeManifestRequest.Key` is `string?` (so the TomeParam widening is the true source of the old CS8604 at TomeEndpoints); `Post_Create_WithAMissingKeyProperty` at TomeEndpointsTests.cs:665 pins the null-branch message; `SpaHosting.cs:36` logger-resolution precedent; `OllamaEmbeddingProvider.cs:29` cancellation-filter precedent.

## AC decomposition
1. Forced-exception test → structured 500, `code=internal_error`, generic message + `Correlation id`, leak assertions meaningful (`ShouldNotContain` covers exception message text, type name, stack markers — would catch a developer-exception page; the fake throws `InvalidOperationException` with a distinctive payload string, so the denominator is real). TICKED, holds.
2. Api build 0 warnings. TICKED, reproduced.
3. Degradation green. TICKED via full project run; the 503/409 catch blocks are byte-identical in the diff (pure addition of a last catch).
4. Api.Tests green. TICKED, reproduced.

## Envelope decision compliance
Honored. The refusals (HostFiltering bare 400, OriginGuard) run *above* `UseStructuredErrors` in the pipeline, so they keep shaping their own envelopes and the catch-all only reaches endpoint-path exceptions — confirmed structurally at ApiHostBuilder.cs:95-97, not just asserted. The catch is correctly LAST (after 503/409), so graceful-degradation shapes are untouched and the new `internal_error` code is additive to the schema, not a change.

## Findings

### BLOCKERS
None.

### MAJORS
None.

### WARNINGS
- **BP-41/BP-32 [W]** — `src/Grimoire.Api/StructuredErrors.cs:52` — **catch-all swallows client aborts.** `await next(context)` can surface `OperationCanceledException`/`TaskCanceledException` bound to `RequestAborted`: endpoints bind `CancellationToken` (TomeEndpoints.cs:65 et al.) and the infra deliberately propagates true client cancellation (`OllamaEmbeddingProvider.cs:29` converts only non-request cancellations to 503; real aborts travel). Today each disconnect is logged at Error and answered with a pointless 500 attempt on a dead connection. Fix — insert before the generic catch:
 ```csharp
 catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
 {
 throw; // client aborted; Kestrel owns the shutdown
 }
 ```
- **BP-32 [W]** — `src/Grimoire.Api/StructuredErrors.cs:52-63` — **no `HasStarted` guard on the catch paths.** The 400 path guards it (line 27); the generic catch does not. An exception thrown *after* the response started (result serialization inside `Results.ExecuteAsync`, or a static-file failure inside the SPA leg — all of it runs under `next`) makes `WriteAsync` append the error JSON to an already-committed body (status-code set is a no-op post-start), producing a corrupted response where Kestrel would previously have honestly aborted the connection. Before this task the gap was theoretical (503/409 only throw pre-start); the catch-all makes it reachable. Fix:
 ```csharp
 catch (Exception ex) when (!context.Response.HasStarted)
 ```
 (let started-response exceptions fall through to the server). The same clause pair also covers the 503/409 blocks' latent gap without changing their behavior.
 Neither warning contradicts the task's stated goal or the envelope decision; both are hardening refinements of the new catch — candidate for a follow-up task card, not a rework round.

### MINORS
- **AC1 coverage gap** — `tests/Grimoire.Api.Tests/StructuredErrorsTests.cs` — the test proves the correlation id lands in the *body*, but nothing asserts the exception was *logged* with it (the "correlation logged" half of the goal is untested). An `ILogger` test-double capture would close it. Not blocking: the logging line is plainly correct by reading.

### NITS
- `ApiRouteMatcher.cs:88` `reference.Content!` — accepted as honest: the framework's `ParameterPolicyReference` contract is content-or-policy, never both null, so the null-forgive sits on a provable invariant, and the comment clause states it. A `when (reference.Content is { } text)` pattern would silence the warning without `!`, but the chosen form matches the surrounding style. No action.

## Nullability fixes honesty
- `TomeParam.cs` — real fix, not cosmetic: `Key` is `string?` at the boundary, the widened signature moves the null handling to the message producer, and the new null branch ("...is required and must be...") replaces what was previously an interpolation of `null` into quotes; existing pinned test exercises exactly this branch (passes).
- No other production nullability warnings — the 0-warning figure was reproduced with `--no-incremental` on the Api project (the known 8 test-project warnings are T-scope-external by the AC's wording: production only).

## BP quick pass
BP-32 ✓ (uniform structured shape, no raw exception text to client — the two W findings are about abort handling, not leakage) · BP-41 ✓ (catch is never empty; exception flows to log) · BP-50 ✓ (structured template `{Method} {Path} {Correlation}`, no interpolation in templates; the client message's interpolation is a payload, not a log) · BP-51 ✓ (log carries method/path/correlation + exception — local store, no credentials; TraceIdentifier in body is a random per-request id, safe) · BP-93 ✓ (no restated-code comments; the two comments explain non-obvious decisions — correlation join key, framework contract).

## Anti-scope check
No Host/Origin files touched, zero new packages (only `Microsoft.Extensions.DependencyInjection`/`Logging` usings, already project-referenced), payload schema unchanged (additive code value only). Constraint on `find_references` widening (`InvalidMessage`) is source-compatible: all 17 callsites pass positional args.