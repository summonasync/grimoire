# Release 0.2.0 — 2026-10-21

Feature: `022-single-payload-mcp-result` (Overmind board, Grimoire project).
Source commit: `2a739af` — `feat(mcp): one payload block per tool result, and Cyrillic that
costs what it is`. Annotated tag `v0.2.0` points at that commit, not at this docs commit.
Built tree: clean except `README.md` (018's test-count line, deliberately not staged here).

## What shipped

Two token-cost defects in every MCP tool result, both measured before either was touched.

1. **The payload was sent twice.** Every tool result carried its JSON once as a text block and
   again as `structuredContent`. The spec says a server that also returns structured content
   SHOULD fold it into the text block — this server had no output schema, so `structuredContent`
   was pure duplicate weight on every memory call, in every agent session. It is gone; the text
   block is now the single carrier.
2. **Cyrillic cost six characters a letter.** The response serializer inherited the SDK's
   HTML-safe encoder, which escapes every non-ASCII character. One crew memory record — 587
   body characters — arrived as 352 `\uXXXX` sequences, 64% of the payload the model reads.
   Both call sites now serialize through a relaxed encoder clone. Quotes, backslashes and
   control characters are still escaped by the JSON writer itself, so the payload stays
   well-formed; the change only stops escaping characters that never needed it.

## Measured deltas

Duplicate payload, raw JSON-RPC frames on stdio (`audits/022-single-payload-stdio-baseline.md`,
harness `audits/022-stdio-harness.py`):

| call | frame before | frame after | delta | saved |
|---|---|---|---|---|
| success, first write | 838 | 530 | −308 | 36.8 % |
| success, idempotent replay | 836 | 529 | −307 | 36.7 % |
| error (param_conflict) | 543 | 342 | −201 | 37.0 % |

Escape bloat, same record read twice (denominator printed with every number — one record,
bodyChars 587; a count without a denominator is not a measurement):

| | raw frame | decoded text block | `\uXXXX` in block | literal non-ASCII |
|---|---|---|---|---|
| before | 3504 | 2759 | 352 | 0 |
| after | 3152 | 999 | 0 | 350 |

2759 − 999 = 1760 = 352 × 5, exact. The raw frame moves less than the payload because the SDK's
JSON-RPC envelope re-escapes what our serializer now emits literally; that layer is consumed by
the client's own decode and is out of scope for this release. **What the model reads is the
decoded block: 2759 → 999 characters for the same record.**

## Verification chain

- Attribution ran before any code change: the escaping survived one JSON decode, so it was baked
  in by our `SerializeToElement`, not by the envelope. Scratch probe: 1331 chars / 179 sequences /
  zero non-ASCII, unchanged after the decode that mimics the client's.
- Guard tests pin the encoder per call site with three independent red legs: whole-options revert
  (Failed 2), `Success`-site only (Failed 1, `Failure` green), `Failure`-site only (Failed 1,
  `Success` green). Each site is pinned by its own test; reintroducing the default encoder
  anywhere in the response path fails CI.
- Denominators are asserted inside the tests (list `count == 1`, sample and block both >9
  non-ASCII), so an empty probe cannot go green.
- Hard reviews, all PASS: `audits/review-022-01.md` (payload fix), `audits/review-022-02.md`
  (regression lock), `audits/review-022-03.md` (encoder).
- Release config, from the shipped commit: build 0 errors, 2 warnings — both pre-existing and
  outside this feature's diff (`ScryToolTests.cs:60` CS8604, `ToolSnapshotJson.cs:51` CS8602).
  Tests: Core 208 + Infrastructure 134 + Cli 167 + Api 135 + Mcp 174 = **818 passed, 0 failed**.
- Frozen MCP surface intact: `git status --porcelain` on the tool-snapshot TestData is empty, all
  11 snapshot files byte-unchanged, the surface contract test green.

## Carrier swap

The fix only reaches running agents once the installed self-contained binary changes — that file
is what every client spawns, and it had been v0.1.0 since 2026-10-20.

| | sha256 | size |
|---|---|---|
| pre-image (v0.1.0, kept at `~/.grimoire/pre-release-0.2.0/grimoire`) | `f4252dbed40d26e12fa812a92c0fbc8eb8b2c7bf89bd95963b0ce0c2474b92f4` | 53 263 856 |
| installed carrier (v0.2.0, `~/.local/bin/grimoire`) | `66fdddb207780867ece12e091d5f133c2e1b4bf4a32dc5603d762becad0884f9` | 53 263 972 |

Embedded provenance stamp: `0.2.0+2a739afa47cdc0b489b3cda67dc72e169069fb3b` — the shipped
artifact names the commit whose tree contains both fixes.

Install mechanics, because the naive path fails: the carrier is a busy executable (the serve unit
and live MCP children are executing it), so a direct write returns `ETXTBSY`, and the repo tree
sits on a different device, so a rename straight out of `bin/` degrades to a copy. The swap ran as
`cp` to a sibling temp inside `~/.local/bin`, then `mv -f` onto the carrier path — rename is atomic
and never opens the target for writing.

Restarted unit `grimoire-serve`: pid 8275 → **1812935**, `active`, listening on `127.0.0.1:4770`
only (loopback boundary unchanged). The unit needs a few seconds to bind — an immediate probe
returns empty, which is startup latency, not a failed face.

Proof on the installed binary, read-only over real stdio:

- `initialize` → `serverInfo` `grimoire` / `Grimoire Memory` / **0.2.0**
- `GET /api/version` → `{"version":"0.2.0"}` (agreement between these two is the cheapest proof
  the unit reloaded the file rather than surviving on a cached process)
- `tools/list` → 11 tools
- `memory_list projectId=Squeezr` → **1 content block**, `"structuredContent"` absent from the
  frame, raw 3152 / decoded 999, **0** escape sequences, 350 literal non-ASCII,
  denominator **records=1, bodyChars=587**
- `memory_stats` → 1 block, no `structuredContent`, 0 sequences
- error path (`param_conflict`) → `isError=true`, 1 block, no `structuredContent`, 0 sequences,
  payload fields `["error"]` — the error envelope keeps the same single-carrier rule

## Reconnect caveat, honestly

Replacing the carrier does **not** retroactively fix sessions that are already running. A live MCP
client holds its own spawned server child, and that child stays on the old inode until the session
reconnects. The saving lands on the next reconnect, not at install time.

Delivery surface, read from the registration files on disk: **Qwen Code**
(`~/.qwen/settings.json`, actor `tucker+qwen`) and **Claude Code** (`~/.claude.json`, actor
`guldan+claude`) — both point at `/home/johan-boolman/.local/bin/grimoire mcp` with only the two
reserved env keys (`GRIMOIRE_TOME=crew`, `GRIMOIRE_ACTOR=…`). No codex or kimi entry exists on this
machine, so no claim is made about them. Reconnect by restarting those clients; the registrations
themselves needed no edit and none was made.

## Rollback

One command pair, because the pre-image is byte-preserved outside the repo:

```
cp ~/.grimoire/pre-release-0.2.0/grimoire ~/.local/bin/.grimoire.rollback && \
chmod +x ~/.local/bin/.grimoire.rollback && \
mv -f ~/.local/bin/.grimoire.rollback ~/.local/bin/grimoire && \
systemctl --user restart grimoire-serve
```

Rollback restores the binary only. The two fixes touch no stored data, no schema and no config, so
there is no migration to unwind — the Qdrant payload contract is additive and was not changed.

## Left open by this release

- `README.md` still claims 814 tests; the suite is 818. That line belongs to feature
  `018-readme-test-count`, which must re-run against the new denominator — it is not a 022 path and
  was deliberately kept out of the release commit.
- The SDK's JSON-RPC envelope still escapes non-ASCII on the wire (416 sequences in the after
  frame). It is undone by the client's decode and never reaches the model, so it is a wire-size
  concern, not a token concern — and it would need reflection into the SDK's frozen options, which
  the feature's anti-scope excludes on purpose.
- Grimoire gained no `--version` flag; the CLI answers a usage error and prints the banner. The
  version truth lives in `serverInfo` and `/api/version`.
