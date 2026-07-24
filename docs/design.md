# Game Design

> **Canonical design document for Groundwork.** This is the source of truth — the Obsidian note at `04 - projects/Groundwork.md` is a mirror.

## Overview

A cross-platform, moddable, touch-first city-builder with a pure simulation core and no combat. Designed as an open-source spiritual successor to Banished — peaceful cooperative economy, deep production chains, individual citizen simulation.

**Design pillars:**

1. **Architecture-first** — simulation engine ships before any UI
2. **Moddability from day 1** — content in JSON, behaviors in Lua, mod API == internal API
3. **Cross-platform** — Unity project with one build target toggle (Android, Windows, Linux, WebGL)
4. **Touch-first UX** — designed for fingers on glass, keyboard+mouse as secondary
5. **No warfare, no enemies** — all depth from economy, seasons, citizen well-being, trade

## Core Data Model

### Entities

**Citizen**
- id, name, age, sex, health, happiness, education_level
- current_task, current_location, destination
- home_id, workplace_id
- inventory: [{item_id, quantity}]
- needs: [{need_type, urgency}] — food, warmth, shelter, health, social

**Building**
- id, type, position (x, z, rotation), footprint_size
- construction_progress, is_operational
- workers: [citizen_id], max_workers
- inventory: [{item_id, quantity, capacity}]
- production_queue: [{recipe_id, progress, assigned_worker_id}]
- mod_data: {} — arbitrary key-value for mod extensions

**Item / Resource**
- id, name, category (raw, processed, food, tool, luxury)
- weight, spoilage_rate (days), base_value
- production_recipe: {inputs: [{item_id, quantity}], outputs: [{item_id, quantity}], duration_ticks}

**Map**
- width, height (tiles)
- tiles: [{x, z, terrain_type, elevation, fertility, resource_deposit}]
- water_bodies: [{center, radius, flow_direction}]
- climate_zone: temperate, boreal, tropical, arid

**Season / Calendar**
- year, season (spring, summer, autumn, winter)
- day_of_season, total_days
- temperature_range, precipitation_rate
- daylight_hours
- growing_multiplier — affects crop yield
- event_schedule: [{day, event_type}] — harvest festival, harsh winter warnings

**Trade Route**
- id, origin_town, destination_town
- distance, travel_days
- outgoing_shipment: [{item_id, quantity}]
- incoming_shipment: [{item_id, quantity}]
- merchant_id, arrival_day

### Relationships

```
Citizen --[lives_in]--> Building (house)
Citizen --[works_at]--> Building (workplace)
Citizen --[path_to]--> Tile
Building --[on]--> Tile
Tile --[adjacent_to]--> Tile
Item --[stored_in]--> Building.inventory | Citizen.inventory
Recipe --[consumes]--> Item
Recipe --[produces]--> Item
```

## Simulation Loop

Every tick (configurable, e.g., 1 game-hour = 1 tick at 1x speed):

1. **Season tick** — advance calendar, update temperature/daylight/growing conditions
2. **Citizen tick** — for each citizen: age check → need evaluation → task selection → path step → inventory update
3. **Building tick** — for each building: consume inputs → progress production → output goods → worker attendance check
4. **Resource tick** — spoilage check on stored items, crop growth on farm tiles
5. **Trade tick** — advance trade routes, check arrivals, deliver shipments
6. **Event tick** — fire mod hooks (on_tick), check scheduled events (disasters, festivals)
7. **Death & birth** — check health thresholds, age limits, population reproduction

## MVP Scope

One map (flat, temperate, no water), 50 citizens, 3 resources:

- **Logs** — harvested from forest tiles
- **Firewood** — produced at woodcutter from logs
- **Food** — harvested from gatherer's hut

3 building types: House, Woodcutter, Gatherer's Hut

Core loop working:
1. Citizens are born, age, get hungry/cold
2. Citizens path to workplace, gather/harvest, return home
3. Resources deplete, production chains run, citizens consume
4. If food or firewood runs out, citizens get sick and die
5. Population stabilizes or collapses

No modding, no trading, no seasons, no UI. Simulation engine only, verifiable via headless test harness.

**Success criterion:** Simulation runs for 100 game-years with a stable population oscillating between 30-50.

## Mod API

### Content mods (JSON, no Lua required)

Drop files into `Mods/YourModName/`:
```
Mods/FishingExpansion/
  items.json        — new item definitions
  buildings.json    — new building definitions
  recipes.json      — new production chains
  citizens.json     — citizen templates
  maps.json         — new map definitions
```

### Behavior mods (Lua)

```lua
-- Mods/YourModName/init.lua
function on_building_complete(building)
  if building.type == "fishing_dock" then
    SpawnItem("fish", building.position, 10)
  end
end

function on_season_change(old_season, new_season)
  if new_season == "winter" then
    for _, citizen in pairs(GetAllCitizens()) do
      AddNeed(citizen.id, "warmth", 0.8)
    end
  end
end
```

### Hooks (v0)

- `on_init()` — called once when mod loads
- `on_tick(tick_number)` — every simulation tick
- `on_season_change(old, new)` — season transition
- `on_building_complete(building)` — construction finished
- `on_building_destroyed(building)` — building removed
- `on_citizen_born(citizen)` — new citizen spawned
- `on_citizen_died(citizen, cause)` — citizen removed
- `on_trade_arrival(route, shipment)` — trade goods delivered

**Sandboxed Lua runtime:** mods have no filesystem access, no network, no OS calls. Only the mod API surface.

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 0 — Scaffolding | Repo, docs, architecture, Unity project | In progress |
| 1 — Simulation Core (MVP) | 3 resources, 3 buildings, 50 citizens, headless test harness | Not started |
| 2 — Content & Modding | JSON loader, Lua runtime, mod API hooks, sandbox | Not started |
| 3 — UI (Touch) | Touch-first interface layer, renderer, camera controls | Not started |
| 4 — UI (Desktop) | Keyboard + mouse interface, same command bus | Not started |
| 5 — Seasons & Depth | Calendar, weather, crop seasons, citizen well-being | Not started |
| 6 — Trade | Inter-settlement trade routes, merchant AI | Not started |
| 7 — Polish & Release | Tutorial, mod workshop, Steam/mobile stores | Not started |
