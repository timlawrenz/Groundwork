# Architecture Decision Records

> Each decision is dated and has a status: `proposed`, `accepted`, `superseded` (by which decision), or `deprecated`.

## Template

```markdown
### YYYY-MM-DD — Decision Title

**Status:** proposed | accepted | superseded | deprecated

**Context:** What problem are we solving? What constraints exist?

**Decision:** What did we decide?

**Rationale:** Why this over alternatives?

**Consequences:** What becomes easier? What becomes harder?
```

---

## Decisions

### 2026-07-23 — Unity ECS (DOTS) as Engine

**Status:** accepted

**Context:** Thousands of citizen entities on tablet-class CPUs. Traditional GameObject-based approach would be too heavy. Need data-oriented design for performance.

**Decision:** Use Unity ECS (DOTS) — Entities package, not GameObjects.

**Rationale:**
- Data-oriented design fits citizen simulation perfectly (thousands of identical entities, different data)
- Unity's cross-platform build support (Android, Windows, Linux, WebGL)
- Jobs System for future multi-threading if determinism constraints change
- Large ecosystem, documentation, asset store

**Alternatives considered:**
- **Godot** — good cross-platform, but ECS not first-class. GDScript performance concerns for heavy sim.
- **Bevy (Rust)** — excellent ECS, but no mobile/WebGL support. Smaller ecosystem.
- **Custom C++ engine** — maximum control, but enormous build system and cross-platform burden.

**Consequences:**
- Easier: cross-platform builds, ECS performance, Unity editor tooling
- Harder: Unity dependency, DOTS API churn, larger build size for WebGL

---

### 2026-07-23 — JSON for Content, Lua for Behavior

**Status:** accepted

**Context:** Need a modding system that's approachable for non-programmers (JSON) and expressive enough for game logic (scripting).

**Decision:** Content definitions in JSON. Behavior scripting in Lua (MoonSharp or NLua).

**Rationale:**
- JSON is human-readable, requires no parser build step, and is universally understood
- Lua is the standard game modding language (WoW, Factorio, Roblox, Tabletop Simulator)
- Lua is lightweight, embeddable, and sandboxable
- Separation of data (JSON) from behavior (Lua) keeps mods clean

**Alternatives considered:**
- **YAML** — more human-friendly but slower to parse, less universal
- **TOML** — good for config, less ergonomic for large data arrays
- **Python** — too heavy to embed in Unity
- **C# scripting** — requires compilation, not sandboxable, modder friction

**Consequences:**
- Easier: mod accessibility, sandboxed execution, familiar tooling
- Harder: Lua-C# interop overhead, JSON schema validation, mod conflict resolution

---

### 2026-07-23 — Command Pattern for All Mutations

**Status:** accepted

**Context:** Need to decouple UI from simulation. Want replays, tests, and future networking.

**Decision:** All state mutations go through a command bus. UI submits commands; sim processes them. Renderer reads a read-only snapshot.

**Rationale:**
- UI can be swapped without touching sim (touch, mouse, headless test harness)
- Commands can be recorded for replay (debug, save/load)
- Commands can be recorded for testing (deterministic playback)
- Commands can be networked later (multiplayer)

**Alternatives considered:**
- **Direct method calls** — simpler initially but couples UI to sim, no replay
- **Event-driven for mutations** — more flexible but non-deterministic, harder to debug. Note: this rejection is specifically about events as a *mutation mechanism*, not events as a *notification mechanism*. See ADR 2026-07-25 for the event buffer decision.

**Consequences:**
- Easier: UI swapping, testing, replay, save/load, future networking
- Harder: more indirection, every feature needs a command class, learning curve

---

### 2026-07-23 — Tile-Based Map

**Status:** accepted

**Context:** Need a spatial model for building placement, pathfinding, and resource distribution.

**Decision:** Grid-based tile map, not freeform coordinates.

**Rationale:**
- Simplifies pathfinding (grid-based A* or flow field)
- Building placement snaps to grid — no rotation/alignment edge cases
- Adjacency rules are tile-based (building affects neighboring tiles)
- Resource distribution maps naturally to tile types (forest, mountain, water)

**Alternatives considered:**
- **Freeform/continuous** — more natural-looking but complex for pathfinding, placement validation
- **Hex grid** — aesthetically pleasing but more complex adjacency math, harder to render

**Consequences:**
- Easier: pathfinding, placement, adjacency rules, map editing
- Harder: natural-looking terrain transitions, diagonal building placement

---

### 2026-07-23 — Deterministic Single-Threaded Simulation

**Status:** accepted

