# Project Status

> **Living pointer.** This file always tells you the current phase and the single next action. Update it whenever a phase completes or the next action changes.

## Current Phase

**Phase 1 — Simulation Core (MVP)** — nearly complete

11 systems, 66 tests green. All core mechanics operational: births, aging, needs (food/warmth/shelter/social/health), food production, food consumption (personal→workplace→home), firewood production, firewood consumption, worker requirements, seasonal logging. Abundance bootstrap with 8 houses, 8 gatherer huts, 1 woodcutter.

## Next Action

**Phase 2 — Content system:** Replace hardcoded buildings/recipes with data files (`Buildings.json`, `Recipes.json`). This unblocks economic tuning for the 100-year stability test, which currently hits a firewood supply bottleneck that can't be resolved without configurable production rates.

## What's blocking Phase 1 closure

The 100-year stability test shows population growth (50→73→96 in 2 years via births) but crashes by Year 3 because firewood production (0.1/tick, 1 woodcutter) can't keep up with consumption (~30-40 firewood/citizen/year). Fixing this requires either:

1. **Tweak hardcoded values** — set `chop_firewood` to 0.5/tick and give 50,000 logs. Simple but wrong layer for this decision.
2. **Content system (Phase 2)** — move production rates to `Recipes.json`, where balancing becomes a data entry task.

**Recommendation: #2.** The mechanics work. The bottleneck is a content-tuning problem, not an engineering one.

## Headline

11 systems running. 66 tests green. Births work. Food loop works. Firewood loop works. Worker requirements work. Stability blocked by firewood production rate — content system next.

## Phase History

| Date | Phase | Outcome |
|---|---|---|
| 2026-07-24 | Phase 1 (late) | Births, firewood consumption, continuous production, worker requirements, abundance bootstrap. 66 tests green. Stability bottleneck identified: firewood tuning. |
| 2026-07-24 | Phase 1 (mid) | Stats system, food consumption, production fix, regression tests. 56 tests green. |
| 2026-07-24 | Phase 1 (early) | Core sim systems built: calendar, citizens, pathfinding, production, tick dispatch. 46 tests passing. |
| 2026-07-23 | Phase 0 | Repo, docs, architecture, Unity scaffold, ECS packages, minimum components. |
