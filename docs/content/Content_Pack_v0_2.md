# Floodline — Content Pack v0.2
*Location:* `/docs/content/Content_Pack_v0_2.md`  
*Date:* 2026-01-31  
*Scope:* content definitions that are referenced by the rules (piece library, campaign plan).  

## Document map
- Core GDD: [`../GDD_Core_v0_2.md`](../GDD_Core_v0_2.md)
- Simulation Rules: [`../specs/Simulation_Rules_v0_2.md`](../specs/Simulation_Rules_v0_2.md)

---

<a id="piece-library"></a>
## Piece Library v0.2

*Engine:* Unity (C#)  
*Scope:* Canonical **piece IDs**, voxel definitions, pivot rules, and orientation generation for MVP.

> This document intentionally standardizes **piece IDs** used in level JSON (e.g., `I3`, `L3`, `T3`, `O2`).  
> Naming is “legacy-friendly” to match earlier templates; the IDs are the source of truth.

---

### 1) Representation

#### 1.1 Piece definition (JSON)
Each piece is defined as:
- `pieceId`: string
- `voxels`: array of integer offsets relative to **pivot cell** `(0,0,0)`
- `pivot`: always `(0,0,0)` in v0.2 (the voxel at origin is part of the piece)
- `tags`: optional (e.g., `flat`, `3d`, `tutorial`)

```json
{
  "pieceId": "L3",
  "voxels": [[0,0,0],[1,0,0],[0,1,0]],
  "tags": ["flat"]
}
```

#### 1.2 Pivot rule (hard requirement)
- Pivot is always an occupied voxel at `(0,0,0)`.
- All rotations are applied around pivot.
- After rotation, **no re-centering** is performed (offsets remain integers by construction).

This keeps rotation deterministic and simple.

---

### 2) Orientation Generation (Canonical)

#### 2.1 Rotation group
- Use the 24 proper rotations of the cube (`SO(3)` with integer matrices).
- Apply each rotation matrix `R` to every voxel offset `v`:
  - `v' = R * v`
- Normalize the oriented shape by translating all voxel offsets so that:
  - the minimum `(x,y,z)` in the oriented set becomes `(0,0,0)` **ONLY for uniqueness testing**, not for placement.
- Deduplicate orientations by comparing normalized voxel sets.

#### 2.2 Runtime orientation application
At runtime, when rotating a piece, use the integer matrix and apply directly to offsets:
- Offsets remain relative to pivot.
- If you use “wall-kick,” it shifts the **piece origin cell**, not the voxel offsets.

---

### 3) Rotation Kick Policy (MVP)
To reduce frustration near obstacles, MVP uses a **small deterministic kick set** for *piece rotations* (not world rotations).

When a piece rotation is requested:
1. Apply rotation to offsets.
2. Try to place at same origin.
3. If collision/out-of-bounds, try kicks in order:

Kick list in local axes (world grid axes):
1. `(0,0,0)`
2. `(+1,0,0)`, `(-1,0,0)`
3. `(0,0,+1)`, `(0,0,-1)`
4. `(0,+1,0)`  *(rare; helps near ledges)*
5. `(+1,0,+1)`, `(+1,0,-1)`, `(-1,0,+1)`, `(-1,0,-1)`

First valid placement wins. If none valid, rotation is rejected.

> This is intentionally simpler than modern Tetris SRS.

---

### 4) MVP Piece Set (12 pieces)

#### 4.1 Notes
- Pieces are designed to be mostly “2.5D” early (flat), with a few true 3D polycubes to introduce depth later.
- All voxel offsets are small (fit within ~3×3×3 bounding boxes).

#### 4.2 Definitions (canonical)

##### O2 — 2×2 flat square (4 voxels)
```json
{ "pieceId":"O2", "voxels":[[0,0,0],[1,0,0],[0,0,1],[1,0,1]], "tags":["flat","tutorial"] }
```

##### I3 — 3-long line (3 voxels)
```json
{ "pieceId":"I3", "voxels":[[0,0,0],[1,0,0],[2,0,0]], "tags":["tutorial"] }
```

##### I4 — 4-long line (4 voxels)
```json
{ "pieceId":"I4", "voxels":[[0,0,0],[1,0,0],[2,0,0],[3,0,0]] }
```

##### L3 — small L (3 voxels)
```json
{ "pieceId":"L3", "voxels":[[0,0,0],[1,0,0],[0,1,0]], "tags":["tutorial"] }
```

##### L4 — classic L (4 voxels)
```json
{ "pieceId":"L4", "voxels":[[0,0,0],[1,0,0],[2,0,0],[0,1,0]] }
```

##### J4 — mirrored L (4 voxels)
```json
{ "pieceId":"J4", "voxels":[[0,0,0],[1,0,0],[2,0,0],[2,1,0]] }
```

##### T3 — T shape (4 voxels)  *(legacy ID)*
```json
{ "pieceId":"T3", "voxels":[[0,0,0],[1,0,0],[2,0,0],[1,1,0]] }
```

##### S4 — S shape (4 voxels)
```json
{ "pieceId":"S4", "voxels":[[0,0,0],[1,0,0],[1,0,1],[2,0,1]] }
```

##### Z4 — Z shape (4 voxels)
```json
{ "pieceId":"Z4", "voxels":[[0,0,1],[1,0,1],[1,0,0],[2,0,0]] }
```

##### U5 — U shape (5 voxels)
```json
{ "pieceId":"U5", "voxels":[[0,0,0],[2,0,0],[0,0,1],[1,0,1],[2,0,1]] }
```

##### P5 — “P” / 2×2 plus tail (5 voxels)
```json
{ "pieceId":"P5", "voxels":[[0,0,0],[1,0,0],[0,1,0],[1,1,0],[2,0,0]] }
```

##### C3D5 — simple 3D corner (5 voxels) *(introduces depth)*
```json
{ "pieceId":"C3D5", "voxels":[[0,0,0],[1,0,0],[0,1,0],[0,0,1],[1,0,1]], "tags":["3d"] }
```

---

### 5) Piece Pack Progression (Recommended)
- **Chapter 1 (Foundation):** O2, I3, L3, T3, I4 (introduce)  
- **Chapter 2 (Floodplain):** add S4, Z4, L4, J4  
- **Chapter 3 (High-Rise):** add U5, P5, C3D5 (and bias heavy materials)

---

### 6) Validation Checklist
- All voxel offsets are integers.
- `(0,0,0)` is included for every piece.
- No duplicate voxel coordinates within a piece.
- Orientation generation deduplicates symmetric shapes.
- Kick set order is fixed and tested.

---

*End of Piece Library v0.2*


---


---

<a id="campaign-plan-30"></a>
## 30-Level Campaign Plan v0.2

*Engine:* Unity (C#)  
*Scope:* A curated 30-level campaign that introduces mechanics in a controlled sequence.  
*Design intent:* Every level introduces **one new idea**, then mixes it with prior ideas. Difficulty emerges from **system interactions**, not raw speed.

---

### Overview
**Chapter 1 (Lv 1–10): Foundation**  
Teaches core block placement, world rotation, stability/collapse, plateau building, and rotation scarcity.

**Chapter 2 (Lv 11–20): Floodplain**  
Introduces water as a non-supporting fluid, drains, channel engineering, freeze tactics, and flood constraints.

**Chapter 3 (Lv 21–30): High-Rise**  
Introduces weight limits, footprint constraints, wind forcing, stabilization, and mixed hazards.

> Assumed defaults:
> - Snap 90° world rotation.
> - Deterministic resolve after each lock.
> - Water model: binary cells, basin leveling.
> - Campaign boards: mostly 10×10, some 8×8–9×9.
> - Stars: 1=win, 2=efficiency, 3=stability/mastery.

---

## Chapter 1 — Foundation (Lv 1–10)
**Teaching goal:** the player must internalize *gravity as a tool* and *support rules*.

#### Level Table
| Lv | Title | New teach beat | Primary objective | Constraints / Star hooks |
|---:|---|---|---|---|
| 1 | First Stack | Basic move/rotate piece, lock | Place 8 pieces without overflow | No world rotation; ★2 ≤10 holes, ★3 no overhangs |
| 2 | First Tilt | World rotation (E/W) | Reach height 6 | Max rotations 3; ★3 shift voxels = 0 |
| 3 | Four Winds | Add N/S rotation | Reach height 8 | Cooldown 1.5s; ★2 ≤2 tilts |
| 4 | Unsupported | Overhangs fall during resolve | Survive 2 tilts with ≤5 voxels lost | Prebuilt overhang demo; ★3 loss ≤1 |
| 5 | Parking Lot | Flatness matters | Build plateau 3×3 at elev=2 | ★2 ≤10 pieces, ★3 plateau survives 2 tilts |
| 6 | Hold It | Introduce Hold | Reach height 10 | Hold on; ★2 hold used ≤2 |
| 7 | Reinforced Intro | Reinforced pieces for bridging | Build bridge gap length 3 | Bag includes reinforced; ★3 bridge survives 2 tilts |
| 8 | Tilt Budget | Rotation scarcity | Reach height 12 | Tilt budget 6; ★3 finish with ≥2 left |
| 9 | Cavity Bonus | Enclosure (air pocket) | Enclose 1 cavity | ★2 cavity + height 10, ★3 ≤12 pieces |
| 10 | Foundation Exam | Combine concepts | Height 14 AND plateau 3×3 | Tilt budget 8; ★3 shift voxels ≤6 |

#### Notes for Chapter 1
- **Lv1–3:** rotation introduced with low penalty.
- **Lv4–6:** collapse becomes the “why” of the game.
- **Lv7–10:** scarcity + multi-objectives create planning.

---

## Chapter 2 — Floodplain (Lv 11–20)
**Teaching goal:** water is hostile, predictable, and controllable through engineering.

#### Level Table
| Lv | Title | New teach beat | Primary objective | Constraints / Star hooks |
|---:|---|---|---|---|
| 11 | First Basin | Water exists; cannot support | Place 6 pieces; end stable | Starter pool under shelf; ★3 shift voxels 0 |
| 12 | Meet the Drain | Drain tile behavior | Drain 10 units | One drain preplaced; ★2 ≤12 pieces |
| 13 | Dig a Channel | Guide flow with terrain | Drain 18 units | Tilt allowed E/W; ★3 ≤4 tilts |
| 14 | Two Basins | Basins connect + level | Drain 20 units | Must connect basins; ★3 no water above elev=1 |
| 15 | Don’t Build on Water | Support rule enforcement | Reach height 10 | ★3 “no resting on water” at end-of-resolve |
| 16 | Freeze 101 | Freeze introduces temporary support | Drain 20 units | Freeze charges 1; ★3 freeze unused |
| 17 | Temporary Bridge | Freeze as tactical bridge | Create crossing then drain | Fixed piece seq; ★3 ≤1 tilt |
| 18 | Drain Placement | Player places drain block | Drain 25 units | Drain placement charges 1; ★2 ≤18 pieces |
| 19 | Forbidden Zone | Flood constraint | Drain 30 units | Lose if water elev≥3; ★3 ≤6 tilts |
| 20 | Floodplain Exam | Combine drain + freeze + planning | Drain 40 AND height 12 | Tilt budget 10; ★3 loss ≤8 & ≤22 pieces |

#### Notes for Chapter 2
- Emphasize: **water is deterministic** and will always settle to lowest reachable cells.
- Drain scope visualization is essential (highlight radius).
- Freeze levels should teach “temporary support” and “controlled timing,” not spam.

---

## Chapter 3 — High-Rise (Lv 21–30)
**Teaching goal:** vertical engineering under constraints and external forcing.

#### Level Table
| Lv | Title | New teach beat | Primary objective | Constraints / Star hooks |
|---:|---|---|---|---|
| 21 | Weight Limit | Total mass constraint | Reach height 18 | Max weight 250; ★3 finish ≤220 |
| 22 | Heavy Pieces | Heavy material tradeoff | Reach height 20 | Bag biased heavy; ★3 ≤1 major collapse |
| 23 | Skyline Footprint | Small base footprint | Reach height 18 | Field 8×8; ★3 plateau 3×3 also |
| 24 | First Gust | Wind forcing begins | Reach height 20 | Wind interval 12s; ★3 ≤4 tilts |
| 25 | Stabilize | Stabilize ability intro | Reach height 22 | Stabilize charges 1; ★3 no stabilize used |
| 26 | Cantilever | Overhang design under wind | Build overhang length 4 survives 2 tilts | Max weight 280; ★3 ≤14 pieces |
| 27 | Tight Budget | Scarce rotation under pressure | Reach height 24 | Tilt budget 6; ★3 finish with ≥2 left |
| 28 | Alternating Wind | Predictable wind patterns | Reach height 26 | Wind alternates E/W; ★3 shift voxels 0 |
| 29 | Wet Foundation | Water returns as foundation hazard | Reach height 26 under weight | Small base puddles; ★3 no resting on water at end |
| 30 | High-Rise Exam | Full mastery | Height 30 AND weight ≤400 | Wind stronger; tilt budget 10; ★3 ≤26 pieces & loss ≤10 |

#### Notes for Chapter 3
- Wind must be **predictable** (patterned) and signposted (arrow + countdown).
- Stabilize is a clutch tool; design levels so it feels “earned,” not mandatory.

---

### Campaign Progression & Unlocks
#### Unlock cadence (recommended)
- After Lv3: rotation N/S enabled generally (unless level restricts).
- After Lv6: Hold enabled by default (levels can disable).
- After Lv7: Reinforced material appears in bag.
- After Lv12: Drain UI appears; drain tile becomes common.
- After Lv16: Freeze introduced (limited charges).
- After Lv21: Weight UI and heavy material become common.
- After Lv24: Wind icon + timer UI appears.
- After Lv25: Stabilize introduced.

---

### Difficulty Tuning Checklist (Per Level)
- **Clarity:** is the new teach beat visible in the first 20 seconds?
- **Constraint fairness:** can a reasonable player recover after 1 mistake?
- **Rotation pressure:** does the level force rotation choices, not spam?
- **Water pressure:** does the level require basin/channel reasoning?
- **Stability:** do collapses have obvious causes (unsupported / resting on water)?
- **Stars:** ★2 requires efficiency, ★3 requires mastery (stability/constraints).

---

### Deliverable Notes (Implementation)
This plan is intended to be authored into actual JSON levels using the level template:
- Tutorial levels should use `bag.type = FIXED_SEQUENCE`.
- All levels should specify `meta.seed` for reproducibility.
- All time/duration fields in level JSON use **integer ticks** at `TICK_HZ=60` (no floats).
- Each chapter should include 1–2 “exam” levels that combine prior concepts.

---

### Campaign Authoring Conventions (MVP)
- Campaign manifest path: `levels/campaign.v0.2.0.json` with `meta.schemaVersion` set to the latest `0.2.x`.
- Level file naming: `levels/campaign/L01_First_Stack.json`, `L02_First_Tilt.json`, ... (two-digit level number, title case with underscores).
- `meta.id` must equal the campaign `levels[].id`, and should match the filename stem (e.g., `L01_First_Stack`).
- Seeds are deterministic: always set `meta.seed` to an integer.
- Numeric policy: no floats in level JSON; all durations/intervals are integer ticks (`TICK_HZ=60`).
- Manifest ordering is canonical: keep `levels[]` in ascending level number order.
- Validation: `powershell -ExecutionPolicy Bypass -File ./scripts/ci.ps1 -Scope M4 -ValidateLevels`.

### Campaign Solution Replays (MVP)
- Canonical solution files are replay JSON files produced by the CLI (`--record`) and stored at `levels/solutions/{levelId}.replay.json`.
- `meta.replayVersion` must match the current replay format version (currently `0.1.2`).
- `meta.rulesVersion` must match the current rules version (currently `0.2.1`).
- `meta.tickRate` must be `60`; replay inputs are per-tick commands with contiguous ticks starting at `0`.
- `meta.inputEncoding` must be `command-v2`.
- CLI runner (strict): `Floodline.Cli --level <levelPath> --solution <solutionPath>`; fails on invalid replay format or if the simulation does not finish with `Status=Won`.

*End of 30-Level Campaign Plan v0.2*
