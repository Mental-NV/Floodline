# Floodline — Simulation Rules Bible v0.2
*Location:* `/docs/specs/Simulation_Rules_v0_2.md`  
*Date:* 2026-01-31  
*Status:* MVP-lock, deterministic (gameplay source of truth for simulation rules)

## Document map (deep links)
- Core GDD (player-facing): [`../GDD_Core_v0_2.md`](../GDD_Core_v0_2.md)
- Input & Feel (controls, tick, lock delay): [`Input_Feel_v0_2.md`](Input_Feel_v0_2.md)
- Water Algorithm (equilibrium solver, displacement, drains/freeze): [`Water_Algorithm_v0_2.md`](Water_Algorithm_v0_2.md)
- Content Pack (pieces + 30-level campaign): [`../content/Content_Pack_v0_2.md`](../content/Content_Pack_v0_2.md)

**Rule:** if any conflict exists: contracts/schemas > tests > code > docs prose.

---

*Status:* MVP-lock specification (deterministic).  
*Target engine:* Unity (C#), Windows PC.  
*Scope:* This document defines the **canonical gameplay rules** and **resolution order** for solids, gravity rotation, water, drains/freeze, objectives, and fail states. It is intended to prevent implementation drift.

---

## 0. Non‑Goals
- No continuous fluid simulation.
- No Unity Rigidbody physics for gameplay outcomes (visual-only allowed).
- No smooth rotation/inertia in MVP (snap only).

---

<a id="determinism-contract"></a>
## 1. Determinism Contract (Hard Requirement)
Given:
- the same level file,
- the same RNG seed,
- the same input stream (player actions with timestamps in fixed ticks),
- the same rules version,

…the simulation must produce identical results (grid state, objective progress, score).

### 1.1 Fixed Tick Model
- Simulation advances in **fixed ticks** (e.g., 60 Hz).  
- **Resolve Phase** is executed atomically at tick boundaries (may be animated over multiple frames, but outcomes are locked at start).

### 1.2 No Floating-Point in Core
Core solver (solids, water, objectives) uses:
- integer coordinates,
- integer comparisons,
- deterministic iteration order.
- **Serialized gameplay data rule:** level JSON and replay must not contain floating-point numbers; store time/durations as integer ticks.

### 1.3 Canonical Tie-Break
Whenever ordering is needed, sort by the tuple:
1. `gravElev(c)` (see §2.3) ascending  
2. `tieCoord(c)` ascending (see §2.4)

---

## 2. Grid Model

### 2.1 Coordinates
Grid cells are integer coordinates:
- `c = (x, y, z)` where `x∈[0..X-1]`, `z∈[0..Z-1]`, `y∈[0..H-1]`
- Base footprint is square or rectangular; MVP uses square.

**Coordinate system (Unity-style):**
- **X-axis:** `+X = EAST/right`, `-X = WEST/left`
- **Y-axis:** `+Y = UP`, `-Y = DOWN` (default gravity)
- **Z-axis:** `+Z = SOUTH/back`, `-Z = NORTH/forward`

This aligns with Input_Feel_v0_2.md §2.1 and Unity's left-handed coordinate system.

<a id="cell-occupancy-types"></a>
### 2.2 Cell Occupancy Types
Each cell contains **exactly one** of:
- `EMPTY`
- `SOLID(materialId)` (supports blocks)
- `WALL` / `BEDROCK` (immovable SOLID)
- `WATER` (1 unit, non-supporting)
- `ICE` (frozen water; supports and blocks)
- `POROUS(materialId)` (supports; passable for water pathing; not occupiable by water)
- `DRAIN(params)` (acts as SOLID for support; removes nearby water)

> MVP materials: `STANDARD`, `HEAVY`, `REINFORCED`. (Porous can be off initially.)

### 2.3 Gravity and Height Semantics
Gravity is a cardinal direction vector `g ∈ {±X, ±Y, ±Z}`.  
In MVP, the player can rotate around horizontal axes, so gravity is restricted to:
- `DOWN`, `NORTH`, `SOUTH`, `EAST`, `WEST` (no `UP` during gameplay).

We use **two different height concepts**:

1) **World height (player-facing, objectives/overflow):**
- `worldHeight(c) = y` (constant “vertical” axis in the level file and camera).

2) **Gravity elevation (solver-facing, settling + water equilibrium ordering):**
- Define gravity-up direction: `u = -g`.
- `gravElev(c) = dot(c, u)` (integer scalar).

