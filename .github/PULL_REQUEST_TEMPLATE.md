## What and why

<!-- What shipped, and the problem it closes. Link the issue when there is one. -->

## Verification

<!-- There is no CI — local evidence is the gate. Paste output or counts for:

dotnet build Grimoire.sln
dotnet test Grimoire.sln
dotnet format --verify-no-changes
web/ touched: npm run lint && npx tsc --noEmit && npm run build
-->

## Contract impact

- [ ] MCP tool surface unchanged, or snapshots updated in this PR
      (`GRIMOIRE_UPDATE_TOOL_SNAPSHOTS=1`)
- [ ] Qdrant payload schema changes are additive-only
- [ ] HTTP face stays loopback-only

## Checklist

- [ ] Tests added or updated for behavior changes
- [ ] Docs updated where the change surfaces (README / CONTEXT / TECHNICAL-SOLUTIONS)
- [ ] New dependencies (if any) carry an OSI license and central version pinning
