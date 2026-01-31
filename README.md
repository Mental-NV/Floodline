# Floodline (Working Title)

This repo is designed for **fully autonomous development** using coding AI agents with strict, test-first, contract-first gates.

## Source of truth
- Game Design Document: `/docs/GDD_v0_2.md`

## Agent operating system
The agent must follow `/.agent/` as the repo constitution.

### Core rules
- `/.agent/README.md` — execution loop (includes backlog status updates)
- `/.agent/rules.md` — hard constraints (artifact precedence, change control, backlog tracking)
- `/.agent/roles.md` — Planner/Architect/Implementer/Verifier role split
- `/.agent/definition-of-done.md` — Definition of Done

### Project-specific layer (derived from the GDD)
- `/.agent/context.md` — glossary, invariants, out-of-scope, acceptance criteria
- `/.agent/milestones.md` — dependency graph and exit criteria aligned to the autonomy-first sequence
- `/.agent/contract-policy.md` — versioning rules for levels/replays/determinism hashes
- `/.agent/backlog.json` — canonical backlog with statuses (New/InProgress/Done)
- `/.agent/backlog.md` — human-readable backlog view

### Operational templates
- `/.agent/task-template.md` — required structure for every backlog item
- `/.agent/change-proposal-template.md` — required for any spec/architecture change
- `/.agent/commands-windows.md` — canonical Windows commands (dotnet + Unity batchmode)
- `/.agent/ci-gates.md` — CI gates (must pass)

## Implementation sequence (autonomy-maximizing)
1. Core sim library + CLI runner
2. Golden tests for resolve + water + objectives
3. Replay format + determinism hash
4. Level validator + campaign validation
5. Only then: Unity client UI loop + camera + input feel

## Local development (Windows)
Typical:
- `dotnet restore`
- `dotnet build -c Release`
- `dotnet test -c Release`

Formatting gate (if enabled):
- `dotnet format --verify-no-changes`

## Backlog discipline (non-negotiable)
The agent must keep `/.agent/backlog.json` accurate:
- exactly one item `InProgress`
- when starting: set InProgress
- when done: set Done with evidence (commands + results)