Examples:
- If `g = (0, -1, 0)` then `u=(0,1,0)`, `gravElev = y`.
- If `g = (1,0,0)` then `u=(-1,0,0)`, `gravElev = -x` (used for ordering and minimax water paths).

**Rule:** objectives that say “height” or “forbidden zone” use `worldHeight`, never `gravElev`, unless explicitly stated.

### 2.4 Gravity-Dependent Tie Coordinates

To keep tie-break stable across gravity, define a local orthonormal axis triple `(U, R, F)`:
- `U = u`
- `R` is selected by table per gravity (fixed constants)
- `F = cross(U, R)` (also fixed)

Then:
- `tieCoord(c) = (dot(c,U), dot(c,R), dot(c,F))`

#### 2.4.1 Right-axis table (MVP)
Use the following `R` mapping:

| Gravity `g` | Up `U=-g` | Right `R` |
|---|---|---|
| DOWN (0,-1,0) | (0,1,0) | (1,0,0) |
| NORTH (0,0,-1) | (0,0,1) | (1,0,0) |
| SOUTH (0,0,1) | (0,0,-1) | (1,0,0) |
| EAST (1,0,0) | (-1,0,0) | (0,0,1) |
| WEST (-1,0,0) | (1,0,0) | (0,0,1) |

This avoids rotation-jitter in “same cost” cases.

---

## 3. World Rotation (Snap 90°)

### 3.1 Rotation Inputs
- World rotation is a **90° snap** around X or Z axis (horizontal axes).
- Rotation is allowed:
  - while an active piece is falling,
  - while no piece is falling,
  - during post-lock resolve is **not** allowed (inputs buffered for after resolve).

### 3.2 Rotation Effects
On successful rotation (snap 90°):
1. Gravity `g` is updated instantly.
2. The **active piece** continues falling under the new gravity.
3. A **Tilt Resolve** is executed immediately for the *settled world* (locked solids + water), using the new gravity:
   - treat the active piece’s cells as occupied, immovable obstacles during Tilt Resolve;
   - if Tilt Resolve would require moving a settled voxel into an occupied active-piece cell, the rotation is **rejected** (state rolls back; no partial changes).

*Rationale:* Tilting should have immediate, readable consequences, and authored levels rely on this.

### 3.3 Rotation Constraints (per level)
 Rotation Constraints (per level)
A level can constrain rotation via:
- allowed directions set `{DOWN, NORTH, SOUTH, EAST, WEST}`,
- cooldown seconds,
- max rotations or tilt budget.

If a rotation input violates constraints, it is ignored.

---

## 4. Active Piece Rules

### 4.1 Representation
The active piece is a set of local voxel offsets `P = {p_i}` around an origin cell.

### 4.2 Movement & Collision
- Piece can translate/rotate in discrete grid steps.
- A move is valid if all occupied destination cells are in bounds and are `EMPTY` or `WATER` (see §4.4).
- The active piece does **not** merge into the grid until it locks.

### 4.3 Lock Condition
A piece locks when, after applying gravity step attempts:
- it cannot advance one cell along `g` because at least one voxel would collide with `SOLID/WALL/BEDROCK/ICE/DRAIN` or out-of-bounds.

(Optionally allow “lock delay”; if used, lock delay is measured in fixed ticks and must be deterministic.)

### 4.4 Landing Into Water
If a piece voxel would enter a `WATER` cell on lock placement:
- it is allowed; the water will be **displaced** during resolve (see §6.4).

---

<a id="resolve-phase-order"></a>
## 5. Resolve Phase (Canonical Order)
Resolve is executed atomically immediately after lock (and also after any scripted events that demand stability).

Order:

1. **Merge Active Piece**
   - Convert its voxels into `SOLID(materialId)` (or special block voxels).
   - Record all overlaps with water as **displaced water sources** (see §6.4).

2. **Settle Solids**
   - Drop unsupported solid components along `g` until stable (§6).

3. **Settle Water**
   - Compute deterministic equilibrium distribution (§7).

4. **Recheck Solids**
   - Because water moved, re-run solid settling once more (§6).
   - (If this creates new water displacement, add to sources and re-settle water once.)

5. **Apply Drains**
   - Remove water per drain rules (§8), then optionally re-settle water once.

6. **Evaluate Objectives & Fail States**
   - Update progress, award score, check win/lose (§10, §11).

> Loop cap: At most **2 full solid-water cycles** per resolve to avoid pathological oscillations. (Empirically should converge; cap is safety.)

