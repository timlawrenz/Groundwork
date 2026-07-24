---
name: groundwork-testing
description: TDD workflow, SimulationTestWorld usage, test structure conventions, and headless test execution for Groundwork. Use when writing tests for any ISystem, adding new test files, or running the test suite — including EditMode simulation tests, RED-GREEN-REFACTOR enforcement, and CI-compatible headless execution.
---

# Groundwork Testing

Use this skill when writing or running tests for Groundwork simulation systems. Covers the TDD workflow, SimulationTestWorld API, test file conventions, and headless test execution.

## TDD Workflow (RED → GREEN → REFACTOR)

Per ADR `docs/decisions.md` (2026-07-23), **all simulation code must use TDD.** No simulation logic is committed without passing tests.

### Step 1: RED — Write a Failing Test

```csharp
[Test]
public void NewSystem_DoesExpectedBehavior()
{
    using var world = new SimulationTestWorld();
    // Arrange: set up entity state
    var entity = world.CreateCitizen(age: 30f, position: new int2(5, 5));

    // Act: run the system under test
    world.UpdateSystem<NewSystem>();

    // Assert: verify the result
    var result = world.EntityManager.GetComponentData<SomeComponent>(entity);
    Assert.That(result.SomeField, Is.EqualTo(expectedValue));
}
```

Key rules for RED phase:
- One test per discrete behavior (not one test per system)
- Test name describes the behavior: `MethodName_ExpectedBehavior_WhenCondition`
- Test must fail before implementation exists — verify the failure message makes sense
- Use `SimulationTestWorld` for isolation — never depend on other systems' output

### Step 2: GREEN — Make It Pass

- Implement the minimum code to make the test pass
- Don't add features the test doesn't demand
- Commit tests + implementation together

### Step 3: REFACTOR

- Only after all tests pass
- Extract helpers, simplify logic, improve naming
- If a refactor breaks a test, undo and try a smaller step
- Run the full suite after every refactor

## Test File Conventions

### Location

```
Assets/Tests/
├── EditMode/
│   └── Simulation/
│       ├── CalendarSystemTests.cs
│       ├── CitizenAgeSystemTests.cs
│       ├── CitizenNeedSystemTests.cs
│       ├── BuildingProductionSystemTests.cs
│       ├── PathfindingSystemTests.cs
│       ├── TickDispatchSystemTests.cs
│       ├── DeathSystemTests.cs
│       └── SimulationBootstrapTests.cs      ← integration test
└── TestHelpers/
    ├── SimulationTestWorld.cs                ← test world factory
    └── Groundwork.TestHelpers.asmdef
```

### One Test File Per System

- File: `Assets/Tests/EditMode/Simulation/{SystemName}Tests.cs`
- Namespace: `Groundwork.Tests.Simulation`
- Class: `{SystemName}Tests` with `[TestFixture]`
- Imports: `NUnit.Framework`, `Unity.Entities`, `Unity.Mathematics`, `Groundwork.Simulation`, `Groundwork.TestHelpers`

### Test Naming Convention

```
MethodUnderTest_ExpectedBehavior_WhenCondition
```

Examples:
- `ComputesPath_BetweenTwoTiles`
- `ReturnsEmptyPath_WhenSameTile`
- `CitizensAge_EachDay`
- `Dies_WhenAgeExceedsMaxLifespan`
- `DoesNotAge_WhenTickIsNotDayBoundary`

### Test Categories

Use `[Category]` attributes for filtering:
- `[Category("Unit")]` — single system, isolated world
- `[Category("Integration")]` — bootstrap or multi-system pipeline

## SimulationTestWorld API

`SimulationTestWorld` is the test world factory at `Assets/Tests/TestHelpers/SimulationTestWorld.cs`. Always use `using var world = new SimulationTestWorld();` for automatic cleanup.

### Entity Creation

```csharp
// Create citizen with defaults
var citizen = world.CreateCitizen(age: 30f, position: new int2(10, 10));

// Create citizen with full parameters
var citizen = world.CreateCitizen(
    age: 30f,
    position: new int2(10, 10),
    home: homeBuilding,        // Entity reference
    workplace: workBuilding,   // Entity reference
    health: 100f,
    happiness: 50f
);

// Create building
var building = world.CreateBuilding(
    buildingType: "lumberjack_hut",
    position: new int2(15, 15),
    operational: true,
    maxWorkers: 3
);
```

### Running Systems

```csharp
// Run a single system
world.UpdateSystem<CitizenAgeSystem>();

// Run a full tick (all systems in pipeline order)
world.RunFullTick();

// Advance ticks (TickDispatchSystem only — doesn't run other systems)
world.AdvanceTicks(10);

// Run bootstrap (creates initial world population)
world.RunBootstrap();
```

### Manipulating State

```csharp
// Set tick number
world.SetTick(720);  // day 30, tick 0

// Add items to inventory
world.AddToInventory(building, "logs", 10);

// Add production orders
world.AddProductionOrder(building, "firewood");

// Add components directly
world.EntityManager.AddComponent<PathRequest>(citizen, new PathRequest
{
    Destination = new int2(10, 5)
});
```