**Context:** Need reproducibility for debugging and testing. Multi-threading introduces non-determinism from scheduling.

**Decision:** Single-threaded simulation. Citizens tick in ID order. Each tick is fully deterministic given the same initial state and command sequence.

**Rationale:**
- Reproducible bugs — replay the same command log, get the same result
- Save/load is just command log replay
- Bisect bugs by replaying subsets of command logs
- Multi-threading can be added later with determinism constraints (deterministic task scheduling)

**Alternatives considered:**
- **Multi-threaded with locks** — faster but non-deterministic, harder to debug
- **Deterministic multi-threading** — possible but complex, premature optimization

**Consequences:**
- Easier: debugging, testing, save/load, bisecting
- Harder: single-core performance ceiling, may need multi-threading for 10k+ citizens

---

### 2026-07-23 — Engine Selection Under Revised Constraints

**Status:** proposed

**Context:** Original ADR assumed 4 targets including native Android. User clarified that tablet play via browser (WebGL) is acceptable, removing native Android as a requirement. Targets are now: Windows, Linux, WebGL. Question: does Unity still win, or do alternatives become viable?

**Constraint check:**
- Cross-platform: desktop (Windows, Linux) + WebGL — **must ship to all 3 from one codebase**
- ECS architecture: data-oriented, thousands of entities, deterministic tick loop
- Modding: embeddable Lua runtime
- Touch-first UI: input abstraction layer
- Language: simulation logic in a systems language (C#, Rust, C++ — not GDScript/JS)
- License: open-source project, no per-install royalty risk

**Options:**

| Criterion | Unity | Godot 4 | Bevy (Rust) | Custom C++ |
|---|---|---|---|---|
| WebGL export | ✅ | ✅ (smaller builds) | ⚠️ immature | ✅ (Emscripten) |
| C# on WebGL | ✅ | ❌ (GDScript only) | N/A | N/A |
| First-class ECS | ✅ (DOTS) | ❌ (node/scene) | ✅ (best-in-class) | ✅ (EnTT) |
| Lua embedding | ✅ (MoonSharp) | ✅ (GDExtension) | ✅ (mlua) | ✅ |
| Touch input | ✅ (Input System) | ✅ | ⚠️ manual | ⚠️ manual |
| Build simplicity | Medium | Low | Low | High pain |
| Licensing risk | ⚠️ Unity runtime fee history | ✅ MIT, zero risk | ✅ MIT/Apache | ✅ |
| Ecosystem | Huge asset store | Growing fast (2,864 Steam releases 2025-26) | Small | None |
| AAA precedent | Genshin Impact, Hearthstone | Cassette Beasts, Dome Keeper | Tiny Glade (rendering only) | Factorio |

**Critical finding — Godot WebGL C# gap:** Godot 4.x does not support C# on WebGL exports. This is a fundamental mismatch: we'd need to write the simulation in C# for desktop but GDScript for web, or write the entire sim in GDScript. Neither is acceptable — GDScript is too slow for thousands of entities, and maintaining two codebases defeats the point.

**Critical finding — Godot no first-class ECS:** Godot's architecture is node/scene-based, not data-oriented. You can build ECS on top of it (add-ons exist), but it fights the engine's design. Unity DOTS is purpose-built for this pattern.

**Analysis:**
- **Unity** is the only engine that hits all constraints: C# on WebGL, first-class ECS, touch input, Lua embedding. The runtime fee controversy is a real risk but Unity backtracked under community pressure and the Personal tier remains free under $200K. Genshin Impact proves the model works at AAA scale.
- **Godot** wins on licensing (MIT, zero risk, forever) and WebGL build size (smaller), but loses on the two most important technical constraints (C# on web, ECS). It's the right answer for a different project.
- **Bevy** has the best ECS architecture of any engine and would be the first choice for a pure-desktop project, but WebGL support is immature and there's no editor. Too early for a cross-platform game targeting tablet browsers.
- **Custom C++** gives maximum control and Factorio proves it works, but the build system burden is enormous and eats time that should go into the simulation. Premature optimization.

**Decision:** **Stay with Unity ECS (DOTS).** The revised constraints actually strengthen the case — removing native Android doesn't open up alternatives because WebGL + C# + ECS narrows the field to Unity.

**Consequences:**
- Easier: all 3 targets from one C# codebase, DOTS fits the architecture, Genshin validates the approach
- Harder: Unity licensing uncertainty (mitigated by open-source nature and <$200K revenue threshold), larger WebGL builds than Godot, Unity dependency lock-in

---

### 2026-07-23 — Test-Driven Development (TDD) Mandate

**Status:** accepted

**Context:** Simulation engine is headless by design — testable without Unity's renderer. Unity Test Framework (Edit Mode) supports running DOTS systems in isolated worlds. Without TDD, simulation bugs compound silently and aren't discovered until the headless harness runs (potentially thousands of ticks later).

**Decision:** **All simulation code must be developed using TDD.** Write failing test first, make it pass, then refactor. No simulation logic is committed without passing tests.

**Rationale:**
- Headless sim is the ideal TDD target — pure data in, pure data out, no rendering, no input
- Unity Edit Mode tests run in milliseconds, not seconds — fast feedback loop
- Isolated DOTS worlds mean each test controls its own state — no shared state leakage
- Simulation bugs (citizens not aging, needs not escalating, production stalling) are subtle and compound over thousands of ticks. A 100-year simulation takes minutes to run; unit tests take milliseconds.
- The test suite IS the specification. When a test says "citizens age each day," that's the contract.

**Test structure:**
```
Assets/Tests/
├── EditMode/
│   └── Simulation/        ← one test file per system
└── TestHelpers/
    └── SimulationTestWorld.cs  ← creates isolated worlds with singletons
```

**Test conventions:**
- Every ISystem gets a corresponding `*Tests.cs` file
- Tests use `SimulationTestWorld` to create isolated worlds with required singletons
- Tests assert on component data after running the system under test
- Integration tests verify the bootstrap creates the correct world state
- Run with Unity Test Runner → EditMode, or headless via command line

**RED-GREEN-REFACTOR enforcement:**
- New feature starts with a failing test that defines the expected behavior
- Implementation is complete when the test passes
- Refactoring happens only with passing tests

**Consequences:**
- Easier: confident refactoring, bisectable bugs, self-documenting system behavior
- Harder: more upfront code (tests), must design for testability, boilerplate for world setup

---

### 2026-07-25 — Deterministic Event Buffer for System Decoupling & Mod API

**Status:** accepted

**Context:** The simulation pipeline is rigidly ordered — systems must know their position relative to others to consume their outputs. As system count grows, this becomes a coupling bottleneck: adding a system that reacts to births, deaths, or building completions requires knowing which system emits those signals and placing the new system after it in the pipeline. The mod API needs event hooks (on_citizen_born, on_building_complete, etc.), and maintaining a separate "mod surface" alongside internal inter-system communication creates duplication. The previous ADR rejected "event-driven" for mutations — correctly — but events for *notification* (signaling that state changed) are different from events for *mutation* (changing state).

**Decision:** Add a deterministic event buffer (ECS `DynamicBuffer<SimulationEvent>`) on a singleton entity. Systems emit events to the buffer during their tick. A dedicated `EventDispatchSystem` runs after all emitting systems, processes the buffer in emission order, invokes any Lua mod hooks, and clears the buffer for the next tick.

Event types: `CitizenBorn`, `CitizenDied`, `CitizenAged`, `BuildingComplete`, `BuildingDeconstructed`, `SeasonChanged`, `DayChanged`, `ResourceDepleted`, `NeedCritical`, `PopulationMilestone`, etc.

```csharp
struct SimulationEvent : IBufferElementData {
    EventType Type;
    int EntityId;
    float Data0, Data1, Data2, Data3;  // generic payload
}
```

**Rationale:**
- **Decoupling:** Emitting systems don't know who's listening. Adding a system that reacts to births doesn't require touching the birth system.
- **Mod API == internal API:** Mods and internal systems subscribe to the same event stream. Every internal notification is automatically a mod hook. No separate surface to maintain.
- **Deterministic:** Events are processed in emission order within a single-threaded dispatch phase. Same initial state + same command log → same event sequence → same outcome.
- **Lightweight:** ~50 lines of C#, no external store, no message broker. Replay is already handled by the command log — events are derived from state, not the other way around.
- **Complements command pattern:** Commands mutate state (PlaceBuilding, AssignWorker). Events notify that state changed (BuildingPlaced, WorkerAssigned). Two different jobs.

**Consequences:**
- **Easier:** Adding reactive systems without pipeline gymnastics, mod hooks are automatic, inter-system communication is discoverable (grep for `EventType.X`), event-driven features (achievements, quests, tutorial triggers, UI notifications) become trivial
- **Harder:** Pipeline ordering still matters for emission timing — a system that needs to react to births must run after the system that *emits* birth events. Event type proliferation needs discipline — every new event type should justify its existence. Debugging event chains requires tracing emission → consumption across systems.