---

<a id="solid-settling"></a>
## 6. Solid Stability & Settling (Deterministic)

### 6.1 Support Rule
A solid voxel at cell `c` is supported if the adjacent cell `b = c + g` is one of:
- `SOLID` (any material),
- `WALL/BEDROCK`,
- `ICE`,
- `DRAIN` (support-capable),
- or other support-capable special tile.

It is **not supported** if `b` is:
- `EMPTY`, or
- `WATER`.

### 6.2 Connected Components
Solids settle as connected components using 6-connectivity (faces touch).

Procedure:
1. Build all solid voxels set `S` (including bedrock, walls excluded if immovable).
2. Identify components `C_k` among **movable** solids (exclude immovable walls/bedrock).
3. For each component, compute whether it is supported (any voxel in component has support under rule, or component is attached to immovable support via adjacency).

### 6.3 Component Drop Distance
For each unsupported component `C`:
- Compute maximum integer drop distance `d ≥ 1` such that moving all voxels `c ∈ C` to `c + d*g` stays in bounds and does not collide with:
  - immovable solids,
  - other solids not in C,
  - ice,
  - drains,
  - walls/bedrock.

`WATER` counts as empty for collision purposes (since water will be displaced).

Drop the component by `d` in one step.

### 6.4 Displaced Water From Solid Movement
If a solid voxel enters a water cell during merging or dropping:
- the water unit is removed and counted as displaced.
- displaced units become water sources in the next water settle (§7.1).

### 6.5 Iteration Order
To avoid “which component falls first” ambiguity, process unsupported components in deterministic order:
Sort components by:
1. `minElev(C) = min(gravElev(c))` over voxels in C ascending
2. `minTie(C) = min(tieCoord(c))` ascending

Then process in that order and repeat until no unsupported components remain or step cap reached.

### 6.6 Out-of-Bounds & Lost Voxels
If during any movement (active piece fall, settling, or special level rules) a solid voxel would go out-of-bounds, that voxel is removed and counted as **lost**.

- This metric is **not** used as the primary “collapse” tuning knob in v0.2.
- Structural instability is tuned primarily via **shift** (`ShiftVoxels*`), i.e., how much the structure moves during settling.

In MVP, correct collision/bounds enforcement should make **lost voxels** rare unless the level explicitly includes void openings or “outflow” rules.

---



## 7. Water Rules (Deterministic Equilibrium)

Water is simulated as **discrete units** that re-settle deterministically after every Resolve Phase (and after a successful world rotation, via Tilt Resolve).

**Key properties**
- Water occupies cells as `WATER`.
- Water is **non-supporting**: it never counts as structural support for solids.
- Water can be **displaced** by solids entering a cell; displaced units are re-inserted during the next water settle.
- Drains remove water deterministically after water has settled.

**Authoritative algorithm**
- See **Water Algorithm Spec v0.2**: [docs/specs/Water_Algorithm_v0_2.md](Water_Algorithm_v0_2.md).

## 8. Drains (Exact)

### 8.1 Drain Scope
Each drain has:
- `ratePerResolve` (integer)
- `scope` ∈ {`SELF`, `ADJ6`, `ADJ26`}

### 8.2 Drain Order
Drain tiles are processed in deterministic order by:
- `gravElev(d)` ascending, then `tieCoord(d)`.

### 8.3 Removal Rule
After water settle:
1. Gather all water cells within drain scope.
2. Sort by `(gravElev(c), tieCoord(c))` ascending.
3. Remove up to `ratePerResolve` units (set cells to `EMPTY`).

Then **re-run water settle once** (recommended for clarity).

---

## 9. Freeze / Ice (Exact)

### 9.1 Freeze Action
Freeze converts target `WATER` cells to `ICE` for `T` resolves (integer duration).

- `ICE` behaves as `SOLID` for collision and support.
- `ICE` is **impassable** and **not occupiable** by water.

### 9.2 Thaw
When timer expires:
- `ICE` cells convert back to `WATER`,
- those positions are added to water sources,
- water settle is executed.

### 9.3 Freeze ability (MVP input/targeting)
Freeze is an **armed-on-lock** ability (see Input_Feel_v0_2.md �2).

**Activation:**
- A `FreezeAbility` input arms the current active piece if `freezeCharges > 0`.
- Pressing again before lock disarms it (no charge refund).

