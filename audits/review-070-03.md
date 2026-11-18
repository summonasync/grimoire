# review-070-03 — T1 Origin defense on mutations (hard, Security elevated)

> Provenance: dotnet-code-reviewer sub-agent (qwen3.8-flash), dispatched by kelthuzad (lane api-hardening), 2026-11-18.
> Carrier reviewed: 7de0a69 on feat/070-public-security-hardening (prev tip fa76dcf).
> Post-review: MINORS M1/M2 and nits S1/S2/S4 fixed in-slice by kelthuzad, commit 6976cf4 (Api.Tests 430/430 green). S3 optional, skipped. S5 (commit trailer policy) escalated to the wave/owner — carriers are board-cited, no history rewrite without approval.
> Report text below is the reviewer's verbatim output.

verdict: PASS

# Review 070-03 — T1 Origin defense on mutations (hard)

Carrier: `7de0a69` on `feat/070-public-security-hardening` (5 files, +188/-17). Task artifact `de706b05` read: all 5 AC ticked, red/green evidence logged, find_references recorded.

## Verified by running
- `dotnet test tests/Grimoire.Api.Tests` → **424 passed / 0 failed, exit 0** — executor's green claim reproduced.
- `dotnet test --filter OriginGuardTests` → **10/10** (4 refusal theory + no-Origin + GET + 4 loopback-accepted).
- Red evidence not re-runnable (guard exists, read-only review); log story is arithmetically coherent (4+10+4=18 in the filtered pair, 4 red = exactly the four evil-origin theories) and consistent with the T0 harness trap that was carried forward.

## Verified by reading
- Guard logic `src/Grimoire.Api/OriginGuard.cs`; wiring `ApiHostBuilder.cs:95-97` (`UseHostFiltering → UseOriginGuard → UseStructuredErrors`, before all `Map*` and the SPA fallback — no endpoint bypasses it; a route-mismatching lowercase `patch` 404s, never reaching a handler).
- Immutability witness non-vacuous: PATCH success path calls `store.GetAsync` (`MemoryEndpoints.cs:170`) and the fake increments (`FakeMemoryStore.cs:65`), so `GetCount == 0` on refusal genuinely proves pre-endpoint rejection; `Seed` writes bypass the counters, and `GrimoireApiFactory` carries a fresh fake per test.
- Error shape BP-32: guard's refusal body (`WriteAsJsonAsync(ApiResponses.Payload(...), RequestAborted)`) is byte-for-byte the same envelope the `StructuredErrors` middleware itself writes (`StructuredErrors.cs:70-73`), and unlike its catch-based neighbors it needs no wrapper — self-contained short-circuit is consistent with the contract.
- `RawHttpProbe.cs`: Content-Length computed on UTF-8 bytes (correct), status-line parse is fine for Kestrel's output, `ReadToEnd` gated by `Connection: close` + 5s cts — sound. `HostGuardTests` refactor delegates without behavior change (8 green unchanged, also inside my re-run).

