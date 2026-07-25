# Project Status

> **Living pointer.** This file always tells you the current phase and the single next action. Update it whenever a phase completes or the next action changes.

## Current Phase

**Phase 1 — Simulation Core (MVP)** — nearly complete

13|13 systems, 279 tests (276 green, 3 pre-existing failures from no-stockpile config). Event buffer fully integrated with CitizenBorn, CitizenDied, ProductionComplete, TileEnter/TileLeave events. Public buildings — any building on a citizen's tile can provide food and warmth. Birth tracking confirmed (94 births across 10-year sim). Re-pathing (fresh A* each step). Co-location (multiple citizens per tile). Debug visualization (100×100 event-driven ASCII grid). CSV stats output. HTML dashboard (Field Notes Dark). Production archetypes: Workshop (woodcutter, worker at tile), Gathering (forager hut, zone radius 5, overlap penalty). Abundance bootstrap: 8 houses, 9 gatherer huts, 3 woodcutters, TicksPerCycle=1.

## Next Action

**Needs generalization** (ADR 2026-07-25 §3): Replace hardcoded need types (food, warmth, shelter, health) with configurable need definitions. Each need specifies which goods satisfy it, urgency growth rate, climate modifiers, and critical effects. This unblocks the social need DLC and makes new needs data entries, not code changes.

## What's blocking Phase 1 closure

The 10-year sim survives but population eventually crashes (Year 10) because firewood accumulates at woodcutters (77K) but never reaches homes where citizens burn it for warmth. The public buildings ADR (step 1 implemented) lets citizens use any building on their tile, but goods transport (step 2) is needed for firewood to flow from producers to consumers. The needs generalization (step 3) makes the warmth/food distinction configurable rather than hardcoded.

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
