# Release 0.9.1 (public channel) — 2026-11-19

Feature: `067-distribution-first-run`. Branch `feat/067-distribution-first-run`,
final tree `232ba90` (main `dcb009b` merged as `9ee171e`). Carriers: `55b6e0b`
(pack pipeline), `2554ce3` + `da50784` (install scripts + URL-leg fix),
`89a8d83` (guided init), `232ba90` (README binary channel). This record is the
public-release verification receipt; the patch-release record remains
`audits/release-0.9.1-2026-11-16.md`.

## Carriers (re-packed on the final tree 232ba90)

| Archive | Bytes | sha256 |
|---|---|---|
| grimoire-0.9.1-linux-x64.tar.gz | 48827383 | 0f6f4ded08e8ea2d9569a77ad7c117580e370a0dd57c9b92bcba3e438559084c |
| grimoire-0.9.1-osx-arm64.tar.gz | 46366648 | 132337642bc1eb4a0f1636fc34a4ce21a40d48a20e4b67d8e6664b977f9380e4 |
| grimoire-0.9.1-win-x64.zip | 49112665 | e6016fd704968dfff0d378f0b9fc5c690a388f8bf503a9522c60fe5402cdad81 |

Each archive carries the self-contained single-file binary, the stamped SPA
export (`web/`) and an internal `SHA256SUMS`. Deployed SPA stamp inside all
three archives: `productVersion 0.9.1`, `webSha 232ba90` (read back from the
archive members, not from the build tree). `sha256sum -c SHA256SUMS --quiet`
green inside the extracted linux-x64 tree; the first pack (yesterday, stamp
`20000b6`) was superseded by this re-pack so the stamp names the final tree.
Pack refusal guard proven: stale stamp + `PACK_SKIP_WEB_BUILD=1` → exit 1
«refusing to pack: SPA stamp 0.0.0 != assembly version 0.9.1».

## Gates (all measured on 232ba90, proxy-stripped env, marshal R-window)

- `dotnet test Grimoire.sln -c Release -m:1` plain: **2620 passed / 0 failed / 0 skipped**, exit 0
  (Core 861, Infrastructure 368, Mcp 519, Api 431, Cli 441). Log: dist/suite-plain.log.
- Same under `TZ=UTC`: **2620 / 0 / 0**, exit 0. Log: dist/suite-utc.log.
- `dotnet format Grimoire.sln --verify-no-changes`: exit 0. Log: dist/format.log.
- web: `npm run lint` exit 0, `npx tsc --noEmit` exit 0, `npm run build` exit 0
  (stamp re-emitted 0.9.1 @ 232ba90). Logs: dist/web-lint.log, dist/web-tsc.log, dist/web-build.log.
- Environment note: the process env carries dead proxies (127.0.0.1:2080/2081);
  every gate above ran with HTTP(S)_PROXY/ALL_PROXY (both cases) stripped. With
  them present, three in-proc HttpClient tests fail with connection refused —
  environmental, proven by the stripped rerun (marshal root-cause broadcast).

## Install smoke (T2, isolated HOME, no SDK on PATH, no repo clone)

- `install.sh` from local archive and from a loopback HTTP URL: binary into
  `~/.local/bin`, stamped export into `~/.grimoire/web`; second run reports
  «grimoire unchanged»; no sudo; PATH note honest.
- `install.ps1` under pwsh with explicit -BinDir/-WebRoot: install + unchanged
  second run (Windows-target script; names the binary grimoire.exe by design).
- `grimoire --version` → `grimoire 0.9.1` with `env -i PATH=<smoke bin>:/usr/bin:/bin`.
- serve on 4799 against a dedicated scratch Qdrant (6363/6364, deleted in-slice):
  `/` 200, `/grimoire-web.json` = 0.9.1, `/health` degraded with honest reason
  (store reachable, embeddings 404 on the live Ollama which lacks the model —
  environment observation, not a distribution defect).
- MCP `initialize` over stdio: full JSON-RPC result, serverInfo
  grimoire / Grimoire Memory / 0.9.1 (receipt dist/mcp-init-stdout.jsonl; the
  probe must hold stdin open — closing it lets the server exit before flush).
- All scratch homes and the scratch container deleted in the slice that wrote them.

## First-run model story (T3, owner fork F3=A)