**On lock (deterministic):**
- Freeze all `WATER` cells in `freezeScope` around the locking piece voxels.
- Frozen cells become `ICE` with timer `freezeDurationResolves`.
- Duplicate targets are ignored (a cell freezes once).

**Resolve order:**
- Apply freeze immediately after merge and **before** solid settling in the Resolve Phase.

### 9.4 Drain placement ability (MVP input/targeting)
Drain placement is an **armed-on-lock** ability.

**Activation:**
- A `DrainPlacementAbility` input arms the current active piece if `drainPlacementCharges > 0`.
- Pressing again before lock disarms it (no charge refund).

**On lock (deterministic):**
- Convert the **pivot voxel** of the locking piece to a `DRAIN` tile using `abilities.drainPlacement` params.
- All other voxels of the piece become normal `SOLID` voxels.
- If the pivot overlaps `WATER`, that water unit is displaced per standard rules.

**Resolve order:**
- Apply drain placement immediately after merge and **before** solid settling in the Resolve Phase.

---

<a id="objectives"></a>
## 10. Objectives & Evaluation Order

### 10.1 Evaluation Timing
Objectives are evaluated **only after the full Resolve Phase** completes (§5).

### 10.2 Canonical Metrics
Metrics are computed at end-of-resolve unless noted.

**Placement / progression**
- `PiecesUsed`: count of locked pieces (includes held pieces when locked later).
- `MaxWorldHeight`: `max(worldHeight(c))` over all **solid** voxels.

**Structural change**
- `ShiftVoxelsResolve`: number of solid voxels that changed cell position during **Solid Settling** in the current Resolve Phase.
- `ShiftVoxelsTotal`: cumulative shift over the level.
- `LostVoxelsResolve`: number of solid voxels removed in the current resolve due to out-of-bounds / explicit void rules.
- `LostVoxelsTotal`: cumulative lost voxels over the level.

**Water**
- `WaterRemovedTotal`: cumulative water units removed by drains or other removals.
- `WaterInForbiddenZone`: any water cell with `worldHeight(c) >= threshold`.

### 10.3 Objective Types (MVP)
 Objective Types (MVP)
- `DRAIN_WATER(targetUnits)` measured as `WaterRemovedTotal`
- `REACH_HEIGHT(height)` measured as `MaxWorldHeight`
- `BUILD_PLATEAU(area, worldLevel)` exact set match or floodfill area on same world height
- `STAY_UNDER_WEIGHT(maxMass)` mass = sum of placed voxels weighted by material mass
- `SURVIVE_ROTATIONS(k)` count of successful rotations executed

### 10.4 Constraint: NO_RESTING_ON_WATER (MVP)
If a level enables the `NO_RESTING_ON_WATER` constraint:
- Evaluate at end-of-resolve:
  - For every solid voxel `c`, if cell `c+g` is `WATER`, the constraint fails.
- A failure triggers the `NO_RESTING_ON_WATER` fail state (see Section 11).

(If you need stricter "ever happened" behavior, track a boolean flag when detected during resolve.)

### 10.5 Stars and Score (MVP)
**Stars**
- Star 1: all primary objectives completed.
- Star 2/3: AND-list of typed conditions (if present).

**Supported star condition types (v0.2.3+):**
- `MAX_PIECES_USED` (params: `count`)
- `MAX_ROTATIONS_USED` (params: `count`)
- `MAX_SHIFT_VOXELS_TOTAL` (params: `count`)
- `MAX_LOST_VOXELS_TOTAL` (params: `count`)

**Score (optional, deterministic)**
- If `score.enabled=true`, compute:
  - `score = perPiece * PiecesUsed + perWaterRemoved * WaterRemovedTotal - penaltyShiftVoxel * ShiftVoxelsTotal - penaltyLostVoxel * LostVoxelsTotal - penaltyRotation * RotationsExecuted`
- All weights are integers; if `score` is omitted or `enabled=false`, score is not computed.

---

<a id="fail-states"></a>
## 11. Fail States (MVP)
Lose immediately if any condition becomes true after resolve:
- `OVERFLOW`: any solid voxel outside allowed height limit
- `TILT_BUDGET_EXCEEDED`: budget < 0 (rotation input rejected before this in normal flow)
- `WEIGHT_EXCEEDED`: total placed mass > maxMass
- `WATER_FORBIDDEN`: water in forbidden zone (if configured)
- `NO_RESTING_ON_WATER`: any solid voxel resting on water (if configured)

Win when all primary objectives are satisfied (checked after resolve).

---

## 12. RNG, Bags, and Hold

