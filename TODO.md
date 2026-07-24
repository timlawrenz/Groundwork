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

- [x] Implement data model — Citizen, Building, Item, Map, Calendar as pure C# structs (2026-07-23)
- [x] Implement sim loop skeleton — tick dispatch, system ordering, bootstrap (2026-07-23)
- [x] Implement first production chain: logs → firewood (2026-07-23)
- [x] Implement citizen needs system (2026-07-23)
- [x] Implement pathfinding (A* on grid) + citizen movement (2026-07-23)
- [x] Implement citizen aging & death (2026-07-24)
- [x] Test infrastructure + TDD ADR + 46 tests for all 10 systems (2026-07-24)
- [x] Simulation stats system — population, resources, seasonal logging (2026-07-24)
- [x] 52 tests all green (2026-07-24)
- [ ] Headless test harness: tick 100 years, verify population stability (30-50 citizens)
- [ ] Success criterion met: stable population for 100 game-years

## Phase 2 — Content & Modding

- [ ] Content loader: Items.json, Buildings.json, Recipes.json
- [ ] Lua runtime integration
- [ ] Mod API hooks: on_init, on_tick, on_season_change, on_building_complete, etc.
- [ ] Sandbox validation: no filesystem, no network, no OS access
