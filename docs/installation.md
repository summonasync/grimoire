# Installation

Grimoire ships as self-contained release binaries — no .NET SDK, no repo clone
needed. Qdrant and Ollama must be running on loopback; everything else is one
archive and one script.

## The release archive

Each platform archive (from the GitHub Releases page) contains:

- the `grimoire` binary (single file, self-contained),
- the `web/` static console export with its stamp (`web/grimoire-web.json`),
- `SHA256SUMS` covering both, verifiable with `sha256sum -c SHA256SUMS`
  from inside the extracted tree.

| Platform | Archive |
|---|---|
| Linux x86-64 | `grimoire-<version>-linux-x64.tar.gz` |
| macOS Apple silicon | `grimoire-<version>-osx-arm64.tar.gz` |
| Windows x86-64 | `grimoire-<version>-win-x64.zip` |

## Linux / macOS — install.sh

```bash
bash install.sh grimoire-<version>-linux-x64.tar.gz
```

`install.sh` accepts an archive path or a URL (it downloads with `curl -fL`).
Options: `--bin-dir DIR` (default `~/.local/bin`) and `--web-root DIR`
(default `~/.grimoire/web`). What it does:

1. extracts the archive and checks it carries the binary, the web export and
   the stamp (`scripts/install.sh` refuses archives missing any of them);
2. compares the binary hash and the stamp against what is already installed —
   if both match, it prints `grimoire unchanged` and exits 0, so re-running is
   free;
3. otherwise installs the binary mode 0755 into the bin dir and replaces the
   web export wholesale in the web root;
4. prints a PATH note when the bin dir is not on `PATH`.

No sudo, no system directories: everything lands under your home.

## Windows — install.ps1

Requires PowerShell 7 (`pwsh`); the script uses `Expand-Archive` and
`Get-FileHash` with the same idempotence rule as the shell script.

```powershell
pwsh -File install.ps1 -Archive grimoire-<version>-win-x64.zip
```

Defaults: binary into `%LOCALAPPDATA%\grimoire`, export into
`%USERPROFILE%\.grimoire\web`; override with `-BinDir` / `-WebRoot`. If the
bin dir is not on `PATH` the script says so — add it through your user
environment variables.

## Infrastructure: Qdrant and Ollama

Grimoire talks to two local services and nothing else (see
`docs/platform-support.md` for the loopback boundary). The ready-made compose
file is `deploy/docker-compose.yml` in the repo — copy that one file, or run
your own instances:

```bash
docker compose -f deploy/docker-compose.yml up -d
```

The embedding model is not fetched automatically. Pull the default once:

```bash
ollama pull qwen3-embedding
```

(`grimoire init` below will name this exact command if the model is missing.)

## First run

```bash
grimoire init     # writes ~/.grimoire/config.json, creates the Qdrant
                  # collection, then probes Ollama for the configured model
grimoire serve    # web console on http://127.0.0.1:4770
grimoire ui       # opens the console in your browser (see platform notes)
```

`grimoire init` is loud about a broken first-run environment instead of
bootstrapping a store whose first write would fail:

- model missing → prints `Pull it first: ollama pull <model>` and exits 4;
- Ollama unreachable → prints the endpoint and the failure, exits 6;
- model present → prints that it answered the probe and proceeds.

Day-to-day reads worth knowing:

```bash
grimoire report           # the day's daily notes, grouped by actor
grimoire brief --daily    # the same day folded into the startup brief
```

## Building from source instead

Prerequisites: .NET 10 SDK; Node.js for the SPA.

```bash
dotnet build Grimoire.sln
dotnet test Grimoire.sln
dotnet run --project src/Grimoire.Cli -- <verb>    # init, serve, mcp, ...
(cd web && npm ci && npm run build)                # SPA export; postbuild stamps web/out/grimoire-web.json
rsync -a --delete web/out/ ~/.grimoire/web/        # deploy the export for `serve`
```

The release channel and the dev tool feed (dotnet tool) are described in
`README.md` → Distribution; this page is about the binary channel only.

## Verifying what you installed

```bash
grimoire --version              # must name the release you installed
curl -s http://127.0.0.1:4770/health   # store + embeddings reachability
```

If `/health` reports `degraded`, the JSON names which component and why —
see `docs/troubleshooting.md`.
