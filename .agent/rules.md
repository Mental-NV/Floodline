# Agent Constitution (Hard Rules)

These rules are higher priority than convenience refactors or style preferences.

## 0) Artifact precedence
If conflicts exist, resolve in this order:
1) contracts / schemas
2) automated tests (incl. golden/snapshots)
3) code
4) docs text

## 1) Scope control
- Implement ONLY what the task requires.
- No "while I'm here" refactors.
- If a requirement is ambiguous, create a "Clarify" or "Change Proposal" task; do not invent behavior.

## 2) Change control (no silent spec edits)
If the agent believes design/architecture must change:
- Create a Change Proposal using `change-proposal-template.md`.
- Include alternatives + migration + compatibility notes.
- Only then implement the change.

## 3) Definition of Done is mandatory
Every completed task must meet DoD per `definition-of-done.md`.
If it cannot, mark task as NOT DONE and explain what is missing.

## 4) Tests are not optional
- Any behavior change requires tests that would fail without the change.
- Prefer deterministic tests (seeded RNG, fixed clocks, controlled IO).
- Add at least one negative test when feasible.

## 5) Determinism + reproducibility
- Pin toolchains and dependencies when possible (lockfiles, SDK versions).
- Avoid time-dependent behavior in core logic.
- Seed randomness and document the seed strategy.

## 6) Dependency discipline
- No new dependencies without explicit justification in the task.
- Prefer standard library / existing deps.
- Update lockfiles and verify restore/build.

## 7) Security & secrets
- Never commit secrets, tokens, private keys, passwords, or personal data.
- Use placeholders and local dev configuration patterns.

## 8) Repo hygiene
- Keep diffs minimal and readable.
- Keep formatting consistent (apply formatter once per task).
- Prefer incremental commits. One task = one commit (or a small series) with clear messages.

## 9) Evidence requirement
Every task completion must include:
- Commands executed
- Result summary (pass/fail, counts if relevant)
- Any follow-up risks or tech debt noted explicitly

## 10) Unity-specific constraints
- Avoid heavy runtime reflection in performance-sensitive paths.
- Prefer Assembly Definition Files for compile-time boundaries.
- Separate EditMode vs PlayMode tests intentionally.
