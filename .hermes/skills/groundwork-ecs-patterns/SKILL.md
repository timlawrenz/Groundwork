---
name: groundwork-ecs-patterns
description: DOTS/ECS coding patterns, anti-patterns, and design rules for Groundwork's simulation engine. Use when writing or reviewing any ISystem, IComponentData, IBufferElementData, blob assets, or NativeContainer code — including component design, system scheduling, queries, Jobs/Burst, ECB usage, and memory management.
---

# Groundwork ECS Patterns

Use this skill when writing or reviewing DOTS/ECS code in the Groundwork simulation. Covers component design, system architecture, query patterns, Jobs/Burst, memory management, and common anti-patterns specific to this project.

## Component Design Rules

Groundwork components live under `Assets/Scripts/Simulation/Components/`, buffers under `Buffers/`, tags under `Tags/`.

### IComponentData (per-entity data)

- Components are **pure data** — no methods, no logic, no references to managed objects
- Keep components small — only include fields the system actually reads/writes
- Split by access pattern, not by game concept:
  - ✅ `MapPosition`, `Citizen`, `PathRequest` (separate, each read by different systems)
  - ❌ `CharacterData` (position + health + inventory + AI state all in one)
- Tag components (`struct Child : IComponentData {}`) are free — use them for filtering
- Use `[BurstCompile]`-compatible types only: `int`, `float`, `int2`, `FixedString32Bytes`, `Entity`, `BlobAssetReference<T>`
- Never store `string`, `class`, `List<T>`, delegates, or managed arrays

### IBufferElementData (variable-length per-entity data)

Buffers live under `Assets/Scripts/Simulation/Buffers/`:

- Use for variable-length data: `PathFollowing` (waypoints), `InventorySlot` (items), `ProductionOrder` (queue), `CitizenNeed` (needs list)
- Always use a named struct, not a raw type alias:
  ```csharp
  public struct PathFollowing : IBufferElementData
  {
      public int2 TileCoordinate;
  }
  ```
- Buffers are created at entity creation time — include them in the archetype
- When adding buffer elements via ECB, use `ecb.AddBuffer<T>(entity)` (not `ecb.SetBuffer`)

### Blob Assets (read-only shared data)

- `MapGridData` uses `BlobAssetReference<MapGridBlob>` — the canonical pattern
- Build with `BlobBuilder` in `Allocator.Temp`, create persistent reference, dispose builder
- Never mutate a blob after creation — they are immutable by contract
- Dispose in `System.IDisposable` (SimulationTestWorld pattern) or in `OnDestroy`

### IEnableableComponent

- Prefer over adding/removing components for toggling behavior without structural changes
- Structural changes (add/remove component) cause sync points and chunk migrations

## System Design Rules

Groundwork uses **ISystem** (unmanaged, Burst-compatible), not SystemBase. All systems live under `Assets/Scripts/Simulation/Systems/`.

### ISystem vs SystemBase

- ✅ **ISystem** — used everywhere in Groundwork. Burst-compatible, unmanaged, no GC.
- ❌ **SystemBase** — never use. Managed, GC pressure, no Burst.
- Every ISystem should have `[BurstCompile]` on both the struct and `OnUpdate`

### Scheduling

System execution order is defined in `GroundworkSimulationGroup.cs`:

```
TickDispatch → Calendar → CitizenAge → CitizenNeed → Pathfinding → CitizenMovement → BuildingProduction → Death
```

When adding a new system:
1. Add its ordering entry to `GroundworkSimulationGroup.cs` with `[UpdateAfter(typeof(PreviousSystem))]`
2. Add it to `SimulationTestWorld.RunFullTick()` in the correct position
3. Systems must be stateless — all state lives in components/singletons

### Singleton Pattern

- `SimulationConfig`, `CalendarSingleton`, `MapGridData` — exactly one entity with each
- Access via `SystemAPI.GetSingleton<T>()`
- Never create multiple singletons of the same type — queries assume exactly one

### EntityCommandBuffer (ECB)

- Use ECB for structural changes inside `foreach` loops
- Never make structural changes directly inside a query iteration
- Create ECB with `Allocator.Temp` (frame-scoped), call `Playback`, then `Dispose`:
  ```csharp
  var ecb = new EntityCommandBuffer(Allocator.Temp);
  // ... add/remove components, create/destroy entities via ecb ...
  ecb.Playback(state.EntityManager);
  ecb.Dispose();
  ```
- For systems that need ECB across jobs, use `EndSimulationEntityCommandBufferSystem`

## Query Patterns

### EntityQuery in ISystem

Groundwork uses `SystemAPI.Query<T>()` inside `foreach`:

```csharp
foreach (var (position, request, entity) in
         SystemAPI.Query<RefRO<MapPosition>, RefRO<PathRequest>>()
             .WithAll<Citizen>()
             .WithEntityAccess())
```

- `RefRO<T>` for read-only access (zero structural barriers)
- `RefRW<T>` for read-write access (adds structural barrier)
- `.WithAll<T>()` — entity must have component T
- `.WithNone<T>()` — entity must NOT have component T
- `.WithAny<T1, T2>()` — entity must have at least one
- `.WithEntityAccess()` — when you need the Entity handle

