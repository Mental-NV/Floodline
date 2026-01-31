# Floodline — Backlog

**Canonical source of truth:** `/.agent/backlog.json`  
This Markdown is a human-readable view; the agent must update **JSON** statuses.

## Status workflow (mandatory)
- Initial status for all items: **New**
- When starting an item: set status to **InProgress**
- When finished: set status to **Done**
- **WIP limit = 1**: exactly one item may be InProgress at a time.

## How to pick what’s next
Pick the **lowest ID** item with:
- `status == New`, and
- all `dependsOn` items have `status == Done`.

## Backlog (summary)
| ID | Status | Milestone | Epic | Title | DependsOn |
| --- | --- | --- | --- | --- | --- |
| FL-0001 | New | M0 | E0 | Create solution + project skeletons | - |
| FL-0002 | New | M0 | E0 | Enable Central Package Management + locked restore | FL-0001 |
| FL-0003 | New | M0 | E0 | Strict analyzers + formatting gates (warnings-as-errors) | FL-0002 |
| FL-0101 | New | M1 | E1 | Core primitives: coords, gravity, ordering | FL-0003 |
| FL-0102 | New | M1 | E1 | Grid model + occupancy types | FL-0101 |
| FL-0110 | New | M1 | E1 | CLI runner (level + scripted inputs) | FL-0102 |

## Per-item details
For details (deliverables, validation commands, evidence), open `/.agent/backlog.json`.
