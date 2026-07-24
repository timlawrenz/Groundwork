---
name: groundwork-architecture
description: Groundwork project structure, system ordering, component organization, namespace conventions, and architectural decision workflow. Use when adding new systems, restructuring code, making namespace decisions, or evaluating whether a feature belongs in the simulation core vs a mod hook. Also use when running the project — headless builds, test execution, and Unity CLI operations.
---

# Groundwork Architecture

Use this skill when navigating the Groundwork codebase, adding new systems, making architectural decisions, or running the project headless. Covers project structure, system pipeline, namespace conventions, ADR workflow, and key constraints.

## Project Structure

```
Groundwork/
├── AGENTS.md                        ← AI agent instructions (read this first)
├── PROJECT_STATUS.md                ← current phase + next action (living pointer)
├── TODO.md                          ← near-term task tracking
├── README.md
├── docs/
│   ├── design.md                    ← game design document
│   ├── architecture.md              ← technical architecture
│   ├── decisions.md                 ← ADRs (architecture decision records)
│   └── unity-features.md            ← Unity feature catalog (USE/EXTEND/BUILD)
├── Assets/
│   ├── Scripts/
│   │   └── Simulation/
│   │       ├── Components/          ← IComponentData structs
│   │       │   ├── Citizen.cs
│   │       │   ├── Building.cs
│   │       │   ├── MapPosition.cs
│   │       │   ├── CalendarSingleton.cs
│   │       │   ├── SimulationConfig.cs
│   │       │   ├── MapGridData.cs
│   │       │   ├── ItemDefinition.cs
│   │       │   ├── PathRequest.cs
│   │       │   └── CitizenTask.cs
│   │       ├── Buffers/             ← IBufferElementData structs
│   │       │   ├── InventorySlot.cs
│   │       │   ├── ProductionOrder.cs
│   │       │   ├── CitizenNeed.cs
│   │       │   └── PathFollowing.cs
│   │       ├── Tags/                ← Tag IComponentData (empty structs)
│   │       │   └── GameEntityTags.cs  (Dead, Child, Elderly, UnderConstruction, Homeless)
│   │       └── Systems/             ← ISystem implementations
│   │           ├── GroundworkSimulationGroup.cs  ← ordering + system group
│   │           ├── SimulationBootstrap.cs
│   │           ├── TickDispatchSystem.cs
│   │           ├── CalendarSystem.cs
│   │           ├── CitizenAgeSystem.cs
│   │           ├── CitizenNeedSystem.cs
│   │           ├── PathfindingSystem.cs
│   │           ├── CitizenMovementSystem.cs
│   │           ├── BuildingProductionSystem.cs
│   │           └── DeathSystem.cs
│   └── Tests/
│       ├── EditMode/
│       │   └── Simulation/          ← one test file per system
│       │       ├── CalendarSystemTests.cs
│       │       ├── CitizenAgeSystemTests.cs
│       │       ├── CitizenNeedSystemTests.cs
│       │       ├── BuildingProductionSystemTests.cs
│       │       ├── PathfindingSystemTests.cs
│       │       ├── TickDispatchSystemTests.cs
│       │       ├── DeathSystemTests.cs
│       │       └── SimulationBootstrapTests.cs
│       └── TestHelpers/
│           ├── SimulationTestWorld.cs
│           └── Groundwork.TestHelpers.asmdef
├── Packages/
│   ├── manifest.json                ← Unity package dependencies
│   └── packages-lock.json
└── ProjectSettings/
```

## System Pipeline Order

Defined in `GroundworkSimulationGroup.cs`. Systems execute in this exact order each tick:

```
1. TickDispatchSystem      ← Advances tick counter, triggers day/season transitions
2. CalendarSystem          ← Updates calendar (day, season, temperature, daylight)
3. CitizenAgeSystem        ← Ages citizens at day boundaries, assigns Child/Elderly tags
4. CitizenNeedSystem       ← Escalates citizen needs (hunger, warmth) based on calendar
5. PathfindingSystem       ← Processes PathRequest → PathFollowing buffer (A*)
6. CitizenMovementSystem   ← Moves citizens along PathFollowing waypoints, one tile per tick
7. BuildingProductionSystem← Processes ProductionOrders, consumes inputs, creates outputs
8. DeathSystem             ← Kills citizens with 0 health or exceeding max age
```