### Reading State

```csharp
// Read component data
var citizen = world.EntityManager.GetComponentData<Citizen>(entity);
Assert.That(citizen.Age, Is.EqualTo(31f));

// Read buffer
var inventory = world.EntityManager.GetBuffer<InventorySlot>(entity);
Assert.That(inventory[0].Quantity, Is.EqualTo(10));

// Check component existence
Assert.That(world.EntityManager.HasComponent<Dead>(entity), Is.True);
Assert.That(world.EntityManager.HasComponent<PathRequest>(entity), Is.False);
```

### Map Grid Defaults

- Default map: 20×20, all tiles walkable
- To test unwalkable terrain, modify `MapGridData` directly:
  ```csharp
  world.EntityManager.SetComponentData(mapGridEntity, new MapGridData { Grid = customBlob });
  ```

### World Lifecycle

- `SimulationTestWorld` implements `System.IDisposable`
- Always use `using var world = new SimulationTestWorld();` (automatic cleanup)
- Never manually dispose — use the `using` pattern
- Each test gets a fresh world — no shared state, no test ordering dependencies

## Running Tests

### Unity Test Runner (GUI)

1. Open Unity
2. Window → General → Test Runner
3. Select **EditMode** tab
4. Click "Run All" or select specific tests

### Headless (CLI)

```bash
# From the Unity project directory
Unity -runTests -batchmode -nographics -testPlatform EditMode -projectPath .
```

With explicit Unity path:
```bash
~/Unity/Hub/Editor/6000.3.20f1/Editor/Unity \
  -runTests -batchmode -nographics \
  -testPlatform EditMode \
  -projectPath /home/tim/source/activity/Groundwork
```

### Filtering Tests

```bash
# Run a single test fixture
Unity -runTests -batchmode -nographics \
  -testPlatform EditMode \
  -testFilter "Groundwork.Tests.Simulation.PathfindingSystemTests"

# Run a specific test
Unity -runTests -batchmode -nographics \
  -testPlatform EditMode \
  -testFilter "Groundwork.Tests.Simulation.PathfindingSystemTests.ComputesPath_BetweenTwoTiles"
```

### Results

- Results file: `<project>/TestResults-<timestamp>.xml`
- Exit code 0 = all passed, non-zero = failures
- See `docs/decisions.md` §2026-07-23 TDD Mandate for the full test strategy

## Common Test Patterns

### Testing Time-Based Systems

```csharp
[Test]
public void CitizensAge_OncePerDay_WhenTicksAdvance()
{
    using var world = new SimulationTestWorld();
    var citizen = world.CreateCitizen(age: 30f);

    // Advance 23 ticks (same day) — should NOT age
    world.AdvanceTicks(23);
    world.UpdateSystem<CitizenAgeSystem>();
    var c = world.EntityManager.GetComponentData<Citizen>(citizen);
    Assert.That(c.Age, Is.EqualTo(30f), "Should not age mid-day");

    // Advance 1 more tick (day boundary) — SHOULD age
    world.AdvanceTicks(1);
    world.UpdateSystem<CitizenAgeSystem>();
    c = world.EntityManager.GetComponentData<Citizen>(citizen);
    Assert.That(c.Age, Is.EqualTo(31f), "Should age at day boundary");
}
```

### Testing Production Systems

```csharp
[Test]
public void Produces_WhenInputsAvailable()
{
    using var world = new SimulationTestWorld();
    var building = world.CreateBuilding("woodcutter", new int2(5, 5));
    world.AddToInventory(building, "logs", 3);

    world.UpdateSystem<BuildingProductionSystem>();

    var inventory = world.EntityManager.GetBuffer<InventorySlot>(building);
    var firewood = inventory.FirstOrDefault(s => s.ItemId == "firewood");
    Assert.That(firewood.Quantity, Is.GreaterThan(0));
}
```

### Testing Death System

```csharp
[Test]
public void Dies_WhenAgeExceedsMaxLifespan()
{
    using var world = new SimulationTestWorld();
    var citizen = world.CreateCitizen(age: 95f, health: 10f); // near death

    world.UpdateSystem<DeathSystem>();

    Assert.That(world.EntityManager.HasComponent<Dead>(citizen), Is.True);
}
```

### Testing Bootstrap (Integration)

```csharp
[Test]
public void Bootstrap_CreatesExpectedCitizenCount()
{
    using var world = new SimulationTestWorld();
    world.RunBootstrap();

    int count = 0;
    foreach (var (citizen, entity) in
             SystemAPI.Query<RefRO<Citizen>>().WithEntityAccess())
        count++;

    Assert.That(count, Is.EqualTo(50));
}
```

## When NOT to Test

- Pure data structs with no behavior (IComponentData, IBufferElementData with only fields) — tested implicitly through system tests
- Tag components — tested implicitly when systems query for them
- Unity Editor tooling (not simulation code) — wait until PlayMode tests exist
- Performance/benchmark tests — these go in a separate harness, not the TDD suite
