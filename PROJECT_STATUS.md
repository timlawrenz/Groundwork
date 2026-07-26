# Project Status

> **Living pointer.** This file always tells you the current phase and the single next action. Update it whenever a phase completes or the next action changes.

## Current Phase

**Phase 2 — Content & Modding**

13 systems, 293 tests green. Firewood production chain complete (forester → woodcutter → house), population stable at 43 for 100 years. Production archetypes, needs generalization, goods transport all done. APK built via Sidequest.

## Next Action

**Phase 2 complete** — JSON content files, Lua runtime (MoonSharp), mod API hooks, sandbox validation all done. 293 tests green. The project is feature-complete for Phase 2.

Next: **Phase 3** — Renderer + UI, or proceed to flesh out content (more buildings, recipes, items).

## What's blocking Phase 2

Content is hardcoded in C# — adding a building requires modifying and recompiling `ContentLoaderSystem`. JSON files make content data-driven, unblocking Lua scripting and the mod API.

## Headline

Phase 1 closed. 293 tests. Population stabilizes at 43 for 100 years with complete firewood chain. Phase 2 begins: JSON content files, Lua scripting, mod API.

## Phase History

| Date | Phase | Outcome |
|---|---|---|
| 2026-07-25 | Phase 1 (closure) | Forester hut closes firewood chain. Haulers-can-move bug fixed (HaulTask filter in MovementSystem). 100-year sim: 43 survivors, firewood flows. Phase 1 complete. |
| 2026-07-25 | Phase 1 (needs + hauling) | Needs generalization (data-driven NeedDefinition). Hauling tests + PathRequest fix. Haul delivery to InputInventory. Dedicated haulers in bootstrap. |
| 2026-07-25 | Phase 1 (production archetypes) | Workshop/Gathering/Source/Service archetypes. Gathering zone overlap penalty. Worker-at-tile and worker-in-zone checks. |
| 2026-07-25 | Phase 1 (event buffer) | Deterministic event buffer. Tile events, birth/death/production events. Debug viz. Re-pathing. Co-location. Dashboard. |
| 2026-07-24 | Phase 1 (late) | Births, firewood consumption, continuous production, worker requirements, abundance bootstrap. |
| 2026-07-24 | Phase 1 (mid) | Stats system, food consumption, production fix. |
| 2026-07-24 | Phase 1 (early) | Core sim systems: calendar, citizens, pathfinding, production, tick dispatch. |
| 2026-07-23 | Phase 0 | Repo, docs, architecture, Unity scaffold, ECS packages. |
