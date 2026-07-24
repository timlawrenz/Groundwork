# Unity Built-in Feature Catalog

> **Purpose:** Prevent reinventing wheels. Before building anything, check this catalog to see if Unity already ships it. Each feature is tagged: **USE** (built-in, just configure), **EXTEND** (built-in but needs customization), or **BUILD** (we write from scratch).

## Legend

| Tag | Meaning |
|---|---|
| **USE** | Unity ships it. Works out of the box. Just configure. |
| **EXTEND** | Unity provides a base, but we need to wrap, extend, or customize it. |
| **BUILD** | Not provided. We build it from scratch. |
| **ASSET** | Available via Asset Store or third-party package. We decide whether to buy or build. |

---

## ECS / Data Layer

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Entity-Component-System | **Entities** package (shipping as Core in 6.4) | **USE** | Archetype-based ECS. IComponentData, ISystem, IJobEntity. Citizens, Buildings, Items are entities with component data. |
| Fast collections | **Collections** package — NativeArray, NativeHashMap, NativeList | **USE** | Burst-compatible data structures. Use for tile maps, entity lookups. |
| Mathematics | **Mathematics** package — float3, quaternion, Random | **USE** | SIMD-optimized math. Replace System.Math with this. |
| Burst compiler | **Burst** — compiles C# jobs to native code | **USE** | 10-50x speedup on entity iteration. Mandatory for 1000+ citizens. |
| Job System | **Jobs** package — IJob, IJobFor, IJobChunk | **USE** | Multi-threaded entity processing. Citizen ticks, building ticks, resource ticks run as jobs. |
| Deterministic random | **Unity.Mathematics.Random** with fixed seed | **USE** | Seed the random per tick number → deterministic replay. |
| Serialization (save/load) | Unity serialization is GameObject-oriented, not ECS | **BUILD** | Our save system is command-log replay, not state snapshots. We serialize command streams, not entities. |

---

## Map / World

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Tilemap (2D grid) | **Tilemap** package — grid, tile placement, brushes | **EXTEND** | Designed for 2D platformers. We use the grid/tile data model but render our own ECS-based tiles. Can use Tilemap for editor painting. |
| Terrain (3D) | **Terrain** system | **BUILD** | Too heavy for our tile-based approach. We build our own flat grid with elevation data. |
| Grid math | **Mathematics** — int2 coordinates, grid snapping | **USE** | Tile coordinates = int2. Grid snapping = math.round(position). No custom grid math needed. |

---

## Simulation Engine

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Tick loop | Unity's Update() != our tick | **BUILD** | Unity's frame-based Update is not our tick. We build our own tick dispatcher that advances game-time independently of frame rate. |
| Citizen AI / needs | — | **BUILD** | Game-specific. Need evaluation → task selection → path step. We build the state machine. |
| Production chains | — | **BUILD** | Recipes, inputs, outputs, worker assignment. We build the economy engine. |
| Seasons / weather | — | **BUILD** | Calendar advancement, temperature, growing multipliers. We build the seasonal engine. |
| Pathfinding | **AI Navigation** (NavMesh) — for 3D freeform worlds | **BUILD** | NavMesh assumes continuous space. We're grid-based. We build A* or flow field on an int2 grid. Could **EXTEND** if we adapt NavMesh to a grid. |
| Birth / death / aging | — | **BUILD** | Game-specific citizen lifecycle. |

---

## Input (Touch-First)

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Touch input | **Input System** — EnhancedTouch, Touchscreen, activeTouches | **USE** | Multi-touch tracking, touch phases (began/moved/ended), finger ID tracking. |
| Mouse + keyboard | **Input System** — Mouse, Keyboard, Pointer | **USE** | Same Input System handles all input devices. One API. |
| Gesture recognition | **Input System** — tap, tapCount, pressure, radius | **EXTEND** | Tap detection is built-in. Pinch-to-zoom and drag-to-pan need to be built from touch deltas — straightforward. |
| Device simulation | **Device Simulator** — simulate touch in editor | **USE** | Test touch input in editor without deploying to a device. |
| Drag selection | — | **BUILD** | Box-select citizens, drag to pan camera. Built from touch/mouse deltas. |
| Camera controls | — | **BUILD** | Pan, zoom, rotate. Built from input + camera transform. |

---

## UI / Interface

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| UI framework | **UI Toolkit** — UXML, USS, data binding | **USE** | Web-like development. USS is CSS. UXML is HTML. Data binding for reactive UI. Recommended for runtime UI in Unity 6.x. |
| UI Toolkit — touch | **UI Toolkit** — touch events, Clickable, Manipulators | **USE** | Built-in touch handling for UI elements. Works on mobile and desktop. |
| UI Toolkit — world-space UI | **UI Toolkit** — PanelSettings for world-space | **USE** | Floating UI panels in the game world (building info popups, citizen status). |
| HUD / overlays | **UI Toolkit** | **USE** | Resource counters, minimap, citizen count — standard UI Toolkit panels. |
| Minimap | — | **BUILD** | Custom render texture + UI Toolkit overlay. Game-specific. |
| UI animations | **UI Toolkit** — transitions, USS transitions | **USE** | Panel slide-in, button press feedback. Limited compared to full animation system. |

---

## Rendering

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| 2D sprite rendering | **2D Sprite** package — SpriteRenderer, Sorting Layers | **USE** | Buildings, resources, citizens as sprites. Sorting by Y-position for isometric-ish depth. |
| Tile rendering | **Tilemap Renderer** | **EXTEND** | We may use it for terrain tiles, or render tiles as ECS entities for performance. |
| 3D mesh rendering | **MeshRenderer**, URP/HDRP | **USE** | Future: 3D buildings, terrain. Not MVP. |
| Shaders / materials | **Shader Graph** — visual shader authoring | **USE** | Season-based color shifts (snow in winter, green in spring). Custom shaders for tile highlights. |
| Camera | **Cinemachine** — camera rig, follow, zoom limits | **USE** | Camera bounds, smooth zoom, pan constraints. |
| Lighting (2D) | **2D Renderer** — 2D lights, shadows | **USE** | Day/night cycle lighting. |
| Render-to-texture (minimap) | **Render Texture** | **USE** | Top-down camera renders to texture → displayed in UI Toolkit minimap. |

