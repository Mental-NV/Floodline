# Change Proposal: Freeze ability + Drain placement (MVP semantics)

## Problem statement
The campaign plan references “Freeze charges” and “Drain placement charges”, but the canonical specs do not define:
- how the player triggers these actions (input model)
- how targets are selected deterministically
- how the actions are represented in level JSON / replay

An autonomous agent cannot implement or author levels safely without a deterministic definition.

## Current behavior / architecture
- Core supports drains as pre-placed tiles and supports ICE timers + `QueueFreeze(...)`, but there is no player-facing Freeze input/targeting model.
- No level JSON fields exist to declare Freeze/DrainPlacement charges and params.

## Proposed change
Add explicit MVP rules for:
- Freeze: activation, targeting, duration, charges, and replay encoding.
- Drain placement: activation, placement semantics, params, charges, and replay encoding.

## Alternatives considered
1) Cursor-based area selection — higher Unity/UI complexity; more replay data; higher autonomy risk.
2) Piece-tied “arm then apply on lock” semantics — deterministic, easy to replay, minimal UI for MVP.
3) Do nothing — campaign authoring for Lv16–20 is blocked.

## Impact analysis
- API/contracts impact: new level JSON fields (likely under `abilities`) and new replay input commands/events.
- Save data / migrations impact: replayVersion bump likely required once encoded.
- Performance impact: minimal.
- Tooling/CI impact: validator + solution runner need to understand new inputs.
- Risk: design choice may constrain level design; mitigate by keeping rules simple and authoring levels accordingly.

## Migration / rollout plan
- Decide and document the MVP semantics in the relevant canonical spec(s) and schemas.
- Add backlog items to implement + test in Core/CLI before campaign authoring starts.

## Test plan
- Unit tests for charge consumption, determinism, and target selection.
- Replay record→replay parity test updated to include new commands.

## Decision
- Status: Proposed
- Rationale: required to unblock deterministic campaign authoring without inventing behavior ad-hoc per level.

