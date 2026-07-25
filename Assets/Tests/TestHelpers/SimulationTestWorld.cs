using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Groundwork.Simulation;

namespace Groundwork.TestHelpers
{
    /// <summary>
    /// Creates isolated DOTS worlds pre-configured with required singletons
    /// for testing Groundwork simulation systems. Disposable — use in a using block.
    ///
    /// Usage:
    ///   using var testWorld = new SimulationTestWorld();
    ///   var citizen = testWorld.CreateCitizen(age: 30f, position: new int2(10, 10));
    ///   testWorld.UpdateSystem&lt;AgeSystem&gt;();
    ///   var result = testWorld.EntityManager.GetComponentData&lt;Citizen&gt;(citizen);
    /// </summary>
    public class SimulationTestWorld : System.IDisposable
    {
        public World World { get; }
        public EntityManager EntityManager => World.EntityManager;

        private readonly ComponentSystemGroup _testGroup;
        private BlobAssetReference<MapGridBlob> _mapGridBlob;
        private Entity _configEntity;
        private Entity _calendarEntity;
        private Entity _mapGridEntity;

        public const int DefaultMapWidth = 20;
        public const int DefaultMapHeight = 20;

        public SimulationTestWorld(string worldName = "TestWorld")
        {
            World = new World(worldName);
            _testGroup = World.GetOrCreateSystemManaged<GroundworkSimulationGroup>();
            CreateSingletons();

            // Create content definitions (recipes, building types)
            var contentHandle = World.CreateSystem<ContentLoaderSystem>();
            _testGroup.AddSystemToUpdateList(contentHandle);
            _testGroup.Update();
            _testGroup.RemoveSystemFromUpdateList(contentHandle);
        }

        private void CreateSingletons()
        {
            _configEntity = EntityManager.CreateEntity(typeof(SimulationConfig));
            var config = SimulationConfig.Default;
            config.MapWidth = DefaultMapWidth;
            config.MapHeight = DefaultMapHeight;
            EntityManager.SetComponentData(_configEntity, config);

            _calendarEntity = EntityManager.CreateEntity(typeof(CalendarSingleton));
            EntityManager.SetComponentData(_calendarEntity, new CalendarSingleton
            {
                Year = 1, Season = 0, DayOfSeason = 0,
                Temperature = 10f, Precipitation = 0.5f,
                DaylightHours = 12f, GrowingMultiplier = 0.5f,
            });

            CreateMapGrid();

            // Event buffer singleton
            var eventEntity = EntityManager.CreateEntity(
                typeof(SimulationEventSingleton));
            EntityManager.AddBuffer<SimulationEvent>(eventEntity);
        }

        private void CreateMapGrid()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MapGridBlob>();
            root.Width = DefaultMapWidth;
            root.Height = DefaultMapHeight;
            var walkable = builder.Allocate(ref root.Walkable, root.Width * root.Height);
            for (int i = 0; i < walkable.Length; i++) walkable[i] = 1;
            _mapGridBlob = builder.CreateBlobAssetReference<MapGridBlob>(Allocator.Persistent);
            builder.Dispose();
            _mapGridEntity = EntityManager.CreateEntity(typeof(MapGridData));
            EntityManager.SetComponentData(_mapGridEntity, new MapGridData { Grid = _mapGridBlob });
        }

        // ─── Entity creation helpers ───

        public Entity CreateCitizen(
            float age = 30f, int2? position = null,
            Entity home = default, Entity workplace = default,
            float health = 100f, float happiness = 50f)
        {
            var entity = EntityManager.CreateEntity(
                typeof(LivingBeing), typeof(Citizen), typeof(MapPosition), typeof(CitizenTask),
                typeof(CitizenNeed), typeof(InventorySlot), typeof(PathFollowing));

            EntityManager.SetComponentData(entity, new LivingBeing
            {
                Age = age, Sex = 0, Health = health, Happiness = happiness,
            });
            EntityManager.SetComponentData(entity, new Citizen
            {
                Name = "Test Citizen", EducationLevel = 0,
                HomeBuilding = home, WorkplaceBuilding = workplace,
            });
            EntityManager.SetComponentData(entity, new MapPosition
                { TileCoordinate = position ?? int2.zero, Rotation = 0 });
            EntityManager.SetComponentData(entity, new CitizenTask
                { TaskType = "idle", TargetEntity = Entity.Null, Progress = 0f });

            var needs = EntityManager.GetBuffer<CitizenNeed>(entity);
            needs.Add(new CitizenNeed { NeedType = "food", Urgency = 0.3f });
            needs.Add(new CitizenNeed { NeedType = "warmth", Urgency = 0.1f });

            if (age < 16f) EntityManager.AddComponent<Child>(entity);
            else if (age >= 60f) EntityManager.AddComponent<Elderly>(entity);
            return entity;
        }

