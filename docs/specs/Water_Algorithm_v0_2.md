# Floodline — Water Algorithm Spec v0.2
*Location:* `/docs/specs/Water_Algorithm_v0_2.md`  
*Date:* 2026-01-31  
*Status:* MVP-lock specification (deterministic)

## Document map
- Core GDD: [`../GDD_Core_v0_2.md`](../GDD_Core_v0_2.md)
- Simulation Rules: [`Simulation_Rules_v0_2.md`](Simulation_Rules_v0_2.md)
- Input & Feel: [`Input_Feel_v0_2.md`](Input_Feel_v0_2.md)

---

*Engine:* Unity (C#)  
*Scope:* Canonical, deterministic **water equilibrium solver** and exact rules for **displacement**, **drains**, and **freeze/ice**.  
*Status:* MVP-lock specification.

> This document is the single source of truth for all water behavior.  
> It is compatible with **Simulation Rules Bible v0.2** ([`Simulation_Rules_v0_2.md`](Simulation_Rules_v0_2.md)) and referenced by level JSON.

---

## 1) Goals & Constraints
Water must:
- **Spread evenly** at the lowest reachable level(s).
- **Never support** solids.
- React deterministically to gravity changes and block movement.
- Be implementable as a fast discrete solver for typical boards (10×10×20–60).

Non-goals:
- continuous fluid simulation
- turbulence / velocities
- fractional water volumes (MVP uses one unit per cell)

---

## 2) Grid & Terminology

### 2.1 Grid
- Cells `c = (x, y, z)` with integer coordinates.
- Board footprint: `0 ≤ x < X`, `0 ≤ z < Z`
- Simulation height: `0 ≤ y < H` (use `H = heightLimit + margin`)

### 2.2 Occupancy
Each cell holds exactly one:
- `EMPTY`
- `SOLID(materialId)`
- `WALL/BEDROCK` (immovable)
- `WATER` (one unit)
- `ICE` (frozen water; supports)
- `POROUS` (supports; passable for water pathing; not occupiable)
- `DRAIN` (support-capable; removes water)

### 2.3 Gravity & Elevation
Gravity is cardinal `g`. Up direction `u = -g`.

Define elevation (integer):
- `gravElev(c) = dot(c, u)`

### 2.4 Canonical tie-break coordinate
Use local axes `(U,R,F)` from Simulation Rules Bible v0.2: [`Simulation_Rules_v0_2.md`](Simulation_Rules_v0_2.md)
- `U = u`
- `R` from table
- `F = cross(U,R)`

Then:
- `tieCoord(c) = (dot(c,U), dot(c,R), dot(c,F))`

Whenever sorting is required, order by:
1) `gravElev(c)` ascending  
2) `tieCoord(c)` ascending

---

## 3) Core Water Rules (Gameplay)

### 3.1 Water is non-supporting
A solid voxel is supported by the cell in direction `g` only if that cell is support-capable.  
`WATER` is treated as `EMPTY` for support.

### 3.2 Water occupies cells as discrete units
In MVP:
- each `WATER` cell represents exactly one unit
- no fractional volumes

### 3.3 Water cannot overlap solids
A cell cannot contain both water and solid.  
When solids enter water, water is **displaced** (see §6).

---

## 4) Passability vs Occupiability

### 4.1 Passability (for water pathfinding)
Water can traverse (graph edges) through:
- `EMPTY`
- `POROUS`
- (conceptually) `WATER` itself (during solver it’s cleared to empty)

Water cannot traverse through:
- `SOLID`, `WALL/BEDROCK`, `ICE`, `DRAIN`

### 4.2 Occupiability (where water can end up)
Water can occupy only:
- `EMPTY` cells

Water cannot occupy:
- `POROUS` (default MVP), solids, walls, drains, ice

> If you later want porous “to hold water,” allow occupiable porous. MVP default: **passable but not occupiable**.

---

<a id="water-settle-algorithm"></a>
## 5) Water Settle Algorithm — Deterministic Equilibrium
This solver computes the stable water distribution after changes (block placement, collapse, rotation).

### 5.1 Inputs & Outputs
**Input:**
- current grid state
- gravity direction `g`
- current water cells (as units)
- displaced water sources (from solids entering water)
- optional spawned water sources (later)

**Output:**
- set of water cells after settling (equilibrium)

### 5.2 High-level idea
Compute which cells are reachable from water sources if the water surface were allowed to rise.  
Then fill the lowest reachable cells first, producing:
- flat surfaces inside basins,
- correct spillover when volume is sufficient,
- deterministic, no update-order artifacts.

### 5.3 Algorithm steps (exact)

#### Step A — Collect water units and sources
Let:
- `W = { c | grid[c] == WATER }`
- `N = |W|` (number of water units)
- `S = list(W)` (water sources)

Additionally:
- For each displaced water event at cell `d` (solid entered water):
  - increment `N += 1`
  - add `d` to `S`

Optionally later:
- Add spawned water units similarly.

Then:
- set all `grid[c] == WATER` to `EMPTY` temporarily.

