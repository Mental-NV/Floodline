# Floodline — AGENT_OS (Canonical Agent Operating System)

**Purpose:** minimize context switching while keeping autonomous execution strict, deterministic, and verifiable.

## Table of contents
- [1) Canonical artifacts (ALWAYS read)](#1-canonical-artifacts-always-read)
- [2) Non-negotiable constraints](#2-non-negotiable-constraints)
- [3) Milestones (order is mandatory)](#3-milestones-order-is-mandatory)
- [4) Backlog item requirements (for adding/splitting)](#4-backlog-item-requirements-for-adding-splitting)
- [5) Role loop (run sequentially per item)](#5-role-loop-run-sequentially-per-item)
- [6) Execution loop (repeat until backlog complete)](#6-execution-loop-repeat-until-backlog-complete)
- [7) Gates (canonical)](#7-gates-canonical)
- [8) Standard validation entrypoints (preferred)](#8-standard-validation-entrypoints-preferred)
- [9) Autonomy acceptance criteria (MVP targets)](#9-autonomy-acceptance-criteria-mvp-targets)

---

## 1) Canonical artifacts (ALWAYS read)

1) [`docs/GDD_Core_v0_2.md`](../docs/GDD_Core_v0_2.md)
   Player-facing design and product framing.

2) [`docs/specs/Simulation_Rules_v0_2.md`](../docs/specs/Simulation_Rules_v0_2.md)
   Determinism-critical simulation rules (resolve order, solids, objectives, hazards).

3) [`AGENT_OS.md`](AGENT_OS.md) (this file)  
   The only canonical agent workflow + gates + milestone order.

4) [`backlog.json`](backlog.json)  
   The only canonical work state (DONE / CURRENT / NEXT) and evidence log.

**Rule:** open any other file only if the CURRENT backlog item’s `requirementRef` explicitly points to it.

**Precedence:** contracts/schemas > tests (incl. golden) > code > docs prose.

---

## 2) Non-negotiable constraints

### 2.1 Determinism (hard rule)
<a id="determinism"></a>

- Core simulation must be deterministic given `(level, seed, per-tick inputs, rulesVersion)`.
- **No floating-point** in Core gameplay solver logic (solids/water/objectives).
- **No floating-point in serialized gameplay data** (level JSON, replay). Store time/durations as integer ticks (`TICK_HZ=60`).
- Canonical ordering and tie-breaks are **mandatory**; do not invent orderings.
- Resolve Phase ordering must match the GDD and any explicit tie-break rules referenced by the backlog.

### 2.2 Architecture split
- `Floodline.Core` must not reference `UnityEngine` (or any Unity package).
- Unity work is forbidden until milestone **M5**.

### 2.3 Strict .NET quality gates (high strictness)
<a id="strict-net-baseline"></a>

- Central Package Management is mandatory (`Directory.Packages.props`).
- Lock files mandatory; CI uses locked restore.
- Nullable enabled and enforced.
- `TreatWarningsAsErrors = true`.
- `EnforceCodeStyleInBuild = true`.
- Once introduced, formatting gate must pass:
  - `dotnet format --verify-no-changes`

### 2.4 Backlog truth + WIP discipline (hard rule)
- Canonical backlog: [`backlog.json`](backlog.json).
- Status enum: `New → InProgress → Done`.
- **WIP limit = 1**: at most one `InProgress` item at a time.
  - `0` is allowed only between items.
  - If any eligible `New` item exists (all `dependsOn` are `Done`), you MUST immediately set NEXT to `InProgress`
    before making other repo changes.

**Always be able to state:**
- DONE items (`Done`)
- CURRENT item (`InProgress`) if any
- NEXT item (lowest ID `New` with all dependencies `Done`)

### 2.5 Change control (no silent spec drift)
If design/architecture must change:
- Create a Change Proposal using [`change-proposal-template.md`](change-proposal-template.md)
- Add a Change Proposal backlog item with `requirementRef` to the relevant GDD section(s)
- Do not silently edit behavior and “fix docs later”.

### 2.6 Scope control
- Implement ONLY what the current backlog item requires.
- No “while I’m here” refactors.
- If ambiguous: add a Clarify / Change Proposal item; do not invent behavior.

### 2.7 Security / secrets
- Never commit secrets, tokens, keys, passwords, private personal data.

---

## 3) Milestones (order is mandatory)
<a id="milestones"></a>

**Do not reorder milestones:** `M0 → M1 → M2 → M3 → M4 → M5`  
You may NOT start `M(N+1)` until `M(N)` exit criteria are satisfied.

<a id="milestone-m0"></a>
### M0 — Repo Baseline (strict .NET + CI)
**Exit criteria**
- `dotnet restore` uses lock file and passes in locked mode
- `dotnet build -c Release` passes with zero warnings
- `dotnet test -c Release` passes
- formatting gate passes once introduced (`dotnet format --verify-no-changes`)

<a id="milestone-m1"></a>
### M1 — Core Sim + CLI Runner (no visuals)
**Exit criteria**
- CLI runs a minimal level end-to-end and outputs a final state summary
- Core remains Unity-free

<a id="milestone-m2"></a>
### M2 — Golden Tests (Resolve + Water + Objectives)
**Exit criteria**
- Golden suite passes in Release on Windows
- At least one negative test per subsystem

<a id="milestone-m3"></a>
### M3 — Replay Format + Determinism Hash
**Exit criteria**
- Recorded replay replays to identical determinism hash in CI
- Replay versioning rules enforced (see [`contract-policy.md`](contract-policy.md) **only if referenced by backlog**)

<a id="milestone-m4"></a>
### M4 — Level Schema + Validator + Campaign Validation
**Exit criteria**
- Validator passes for all campaign levels in CI
- Errors actionable (file + JSON path + rule id)

