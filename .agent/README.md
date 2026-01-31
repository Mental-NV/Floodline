# Agent Operating Pack

This folder defines the "constitution" and execution loop for coding AI agents working in this repo.

## Golden rule
The agent must prefer **verifiable artifacts** over prose:
**contracts > tests > code > docs text**.

## Execution loop (per task)
1) Restate goal + cite requirement (source file/section if available).
2) List touched artifacts (contracts/tests/docs/code).
3) Implement minimal diff.
4) Run required validation commands.
5) Update docs/ADR if behavior or architecture changed.
6) Commit with evidence (commands + results).

## When the agent is blocked
- Do NOT guess silently.
- Create a Blocking Note in the task:
  - what failed
  - 2–3 options
  - recommended option + why
  - what info is needed

## Key references
- rules: ./rules.md
- roles: ./roles.md
- DoD: ./definition-of-done.md
- commands: ./commands-windows.md
- change proposals: ./change-proposal-template.md
- CI gates: ./ci-gates.md
