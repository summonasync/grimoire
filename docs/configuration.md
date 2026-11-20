# Configuration

One config file, one env namespace, honest refusals. Defaults below are quoted
from the Options records in `src/Grimoire.Infrastructure/Configuration/`, not
from memory — if you find drift, the code wins and the bug is in this page.

## File and shape

`~/.grimoire/config.json` (written by `grimoire init` if missing). Sections map
to the Options records:

### `defaultTome`
String, default `default`. The tome an agent lands in when nothing in the
resolution chain (`docs/agent-wiring.md`) says otherwise. Note: it is a root
property — the `grimoire config set` verb addresses `section.property` pairs,
so this one key is edited in the file directly.

### `qdrant`
| Key | Default | Meaning |
|---|---|---|
| `endpoint` | `http://localhost:6334` | gRPC endpoint of the store |
| `apiKey` | none | optional auth |
| `collection` | `grimoire` | memory collection |
| `manifestCollection` | derived | tome manifest registry collection |
| `sessionCollection` | derived from `collection` | handshake registry; must not equal `collection` |

### `embedding`
| Key | Default | Meaning |
|---|---|---|
| `provider` | `ollama` | embeddings adapter |
| `model` | `qwen3-embedding` | model pulled via `ollama pull` (init checks it, see troubleshooting) |
| `endpoint` | `http://localhost:11434` | Ollama URL |
| `dimensions` | `1024` | vector width; changing it requires `grimoire reindex` |

### `memory`
| Key | Default | Meaning |
|---|---|---|
| `sessionTtlHours` | 24 | session-scope lifetime; GC reaps expired **Active** records only (superseded/tombstoned young rows stay, reported as `RetainedPastTtl`) |
| `tombstoneRetentionDays` | 30 | age at which tombstones become GC-eligible |
| `nearDuplicateSimilarityThreshold` | 0.80 | remember-time near-duplicate hint |
| `dedupSimilarityThreshold` | 0.92 | same-fact detection |
| `relatedSimilarityThreshold` | 0.82 | relatedness surfacing |
| `digestSimilarityThreshold` | 0.60 | topic digest membership |
| `recallForeignProjectFactor` | 0.6 | score factor for foreign-project records |
| `recencyHalfLifeDays.session/project/global/personal` | 0.5 / 30 / 90 / 180 | recency decay per scope |
| `reindexBatchSize` | 64 | points per reindex batch |
| `dailyTag` | `daily-notes` | tag the daily write-once path stamps |
| `dailyTimeZone` | none (machine local) | zone for day-window math; a dead IANA id gets a named warning, not a silent fallback |

### `serve`
| Key | Default | Meaning |
|---|---|---|
| `port` | 4770 | loopback console port (the bind itself is not configurable — `127.0.0.1` only, see `SECURITY.md`) |
| `gcIntervalMinutes` | 30 | in-server GC sweep cadence |

### `sessions`
| Key | Default | Meaning |
|---|---|---|
| `retentionHours` | follows `memory.sessionTtlHours` when unset | handshake registry window pruned by the serve sweep |

### `projects`
Registry of kebab-case aliases → absolute workspace paths (optional
`boardProjectId`, `displayName`, `description`), the source behind project
tags and `grimoire projects`.

## Environment overrides

Every property is overridable as `GRIMOIRE_SECTION__PROPERTY` (double
underscore), e.g. `GRIMOIRE_EMBEDDING__MODEL`, `GRIMOIRE_SERVE__PORT`. Env
beats file beats default. The loader refuses, loudly and with the name, an
override that:

- carries no property path (`GRIMOIRE_FOO`) — ConfigMerger rejects it;
- targets a non-section (`GRIMOIRE_FOO__BAR`);
- matches no configuration property (`GRIMOIRE_BIN` → `error [config]:
  Environment override 'GRIMOIRE_BIN' does not match any configuration
  property`);
- parses to the wrong type (boolean/number/shape checks per property).

This strictness is why the hook scripts use the `HOOK_` prefix for their
knobs: the CLI claims the whole `GRIMOIRE_*` namespace and a foreign knob in
it would break the call it configures. Three names are carved out as identity
variables, not config: `GRIMOIRE_ACTOR`, `GRIMOIRE_TOME`, `GRIMOIRE_HOME`.

## Precedence in one list

1. explicit tool parameter (where the door takes one, e.g. `tome`);
2. `GRIMOIRE_*` env override;
3. `~/.grimoire/config.json`;
4. code default (the tables above).

Tome resolution is its own chain — see `docs/agent-wiring.md`.
