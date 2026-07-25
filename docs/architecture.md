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

### Unity ECS (DOTS)

Data-oriented design for thousands of entities on tablet CPUs. Citizens are archetypes, not GameObjects.

- **Entities**: lightweight IDs, not objects
- **Components**: pure data structs (CitizenData, BuildingData, ItemData)
- **Systems**: stateless functions that iterate components (CitizenAgeSystem, ProductionSystem, PathfindingSystem)

### Command Pattern

All mutations flow through the command bus. UI submits commands, sim processes them.

```
Touch UI ──→ Command Bus ──→ Simulation Engine
Mouse+KB ──→              ──→
Test Harness ──→          ──→
```

Commands:
- `PlaceBuilding(building_type, x, z)`
- `AssignWorker(citizen_id, building_id)`
- `SetSpeed(1x | 2x | 4x | pause)`
- `Select(x, z)`
- `Pan(dx, dz)`
- `Zoom(delta)`

**Benefits:**
- UI decoupled from simulation
- Commands can be recorded, replayed, networked later
- Test harness drives the sim through the same API a player uses

### Deterministic Simulation

Single-threaded, citizens tick in ID order. Reproducible and debuggable.

- No race conditions
- Save files are command logs — replay from initial state = load game
- Bisect bugs by replaying command logs

### Stateless Renderer

Renderer reads a read-only `GameSnapshot` each frame. Simulation never touches rendering.

```
GameSnapshot {
  citizens: [CitizenState],
  buildings: [BuildingState],
  items: [ItemState],
  map: TileState[][],
  calendar: CalendarState,
  trade_routes: [TradeRouteState],
  camera: CameraState,
  selection: SelectionState,
  ui_state: UIState
}
```

### Tile-Based Map

Not freeform. Simplifies:
- Pathfinding (grid-based A* or flow field)
- Building placement (snap to grid)
- Adjacency rules (buildings affect neighboring tiles)
- Resource distribution (trees on forest tiles, ore on mountain tiles)

## Simulation Loop Detail

Every tick (configurable, e.g., 1 game-hour = 1 tick at 1x speed):

1. **Season tick** — advance calendar, update temperature/daylight/growing conditions
2. **Citizen tick** — for each citizen: age check → need evaluation → task selection → path step → inventory update
3. **Building tick** — for each building: consume inputs → progress production → output goods → worker attendance check
4. **Resource tick** — spoilage check on stored items, crop growth on farm tiles
5. **Trade tick** — advance trade routes, check arrivals, deliver shipments
6. **Event dispatch** — process SimulationEvent buffer: invoke Lua mod hooks for subscribed event types, clear buffer for next tick
7. **Death & birth** — check health thresholds, age limits, population reproduction

### Event Buffer

Systems emit `SimulationEvent` entries into a singleton `DynamicBuffer<SimulationEvent>` during their update. `EventDispatchSystem` runs late in the pipeline, processes all events in emission order, and clears the buffer. This is the mechanism behind mod hooks — internal systems and mods subscribe to the same event stream. See ADR 2026-07-25.

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

**Lua runtime:**
- MoonSharp or NLua embedded in Unity
- Sandboxed: no filesystem, no network, no OS calls
- Hooks registered in `init.lua`, called by the simulation engine at appropriate tick phases

**Mod API surface:**
- `SpawnItem(item_id, position, quantity)`
- `AddNeed(citizen_id, need_type, urgency)`
- `GetAllCitizens()`, `GetAllBuildings()`
- `GetBuilding(id)`, `GetCitizen(id)`
- `Log(message)` — writes to mod debug console
- `GetSeason()`, `GetYear()`, `GetDaylightHours()`