<a id="milestone-m5"></a>
### M5 — Unity Client Shell (last)
**Exit criteria**
- CLI and Unity produce identical determinism hashes for same replay/seed/level
- Basic playability for early campaign levels

---

## 4) Backlog item requirements (for adding/splitting)
<a id="backlog-item-format"></a>

You may add/split backlog items ONLY:
- before starting a new item (Plan window), OR
- when CURRENT item is blocked and you need an enabler/bugfix/change-proposal item.

Allowed reasons:
- missing infrastructure discovered while executing
- bug uncovered by tests/golden regressions
- task too large → split
- spec/design mismatch → Change Proposal

Every new backlog item MUST include:
- `id`, `title`, `milestone`, `status`, `dependsOn`
- `requirementRef` (exact GDD section anchor preferred; or failing test reference)
- `rationale`
- `validation.commands` (prefer the scripts in [`scripts/`](../scripts/))
- `definitionOfDoneRef` (usually [`AGENT_OS.md#dod`](#dod))
- evidence fields (`evidence.commandsRun`, `evidence.notes`)

**No `requirementRef` = scope creep = do not add.**

---

## 5) Role loop (run sequentially per item)

Even as a single agent, run roles in this order:

### Planner
Outputs:
- confirm DONE / CURRENT / NEXT
- verify sizing; split/add enablers only if allowed
- confirm milestone consistency and dependencies

Prohibitions:
- no code changes

### Architect
Outputs:
- module boundaries and invariants touched
- ADR-style decision note if needed (brief)
- contract/versioning decisions if needed

Prohibitions:
- no large refactors unless justified by a Change Proposal

### Implementer
Outputs:
- minimal change set that satisfies the item deliverables
- tests + documentation updates required by rules

Prohibitions:
- no unrequested refactors
- no silent design edits

### Verifier
Outputs:
- runs the item’s validation commands
- ensures gates satisfied
- flags drift / weak tests / contract mismatches / scope creep

Prohibitions:
- no feature additions

---

## 6) Execution loop (repeat until backlog complete)
<a id="execution-loop"></a>

### Step 0 — Preflight (each session)
- Ensure working tree is clean (no uncommitted changes).
- Read the 4 canonical artifacts (Core GDD, Simulation Rules, AGENT_OS, backlog).
- Confirm there is at most one `InProgress` item.
- Run:
  - [`pwsh ./scripts/preflight.ps1`](../scripts/preflight.ps1)

### Step 1 — Select work
- If there is a CURRENT `InProgress` item: continue it.
- Else select NEXT = lowest ID `New` item with all `dependsOn` = `Done`.

### Step 2 — Start work (mandatory backlog-only commit)
- Set item `status=InProgress`, set `startedAt` (UTC ISO 8601).
- Commit this backlog-only change immediately (no code yet):
  - `FL-XXXX: start <short title>`

### Step 3 — Implement and verify
- Implement per constraints.
- Run validation commands exactly as listed in backlog item:
  - prefer [`pwsh ./scripts/ci.ps1`](../scripts/ci.ps1)
- Record commands + results in item evidence.

### Step 4 — Finish
Only if gates are satisfied:
- set `status=Done`, set `doneAt` (UTC ISO 8601)
- append evidence
- Create a separate git branch
- commit:
  - `FL-XXXX: <short title>`
- Create a PR (GitHub)

### Step 5 — If blocked
- Keep `status=InProgress`.
- Write a Blocking Note into evidence:
  - what failed
  - 2–3 options
  - recommended option + why
  - what info is needed
- If allowed, add an enabler/bugfix/change-proposal backlog item with `requirementRef`.

---

## 7) Gates (canonical)
<a id="dod"></a>

### Always (once solution exists)
- restore (locked mode when applicable)
- build `Release`
- unit tests `Release`

### Formatting gate (once introduced)
- `dotnet format --verify-no-changes`

### Milestone-specific additions
- M2+: golden/snapshot tests
- M3+: replay record/playback + determinism hash checks
- M4+: schema + semantic validation for levels/campaign
- M5+: Unity EditMode + PlayMode tests (smoke suite minimum)

---

## 8) Standard validation entrypoints (preferred)

Use scripts to reduce per-item command drift:

- [`pwsh ./scripts/preflight.ps1`](../scripts/preflight.ps1)
- [`pwsh ./scripts/ci.ps1 -Scope Always -LockedRestore:$false`](../scripts/ci.ps1)
- [`pwsh ./scripts/ci.ps1 -Scope M0 -UseLockFile`](../scripts/ci.ps1)
- [`pwsh ./scripts/ci.ps1 -Scope M0 -IncludeFormat`](../scripts/ci.ps1)
- [`pwsh ./scripts/ci.ps1 -Scope M1`](../scripts/ci.ps1)

**Reserved switches (placeholders):** `-Golden`, `-Replay`, `-ValidateLevels`, `-Unity`  
These flags should become enforcing gates only after the related projects/tests/tools exist.


## 9) Autonomy acceptance criteria (MVP targets)
<a id="autonomy-acceptance"></a>

The autonomous build is considered **MVP complete** when **milestone exit criteria** for **M1–M4** are satisfied, and Unity work starts only at **M5**.

In practice this means:
- Headless deterministic sim exists (Core + CLI) and produces stable outcome summaries.
- Golden tests lock Resolve + Water + Objectives behavior.
- Replay format exists and replays to identical determinism hash.
- Level schema + validator exists; campaign validates in CI.
- Unity shell (M5) matches determinism hashes with Core/CLI for the same replay/seed/level.

(These criteria intentionally mirror the Milestones section to avoid introducing a second rules bible.)