        public Entity CreateBuilding(FixedString32Bytes buildingType, int2 position,
            bool operational = true, int maxWorkers = 3)
        {
            var entity = EntityManager.CreateEntity(
                typeof(Building), typeof(MapPosition),
                typeof(InventorySlot), typeof(ProductionOrder));
            EntityManager.SetComponentData(entity, new Building
            {
                BuildingType = buildingType, ConstructionProgress = 1f,
                IsOperational = operational, MaxWorkers = maxWorkers,
            });
            EntityManager.SetComponentData(entity, new MapPosition
                { TileCoordinate = position, Rotation = 0 });
            return entity;
        }

        public void AddToInventory(Entity entity, FixedString32Bytes itemId, int quantity)
        {
            var inv = EntityManager.GetBuffer<InventorySlot>(entity);
            inv.Add(new InventorySlot { ItemId = itemId, Quantity = quantity });
        }

        public void AddProductionOrder(Entity building, FixedString32Bytes recipeId)
        {
            var queue = EntityManager.GetBuffer<ProductionOrder>(building);
            queue.Add(new ProductionOrder { RecipeId = recipeId, Progress = 0f });
        }

        public void SetTick(long tick)
        {
            EntityManager.SetComponentData(_configEntity, new SimulationConfig
            {
                TicksPerDay = 24, DaysPerSeason = 30, CurrentTick = tick,
                TickSpeed = 1f, MapWidth = DefaultMapWidth, MapHeight = DefaultMapHeight,
            });
        }

        /// <summary>Read the SimulationStats singleton. Must exist in the world.</summary>
        public SimulationStats GetStats()
        {
            var query = EntityManager.CreateEntityQuery(typeof(SimulationStats));
            return query.GetSingleton<SimulationStats>();
        }

        /// <summary>Read the CalendarSingleton. Must exist in the world.</summary>
        public CalendarSingleton GetCalendar()
        {
            var query = EntityManager.CreateEntityQuery(typeof(CalendarSingleton));
            return query.GetSingleton<CalendarSingleton>();
        }

        /// <summary>Get or create the singleton entity holding the SimulationEvent buffer.</summary>
        public Entity GetOrCreateEventBufferEntity()
        {
            var query = EntityManager.CreateEntityQuery(
                typeof(SimulationEventSingleton));
            if (!query.IsEmpty)
                return query.GetSingletonEntity();

            var entity = EntityManager.CreateEntity(typeof(SimulationEventSingleton));
            EntityManager.AddBuffer<SimulationEvent>(entity);
            return entity;
        }

        /// <summary>Get the entity that carries the CalendarSingleton.</summary>
        public Entity GetCalendarSingletonEntity()
        {
            var query = EntityManager.CreateEntityQuery(typeof(CalendarSingleton));
            return query.GetSingletonEntity();
        }

        /// <summary>Count entities having component T (excluding Dead).</summary>
        public int CountEntities<T>() where T : unmanaged, IComponentData
        {
            var query = EntityManager.CreateEntityQuery(typeof(T));
            return query.CalculateEntityCount();
        }

        // ─── System execution ───

        public void AdvanceTicks(int count)
        {
            var handle = World.CreateSystem<TickDispatchSystem>();
            _testGroup.AddSystemToUpdateList(handle);
            for (int i = 0; i < count; i++)
                _testGroup.Update();
            _testGroup.RemoveSystemFromUpdateList(handle);
        }

        /// <summary>Run a single ISystem once against this world, in isolation.</summary>
        public void UpdateSystem<T>() where T : unmanaged, ISystem
        {
            var handle = World.CreateSystem<T>();
            _testGroup.AddSystemToUpdateList(handle);
            _testGroup.Update();
            _testGroup.RemoveSystemFromUpdateList(handle);
        }

