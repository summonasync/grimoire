# Troubleshooting

Every entry below names the behavior as the code actually produces it — the
message shapes and exit codes come from `src/Grimoire.Cli` and
`src/Grimoire.Api`, not from folklore.

## `grimoire init` exits 4: embedding model missing

Symptom:

```
Embedding model 'qwen3-embedding' is not present in Ollama.
Pull it first: ollama pull qwen3-embedding
```

The model named in your config (`embedding.model`, default
`qwen3-embedding`) is not pulled in the Ollama instance Grimoire talks to.
Run the printed command. This is deliberate: init refuses to bootstrap a
store whose first write would die silently later.

## `grimoire init` exits 6: Ollama unreachable

Symptom: `Ollama is not reachable at <endpoint>: <reason>`.

Ollama is down or the endpoint is wrong. Start it (`ollama serve`, or the
compose stack) or point `embedding.endpoint` at the right instance
(default `http://localhost:11434`). Note the difference from the case above:
unreachable gets no `ollama pull` remedy — the server itself is not answering.

## serve does not start at all

`grimoire serve` (and `grimoire init`) touch Qdrant at startup — the
collection is bootstrapped before the host opens. If Qdrant is down or
unreachable, startup fails there; that is the dependency, not a Grimoire bug.
Check `qdrant.endpoint` (default `http://localhost:6334` — gRPC port) and
that the store is up (`curl http://localhost:6333/readyz` on the compose
stack).

## The console says `degraded`

`/health` answers honestly with per-component verdicts:

```json
{"status":"degraded","store":{"reachable":true,"error":null},
 "embeddings":{"reachable":false,"error":"Ollama returned HTTP 404 for model 'qwen3-embedding'."}}
```

`store.reachable:false` — Qdrant problem. `embeddings.reachable:false` with a
404 — model not pulled; with a connection error — Ollama down. The memory
surfaces degrade read-wise rather than pretending: reads return what they can,
with the failure named.

## Port conflict

The console binds `127.0.0.1:4770` by default. Another listener on the port
fails startup — set `serve.port` in `~/.grimoire/config.json` (see
`docs/configuration.md`). `grimoire ui` probes the configured port, so the
pair stays consistent.

## "Deployed SPA export reports version X while this binary is Y"

The console files under `~/.grimoire/web` were built for a different release
than the binary serving them — a skipped deploy, not corruption. `serve`
prints the warning at boot and still serves; rebuild the export and redeploy
(`cd web && npm run build`, then copy `web/out/` into the web root), or just
re-run the install script from a current archive — it replaces the export and
the stamp together. When the stamp file is missing entirely the warning says
the version is unknown and never claims stale — there was nothing to measure.

## `grimoire ui` exits 6: console unreachable

The health probe got no answer within the budget. On Linux the command has
already tried `systemctl --user start grimoire-serve` once; the remedy line
it prints names what failed (the unit start exited non-zero, systemctl is not
available, or the wait expired). On macOS/Windows start `grimoire serve`
yourself first — the auto-start step is a systemd mechanism.

## Corporate proxies eating loopback

If HTTP calls to `127.0.0.1` fail with `Connection refused (127.0.0.1:<proxy
port>)`, your environment is routing loopback through a dead proxy: unset or
fix `HTTP_PROXY` / `HTTPS_PROXY` / `ALL_PROXY` (both cases) and make sure
`NO_PROXY` covers `localhost,127.0.0.1`. Observed and measured during the
0.9.1 release gates (`audits/release-0.9.1-public-2026-11-19.md`).

## Where to look next

- `docs/configuration.md` — every knob and its default.
- `docs/platform-support.md` — what is platform-dependent and what is not.
- `SECURITY.md` — the loopback boundary and what counts as a vulnerability.
