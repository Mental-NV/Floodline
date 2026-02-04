# Floodline

Floodline is a deterministic puzzle/strategy game implemented with a **Unity client** on top of a **headless .NET simulation**.

## Canonical documents (read first)

1) [`docs/GDD_Core_v0_2.md`](docs/GDD_Core_v0_2.md) — core GDD (player-facing design)
2) [`docs/specs/Simulation_Rules_v0_2.md`](docs/specs/Simulation_Rules_v0_2.md) — determinism + resolve order (simulation source of truth)
3) [`.agent/AGENT_OS.md`](.agent/AGENT_OS.md) — agent constitution: workflow, gates, milestone order
4) [`.agent/backlog.json`](.agent/backlog.json) — canonical work state (DONE / CURRENT / NEXT) + evidence  

The agent should open any other file only if the CURRENT backlog item’s `requirementRef` explicitly points to it.

## Repo structure

- `.agent/` — autonomous agent operating system + backlog
- `docs/` — game design document(s)
- `scripts/` — canonical validation entrypoints
- (created by backlog) `src/` / `tests/`

## Validation entrypoints

Preflight (must be clean tree + prints DONE/CURRENT/NEXT):

```powershell
powershell -File ./scripts/preflight.ps1
```

CI (use this from backlog item `validation` to avoid command drift):

```powershell
# early repo (before lock files exist)
powershell -File ./scripts/ci.ps1 -Scope Always -LockedRestore:$false

# M0: generate lock files (then verify locked restore)
powershell -File ./scripts/ci.ps1 -Scope M0 -UseLockFile

# M0+: include formatting once introduced
powershell -File ./scripts/ci.ps1 -Scope M0 -IncludeFormat

# M1+: default validations (restore/build/test)
powershell -File ./scripts/ci.ps1 -Scope M1
```

Note: `-Golden`, `-Replay`, `-ValidateLevels`, `-CampaignSolutions`, `-Unity` switches exist as reserved placeholders and should be implemented when the corresponding tooling/tests land.

## Starting the autonomous run

1) Read the 4 canonical documents above.  
2) Run `powershell -File ./scripts/preflight.ps1`.  
3) If no CURRENT item exists, set NEXT to `InProgress` and commit that backlog-only change.  
4) Execute the role loop per [`.agent/AGENT_OS.md`](.agent/AGENT_OS.md).
