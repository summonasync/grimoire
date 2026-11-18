# Security Policy

## Supported versions

Grimoire is young (0.x) and maintained by a small team. Only the latest released
version receives fixes; they land on `main` and ship in the next release. Older
versions are not patched.

## Threat model

Grimoire is a local, single-user service: one binary with three faces — an MCP
stdio server, a loopback HTTP host with a static SPA, and a management CLI.
Qdrant (store) and Ollama (embeddings) are local dependencies on the same machine.

The security boundary is the **loopback bind**. The HTTP face listens on
`127.0.0.1` only (default port 4770), and a post-bind guard refuses any wider
reach. There is no authentication on the HTTP face — loopback isolation *is* the
access control. The MCP stdio face trusts the client that spawned it. Binding a
public interface is a design change that requires a new ADR, not a configuration
tweak.

### What counts as a vulnerability

- **Bind escape** — any path that makes the HTTP face reachable from outside
  loopback, or that defeats the post-bind loopback guard.
- **Tome isolation break** — a write landing in a tome other than the resolved
  one; a cross-tome read through any door other than `memory_scry` or naming a
  tome you own; session-scoped records crossing tomes at all.
- **Host/Origin validation bypass** on the HTTP face — for example DNS rebinding
  against the loopback host.
- Any other defect that breaks one of the boundaries above, including
  forgeable actor provenance on memory writes.

### What is out of scope

- Attacks that already require local access as the user running Grimoire:
  reading `~/.grimoire/config.json`, talking to Qdrant or Ollama directly on
  their loopback ports, or modifying the binary.
- Misbehavior of an MCP client that is already trusted to spawn the server —
  the stdio face is an interface for the user's own local agents.
- Local resource exhaustion (a local process can always fill a disk).

## Reporting a vulnerability

Please use **GitHub private vulnerability reporting**: the *Security* tab of
<https://github.com/summonasync/grimoire> → *Report a vulnerability*. Do not open
public issues for security problems.

Include what you observed, a reproduction path, the affected version
(`grimoire --version`), and the impact you see. Memory content can be personal —
redact anything private from proofs before sending them.

This is a best-effort, small-maintainer project: expect an honest acknowledgment
and a fix or a documented refusal, not an SLA.
