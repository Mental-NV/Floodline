# Floodline Unity Client (M5 Scaffold)

This is the placeholder Unity project root for Milestone M5. Constraints:

- No references from Floodline.Core to Unity assemblies.
- Unity layer will consume the compiled Core assembly via assembly definition/package.
- Determinism remains in Core; Unity is view/controller only.

Initial structure (scaffold):

- `Assets/` – game assets, scenes, scripts (to be added in FL-0501+).
- `Packages/manifest.json` – Unity package manifest (kept minimal for now).
- `ProjectSettings/` – Unity project settings (created when the project is opened in a specific Editor version).

Next items in M5 will populate this with input, HUD, parity tests, and CI hooks. Until then, this folder exists to reserve the path and keep .gitignore rules stable.
