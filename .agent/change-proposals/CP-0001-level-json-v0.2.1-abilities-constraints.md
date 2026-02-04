# Change Proposal: Level JSON v0.2.1 (typed objectives/hazards + abilities/constraints)

## Problem statement
Upcoming Core + Campaign work requires explicit, deterministic per-level configuration for:
- constraints (overflow/weight/water-forbidden/no-resting-on-water)
- abilities toggles/counters (hold, stabilize)

Today, `schemas/level.schema.v0.2.0.json` has no place to represent these, and `hazards` is being overloaded in backlog language to include non-hazard concepts (abilities/constraints). This blocks autonomous content authoring and makes validation ambiguous.

## Current behavior / architecture
- Level schema v0.2.0 validates basic structure but leaves objective/hazard typing unconstrained (generic `type` + `params`).
- `hazards` is a generic array; the Wind spec expects fields like `enabled` that v0.2.0 schema does not allow.
- Core `Level` model has no `abilities`/`constraints` fields.

## Proposed change
1) Add `schemas/level.schema.v0.2.1.json` (keep v0.2.0 unchanged) that:
   - Adds required top-level objects: `abilities` and `constraints` (with known, bounded fields only).
   - Defines typed objectives via `oneOf` with required integer params for MVP objective types.
   - Defines typed hazards via `oneOf` for MVP hazard types (currently `WIND_GUST`), including `enabled` + typed params.
2) Update Core level model + loader to support these fields (defaults applied for v0.2.0 levels).
3) Update CLI validator default schema version to `0.2.1` so new content fails fast.

### Canonical field semantics (v0.2.1)
**abilities**
- `holdEnabled` (bool): enables Hold input/behavior for the level.
- `stabilizeCharges` (int >= 0): number of Stabilize charges available (0 disables/hides).

**constraints**
- `maxWorldHeight` (int >= 0, optional): lose with `OVERFLOW` if any solid voxel has `worldHeight(c) > maxWorldHeight`.
- `maxMass` (int >= 0, optional): lose with `WEIGHT_EXCEEDED` if total placed mass `> maxMass`.
- `waterForbiddenWorldHeightMin` (int >= 0, optional): lose with `WATER_FORBIDDEN` if any water voxel has `worldHeight(c) >= waterForbiddenWorldHeightMin`.
- `noRestingOnWater` (bool): if true, lose with `NO_RESTING_ON_WATER` if at end-of-resolve any solid voxel `c` has `grid[c+g] == WATER`.

## Alternatives considered
1) Encode abilities/constraints as `hazards` entries — conflates concepts; increases schema/semantic complexity; less readable for authors.
2) Keep everything in semantic validator only — higher implementation burden; weaker early failure; more ambiguous content authoring.
3) Do nothing — blocks M4 campaign authoring and increases agent error rate.

## Impact analysis
- API/contracts impact: introduces a new schema version v0.2.1; Core model adds `Abilities`/`Constraints` fields.
- Save data / migrations impact: levels declare schemaVersion; v0.2.0 remains supported; no migrator required for the repo today (no campaign levels exist yet).
- Performance impact: none meaningful.
- Tooling/CI impact: CLI validator default schemaVersion becomes 0.2.1; new schema file added.
- Risk (what could break): any existing v0.2.0 levels that omit abilities/constraints remain loadable in Core via defaults, but may not validate under v0.2.1 unless upgraded.

## Migration / rollout plan
- Add new schema file and update minimal fixtures to schemaVersion `0.2.1`.
- Keep v0.2.0 schema file unchanged for backward compatibility.

## Test plan
- Unit tests: CLI validation passes for a v0.2.1 level with abilities/constraints; fails with actionable errors when missing.
- Integration tests: `scripts/ci.ps1` passes on Windows in Release.

## Decision
- Status: Accepted
- Rationale: enables deterministic, schema-driven campaign authoring and removes ambiguity between hazards vs abilities vs constraints.