### Cached Queries

For queries used every frame, cache them — don't recreate. Groundwork's current systems are simple enough that direct `SystemAPI.Query` in `OnUpdate` is acceptable, but as the pipeline grows, extract queries to `OnCreate`:

```csharp
private EntityQuery _citizenQuery;

public void OnCreate(ref SystemState state)
{
    _citizenQuery = SystemAPI.QueryBuilder()
        .WithAll<Citizen, MapPosition>()
        .Build();
    _citizenQuery.SetChangedVersionFilter(typeof(MapPosition)); // only when changed
}
```

## Jobs System & Burst

- Mark all systems with `[BurstCompile]` on both struct and `OnUpdate` method
- Avoid managed types in Burst code: no `string`, `class`, `List<T>`, delegates, `Mathf`
- Use `NativeArray<T>`, `NativeList<T>`, `NativeHashMap<K,V>` instead of managed collections
- Use `FixedString32Bytes` instead of `string` in components (already the Groundwork convention)
- Use `Unity.Mathematics` (`math`, `int2`, `float3`) — never `System.Math` or `Vector3`
- For SIMD-friendly operations, use `math.select()` for branchless conditionals
- **Never call `.Complete()` immediately after scheduling** — it removes all parallelism benefit
- Use `[ReadOnly]` on job fields that only read data

### FixedString Conventions in Groundwork

- `FixedString32Bytes` is the standard string type for component data: `BuildingType`, `ItemId`, `RecipeId`, `NeedType`, `TaskType`, `Name`
- 32 bytes allows ~31 characters of ASCII text — sufficient for identifiers
- If longer identifiers are needed, use `FixedString64Bytes` or `FixedString128Bytes`

## NativeContainer Memory Management

- **Always dispose** `NativeArray`, `NativeList`, `NativeHashMap`, `NativeHashSet`:
  - `Allocator.Temp` — frame-scoped, fastest, MUST dispose same frame
  - `Allocator.TempJob` — job-scoped, 4-frame safety handle
  - `Allocator.Persistent` — long-lived, MUST dispose in `OnDestroy` or `Dispose()`
- Pre-allocate capacity when size is known: `new NativeHashMap<K,V>(capacity, Allocator.Temp)`
- Groundwork convention: use `Allocator.Temp` for transient data within `OnUpdate`

## Common DOTS Anti-Patterns

These are the mistakes that have caused real bugs or will cause them:

1. **Putting logic in components** — components are data, systems are logic. Never add methods to `IComponentData`.
2. **Structural changes inside loops** — adding/removing components or creating/destroying entities inside a `foreach` over `SystemAPI.Query`. Always use ECB.
3. **Calling `.Complete()` immediately** — defeats parallelism. Let the job system handle dependencies.
4. **Forgetting to dispose NativeContainers** — `Allocator.Temp` in `OnUpdate` is the most common leak. Triple-check every allocation has a corresponding `Dispose()`.
5. **Using `string` in Burst code** — won't compile. Use `FixedString`.
6. **Giant components with 20+ fields** — cache misses. Split into smaller components by access pattern.
7. **Using `GetComponent` per-entity** instead of bulk queries — O(n) lookups instead of O(1) chunk iteration.
8. **Missing `null` checks on destroyed objects** — use `Entity.Null` comparison, not `== null`.
9. **Creating singletons twice** — if `SystemAPI.GetSingleton<T>()` throws, there are 0 or 2+ entities with that component.

## Groundwork-Specific Patterns

### Adding a New System (checklist)

1. Create `Assets/Scripts/Simulation/Systems/NewSystem.cs` — `[BurstCompile] partial struct NewSystem : ISystem`
2. Add components/buffers/tags as needed under `Components/`, `Buffers/`, `Tags/`
3. Add ordering entry to `GroundworkSimulationGroup.cs`
4. Add to `SimulationTestWorld.RunFullTick()` pipeline
5. Write tests in `Assets/Tests/EditMode/Simulation/NewSystemTests.cs`
6. Run the test suite before committing

### Component Archetype Decisions

When deciding where new data lives:
- **One value per entity, fixed size** → `IComponentData` (e.g., `MapPosition`, `Citizen`)
- **Variable number of values per entity** → `IBufferElementData` (e.g., `InventorySlot`, `CitizenNeed`)
- **Boolean flag, no data** → Tag `IComponentData` (e.g., `Child`, `Elderly`, `Dead`)
- **Shared read-only data for all entities** → `BlobAssetReference<T>` in a singleton (e.g., `MapGridBlob`)
- **One value for the whole world** → singleton entity with `IComponentData` (e.g., `SimulationConfig`)

### When NOT to Use DOTS Features

- **`ISharedComponentData`** — fragments archetypes. Only use when you truly need per-chunk shared values (Groundwork currently has no use case).
- **`Aspect`** — wrapper around components. Adds indirection. Only use when a component group appears in 3+ queries.
- **`SystemGroup`** — Groundwork already uses `GroundworkSimulationGroup`. Don't nest further unless there's a clear subsystem boundary.