### 12.1 RNG
- Single PRNG (e.g., XorShift128+ or PCG32) seeded per level.
- Only used for:
  - piece selection (unless fixed sequence),
  - material roll (if enabled),
  - cosmetic-only particles may use Unity RNG but must not affect gameplay.

### 12.2 Piece Bag Modes
- `FIXED_SEQUENCE`: exact list for tutorials.
- `WEIGHTED`: weighted draw with replacement.
- (Optional later) `BAG_N`: shuffle bag, draw without replacement.

### 12.3 Hold (MVP)
- Hold enabled per level.
- Rule: can hold **once per piece drop** (classic restriction).
- Holding swaps current piece with hold slot; held piece resets to spawn orientation.

---

<a id="replay-versioning"></a>
## 13. Replay & Versioning (Recommended)
To guarantee determinism across updates:
- Record:
  - `rulesVersion` (e.g., 0.1),
  - level id/hash,
  - seed,
  - per-tick inputs (move/rotate/tilt/hold/drop).
- Maintain backward compatibility by keeping old solvers or migrating replays.

---

## 14. Debug Views (Must-Have for Development)
- Show gravity arrow and `g` label.
- Toggle “unsupported solids” highlight (cells where support fails).
- Water debug:
  - show `req[c]` heatmap (minimax required level),
  - show basin boundaries.
- Component debug:
  - color connected components by ID,
  - print `minElev` ordering list.
- Determinism test mode:
  - run 100 resolves with random seeds and compare hashes to golden outputs.

---

## 15. Implementation Notes (Unity)
- Keep solvers in a **pure C# assembly** (no UnityEngine).
- Unity layer only:
  - renders the voxel state,
  - plays animations during resolve,
  - collects inputs and feeds fixed ticks.

---

*End of Rules Bible v0.2*


---


---

## Wind Hazard (High-Rise chapter, MVP)