        /// <summary>Advance tick, then run all simulation systems in pipeline order.</summary>
        public void RunFullTick()
        {
            AdvanceTicks(1);
            UpdateSystem<ContentLoaderSystem>();
            UpdateSystem<CalendarSystem>();
            UpdateSystem<BirthSystem>();
            UpdateSystem<AgeSystem>();
            UpdateSystem<NeedSystem>();
            UpdateSystem<PathfindingSystem>();
            UpdateSystem<CitizenMovementSystem>();
            UpdateSystem<HaulCompletionSystem>();
            UpdateSystem<BuildingProductionSystem>();
            UpdateSystem<CitizenHaulSystem>();
            UpdateSystem<DeathSystem>();
            UpdateSystem<DebugVizSystem>();
            UpdateSystem<SimulationStatsSystem>();
            UpdateSystem<EventDispatchSystem>();
        }

        /// <summary>
        /// Run the full simulation pipeline for many ticks efficiently.
        /// Systems are added to the update list once, then the group updates
        /// N times (running all systems each time). Avoids the state-slot
        /// exhaustion caused by add/remove per tick in RunFullTick().
        /// </summary>
        public void RunFullTicks(int count)
        {
            // Add all systems to the group once
            var systems = new SystemHandle[]
            {
                World.CreateSystem<TickDispatchSystem>(),
                World.CreateSystem<ContentLoaderSystem>(),
                World.CreateSystem<CalendarSystem>(),
                World.CreateSystem<BirthSystem>(),
                World.CreateSystem<AgeSystem>(),
                World.CreateSystem<NeedSystem>(),
                World.CreateSystem<PathfindingSystem>(),
                World.CreateSystem<CitizenMovementSystem>(),
                World.CreateSystem<HaulCompletionSystem>(),
                World.CreateSystem<BuildingProductionSystem>(),
                World.CreateSystem<CitizenHaulSystem>(),
                World.CreateSystem<DeathSystem>(),
                World.CreateSystem<DebugVizSystem>(),
                World.CreateSystem<EventDispatchSystem>(),
                World.CreateSystem<SimulationStatsSystem>(),
            };

            foreach (var handle in systems)
                _testGroup.AddSystemToUpdateList(handle);

            // Update N times — the group runs all systems in order each time
            for (int i = 0; i < count; i++)
                _testGroup.Update();

            // Clean up
            foreach (var handle in systems)
                _testGroup.RemoveSystemFromUpdateList(handle);
        }

        /// <summary>Run the bootstrap system to create a full MVP world.</summary>
        public void RunBootstrap()
        {
            // Destroy existing singletons so the bootstrap can create fresh ones
            DestroyExistingSingletons();

            var handle = World.CreateSystem<SimulationBootstrap>();
            _testGroup.AddSystemToUpdateList(handle);
            _testGroup.Update();
            _testGroup.RemoveSystemFromUpdateList(handle);
        }

        private void DestroyExistingSingletons()
        {
            // Destroy singleton entities individually (they're on separate entities)
            var configQuery = EntityManager.CreateEntityQuery(typeof(SimulationConfig));
            var calQuery = EntityManager.CreateEntityQuery(typeof(CalendarSingleton));
            var mapQuery = EntityManager.CreateEntityQuery(typeof(MapGridData));
            var statsQuery = EntityManager.CreateEntityQuery(typeof(SimulationStats));
            var eventQuery = EntityManager.CreateEntityQuery(typeof(SimulationEventSingleton));

            EntityManager.DestroyEntity(configQuery);
            EntityManager.DestroyEntity(calQuery);
            EntityManager.DestroyEntity(mapQuery);
            if (!statsQuery.IsEmpty)
                EntityManager.DestroyEntity(statsQuery);
            if (!eventQuery.IsEmpty)
                EntityManager.DestroyEntity(eventQuery);

            if (_mapGridBlob.IsCreated)
            {
                _mapGridBlob.Dispose();
                _mapGridBlob = default;
            }
        }

        public void Dispose()
        {
            if (_mapGridBlob.IsCreated) _mapGridBlob.Dispose();
            World?.Dispose();
        }
    }
}