When adding a new system, determine where in the pipeline its effects are needed:
- Systems that produce data for downstream systems go earlier
- Systems that consume data from upstream systems go later
- Systems with no dependencies can go anywhere → place after the last consumer of their outputs

## Namespace Conventions

```
Groundwork.Simulation       ← Components, Buffers, Tags, Systems
Groundwork.TestHelpers      ← SimulationTestWorld
Groundwork.Tests.Simulation ← Test files
```

Never use `Groundwork.Simulation.Components` as a sub-namespace — all simulation types share the flat `Groundwork.Simulation` namespace. This avoids `using` clutter and makes cross-referencing simpler.

## Assembly Definitions

Groundwork uses Assembly Definitions (`.asmdef`) to control compilation boundaries:

| Assembly | Path | Purpose |
|----------|------|---------|
| `Groundwork.Simulation` | `Assets/Scripts/Simulation/` | All simulation code |
| `Groundwork.Tests` | `Assets/Tests/EditMode/` | EditMode tests |
| `Groundwork.TestHelpers` | `Assets/Tests/TestHelpers/` | Test infrastructure |

When adding a new code folder, add an `.asmdef` to keep compilation scoped.

## Key Architectural Constraints

These are non-negotiable per the project governance:

1. **No combat, no enemies, no warfare.** Depth comes from economy, seasons, and citizen well-being.
2. **Mod API == internal API.** If the simulation engine can do it, a mod can do it. Never build internal-only features.
3. **Architecture before UI.** Simulation engine runs headless with automated tests before any renderer exists.
4. **Deterministic simulation.** Single-threaded, citizens tick in ID order. Reproducible and debuggable.
5. **Stateless renderer.** Renderer reads game state snapshot. Sim never touches rendering.
6. **Command pattern.** All mutations flow through the command bus. UI is decoupled from sim.
7. **JSON for content, Lua for behavior.** Items, buildings, recipes are JSON. Game logic is Lua (sandboxed).

## ADR Workflow

When considering a technology choice, architecture change, or design decision:

1. Check `docs/decisions.md` — has this already been decided?
2. If not, propose using the ADR template:
   ```markdown
   ### YYYY-MM-DD — Decision Title
   **Status:** proposed
   **Context:** What problem? What constraints?
   **Decision:** What did we decide?
   **Rationale:** Why this over alternatives?
   **Consequences:** What becomes easier? What becomes harder?
   ```
3. Set status to `accepted` once the decision is made
4. If a prior ADR is overridden, mark it `superseded (by YYYY-MM-DD — New Decision)`

## Unity CLI Operations

### Editor Path

```bash
UNITY_PATH=~/Unity/Hub/Editor/6000.3.20f1/Editor/Unity
```

### Open Project (GUI)

```bash
$UNITY_PATH -projectPath /home/tim/source/activity/Groundwork &
```

### Run Tests (Headless)

```bash
$UNITY_PATH -runTests -batchmode -nographics \
  -testPlatform EditMode \
  -projectPath /home/tim/source/activity/Groundwork
```

### Run Project (Headless Simulation)

```bash
$UNITY_PATH -batchmode -nographics \
  -projectPath /home/tim/source/activity/Groundwork \
  -executeMethod Groundwork.Simulation.HeadlessRunner.Run
```

(Note: `HeadlessRunner` is planned but not yet implemented — see TODO.md Phase 1.)

### Verify Project Opens

```bash
$UNITY_PATH -batchmode -nographics -quit \
  -projectPath /home/tim/source/activity/Groundwork
```

Exit code 0 = project opened successfully.

## Adding a New Feature (End-to-End Workflow)

1. **Design**: Document in `docs/design.md` if it's a gameplay feature
2. **ADR**: Add to `docs/decisions.md` if it involves a technology choice
3. **TODO**: Add checkboxes to `TODO.md` for the implementation steps
4. **RED test**: Write a failing test in `Assets/Tests/EditMode/Simulation/`
5. **Components/Buffers/Tags**: Add needed data structures
6. **GREEN system**: Implement the ISystem
7. **Pipeline**: Add ordering entry to `GroundworkSimulationGroup.cs` and `RunFullTick()`
8. **Commit**: Tests + implementation together
9. **Update**: `PROJECT_STATUS.md` if this changes the next action
