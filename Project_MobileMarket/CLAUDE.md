# Project Rules

Always respond in Korean.

## 0. Before Starting
Ask up to 3 clarifying questions if anything is unclear.

## 1. Architecture
- Central systems (state, sound, UI, etc.) go through Manager classes.
- Singleton allowed only on Managers (`GameManager.Instance`). Never on gameplay objects (player, enemies, etc.).
- Single Responsibility: one script, one job (e.g. split Player into Movement, Health, Inventory).

## 2. Object Communication
- No `GameObject.Find()`. Reference objects via Inspector assignment or Manager caching.
- Prefer events (`event` / `Action`) over tight coupling (e.g. Player fires a "hit" event, UI subscribes and updates itself).

## 3. Object References
- Prefer `GetComponent` over direct Inspector assignment, except in special cases.
- No `Find`-family hierarchy searches (runtime cost).
- No string comparisons for tags/names (perf + human error).

## 4. Resources & Assets
- Prefab-first: dynamic/repeated objects must be prefabs. Edit originals in Prefab Mode, not in-scene.
- Naming prefixes: `P_` prefab, `T_` texture, `M_` material.
- Place prefabs under `@Resources`, organized per prefab folder, so they're easy to swap.

## 5. No Per-Frame Allocation
No allocating new objects inside `Update`-like per-frame code, except when unavoidable.

## 6. Onboarding Prompt
Session-start template referencing this file and `Plan.md`:
> "We're starting Unity/C# development. CLAUDE.md has our architecture and coding conventions, Plan.md has the system design. Read both fully. Once understood, don't write code yet — summarize the core gameplay loop in 3 lines."

## 7. No New Folders
Work inside the existing `_Project` folder structure. Ask before creating any new folder.

## 8. Confirm Before Acting
Before starting a task, restate your understanding of the instruction and confirm it's correct.
