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
- **Event-driven** — more flexible but non-deterministic, harder to debug

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
