# Floodline — Contract & Versioning Policy

**Purpose:** keep the system consistent under autonomous development: APIs, level data, replays, and determinism must not drift.

This policy complements:
- [`docs/GDD_Core_v0_2.md`](../docs/GDD_Core_v0_2.md)
- [`docs/specs/Simulation_Rules_v0_2.md`](../docs/specs/Simulation_Rules_v0_2.md)
- [`docs/specs/Water_Algorithm_v0_2.md`](../docs/specs/Water_Algorithm_v0_2.md)
- [`docs/specs/Input_Feel_v0_2.md`](../docs/specs/Input_Feel_v0_2.md)
- [`AGENT_OS.md`](AGENT_OS.md) (change control; no silent spec edits)
- [`change-proposal-template.md`](change-proposal-template.md) (required for spec/architecture changes)

---

## 1) Contract surfaces (what must be versioned)

### 1.1 Rules engine (simulation behavior)
- The deterministic rules defined in v0.2:
  - tick model, resolve order, tie-break ordering
  - solids settling
  - water equilibrium solver
  - drains/freeze
  - objectives/fail states

### 1.2 Level data
- Level JSON schema and all semantics used by the rules engine.
- **Numeric representation rule:** any numeric value that participates in Core gameplay logic must be an integer.
- **Time rule:** all durations/intervals stored in level JSON are **integer ticks** at `TICK_HZ=60`.
- **Floats are forbidden** in level JSON for Core logic (authoring prose may refer to seconds).

### 1.3 Replay data
- Per-tick input stream format + header metadata needed to reproduce.

### 1.4 Determinism hash
- Canonical serialization and hashing algorithm (the “meaning” of the hash).

---

## 2) Version identifiers (required fields)

Use SemVer for all versions.

- **RulesVersion**: `MAJOR.MINOR.PATCH`  
  Example: `0.2.0` represents the rules from GDD v0.2.
- **LevelSchemaVersion**: `MAJOR.MINOR.PATCH`
- **ReplayVersion**: `MAJOR.MINOR.PATCH`
- **DeterminismHashVersion**: `MAJOR.MINOR.PATCH`

**Rule:** any serialized artifact (level/replay) must declare its version(s) explicitly in a top-level `meta`.

---

## 3) Breaking vs compatible changes

### 3.1 What is breaking?
A change is **breaking** if it can change the simulation outcome for an existing `(level, seed, inputs, rulesVersion)`.

Examples:
- any change to water solver ordering or passability/occupiability rules
- any change to tie-break ordering
- any change to solid settling component order or drop distance computation
- any change to input application order or lock rules
- any change to objective evaluation semantics

Additional breaking examples:
- Changing serialized time fields from seconds↔ticks or introducing floats into level JSON.

### 3.2 Allowed patch changes (RulesVersion PATCH)
PATCH changes are allowed only if:
- golden tests confirm no output changes for all existing golden scenarios, AND
- replay compatibility remains intact.

If outputs change, it’s NOT a patch change.

### 3.3 Minor vs major
- **MINOR**: adds new optional fields / features that can be ignored by old content, while keeping old behavior available.
- **MAJOR**: removes or changes meaning of existing fields/behavior, or makes old artifacts unplayable without migration.

---

## 4) Level JSON policy

### 4.1 Schema storage
- Store schemas in-repo (e.g., `schemas/level.schema.v0.2.0.json`).
- Validate levels against the schema in CI.

### 4.2 Level hashing (required for replay)
- Compute `levelHash` as a stable hash of canonical JSON:
  - UTF-8
  - sorted object keys
  - normalized numbers (integers as integers)
  - no insignificant whitespace
- Store `levelHash` in replay header; validator can recompute it.

### 4.3 Migrations
If `LevelSchemaVersion` is bumped in a breaking way:
- Provide a deterministic migrator tool:
  - input: old level JSON
  - output: new level JSON
- The migrator must be tested with golden fixtures.

### 4.4 No floats in level JSON (hard rule)
- Level JSON must not contain floating-point numbers for Core logic.
- Store time/durations as **integer ticks** (60 TPS). Example: prefer `intervalTicks` over `intervalSeconds`.
- Introducing a seconds-based numeric field in schema is considered **breaking** unless it is purely UI/Unity-side.

---

## 5) Replay policy

### 5.1 Replay must be self-contained
A replay header must include:
- `replayVersion`
- `rulesVersion`
- `levelId` + `levelHash`
- `seed`
- tick rate (expected 60)
- input stream encoding (e.g., list of per-tick action bitmasks or events)

### 5.2 Replay compatibility rule
A replay is only guaranteed to play back if:
- `rulesVersion` matches exactly, OR
- an explicit compatibility layer exists for the older rulesVersion.

Default: **exact rulesVersion match required**.

---

## 6) Determinism hash policy

### 6.1 Purpose
The determinism hash is the machine-checkable “same outcome” proof used by:
- golden tests
- replay playback verification
- CLI vs Unity parity tests

### 6.2 Canonical hashed state (minimum)
Hash must include at least:
- grid occupancy (cell types + material IDs + anchored flags + timers for ICE/stabilize if present)
- current gravity direction
- PRNG internal state
- objective progress + counters (pieces used, water removed total, shift/lost totals, rotations count)
- active piece state (origin + orientation + lock delay counters) IF the hash is computed mid-run

### 6.3 Canonical serialization constraints
- All integers serialized little-endian (or defined JSON canonical form).
- Stable ordering:
  - iterate cells by canonical `(x,y,z)` or by `(gravElev,tieCoord)` — pick one and document it; do not change it without version bump.
- No floating-point.
- Hash algorithm must be specified and pinned by version (e.g., SHA-256 or xxHash64).

### 6.4 Hash versioning
If the serialized fields or ordering changes:
- bump `DeterminismHashVersion`
- bump `RulesVersion` if outcomes can differ or verification semantics change

---

## 7) CI enforcement (non-negotiable)

CI must gate:
- schema validation for all levels
- golden tests
- replay record/playback equivalence
- determinism hash stability (for the same rulesVersion)

---

## 8) Change control (agent rule)
Any change to this policy or to the meaning of contracts must be accompanied by:
- a Change Proposal ([`change-proposal-template.md`](change-proposal-template.md))
- updates to schemas / migrators / golden fixtures as needed
