# Project Status

> **Living pointer.** This file always tells you the current phase and the single next action. Update it whenever a phase completes or the next action changes.

## Current Phase

**Phase 1 — Simulation Core (MVP)** — nearly complete

12 systems, 78 tests green. Event buffer fully integrated: TileEnter/TileLeave from movement, CitizenBorn from births, CitizenDied from deaths, ProductionComplete from production. All core mechanics operational: births, aging, needs (food/warmth/shelter/social/health), food production, food consumption (personal→workplace→home), firewood production, firewood consumption, worker requirements, seasonal logging. Abundance bootstrap with 8 houses, 9 gatherer huts, 3 woodcutters.

## Next Action

**Implement the event buffer** (ADR 2026-07-25): Add `SimulationEvent` buffer component + `EventDispatchSystem` to the pipeline. This is a pre-requisite for the Lua mod API and unblocks reactive features (achievements, quests, UI notifications). Lightweight (~50 lines of C#), test-driven, fits between Death and Stats in the pipeline. After this, the 100-year stability test can close out Phase 1.

## What's blocking Phase 1 closure

The 100-year stability test shows population growth (50→73→96 in 2 years via births) but crashes by Year 3 because firewood production (0.1/tick, 1 woodcutter) can't keep up with consumption (~30-40 firewood/citizen/year). The content system (Phase 2) is now in place — production rates live in `Recipes.json`. Tuning firewood production is a data entry task, and the event buffer ADR (2026-07-25) establishes the pattern for mod hooks.

## Headline

12 systems running. 78 tests green. Event buffer: births, deaths, production, tile transitions all emit events. Births work. Citizens walk. Food loop works. Firewood loop works. Worker requirements work. Stability blocked by firewood production rate — content system (Recipes.json) unblocks tuning; event buffer unblocks reactive features.

## Phase History

| Date | Phase | Outcome |
|---|---|---|
| 2026-07-24 | Phase 1 (late) | Births, firewood consumption, continuous production, worker requirements, abundance bootstrap. 66 tests green. Stability bottleneck identified: firewood tuning. |
| 2026-07-24 | Phase 1 (mid) | Stats system, food consumption, production fix, regression tests. 56 tests green. |
| 2026-07-24 | Phase 1 (early) | Core sim systems built: calendar, citizens, pathfinding, production, tick dispatch. 46 tests passing. |
| 2026-07-23 | Phase 0 | Repo, docs, architecture, Unity scaffold, ECS packages, minimum components. |
