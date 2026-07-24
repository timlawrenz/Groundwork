# TODO

> Checkbox task tracking. Mark completed items with `[x]` and date. Keep this file focused on the near-term — long-term roadmap lives in `docs/design.md`.

## Phase 0 — Scaffolding & Architecture

- [x] Name the project → **Groundwork** (2026-07-23)
- [x] Create `groundwork` Hermes profile (2026-07-23)
- [x] Create repo structure: README, AGENTS.md, PROJECT_STATUS.md, TODO.md, docs/ (2026-07-23)
- [x] Canonical design doc in `docs/design.md` (2026-07-23)
- [x] Architecture doc in `docs/architecture.md` (2026-07-23)
- [x] ADR template in `docs/decisions.md` (2026-07-23)
- [ ] Scaffold Unity project with ECS packages
- [ ] Verify Unity project opens and builds to all 4 targets
- [ ] Push to GitHub (make repo public)

## Phase 1 — Simulation Core (MVP)

- [ ] Implement data model — Citizen, Building, Item, Map, Calendar as pure C# structs
- [ ] Implement sim loop skeleton — tick dispatch, entity iteration
- [ ] Implement first production chain: logs → firewood
- [ ] Implement citizen needs + pathfinding stub
- [ ] Headless test harness: tick 100 years, verify population stability (30-50 citizens)
- [ ] Success criterion met: stable population for 100 game-years

## Phase 2 — Content & Modding

- [ ] Content loader: Items.json, Buildings.json, Recipes.json
- [ ] Lua runtime integration
- [ ] Mod API hooks: on_init, on_tick, on_season_change, on_building_complete, etc.
- [ ] Sandbox validation: no filesystem, no network, no OS access