`grimoire init` probes Ollama for the configured model after bootstrap: missing
model → exact `ollama pull <model>` remedy + exit 4 (Dependency); unreachable
Ollama → distinct message + exit 6 (UpstreamUnavailable); other probe response
errors → honest message, no pull remedy; present → proceeds unchanged. Four
named tests pin the legs; hard review PASS (audits/review-067-03.md, audit
entry 7851e784), follow-up idea 15b07142 (status code on the exception).

## Release execution — PREPARED, NOT EXECUTED (owner's doors)

```bash
# after the 067 PR merges; tag targets the merge commit
gh release create v0.9.1 \
  dist/grimoire-0.9.1-linux-x64.tar.gz \
  dist/grimoire-0.9.1-osx-arm64.tar.gz \
  dist/grimoire-0.9.1-win-x64.zip \
  scripts/install.sh scripts/install.ps1 \
  --target <merge-commit-sha> \
  --title "Grimoire 0.9.1 — first public release" \
  --notes-file audits/release-notes-0.9.1.md
```

Visibility flip (`gh repo edit summonasync/grimoire --visibility public`) stays
the owner's separate go after this release exists. README quickstart links
(`releases/latest/download/...`) resolve only once the assets above are
uploaded — create the release before or together with the flip.

## Rollback

Archives are additive artifacts; rollback = install the previous archive
(0.9.0 tool channel remains available via the local folder feed). No stored
data, schema or config shape changed by 067; the init probe adds no writes.

## Addendum 2026-11-20

- 067 landed as PR #42, merge `e629943` (owner GitHub merge). The archive stamps
  name `232ba90`, the tree the SPA export was built from; it is an ancestor of
  the merge, and the merge added no web/ content, so the stamps stay truthful.
  Tag target for `gh release create v0.9.1` is `297ec67` (main after PR #43;
  supersedes the `e629943` line written before the propagation landed).
- License is Apache-2.0 as of PR #41 (owner ruling 2026-11-20, board decision
  9e7f8188); the MIT wording in the 0.1.0–0.9.1 history stays as shipped.

## Execution 2026-11-20 — RELEASE PUBLISHED

`gh release create` was not needed: a predecessor session had left the release
as a DRAFT (assets: install scripts only; target stale `e629943`; body pre-
license-propagation). medivh-grim completed it on the owner's word:

- Repacked all three archives from main `a71be0f` (PR #45 / 069 web fixes must
  ride the console export — the draft-era archives carried the pre-069 export;
  stamps read back from all three archives: `0.9.1 @ a71be0f`).
- Retargeted the draft to `a71be0f` (full SHA required — short shas are refused
  by the API), replaced the body with `audits/release-notes-0.9.1.md` including
  the Apache-2.0 line and the NEW archive digests, uploaded the three archives
  (GitHub asset digests matched local sha256 byte-for-byte; round-trip download
  verified 2/2).
- Published 2026-11-20 14:04:51Z: https://github.com/summonasync/grimoire/releases/tag/v0.9.1
  — tag `v0.9.1` points at `a71be0fe84b9882ef5d68a23206231d8cb4241a1`.

Final asset digests (sha256):

```
f847b0b19ce9feb099d5d8cba8e602ac9a99f513b7cc125e3ecf9702ef0e7585  grimoire-0.9.1-linux-x64.tar.gz
e19e4248cdae75c5d8d35c1fe164f24565fdae67a86b9abe59123cce8c84b22c  grimoire-0.9.1-osx-arm64.tar.gz
6be2f058e083dc73c344b66ec21b3137df3902c4c227580969fef65fbf307174  grimoire-0.9.1-win-x64.zip
16d03e48492be7da0dcf5d6e56e3390f506e9551ed87a9b6793e9d11a89f6a1d  install.sh
317134cc754e03bacf59c7473f7c7053a571de43f44653b90d9481da1c943810  install.ps1
```

The `## Carriers` table above (232ba90-era hashes) is superseded by this
section. Upload legs stalled under the dead process proxies until
HTTP(S)_PROXY/ALL_PROXY were stripped — same environment trap as the gates.

## Left open

- idea 15b07142: HTTP status on EmbeddingResponseException + /api/tags probe.
- Repo visibility flip + social preview upload: owner's doors, untouched.
