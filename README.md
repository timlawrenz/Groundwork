# Groundwork

A cross-platform, moddable, touch-first city-builder with a pure simulation core and no combat.

**Spiritual successor to Banished** — peaceful cooperative economy, deep production chains, individual citizen simulation.

[![Tests](https://img.shields.io/badge/tests-79%2F79-brightgreen)](PROJECT_STATUS.md)
[![Phase](https://img.shields.io/badge/phase-1%20%E2%80%94%20Simulation%20Core-blue)](PROJECT_STATUS.md)

## Design Pillars

- **Architecture-first** — simulation engine ships before any UI
- **Moddable from day 1** — content in JSON, behaviors in Lua, mod API == internal API
- **Cross-platform** — Windows, Linux, WebGL from one codebase
- **Touch-first UX** — designed for fingers on glass, keyboard+mouse as secondary
- **No warfare, no enemies** — all depth from economy, seasons, citizen well-being, trade

## Tech Stack

| Layer | Technology |
|---|---|
| Engine | Unity ECS (DOTS) — Entities 1.x, ISystem, Burst |
| Content | JSON definitions (hardcoded in ContentLoaderSystem, JSON files planned) |
| Scripting | Lua (MoonSharp / NLua) — sandboxed runtime, planned |
| Build | Unity 6000.3.20f1, one target toggle per platform |
| Testing | 79 EditMode tests, Unity Test Framework, SimulationTestWorld |

## Architecture

### Deterministic Simulation Engine

12 systems run in a fixed pipeline order each tick (1 game-hour):

```
ContentLoader → Bootstrap → Tick → Calendar → Births → Age → Needs
→ Pathfinding → Movement → Production → Death → DebugViz → EventDispatch → Stats
```

- **Deterministic**: single-threaded, citizens tick in ID order — reproducible and debuggable
- **Data-oriented**: entities are archetypes, not GameObjects (ECS DOTS)
- **Command pattern**: all mutations flow through a command bus; UI is decoupled from sim
- **Stateless renderer**: renderer reads game state snapshot each frame; sim never touches rendering

### Event Buffer

All system notifications flow through a deterministic event buffer (`DynamicBuffer<SimulationEvent>`):

| Event | Emitted By | When |
|---|---|---|
| `TileEnter` / `TileLeave` | `CitizenMovementSystem` | Each step |
| `CitizenBorn` | `BirthSystem` | New child created |
| `CitizenDied` | `DeathSystem` | Citizen dies |
| `ProductionComplete` | `BuildingProductionSystem` | Recipe cycle finishes |
| `BuildingPlaced` / `CitizenSpawned` | `SimulationBootstrap` | World initialization |

Events are processed by `EventDispatchSystem` in emission order, then cleared. This is the foundation for the Lua mod API — internal systems and mods subscribe to the same event stream. See ADR 2026-07-25.

### Re-pathing

Citizens recalculate their optimal A* path after every step. The `CitizenMovementSystem` clears remaining waypoints and issues a fresh `PathRequest` to the original destination each tick. `PathfindingSystem` fills a new path next tick — ensuring routes stay optimal even if the world changes.

### Co-location

Multiple citizens can occupy the same tile. The `DebugVizSystem` tracks per-tile citizen counts and renders digits (1-9, or `+` for 10+).

## Simulation State

### Current MVP

| Resource | Building | Count | Production |
|---|---|---|---|
| Food | Gatherer Hut | 9 | 1 food/tick/hut (9 food/tick total) |
| Firewood | Woodcutter | 3 | 1 firewood/tick (consumes 1 log, 3 firewood/tick total) |
| Shelter | House | 8 | — |

- **50 citizens** (25 male, 25 female) on a 100×100 flat temperate map
- **6 woodcutters**, **44 gatherers** at start
- Births: eligible females produce 1 child/year; population grows to ~92 before resource pressure sets in
- Death: citizens die when health reaches 0 from unmet needs (food, warmth, shelter, health, social)

### Debug Visualization

`DebugVizSystem` renders a purely event-driven ASCII grid of the simulation world to `/tmp/groundwork_viz.txt`. It never queries component data — it builds its world state entirely from `BuildingPlaced`, `CitizenSpawned`, `TileEnter`, `TileLeave`, and `CitizenDied` events.

```
Tick 1 (bootstrap)          Tick 5 (walking)            Tick 31 (settled)
| ....B.B.B.B.B.B.B.B.B. |  | ..BBBBBBBB............ |  | ..6.6.5.5.5.5.6.6..3.3. |
| ..B1111111111111111... |  | ...11111212.1.11111... |  | ..BBBBBBBB............ |
| ...1111111111111111... |  | ...11111212.1.11111... |
```

Run with: `python3 scripts/animate_viz.py --run`

## Project Status

See [`PROJECT_STATUS.md`](PROJECT_STATUS.md) for the current phase and next action.
See [`TODO.md`](TODO.md) for the task board.

**Current phase**: Phase 1 — Simulation Core (MVP), nearly complete.
**Tests**: 79/79 green.
**Next**: 100-year stability test → Lua runtime → Mod API hooks → Renderer.

## Quick Start

```bash
# Run all tests
~/Unity/Hub/Editor/6000.3.20f1/Editor/Unity \
  -runTests -batchmode -nographics \
  -testPlatform EditMode \
  -projectPath /home/tim/source/activity/Groundwork

# Run headless simulation
~/Unity/Hub/Editor/6000.3.20f1/Editor/Unity \
  -batchmode -nographics -quit \
  -executeMethod Groundwork.Simulation.HeadlessRunner.Run \
  -projectPath /home/tim/source/activity/Groundwork

# Animate debug viz
python3 scripts/animate_viz.py --run
```

## Docs

- [`docs/design.md`](docs/design.md) — Game design: entities, simulation loop, MVP scope
- [`docs/architecture.md`](docs/architecture.md) — System boundaries, ECS design, command bus, event buffer
- [`docs/decisions.md`](docs/decisions.md) — Architecture Decision Records
- [`docs/unity-features.md`](docs/unity-features.md) — Unity built-in feature catalog

## License

TBD — likely GPLv3 or MIT.
