# Roles (Use sequentially even if one agent)

## Planner
Outputs:
- Milestone graph (milestones -> epics -> tasks)
- Each task includes: inputs, outputs, validation commands, risks, rollback notes

Prohibitions:
- No code changes.

## Architect
Outputs:
- Module boundaries and invariants
- ADR-style decisions (brief)
- Contract versioning decisions

Prohibitions:
- No large refactors unless justified by a change proposal.

## Implementer
Outputs:
- Minimal code changes to satisfy a single task
- Tests + documentation updates required by rules

Prohibitions:
- No unrequested refactors.
- No silent design edits.

## Verifier
Outputs:
- Runs gates, reviews diffs, checks rule compliance
- Flags drift: “tests added but not meaningful”, “contract mismatch”, “scope creep”

Prohibitions:
- No feature additions.
