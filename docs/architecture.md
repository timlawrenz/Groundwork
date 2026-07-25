# Architecture

> System boundaries, component design, and simulation engine architecture.

## System Boundaries

```
┌─────────────────────────────────────────────┐
│              Interface Layer                 │
│  (touch, mouse+KB, headless test harness)    │
│  Input: gestures → commands                  │
│  Output: game state → render commands        │
├─────────────────────────────────────────────┤
│              Command Bus                     │
│  PlaceBuilding, AssignWorker, SetTradeRoute  │
│  All mutations flow through here             │
├─────────────────────────────────────────────┤
│           Simulation Engine                  │
│  ┌──────────┐ ┌──────────┐ ┌─────────────┐  │
│  │ Citizen  │ │ Economy  │ │  Seasonal   │  │
│  │ State    │ │ Engine   │ │  Engine     │  │
│  │ Machine  │ │          │ │             │  │
│  └──────────┘ └──────────┘ └─────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌─────────────┐  │
│  │Pathfinding│ │Building │ │  Trade       │  │
│  │          │ │ Manager  │ │  Manager     │  │
│  └──────────┘ └──────────┘ └─────────────┘  │
├─────────────────────────────────────────────┤
│              Content Loader                  │
│  Items.json, Buildings.json, Recipes.json   │
│  Maps/*.json, Citizens/templates.json       │
├─────────────────────────────────────────────┤
│              Mod API / Lua Runtime           │
│  Hooks: on_tick, on_season_change,           │
│  on_building_complete, on_citizen_born,      │
│  on_citizen_died, on_trade_arrival           │
│  Mods can: add items, buildings, recipes,    │
│  citizens, maps; inject behaviors            │
└─────────────────────────────────────────────┘
```

## Key Architectural Decisions

See [`docs/decisions.md`](docs/decisions.md) for the full ADR log.

| Decision | Date | Summary |
|---|---|---|
| Unity ECS (DOTS) | 2026-07-23 | Entities, not GameObjects. Data-oriented, Burst-compatible. |
| JSON for Content, Lua for Behavior | 2026-07-23 | Content in JSON, game logic in sandboxed Lua. |
| Command Pattern | 2026-07-23 | All mutations via command bus. UI decoupled from sim. |
| Deterministic Simulation | 2026-07-23 | Single-threaded, ID-order. Reproducible and debuggable. |
| Tile-Based Map | 2026-07-23 | Grid-based. Simplifies pathfinding and placement. |
| TDD Mandate | 2026-07-23 | All simulation code test-driven. |
| Deterministic Event Buffer | 2026-07-25 | Lightweight ECS buffer for notification flow. Commands mutate, events notify. |
| Public Buildings & Goods Transport | 2026-07-25 | 3-stage: public buildings → citizen hauling → needs generalization. |

## Simulation Pipeline

Thirteen systems run in fixed order each tick (1 game-hour):

```
TickDispatch → Calendar → Birth → Age → Needs → Pathfinding → Movement
→ Production → Death → DebugViz → Stats → EventDispatch
```

ContentLoader and Bootstrap run once at startup, outside the tick loop.

### Actual System Files

