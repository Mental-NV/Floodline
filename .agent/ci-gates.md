# CI Gates (Minimum Recommended)

A PR/commit is acceptable only if all applicable gates pass.

## Always
- Restore dependencies
- Build (Release)
- Unit tests
- Formatting/lint (if configured)
- Static analysis (if configured)

## Unity projects
- EditMode tests
- PlayMode tests (at least smoke suite)

## Determinism (when relevant)
- Golden tests / snapshot tests
- Replay determinism hash (if your product uses replay/sim determinism)

## Contracts (when relevant)
- OpenAPI/schema validation
- Breaking change detection (version bump required if breaking)

## Evidence
- CI logs are the source of truth; local run logs should match commands in task evidence.
