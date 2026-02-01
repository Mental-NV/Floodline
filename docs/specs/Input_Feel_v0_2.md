# Floodline — Input & Feel Spec v0.2 (MVP)
*Location:* `/docs/specs/Input_Feel_v0_2.md`  
*Date:* 2026-01-31  

## Document map
- Core GDD: [`../GDD_Core_v0_2.md`](../GDD_Core_v0_2.md)
- Simulation Rules: [`Simulation_Rules_v0_2.md`](Simulation_Rules_v0_2.md)

---

*Engine:* Unity (C#)  
*Scope:* Player input timing rules (grid movement), drop behavior, lock behavior, and camera rules that affect “feel.”  
*Goal:* Skillful but readable control, minimal implementation risk, deterministic.

---

## 1) Simulation Timing
- Fixed tick simulation: **60 ticks/sec**.
- Active piece motion is evaluated once per tick.
- Resolve Phase runs immediately on lock (outcomes deterministic).

---

<a id="input-model"></a>
## 2) Input Model (Actions)
Actions are sampled each tick and applied in canonical order:

1. **World rotate** (snap 90°) if pressed and allowed.  
   - On success, immediately execute a **Tilt Resolve** for the settled world (locked solids + water) under the new gravity.  
   - The active piece remains controllable and is treated as an immovable obstacle during Tilt Resolve.
2. **Stabilize ability** (if available) — arms the current active piece to anchor on lock (see §7).
3. **Piece rotate** (local rotation) if pressed.
4. **Piece move** (horizontal translate) via hold-repeat.
5. **Soft drop** (accelerate gravity step rate).
6. **Hard drop** (immediate place+lock).

If multiple inputs of the same class occur, apply in the order they were received this tick (or by fixed priority).

### 2.1 Grid Coordinate Convention (MVP)
**Coordinate system:**
- **+X** = right, **-X** = left
- **-Z** = north/forward (into playfield), **+Z** = south/back (toward camera)
- **-Y** = down (gravity), **+Y** = up

This aligns with Unity's coordinate system and Simulation_Rules_v0_2.md (NORTH = (0,0,-1)).

---

## 3) Movement Repeat (DAS/ARR Equivalent)
All movement is in integer grid cells.

### 3.1 Horizontal move (X/Z)
- **Initial delay (DAS):** 10 ticks (≈166 ms)
- **Repeat rate (ARR):** 2 ticks per step (≈33 ms)
- If direction changes, restart DAS timer.

### 3.2 Soft drop
- Soft drop increases gravity stepping to **1 cell per tick** (60 cells/sec) until lock.
- Soft drop does not change lock delay policy.

### 3.3 No “infinite slide”
Active piece movement is permitted while falling, but lock delay limits late adjustments.

---

<a id="lock-behavior"></a>
## 4) Lock Behavior (MVP)
### 4.1 Lock condition
Piece locks when it cannot advance one cell along gravity direction due to collision/out-of-bounds.

### 4.2 Lock delay
- **Lock delay:** 12 ticks (≈200 ms)
- Lock delay starts when the piece first becomes “grounded.”
- If the piece becomes ungrounded (due to world rotation or successful move), lock delay resets.

### 4.3 Lock delay reset limits
To prevent abuse:
- Maximum **4 lock resets** per piece.
- A “lock reset” occurs when grounded → ungrounded transition happens.

### 4.4 Hard drop
- Hard drop moves the piece along gravity as far as possible and **locks immediately** (no lock delay).
- Then Resolve Phase runs.

---

## 5) Piece Rotation Rules
### 5.1 Rotation kick
Piece rotation uses the deterministic kick set defined in **Piece Library v0.2**:
- Try no kick, then small translations in fixed order.
- If all fail, rotation is rejected.

### 5.2 Rotation while grounded
Allowed. If rotation succeeds and changes grounded state, it can trigger a lock reset (counts toward reset limit).

---

## 6) World Rotation Rules (Player-facing)
- World rotation is snap 90°.
- Allowed while active piece is falling.
- Not allowed during Resolve Phase (input buffered until after).

---

## 7) Camera Rules (MVP)
### 7.1 Camera vs gravity
MVP uses **camera-stable horizon**:
- The camera does not auto-rotate to match gravity.
- World rotation is visually animated (board tilts), but the camera remains comfortable.

### 7.2 Snap views
Provide 4 corner snap views (hotkeys) for readability:
- NE, NW, SE, SW isometric angles.

### 7.3 Focus behavior
On spawn, camera recenters to keep the active piece and the top of the structure visible.

---

<a id="input-defaults-pc"></a>
## 8) Input Defaults (PC)
- Move: A/D (x-/left) / (x+/right), W/S (z-/forward/north) / (z+/back/south)
- Local piece rotation:
  - yaw: Q/E
  - pitch: R/F
  - roll: Z/C
- Soft drop: Left Ctrl (or S, if not used for move)
- Hard drop: Space
- Hold: Left Shift
- World rotation:
  - Tilt forward/back: 1 / 2
  - Tilt left/right: 3 / 4
- Stabilize (ability): V (if level enables)
- Camera snap: F1–F4

(Rebinding supported via Unity Input System.)

---

## 9) Tuneables (Expose in a config asset)
- Tick rate (fixed)
- DAS, ARR
- Soft drop speed
- Lock delay ticks
- Max lock resets
- World rotation cooldown ticks

---

*End of Input & Feel Spec v0.2*


---