| System | File | Purpose |
|---|---|---|
| `TickDispatchSystem` | `Assets/Scripts/Simulation/Systems/TickDispatchSystem.cs` | Advances CurrentTick |
| `CalendarSystem` | `Assets/Scripts/Simulation/Systems/CalendarSystem.cs` | Day/season/year, temperature, daylight |
| `BirthSystem` | `Assets/Scripts/Simulation/Systems/BirthSystem.cs` | Creates children for eligible females |
| `CitizenAgeSystem` | `Assets/Scripts/Simulation/Systems/CitizenAgeSystem.cs` | Ages citizens, applies Child/Elderly tags |
| `CitizenNeedSystem` | `Assets/Scripts/Simulation/Systems/CitizenNeedSystem.cs` | Food/warmth consumption, need growth, health decay |
| `PathfindingSystem` | `Assets/Scripts/Simulation/Systems/PathfindingSystem.cs` | A* on grid, fills PathFollowing buffers |
| `CitizenMovementSystem` | `Assets/Scripts/Simulation/Systems/CitizenMovementSystem.cs` | Moves citizens one tile/tick, re-paths each step |
| `BuildingProductionSystem` | `Assets/Scripts/Simulation/Systems/BuildingProductionSystem.cs` | Consumes inputs, advances recipes, produces outputs |
| `DeathSystem` | `Assets/Scripts/Simulation/Systems/DeathSystem.cs` | Destroys entities tagged Dead |
| `DebugVizSystem` | `Assets/Scripts/Simulation/Systems/DebugVizSystem.cs` | Event-driven ASCII grid renderer |
| `SimulationStatsSystem` | `Assets/Scripts/Simulation/Systems/SimulationStatsSystem.cs` | Collects population/resources, counts births/deaths from events |
| `EventDispatchSystem` | `Assets/Scripts/Simulation/Systems/EventDispatchSystem.cs` | Processes event buffer, invokes Lua hooks, clears |
| `SimulationBootstrap` | `Assets/Scripts/Simulation/Systems/SimulationBootstrap.cs` | Creates initial world (run once) |
| `ContentLoaderSystem` | `Assets/Scripts/Simulation/Systems/ContentLoaderSystem.cs` | Creates recipe/building definitions (run once) |

## Event Buffer

Systems emit `SimulationEvent` entries into a singleton `DynamicBuffer<SimulationEvent>`. `SimulationStatsSystem` and `DebugVizSystem` read events before `EventDispatchSystem` clears them. This is the mechanism behind mod hooks — internal systems and mods subscribe to the same event stream. See ADR 2026-07-25.

**Event types in use:**

| Event | Emitter | Data |
|---|---|---|
| `CitizenBorn` | BirthSystem | EntityId=child.Index, Data0/1=position |
| `CitizenDied` | DeathSystem | EntityId, Data0/1=position |
| `ProductionComplete` | BuildingProductionSystem | EntityId=building.Index |
| `TileEnter` / `TileLeave` | CitizenMovementSystem | EntityId, Data0/1=position |
| `BuildingPlaced` | SimulationBootstrap | EntityId, Data0/1=position |
| `CitizenSpawned` | SimulationBootstrap | EntityId, Data0/1=position |

## Public Buildings

Per ADR 2026-07-25 §1, buildings are public resources. `CitizenNeedSystem` checks buildings at the citizen's current tile position (via `NativeHashMap<int2, Entity>` lookup) and consumes food/firewood from any building, not just the citizen's home or workplace. This is step 1 of the goods transport architecture — prevents citizens from freezing next to a stocked woodcutter.

## Re-pathing

`CitizenMovementSystem` clears remaining waypoints after each step and issues a fresh `PathRequest` for the original destination. `PathfindingSystem` fills a new optimal path next tick — ensuring routes stay optimal even if the world changes. Citizens effectively pause one tick per step for re-path.

## Co-location

Multiple citizens can occupy the same tile. `DebugVizSystem` tracks per-tile citizen counts and renders digits (1-9, or `+` for 10+).

## Headless Runner

`HeadlessRunner.cs` runs the simulation headless via `-executeMethod`. Produces CSV stats at `/tmp/groundwork_stats.csv` and debug viz at `/tmp/groundwork_viz.txt`.

```bash
Unity -batchmode -nographics -quit \
  -executeMethod Groundwork.Simulation.HeadlessRunner.Run \
  -projectPath /home/tim/source/activity/Groundwork
```

## Mod API Architecture

```
Mods/YourModName/
  items.json        — new item definitions
  buildings.json    — new building definitions
  recipes.json      — new production chains
  citizens.json     — citizen templates
  maps.json         — new map definitions
  init.lua          — behavior hooks
```

**Lua runtime:** MoonSharp or NLua embedded in Unity. Sandboxed: no filesystem, no network, no OS calls. Hooks registered in `init.lua`, called by `EventDispatchSystem` at end of each tick.

**Mod API surface:**
- `SpawnItem(item_id, position, quantity)`
- `AddNeed(citizen_id, need_type, urgency)`
- `GetAllCitizens()`, `GetAllBuildings()`
- `GetBuilding(id)`, `GetCitizen(id)`
- `Log(message)` — writes to mod debug console
- `GetSeason()`, `GetYear()`, `GetDaylightHours()`
