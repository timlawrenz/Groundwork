# TODO

> Checkbox task tracking. Mark completed items with `[x]` and date. Keep this file focused on the near-term — long-term roadmap lives in `docs/design.md`.

## Phase 0 — Scaffolding & Architecture

- [x] Name the project → **Groundwork** (2026-07-23)
- [x] Create `groundwork` Hermes profile (2026-07-23)
- [x] Create repo structure: README, AGENTS.md, PROJECT_STATUS.md, TODO.md, docs/ (2026-07-23)
- [x] Canonical design doc in `docs/design.md` (2026-07-23)
- [x] Architecture doc in `docs/architecture.md` (2026-07-23)
- [x] ADR template in `docs/decisions.md` (2026-07-23)
- [x] Unity feature catalog in `docs/unity-features.md` (2026-07-23)
- [x] Scaffold Unity project with ECS packages (2026-07-23)
- [x] Minimum ECS components — Citizen, Building, Item, Map, Calendar (2026-07-23)
- [x] Push to GitHub — [timlawrenz/Groundwork](https://github.com/timlawrenz/Groundwork) (2026-07-24)
- [ ] Verify Unity project opens and builds to all 4 targets
- [ ] Make repo public

## Phase 1 — Simulation Core (MVP)

- [x] Implement data model — Citizen, Building, Item, Map, Calendar (2026-07-23)
- [x] Implement sim loop skeleton — tick dispatch, system ordering, bootstrap (2026-07-23)
- [x] Implement first production chain: logs → firewood (2026-07-23)
- [x] Implement citizen needs system (2026-07-23)
- [x] Implement pathfinding (A* on grid) + citizen movement (2026-07-23)
- [x] Implement citizen aging & death (2026-07-24)
- [x] Test infrastructure + TDD ADR + 46 tests for all 10 systems (2026-07-24)
- [x] Simulation stats system — population, resources, seasonal logging (2026-07-24)
- [x] Fix: production orders now cycle continuously (2026-07-24)
- [x] Fix: citizens consume food from inventory to reduce food need (2026-07-24)
- [x] Birth system — eligible females produce 1 child/year (2026-07-24)
- [x] Firewood consumption — citizens burn firewood from home for warmth (2026-07-24)
- [x] Abundance bootstrap — 8 houses, 9 gatherer huts, 3 woodcutters (2026-07-24)
- [x] Headless test harness: HeadlessRunner (2026-07-25)
- [x] Event buffer: SimulationEvent + EventDispatchSystem (ADRs 2026-07-25)
- [x] Re-pathing: citizens recalculate A* each step (2026-07-25)
- [x] Co-location: multiple citizens per tile (2026-07-25)
- [x] Debug viz: event-driven ASCII grid (DebugVizSystem) (2026-07-25)
- [x] Birth/death tracking via event buffer (SimulationStatsSystem) (2026-07-25)
- [x] Public buildings: any building on citizen's tile provides resources (ADR 2026-07-25 §1) (2026-07-25)
- [x] HTML dashboard: Field Notes Dark, CSV stats input (2026-07-25)
- [x] Production archetypes: Workshop + Gathering with zone overlap (ADR 2026-07-25 §5) (2026-07-25)
- [x] Goods transport: hauling tests + PathRequest fix + lower thresholds (2026-07-25)
- [x] Needs generalization: config-driven need types (ADR 2026-07-25 §3) (2026-07-25)
- [x] Forestry hut: log producer to close firewood chain (2026-07-25)
- [x] Haul delivery to InputInventory: logs flow to woodcutters (2026-07-25)
- [x] Fix: haulers can move — removed HaulTask filter from MovementSystem (2026-07-25)
- [x] 100-year stability test: population stable at 43 for 100 years, firewood flows (2026-07-25)

## Phase 2 — Content & Modding

- [x] BuildingDefinition + RecipeDefinition components (2026-07-25)
- [x] ContentLoaderSystem — creates definition entities at startup (2026-07-25)
- [x] BuildingProductionSystem refactored: reads from definitions, no hardcoded recipes (2026-07-25)
- [x] SimulationStatsSystem: building counting is type-agnostic (2026-07-25)
- [x] Buffer types moved to Buffers/ folder per architecture conventions (2026-07-25)
- [x] SimulationBootstrap reads MaxWorkers from BuildingDefinitionData (2026-07-25)
- [x] Pipeline comments updated to reflect full system order (2026-07-25)
- [x] JSON content files (Items.json, Buildings.json, Recipes.json) — currently hardcoded in ContentLoaderSystem (2026-07-26)
- [x] Lua runtime integration: MoonSharp embedded, mods loaded from StreamingAssets/Mods (2026-07-26)
- [x] Mod API hooks: on_init, on_tick, on_season_change, on_event dispatched from EventDispatchSystem (2026-07-26)
- [x] Sandbox validation: Preset_SoftSandbox, no io/os/debug, exception-safe (2026-07-26)
