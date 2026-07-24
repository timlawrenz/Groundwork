using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Creates the initial MVP world state. Runs once at startup.
    /// World: 100x100 flat temperate map, 50 citizens, 14 buildings, initial resources.
    /// After bootstrap, citizens get PathRequest to commute to their workplaces.
    /// </summary>
    public partial struct SimulationBootstrap : ISystem
    {
        private bool _initialized;
        private BlobAssetReference<MapGridBlob> _mapGridBlob;

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
                return;
            _initialized = true;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // === Map Grid (blob asset) ===
            CreateMapGrid(ref state, ecb);

            // === Simulation Config singleton ===
            var configEntity = ecb.CreateEntity();
            ecb.AddComponent(configEntity, SimulationConfig.Default);

            // === Calendar singleton ===
            var calendarEntity = ecb.CreateEntity();
            ecb.AddComponent(calendarEntity, new CalendarSingleton
            {
                Year = 1,
                Season = 0,
                DayOfSeason = 0,
                Temperature = 10f,
                Precipitation = 0.5f,
                DaylightHours = 12f,
                GrowingMultiplier = 0.5f,
            });

            // === Buildings ===
            for (int i = 0; i < 10; i++)
                CreateBuilding(ecb, "house", new int2(10 + i, 15));
            CreateBuilding(ecb, "woodcutter", new int2(20, 10));
            CreateBuilding(ecb, "woodcutter", new int2(25, 10));
            CreateBuilding(ecb, "gatherer_hut", new int2(30, 10));
            CreateBuilding(ecb, "gatherer_hut", new int2(35, 10));

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Second pass: add initial resources, create citizens, assign paths
            AddInitialResources(ref state);
            CreateCitizens(ref state);
            AssignInitialPaths(ref state);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_mapGridBlob.IsCreated)
                _mapGridBlob.Dispose();
        }

        private void CreateMapGrid(ref SystemState state, EntityCommandBuffer ecb)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MapGridBlob>();
            root.Width = 100;
            root.Height = 100;

            var walkable = builder.Allocate(ref root.Walkable, root.Width * root.Height);
            for (int i = 0; i < walkable.Length; i++)
                walkable[i] = 1; // all tiles walkable in MVP

            _mapGridBlob = builder.CreateBlobAssetReference<MapGridBlob>(Allocator.Persistent);
            builder.Dispose();

            var mapEntity = ecb.CreateEntity();
            ecb.AddComponent(mapEntity, new MapGridData { Grid = _mapGridBlob });
        }

        private static void CreateBuilding(EntityCommandBuffer ecb, FixedString32Bytes buildingType, int2 position)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new Building
            {
                BuildingType = buildingType,
                ConstructionProgress = 1f,
                IsOperational = true,
                MaxWorkers = buildingType == "house" ? 5 : 3,
            });
            ecb.AddComponent(entity, new MapPosition { TileCoordinate = position, Rotation = 0 });
            ecb.AddBuffer<InventorySlot>(entity);
            ecb.AddBuffer<ProductionOrder>(entity);
        }

        private static void AddInitialResources(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (building, inventory, productionQueue) in
                     SystemAPI.Query<RefRO<Building>, DynamicBuffer<InventorySlot>, DynamicBuffer<ProductionOrder>>())
            {
                if (building.ValueRO.BuildingType == "woodcutter")
                {
                    inventory.Add(new InventorySlot { ItemId = "logs", Quantity = 50 });
                    productionQueue.Add(new ProductionOrder { RecipeId = "chop_firewood", Progress = 0f });
                }
                else if (building.ValueRO.BuildingType == "gatherer_hut")
                {
                    inventory.Add(new InventorySlot { ItemId = "food", Quantity = 100 });
                    productionQueue.Add(new ProductionOrder { RecipeId = "gather_food", Progress = 0f });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void CreateCitizens(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var houses = new NativeList<Entity>(Allocator.Temp);
            var woodcutters = new NativeList<Entity>(Allocator.Temp);
            var gathererHuts = new NativeList<Entity>(Allocator.Temp);

            foreach (var (building, entity) in SystemAPI.Query<RefRO<Building>>().WithEntityAccess())
            {
                if (building.ValueRO.BuildingType == "house") houses.Add(entity);
                else if (building.ValueRO.BuildingType == "woodcutter") woodcutters.Add(entity);
                else if (building.ValueRO.BuildingType == "gatherer_hut") gathererHuts.Add(entity);
            }

            var random = new Random(42);

            for (int i = 0; i < 50; i++)
            {
                var entity = ecb.CreateEntity();
                float age = random.NextFloat(16f, 60f);
                byte sex = (byte)random.NextInt(0, 2);
                var homeEntity = houses[i % houses.Length];

                Entity workplace = Entity.Null;
                float roll = random.NextFloat();
                if (roll < 0.4f && woodcutters.Length > 0)
                    workplace = woodcutters[i % woodcutters.Length];
                else if (roll < 0.8f && gathererHuts.Length > 0)
                    workplace = gathererHuts[i % gathererHuts.Length];

                var homePos = new int2(10 + (i % 20), 16 + (i / 20));

                ecb.AddComponent(entity, new Citizen
                {
                    Name = $"Citizen {i + 1}",
                    Age = age,
                    Sex = sex,
                    Health = 100f,
                    Happiness = 50f,
                    EducationLevel = 0,
                    HomeBuilding = homeEntity,
                    WorkplaceBuilding = workplace,
                });

                ecb.AddComponent(entity, new MapPosition { TileCoordinate = homePos });

                ecb.AddComponent(entity, new CitizenTask
                {
                    TaskType = "idle",
                    TargetEntity = Entity.Null,
                    Progress = 0f,
                });

                if (age < 16f) ecb.AddComponent<Child>(entity);
                else if (age >= 60f) ecb.AddComponent<Elderly>(entity);

                var needs = ecb.AddBuffer<CitizenNeed>(entity);
                needs.Add(new CitizenNeed { NeedType = "food", Urgency = 0.3f });
                needs.Add(new CitizenNeed { NeedType = "warmth", Urgency = 0.1f });
                needs.Add(new CitizenNeed { NeedType = "social", Urgency = 0.1f });

                ecb.AddBuffer<InventorySlot>(entity);
                ecb.AddBuffer<PathFollowing>(entity);
            }

            houses.Dispose();
            woodcutters.Dispose();
            gathererHuts.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Give each employed citizen a PathRequest to their workplace.
        /// PathfindingSystem picks these up on the next tick.
        /// </summary>
        private static void AssignInitialPaths(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Build a lookup: building entity → MapPosition
            var buildingPositions = new NativeHashMap<Entity, int2>(20, Allocator.Temp);
            foreach (var (pos, entity) in SystemAPI.Query<RefRO<MapPosition>>()
                         .WithAll<Building>()
                         .WithEntityAccess())
            {
                buildingPositions.Add(entity, pos.ValueRO.TileCoordinate);
            }

            foreach (var (citizen, entity) in SystemAPI.Query<RefRO<Citizen>>()
                         .WithNone<Child>()
                         .WithEntityAccess())
            {
                if (citizen.ValueRO.WorkplaceBuilding != Entity.Null
                    && buildingPositions.TryGetValue(citizen.ValueRO.WorkplaceBuilding, out var wpPos))
                {
                    ecb.AddComponent<PathRequest>(entity, new PathRequest { Destination = wpPos });
                }
            }

            buildingPositions.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