*Engine:* Unity (C#)  
*Scope:* Exact wind event rules for High-Rise chapter.  
*Design intent:* Wind adds forcing without randomness. It must be predictable, telegraphed, and deterministic.

---

<a id="wind-hazard"></a>
### 1) Wind Applies To (MVP Choice)
**Wind affects only the active falling piece**, not the settled structure.

Rationale:
- avoids surprising cascades,
- keeps gameplay skill-based (steer + plan),
- simplifies determinism and level tuning.

---

### 2) Wind Event Definition (Level JSON)
```json
{
  "type": "WIND_GUST",
  "enabled": true,
  "params": {
    "intervalTicks": 600,
    "pushStrength": 1,
    "directionMode": "ALTERNATE_EW",
    "firstGustOffsetTicks": 180
  }
}
```

> Authoring note: **tick rate is 60 TPS** (`TICK_HZ=60`). `intervalTicks=600` means 10 seconds.


#### 2.1 Parameters
- `intervalTicks` (int, **≥ 1**): gust period in ticks (**no floats in JSON**)
- `firstGustOffsetTicks` (optional int): initial offset in ticks (if absent, computed from seed)
- `pushStrength` (int): number of cells to attempt to shove (MVP: 1)
- `directionMode`:
  - `ALTERNATE_EW`: East, West, East, West…
  - `FIXED`: uses `fixedDirection`
  - `RANDOM_SEEDED`: uses level PRNG but deterministic

---

### 3) Tick Scheduling (Deterministic)
Tick rate is **60 ticks/second**. Scheduling uses integer tick values stored in level JSON.

Let:
- `intervalTicks` = `max(1, intervalTicks)`
- `offsetTicks` = `firstGustOffsetTicks` (or computed)

Event fires at ticks:
- `t = offsetTicks + k * intervalTicks` for k = 0,1,2,...

#### 3.1 Seeded offset (if not provided)
If `firstGustOffsetTicks` is missing:
- `offsetTicks = PRNG(seed).NextInt(0, intervalTicks)`  
(where PRNG is the same deterministic generator used for piece bag, with a separate stream label for hazards)

---

### 4) Wind Direction (Deterministic)
Define wind directions in **world XZ plane**:
- `EAST = (+1,0,0)`
- `WEST = (-1,0,0)`
- `NORTH = (0,0,-1)`
- `SOUTH = (0,0,+1)`

#### 4.1 ALTERNATE_EW
- Gust #0: EAST
- Gust #1: WEST
- Gust #2: EAST
- …

#### 4.2 RANDOM_SEEDED
- `dir = PRNG.NextChoice([EAST, WEST])` (or include N/S)
- Store in replay implicitly via seed+tick.

---

### 5) Wind Effect (Canonical Rule)
When a gust fires:
1. If no active piece exists, do nothing.
2. Attempt to translate the active piece origin by `pushStrength` cells in wind direction, one cell at a time:
   - For step i in 1..pushStrength:
     - `candidateOrigin = origin + dir`
     - If candidate placement is valid (all voxels in bounds and not colliding with SOLID/WALL/BEDROCK/ICE/DRAIN), accept and continue.
     - If invalid, stop immediately (no further steps).
3. Wind never rotates the piece.

#### 5.1 Interaction with water
- Wind may move the active piece through/into `WATER` cells (allowed).
- Collisions with `WATER` are treated as empty for active piece movement (same as general collision rule).

---

### 6) Telegraphing (UX Must-Have)
- Display a wind icon and countdown timer to next gust.
- Display arrow indicating upcoming direction at least **1 second** before gust.
- When gust triggers, show a short screen-space nudge + whoosh SFX.

---

### 7) Tuning Guidance
- Early High-Rise: interval 12s, pushStrength 1, alternating.
- Mid High-Rise: interval 10s, pushStrength 1, alternating.
- Late High-Rise exam: interval 8–10s, pushStrength 1, optional N/S inclusion.

Avoid pushStrength > 1 in MVP.

---

*End of Wind Hazard Spec v0.2*


---


---

## Materials, Anchoring, Stabilize (MVP)

<a id="materials"></a>
### 1) Materials (MVP)

Materials are per-voxel properties attached to each locked piece’s voxels.

| Material | Mass (for `STAY_UNDER_WEIGHT`) | Anchoring behavior | Wind interaction (active piece) | Notes |
|---|---:|---|---|---|
| `STANDARD` | 1 | Not anchored | Normal | Default solids |
| `HEAVY` | 2 | Not anchored | Wind push is applied, but **reduced** (see §3) | “High-rise” tuning lever |
| `REINFORCED` | 1 | **Anchors permanently on lock** | Wind push applied normally while falling | Level authors control scarcity |
| `WATER` | 0 | N/A | N/A | Always non-supporting |

#### 1.1 Anchored flag (canonical)
A voxel may have `anchored=true`. Anchored voxels:
- never change cell position during **Solid Settling** (including after world rotations),
- still participate in support checks for other voxels/components,
- may be part of merges; a connected component that contains ≥1 anchored voxel is treated as immovable (fall distance = 0).

**How anchored voxels are created**
- `REINFORCED` voxels: become anchored **permanently** at lock time.
- **Stabilize** (ability): temporarily anchors a piece (see §2).

---

### 2) Stabilize ability (MVP)

**Design intent:** give the player a limited, skillful “insurance” tool against catastrophic structural shift during tilts/wind.

#### 2.1 Resource model
- Levels can enable Stabilize with a configured number of `stabilizeCharges` (integer).
- If `stabilizeCharges=0`, the action is disabled/hidden.

#### 2.2 Activation
- Player presses **Stabilize** (default binding: `V`) while a piece is active.
- If a charge is available, the active piece is marked `stabilizeArmed=true` (UI indicator).
- Arming can be canceled by pressing Stabilize again **before** lock (does not refund charge in MVP; optional refinement).

#### 2.3 Effect on lock
When an armed piece locks:
- all its voxels gain `anchored=true` with a **temporary** duration:
  - `stabilizeAnchorRotations = 2` (default) successful world rotations after lock.

After each successful world rotation, decrement the duration counter; when it reaches 0, remove `anchored=true` from those voxels.

**Determinism requirement:** duration countdown is driven only by discrete “successful rotation” events, not wall-clock time.

#### 2.4 Interaction rules
- Temporary anchored voxels behave identically to Reinforced anchoring while active.
- When temporary anchoring expires, the structure may shift on subsequent resolves/tilts.
- Reinforced anchoring never expires.

---

### 3) Wind + mass interaction (MVP)

Wind applies a deterministic lateral push to the **active piece** (Part V). For `HEAVY` material pieces:
- interpret wind “push strength” as **cells per gust per unit mass**,
- effective push steps = `floor(pushStrength / pieceMassFactor)` where:
  - `pieceMassFactor = 1` for Standard/Reinforced,
  - `pieceMassFactor = 2` for Heavy.

This preserves predictability while letting Heavy pieces be more wind-resistant.


---

