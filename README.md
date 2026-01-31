# Floodline

Floodline is a deterministic puzzle/strategy game implemented with a **Unity client** on top of a **headless .NET Core simulation**.

## Canonical documents (read first)

1) `docs/GDD_v0_2.md` — game rules + mechanics (gameplay source of truth)  
2) `.agent/AGENT_OS.md` — agent constitution: workflow, gates, milestone order  
3) `.agent/backlog.json` — canonical work state (DONE / CURRENT / NEXT) + evidence

The agent should open any other file only if the current backlog item’s `requirementRef` points to it.

## Repo structure

- `.agent/` — autonomous agent operating system + backlog
- `docs/` — game design document(s)
- `scripts/` — canonical validation entrypoints
- (future) `src/` / `tests/` — created by backlog items

## Validation entrypoints

Preflight (must be clean tree + prints DONE/CURRENT/NEXT):
```powershell
pwsh ./scripts/preflight.ps1