#### Step B — Compute minimax flood levels (`req[c]`)
We compute a value for each passable cell `c`:
- `req[c]` = minimal possible *maximum elevation* encountered along any path from any source `s ∈ S` to `c`.

This is a minimax path problem solved by Dijkstra with transition:
- `cand = max(req[cur], gravElev(next))`

Initialize:
- `req[c] = +∞` for all cells
- For each source `s` that is passable:
  - `req[s] = gravElev(s)`
  - push `s` into a priority queue

Priority queue order:
- `(reqVal, gravElev(cell), tieCoord(cell))`

Traverse:
- use 6-neighbor adjacency
- only traverse into passable cells

#### Step C — Build fill candidate list
Create list:
- `C = { c | c is occupiable (EMPTY) and req[c] != +∞ }`

Sort `C` by:
1) `req[c]` ascending (minimum water surface needed to reach c)
2) `gravElev(c)` ascending (lowest first within the same spill level)
3) `tieCoord(c)` ascending

#### Step D — Fill N units
Take the first `N` cells in sorted list `C` and set them to `WATER`.

If `N > |C|`, then the level has overflowed water capacity:
- MVP behavior: clamp to `|C|` and count overflow as failure only if level defines a water overflow lose rule. (By default, no such lose rule.)

### 5.4 Properties guaranteed
- **Even spread at low levels:** lowest reachable cells filled first.
- **Basins level:** within a basin, equal elevation fills are spread across all reachable cells at that elevation.
- **Spillover:** only occurs when required surface level (`req`) must rise to pass a ridge.
- **Deterministic:** fixed tie-break ordering.

---

<a id="water-displacement"></a>
## 6) Solid–Water Interaction (Displacement)
When a solid voxel moves into a water cell, water is displaced, not destroyed.

### 6.1 When displacement occurs
- On merge of active piece into grid (lock)
- During solid settling drops (component movement)

### 6.2 Displacement rule (exact)
If a solid voxel occupies a cell that currently contains `WATER`:
1. Remove water from that cell immediately.
2. Record a displaced water source at that cell (same coordinates).
3. Increment `displacedCount`.

After solids settle (before water settle), add displaced water units:
- `N += displacedCount`
- `S += displacedPositions`

Then run the water settle algorithm (§5).

This yields intuitive “squeezing out” behavior.

---

<a id="drains"></a>
## 7) Drains (Exact)
Drains remove water units deterministically during resolve.

### 7.1 Drain definition
A drain tile has:
- `ratePerResolve` (integer units)
- `scope`: `SELF` | `ADJ6` | `ADJ26`

### 7.2 Drain order
Process drains in deterministic order:
- sort drains by `(gravElev(d), tieCoord(d))` ascending

### 7.3 Removal procedure
After water settle:
1. Enumerate all water cells in drain scope.
2. Sort candidates by `(gravElev(c), tieCoord(c))` ascending
3. Remove up to `ratePerResolve` units:
   - set cells to `EMPTY`
   - increment `WaterRemovedTotal`

### 7.4 Reflow after drain
After all drains processed:
- re-run water settle **once** (recommended for clarity), using remaining water cells as sources.

---

<a id="freeze-ice"></a>
## 8) Freeze / Ice (Exact)
Freeze converts water into temporary support.

### 8.1 Freeze action
Freeze targets a set of water cells based on the armed-on-lock ability:
- When a `FreezeAbility`-armed piece locks, target all `WATER` cells in `freezeScope` around the locking piece voxels.
- Each targeted `WATER` cell becomes `ICE`
- ICE duration: `T` resolves (integer), decremented after each resolve

### 8.2 ICE behavior
ICE:
- is support-capable (treated as SOLID for collision and support)
- is impassable and not occupiable by water
- does not move

### 8.3 Thaw
When a frozen cell’s timer expires:
- ICE converts back to WATER
- That cell is added to water sources
- Run water settle (§5)

---

## 9) Optional Extensions (Post-MVP)
These are not active in MVP but are compatible with the solver:

### 9.1 Leak-to-void
Add an “outside sink” node and treat boundary openings as escape paths.  
Then water can leave the field if there is a path to outside with `req` below the current water surface.

### 9.2 Fractional water volume
Store per-cell volume (0..1) and run a similar basin-level fill with partial cells, but visuals/complexity increase significantly.

### 9.3 Porous holds water
Allow POROUS to be occupiable; keep it passable. This turns porous into “sponges”/channels.

---

## 10) Test Cases (Must-Have)
1. **Single basin fill:** water fills lowest cells, flat surface.
2. **Two basins with ridge:** small N stays in low basin; large N spills into second basin.
3. **Displacement:** dropping a solid into water increases water elsewhere by exactly 1 unit.
4. **Drain:** removing k units reduces total water by k, then reflow produces expected distribution.
5. **Freeze:** frozen cells support solids; thaw reintroduces water and reflows deterministically.
6. **Gravity change:** rotate gravity and settle; results match golden hash for fixed seed.

---

*End of Water Algorithm Spec v0.2*


---

