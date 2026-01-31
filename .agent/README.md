# Agent Operating Pack

This folder defines the "constitution" and execution loop for coding AI agents working in this repo.

## Golden rule
The agent must prefer **verifiable artifacts** over prose:
**contracts > tests > code > docs text**.

## Execution loop (per task)
1) Pick the next backlog item from `/.agent/backlog.json` (or `/.agent/backlog.md`).
2) Set its `status` to **InProgress** (and ensure there is no other InProgress item).
3) Restate goal + cite requirement (source file/section if available).
4) List touched artifacts (contracts/tests/docs/code).
5) Implement minimal diff.
6) Run required validation commands.
7) Update docs/ADR if behavior or architecture changed.
8) Commit with evidence (commands + results).
9) Set backlog item `status` to **Done**.
10) Select the next eligible **New** item (all dependencies Done) and proceed.

**Status enum:** `New` → `InProgress` → `Done`  
**WIP limit:** 1 (exactly one InProgress at any time).

## When the agent is blocked
- Do NOT guess silently.
- Keep item as **InProgress** and record a Blocking Note in the task:
  - what failed
  - 2–3 options
  - recommended option + why
  - what info is needed

## Key references
- design: `/docs/GDD_v0_2.md`
- context: `/.agent/context.md`
- milestones: `/.agent/milestones.md`
- backlog: `/.agent/backlog.json` (canonical), `/.agent/backlog.md` (view)
- contract policy: `/.agent/contract-policy.md`
- rules: `/.agent/rules.md`
- roles: `/.agent/roles.md`
- DoD: `/.agent/definition-of-done.md`
- commands: `/.agent/commands-windows.md`
- change proposals: `/.agent/change-proposal-template.md`
- CI gates: `/.agent/ci-gates.md`
