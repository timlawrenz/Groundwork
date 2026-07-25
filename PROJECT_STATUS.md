# Project Status

> **Living pointer.** This file always tells you the current phase and the single next action. Update it whenever a phase completes or the next action changes.

## Current Phase

**Phase 1 — Simulation Core (MVP)** — nearly complete

13|13 systems, 293 tests green. Event buffer, public buildings, production archetypes (Workshop/Gathering/Source/Service), goods transport (CitizenHaulSystem + HaulCompletionSystem), gathering zone overlap, needs generalization (data-driven NeedDefinition + InitialUrgency). HTML dashboard (Field Notes Dark).

## Next Action

**Forestry hut (log producer)** — the firewood chain is incomplete without a log source. A forestry_hut (Gathering archetype, harvests logs from forest tiles) closes the loop: forestry_hut → woodcutter → house. This also validates that the generalized need system works for new building types.

## What's blocking Phase 1 closure

The firewood chain is incomplete: forestry_hut (log source) → woodcutter → house. A zero-stockpile 10-year run showed population crashing from 93 to 43 in one winter because no one produces logs. With needs generalization done, adding a forestry_hut is a pure data entry (new BuildingDefinition + GatheringZone) with no code changes needed.

## Headline

13 systems running. 272 tests green. Event buffer powers birth/death tracking, debug viz, and eventual mod hooks. Population survives 10 years. Firewood distribution gap is the remaining bottleneck. Public buildings + goods transport + needs generalization are the path to closure.

## Phase History

| Date | Phase | Outcome |
|---|---|---|
| 2026-07-25 | Phase 1 (public buildings) | CitizenNeedSystem refactored: buildings are public resources. Birth tracking via event buffer. Pipeline reorder: Death→DebugViz→Stats→EventDispatch. 272 tests green. |
| 2026-07-25 | Phase 1 (event buffer) | Deterministic event buffer implemented. 12 systems, 79 tests. Tile events, birth/death/production events. Debug viz (100-wide ASCII). Re-pathing. Co-location. Dashboard. |
| 2026-07-24 | Phase 1 (late) | Births, firewood consumption, continuous production, worker requirements, abundance bootstrap. 66 tests green. Stability bottleneck identified. |
| 2026-07-24 | Phase 1 (mid) | Stats system, food consumption, production fix, regression tests. 56 tests green. |
| 2026-07-24 | Phase 1 (early) | Core sim systems built: calendar, citizens, pathfinding, production, tick dispatch. 46 tests passing. |
| 2026-07-23 | Phase 0 | Repo, docs, architecture, Unity scaffold, ECS packages, minimum components. |