## Hostile-shape adjudication (scope item 1)
All refused fail-closed: `http://evil.example`, suffix trap `localhost.evil.example` (exact `Uri.Host` match), `https://localhost` (http-only allowlist per spec), browser `null` (`Uri.TryCreate` fails), multi-value Origin (`Count == 1` required), userinfo `http://localhost@evil.example` (host = evil.example), percent-encoded hosts (no canonical match), trailing-dot `localhost.` (no match — fail-closed; only breaks a page served from a literal dotted host, which the T0 Host filter already 400s), `http://[::1]` (403 — correct: spec excludes it and `LoopbackBinding` is IPv4-only; the Host filter would refuse the connection anyway). Accepted-by-design: uppercase `HTTP://LOCALHOST` (Uri canonicalizes scheme/host — origins are case-insensitive per fetch spec). Two false-PASS shapes exist (`http://localhost\@evil.example` → Uri treats `\` as a path separator → host = localhost; and an Origin with a path) — adjudicated below as acceptable under the threat model, see S1.

## BLOCKERS
None.

## MAJORS
None.

## MINORS

**M1 [W] BP-60/BP-62 — OriginGuardTests.cs — method coverage is single-point.** The guard defends POST/PUT/PATCH/DELETE (`OriginGuard.cs:36-37`); every test exercises PATCH only. The seam is one predicate, so per-method mistake risk is low, but a future refactor dropping, say, `IsDelete` from the predicate ships fully green. One-line fix — add the method dimension to the refusal theory:

```diff
- [InlineData("http://evil.example")]
+ [InlineData("http://evil.example", "PATCH")]
+ [InlineData("http://evil.example", "POST")]
+ [InlineData("http://evil.example", "PUT")]
+ [InlineData("http://evil.example", "DELETE")]
 [InlineData("http://localhost.evil.example")]
 ...
- public async Task Patch_WithNonLoopbackOrigin_RefusedAndRecordUnchanged(string origin)
+ public async Task Mutation_WithNonLoopbackOrigin_RefusedAndRecordUnchanged(string origin)
```
(the POST/PUT/DELETE cases can assert 403 + counters only; record-equality machinery stays on PATCH).

**M2 [W] BP-60 — OriginGuard.cs:20,26 — two fail-closed policy decisions are prose-only, unpinned by tests.** Multi-value Origin and the empty-`Origin:`-header behavior are exactly the shapes a later "cleanup" will silently invert. Multi-value needs a probe tweak (`string? origin` → `string[]? origins`, appending one header line each); empty-origin then becomes `[InlineData("")]`. Cheap insurance for a security guard.

## NITS / SUGGESTIONS

**S1 [S] OriginGuard.cs:29-33 — Uri's lenient authority parsing accepts the backslash/path shapes above.** Security-irrelevant today: browsers serialize origins canonically (scheme://host:port, no path/backslash), so a false-PASS value can only arrive from a hand-built client — which is already allowed to omit Origin entirely by design (CLI/native). Not worth a custom parser; worth one comment line or one test documenting the adjudication so the next reader doesn't re-litigate it.

**S2 [S] OriginGuardTests.cs:52 — make the pre-endpoint witness self-proving.** `Patch_WithoutOrigin_Succeeds` asserts the edit landed but not `GetCount > 0`; adding `factory.Store.GetCount.ShouldBeGreaterThan(0);` there converts `GetCount == 0` in the refusal tests from reading-verified (I did verify the code path) into test-verified non-vacuity.

**S3 [S] OriginGuard.cs:19-20 — double Count handling:** caller checks `origin.Count > 0`, callee re-checks `Count == 1`. Works; folding into one `ShouldRefuse(StringValues)` predicate would read cleaner. Optional.

**S4 [S] HostGuardTests.cs:57 — refactor dropped the trailing newline** (`\ No newline at end of file`).

**S5 [Info, lane-level, not T1's fault] commit trailer `Co-authored-by: Qwen-Coder <qwen-coder@alibabacloud.com>`** — an AI-trace in history of a feature whose whole purpose is public release. Every commit on this branch (including T0's, already reviewed PASS) uses the same trailer, so it's a lane convention, not a T1 slip — flagging for the parent/owner: decide the trailer policy before this branch faces GitHub.

## Scope adjudications requested
- **OPTIONS/HEAD (item 2):** correctly out — non-mutating; no CORS middleware exists, preflight OPTIONS dies at routing, and browsers never attach Origin to simple GET/HEAD. No hole.
- **Bypass paths (item 3):** guard sits before routing and the SPA fallback — every request crosses it; the only way around is a method that both mutates state and isn't in the predicate — none exists in the endpoint surface.
- **Vacuous-green risk (item 5):** three positive controls present (no-Origin 200, GET-evil 200, loopback-with-port 200) — the guard is proven selective, not blanket.

## Spec compliance
Goal/scope/anti-scope all honored: no auth, Host logic untouched (one-line wiring), no catch-all, no web/, no new packages, 403 before any store write, CLI path intact, TDD red-first recorded, lane-alias commit identity.

**Verdict PASS** — M1/M2 are fix-on-T4-or-follow-up candidates, not gates on this task. Task may go done.