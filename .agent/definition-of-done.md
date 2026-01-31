# Definition of Done (DoD)

A task is DONE only if all applicable items below are satisfied.

## General (always)
- [ ] Changes match task scope; no unrelated refactors
- [ ] Builds successfully on Windows
- [ ] Formatting/linting applied (if configured)
- [ ] Evidence provided: commands run + results
- [ ] No secrets committed

## Behavior / feature tasks
- [ ] Tests added/updated; tests fail before the change and pass after
- [ ] At least one negative test when applicable
- [ ] Docs updated if behavior changed
- [ ] Public contracts updated if behavior affects external interface

## Contract/schema tasks
- [ ] Contract updated (OpenAPI/JSON schema/etc.)
- [ ] Compatibility considered (version bump or backward-compatible change)
- [ ] Contract tests or generation steps updated accordingly

## Unity gameplay/system tasks
- [ ] Deterministic behavior where required (seeded randomness)
- [ ] EditMode/PlayMode tests placed appropriately
- [ ] No per-frame allocations introduced in hot paths without justification

## Refactor tasks (rare)
- [ ] Explicitly requested by task OR justified by Change Proposal
- [ ] No behavior changes (or behavior changes documented + tested)
- [ ] Net complexity decreases measurably (brief rationale)
