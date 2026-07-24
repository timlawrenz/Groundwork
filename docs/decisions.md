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
