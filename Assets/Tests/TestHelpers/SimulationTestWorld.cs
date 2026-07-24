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
    ///   testWorld.UpdateSystem&lt;CitizenAgeSystem&gt;();
    ///   var result = testWorld.EntityManager.GetComponentData&lt;Citizen&gt;(citizen);
    /// </summary>
    public class SimulationTestWorld : System.IDisposable
    {
        public World World { get; }
        public EntityManager EntityManager => World.EntityManager;

        private BlobAssetReference<MapGridBlob> _mapGridBlob;
        private Entity _configEntity;
        private Entity _calendarEntity;
        private Entity _mapGridEntity;

        /// <summary>Default map size for tests.</summary>
        public const int DefaultMapWidth = 20;
        public const int DefaultMapHeight = 20;

        public SimulationTestWorld(string worldName = "TestWorld")
        {
            World = new World(worldName);
            CreateSingletons();
        }

        private void CreateSingletons()
        {
            // SimulationConfig
            _configEntity = EntityManager.CreateEntity(typeof(SimulationConfig));
            var config = SimulationConfig.Default;
            config.MapWidth = DefaultMapWidth;
            config.MapHeight = DefaultMapHeight;
            EntityManager.SetComponentData(_configEntity, config);

            // CalendarSingleton
            _calendarEntity = EntityManager.CreateEntity(typeof(CalendarSingleton));
            EntityManager.SetComponentData(_calendarEntity, new CalendarSingleton
            {
                Year = 1,
                Season = 0, // spring
                DayOfSeason = 0,
                Temperature = 10f,
                Precipitation = 0.5f,
                DaylightHours = 12f,
                GrowingMultiplier = 0.5f,
            });

            // MapGridData (all walkable)
            CreateMapGrid();
        }

        private void CreateMapGrid()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MapGridBlob>();
            root.Width = DefaultMapWidth;
            root.Height = DefaultMapHeight;

            var walkable = builder.Allocate(ref root.Walkable, root.Width * root.Height);
            for (int i = 0; i < walkable.Length; i++)
                walkable[i] = 1;

            _mapGridBlob = builder.CreateBlobAssetReference<MapGridBlob>(Allocator.Persistent);
            builder.Dispose();

            _mapGridEntity = EntityManager.CreateEntity(typeof(MapGridData));
            EntityManager.SetComponentData(_mapGridEntity, new MapGridData { Grid = _mapGridBlob });
        }

        // ─── Entity creation helpers ───

        /// <summary>Create a citizen with all required components and initialized buffers.</summary>
        public Entity CreateCitizen(
            float age = 30f,
            int2? position = null,
            Entity home = default,
            Entity workplace = default,
            float health = 100f,
            float happiness = 50f)
        {
            var entity = EntityManager.CreateEntity(
                typeof(Citizen),
                typeof(MapPosition),
                typeof(CitizenTask),
                typeof(CitizenNeed),
                typeof(InventorySlot),
                typeof(PathFollowing)
            );

            EntityManager.SetComponentData(entity, new Citizen
            {
                Name = "Test Citizen",
                Age = age,
                Sex = 0,
                Health = health,
                Happiness = happiness,
                EducationLevel = 0,
                HomeBuilding = home,
                WorkplaceBuilding = workplace,
            });

            EntityManager.SetComponentData(entity, new MapPosition
            {
                TileCoordinate = position ?? int2.zero,
                Rotation = 0,
            });

            EntityManager.SetComponentData(entity, new CitizenTask
            {
                TaskType = "idle",
                TargetEntity = Entity.Null,
                Progress = 0f,
            });

            // Initial needs
            var needs = EntityManager.GetBuffer<CitizenNeed>(entity);
            needs.Add(new CitizenNeed { NeedType = "food", Urgency = 0.3f });
            needs.Add(new CitizenNeed { NeedType = "warmth", Urgency = 0.1f });

            // Apply age tags
            if (age < 16f)
                EntityManager.AddComponent<Child>(entity);
            else if (age >= 60f)
                EntityManager.AddComponent<Elderly>(entity);

            return entity;
        }

        /// <summary>Create a building with inventory and production buffers.</summary>
        public Entity CreateBuilding(
            FixedString32Bytes buildingType,
            int2 position,
            bool operational = true,
            int maxWorkers = 3)
        {
            var entity = EntityManager.CreateEntity(
                typeof(Building),
                typeof(MapPosition),
                typeof(InventorySlot),
                typeof(ProductionOrder)
            );

            EntityManager.SetComponentData(entity, new Building
            {
                BuildingType = buildingType,
                ConstructionProgress = 1f,
                IsOperational = operational,
                MaxWorkers = maxWorkers,
            });

            EntityManager.SetComponentData(entity, new MapPosition
            {
                TileCoordinate = position,
                Rotation = 0,
            });

            return entity;
        }

        /// <summary>Add items to a building or citizen inventory buffer.</summary>
        public void AddToInventory(Entity entity, FixedString32Bytes itemId, int quantity)
        {
            var inventory = EntityManager.GetBuffer<InventorySlot>(entity);
            inventory.Add(new InventorySlot { ItemId = itemId, Quantity = quantity });
        }

        /// <summary>Add a production order to a building.</summary>
        public void AddProductionOrder(Entity building, FixedString32Bytes recipeId)
        {
            var queue = EntityManager.GetBuffer<ProductionOrder>(building);
            queue.Add(new ProductionOrder { RecipeId = recipeId, Progress = 0f });
        }

        /// <summary>Set the current tick number.</summary>
        public void SetTick(long tick)
        {
            EntityManager.SetComponentData(_configEntity, new SimulationConfig
            {
                TicksPerDay = 24,
                DaysPerSeason = 30,
                CurrentTick = tick,
                TickSpeed = 1f,
                MapWidth = DefaultMapWidth,
                MapHeight = DefaultMapHeight,
            });
        }

        /// <summary>Advance the simulation by N ticks.</summary>
        public void AdvanceTicks(int count)
        {
            var tickHandle = World.CreateSystem<TickDispatchSystem>();
            for (int i = 0; i < count; i++)
                World.Unmanaged.ResolveSystemStateRef(tickHandle).Update(World.Unmanaged);
        }

        /// <summary>Run a specific ISystem once against this world.</summary>
        public void UpdateSystem<T>() where T : unmanaged, ISystem
        {
            var handle = World.CreateSystem<T>();
            World.Unmanaged.ResolveSystemStateRef(handle).Update(World.Unmanaged);
        }

        /// <summary>Advance the tick counter, then run all simulation systems in order.</summary>
        public void RunFullTick()
        {
            // Advance tick
            AdvanceTicks(1);

            // Run systems in pipeline order
            UpdateSystem<CalendarSystem>();
            UpdateSystem<CitizenAgeSystem>();
            UpdateSystem<CitizenNeedSystem>();
            UpdateSystem<PathfindingSystem>();
            UpdateSystem<CitizenMovementSystem>();
            UpdateSystem<BuildingProductionSystem>();
            UpdateSystem<DeathSystem>();
        }

        /// <summary>Run the bootstrap to create a full MVP world.</summary>
        public void RunBootstrap()
        {
            var handle = World.CreateSystem<SimulationBootstrap>();
            World.Unmanaged.ResolveSystemStateRef(handle).Update(World.Unmanaged);
        }

        public void Dispose()
        {
            if (_mapGridBlob.IsCreated)
                _mapGridBlob.Dispose();
            World?.Dispose();
        }
    }
}