---

## Content / Asset Management

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Asset loading at runtime | **Addressables** — async loading, remote content, dependency management | **EXTEND** | Perfect for mod loading. Addressables can load from disk, web, or server. Mods are Addressables groups. |
| JSON parsing | **UnityEngine.JsonUtility** (simple) or **Newtonsoft.Json** (full) | **USE** | Newtonsoft.Json is bundled with Unity. Use for Items.json, Buildings.json, Recipes.json. |
| Asset bundles | **Asset Bundles** (legacy) | **SKIP** | Use Addressables instead. Asset Bundles are the old way. |
| Resources folder | **Resources.Load()** (legacy, synchronous) | **SKIP** | Resources folder bloats build size. Addressables is the modern approach. |

---

## Modding Support

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Lua embedding | — | **BUILD** | Integrate MoonSharp or NLua. Not a Unity feature. |
| Lua sandboxing | — | **BUILD** | Restrict Lua access to only our mod API. No filesystem, no network. MoonSharp handles this. |
| Mod file loading | **Addressables** | **EXTEND** | Mods are Addressables groups loaded at runtime. We build the mod scanner that discovers Mods/*/ folders and registers them. |
| Hot-reload mods | — | **BUILD** | Detect new mod folders, load/unload mod content. Built on top of Addressables. |

---

## Build / Platform

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Windows build | **Build Settings** → Windows, Mac, Linux | **USE** | One click. |
| Linux build | **Build Settings** → Linux | **USE** | One click. |
| WebGL build | **Build Settings** → WebGL | **USE** | Compiles C# to WebAssembly. Touch input works in browser. |
| Android build | **Build Settings** → Android | **USE** | Future option. Not MVP. |
| Build size optimization | **Build Report**, stripping levels, code compression | **USE** | WebGL builds tend to be large. Addressables helps by loading content on demand. |
| Splash screen | **Splash Screen** settings | **USE** | "Made with Unity" on Personal tier. Customizable on Pro. |

---

## Audio

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Sound effects | **Audio Source** + **Audio Clip** | **USE** | Building construction sounds, citizen chatter, ambient nature. |
| Background music | **Audio Source** | **USE** | Seasonal music, menu music. |
| Spatial audio | **Audio Source** — 3D spatial blend | **USE** | Sounds fade with distance from camera. |
| Audio mixing | **Audio Mixer** — groups, snapshots, effects | **USE** | Duck music during alerts, seasonal audio snapshots. |

---

## Animation

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Sprite animation | **2D Animation** — sprite swap, skeletal animation | **USE** | Citizen walk cycles, building construction progress, tree sway. |
| Timeline / cutscenes | **Timeline** | **SKIP** | Not needed for MVP. |
| State-machine animation | **Animator** — blend trees, transitions | **EXTEND** | Citizen states (idle, walking, working, dying) → animation clips. |

---

## Testing

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Unit tests | **Test Framework** — NUnit, Edit Mode, Play Mode | **USE** | Edit Mode tests for simulation logic. Play Mode tests for integration. |
| Headless mode | **-batchmode -nographics** CLI flags | **USE** | Exactly what we need for the headless test harness. Run sim for 100 years, verify population stability. |
| Code coverage | **Code Coverage** package | **USE** | Track what parts of the simulation are tested. |
| Performance testing | **Performance Testing API** | **USE** | Measure tick time, entity throughput. |

---

## Profiling / Debugging

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Frame profiler | **Profiler** window | **USE** | Profile ECS systems, find bottlenecks in tick loop. |
| Memory profiler | **Memory Profiler** package | **USE** | Track entity memory, catch leaks. |
| ECS debugger | **Entities** — Entity Debugger | **USE** | Inspect live entities, components, archetypes in editor. |
| Logging | **Debug.Log**, **ILogger** | **USE** | Standard logging. |

---

## Networking (Future)

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Netcode for Entities | **Netcode for Entities** — multiplayer for DOTS | **SKIP** | Future: if we add multiplayer, use this. Not MVP. |
| REST / HTTP | **UnityWebRequest** | **USE** | Mod download, leaderboards, update checks. |

---

## Localization (Future)

| Feature | Unity Provides | Tag | Notes |
|---|---|---|---|
| Multi-language | **Localization** package — string tables, locale switching | **USE** | Not MVP, but built-in when we need it. |

---

## What We Build From Scratch

The following are entirely game-specific and no engine provides them:

| System | Description |
|---|---|
| **Simulation tick loop** | Time-advancement independent of frame rate |
| **Citizen state machine** | Need evaluation, task selection, AI behavior |
| **Economy engine** | Production chains, recipes, resource flow |
| **Seasonal engine** | Calendar, weather, crop growth, temperature |
| **Pathfinding (grid)** | A* or flow field on int2 tile grid |
| **Command bus** | All mutations flow here. Recorded for replay/save. |
| **Game snapshot** | Read-only state struct emitted each tick for the renderer |
| **Mod API (Lua)** | Embed MoonSharp, expose API surface, sandbox |
| **Mod scanner** | Discover Mods/*/ folders, register JSON + Lua |
| **Save/load (command log)** | Append-only command log. Load = replay from tick 0. |
| **Game-specific UI panels** | Building placement, citizen inspection, resource graphs |
