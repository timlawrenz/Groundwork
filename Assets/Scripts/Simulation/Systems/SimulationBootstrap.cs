using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Creates the initial world state in abundance mode — enough food, housing,
    /// and production to sustain a growing population. Runs once at startup.
    /// World: 100x100 flat temperate map, 50 citizens (25M/25F), 8 houses,
    /// 8 gatherer huts, 2 woodcutters, generous starting resources.
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

            CreateMapGrid(ref state, ecb);

            var configEntity = ecb.CreateEntity();
            ecb.AddComponent(configEntity, SimulationConfig.Default);

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

            // Event buffer singleton
            var eventEntity = ecb.CreateEntity();
            ecb.AddComponent<SimulationEventSingleton>(eventEntity);
            ecb.AddBuffer<SimulationEvent>(eventEntity);

            // Buildings: 8 houses, 9 gatherer huts, 3 woodcutters — read MaxWorkers from definitions
            var bDefQuery = state.GetEntityQuery(typeof(BuildingDefinitionData));
            var bDefs = bDefQuery.ToComponentDataArray<BuildingDefinitionData>(Allocator.Temp);
            var bDefMap = new NativeHashMap<FixedString32Bytes, int>(bDefs.Length, Allocator.Temp);
            for (int i = 0; i < bDefs.Length; i++)
                bDefMap.TryAdd(bDefs[i].BuildingType, bDefs[i].MaxWorkers);
            bDefs.Dispose();

            int GetMaxWorkers(FixedString32Bytes type) =>
                bDefMap.TryGetValue(type, out int mw) ? mw : 0;

            for (int i = 0; i < 8; i++)
                CreateBuilding(ecb, "house", new int2(10 + i, 15), GetMaxWorkers("house"));
            for (int i = 0; i < 9; i++)
                CreateBuilding(ecb, "gatherer_hut", new int2(20 + i * 2, 10), GetMaxWorkers("gatherer_hut"));
            CreateBuilding(ecb, "woodcutter", new int2(40, 10), GetMaxWorkers("woodcutter"));
            CreateBuilding(ecb, "woodcutter", new int2(42, 10), GetMaxWorkers("woodcutter"));
            CreateBuilding(ecb, "woodcutter", new int2(44, 10), GetMaxWorkers("woodcutter"));

            bDefMap.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            AddInitialResources(ref state);
            CreateCitizens(ref state);
            AssignInitialPaths(ref state);
            EmitBootstrapEvents(ref state);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_mapGridBlob.IsCreated)
                _mapGridBlob.Dispose();
        }

        private void EmitBootstrapEvents(ref SystemState state)
        {
            // Find the event buffer singleton
            var eventQuery = state.GetEntityQuery(typeof(SimulationEventSingleton));
            if (eventQuery.IsEmpty) return;
            var eventEntity = eventQuery.GetSingletonEntity();
            var eventBuffer = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

            // Emit BuildingPlaced for all buildings
            var buildingQuery = state.GetEntityQuery(typeof(Building), typeof(MapPosition));
            var buildings = buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            var entities = buildingQuery.ToEntityArray(Allocator.Temp);
            var positions = buildingQuery.ToComponentDataArray<MapPosition>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                eventBuffer.Add(new SimulationEvent
                {
                    Type = EventType.BuildingPlaced,
                    EntityId = entities[i].Index,
                    Data0 = positions[i].TileCoordinate.x,
                    Data1 = positions[i].TileCoordinate.y,
                });
            }
            buildings.Dispose();
            entities.Dispose();
            positions.Dispose();

            // Emit CitizenSpawned for all citizens
            var citizenQuery = state.GetEntityQuery(typeof(Citizen), typeof(MapPosition));
            var citizens = citizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
            var cEntities = citizenQuery.ToEntityArray(Allocator.Temp);
            var cPositions = citizenQuery.ToComponentDataArray<MapPosition>(Allocator.Temp);
            for (int i = 0; i < cEntities.Length; i++)
            {
                eventBuffer.Add(new SimulationEvent
                {
                    Type = EventType.CitizenSpawned,
                    EntityId = cEntities[i].Index,
                    Data0 = cPositions[i].TileCoordinate.x,
                    Data1 = cPositions[i].TileCoordinate.y,
                });
            }
            citizens.Dispose();
            cEntities.Dispose();
            cPositions.Dispose();
        }

        private void CreateMapGrid(ref SystemState state, EntityCommandBuffer ecb)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MapGridBlob>();
            root.Width = 100;
            root.Height = 100;

            var walkable = builder.Allocate(ref root.Walkable, root.Width * root.Height);
            for (int i = 0; i < walkable.Length; i++)
                walkable[i] = 1;

            _mapGridBlob = builder.CreateBlobAssetReference<MapGridBlob>(Allocator.Persistent);
            builder.Dispose();

            var mapEntity = ecb.CreateEntity();
            ecb.AddComponent(mapEntity, new MapGridData { Grid = _mapGridBlob });
        }

        private static void CreateBuilding(EntityCommandBuffer ecb, FixedString32Bytes buildingType,
            int2 position, int maxWorkers)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new Building
            {
                BuildingType = buildingType,
                ConstructionProgress = 1f,
                IsOperational = true,
                MaxWorkers = maxWorkers,
            });
            ecb.AddComponent(entity, new MapPosition { TileCoordinate = position, Rotation = 0 });
            ecb.AddBuffer<InventorySlot>(entity);
            ecb.AddBuffer<ProductionOrder>(entity);
        }

        private void AddInitialResources(ref SystemState state)
        {
            var query = state.GetEntityQuery(
                typeof(Building),
                typeof(InventorySlot),
                typeof(ProductionOrder));

            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var inventory = state.EntityManager.GetBuffer<InventorySlot>(entities[i]);
                var productionQueue = state.EntityManager.GetBuffer<ProductionOrder>(entities[i]);

                if (buildings[i].BuildingType == "woodcutter")
                {
                    inventory.Add(new InventorySlot { ItemId = "logs", Quantity = 50000 });
                    productionQueue.Add(new ProductionOrder { RecipeId = "chop_firewood", Progress = 0f });
                }
                else if (buildings[i].BuildingType == "gatherer_hut")
                {
                    inventory.Add(new InventorySlot { ItemId = "food", Quantity = 2000 });
                    productionQueue.Add(new ProductionOrder { RecipeId = "gather_food", Progress = 0f });
                }
                else if (buildings[i].BuildingType == "house")
                {
                    inventory.Add(new InventorySlot { ItemId = "food", Quantity = 500 });
                    inventory.Add(new InventorySlot { ItemId = "firewood", Quantity = 1000 });
                }
            }

            buildings.Dispose();
            entities.Dispose();
        }

        private void CreateCitizens(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var houses = new NativeList<Entity>(Allocator.Temp);
            var gathererHuts = new NativeList<Entity>(Allocator.Temp);
            var woodcutters = new NativeList<Entity>(Allocator.Temp);

            var buildingQuery = state.GetEntityQuery(typeof(Building));
            var buildingEntities = buildingQuery.ToEntityArray(Allocator.Temp);
            var buildingData = buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);

            for (int i = 0; i < buildingEntities.Length; i++)
            {
                if (buildingData[i].BuildingType == "house")
                    houses.Add(buildingEntities[i]);
                else if (buildingData[i].BuildingType == "gatherer_hut")
                    gathererHuts.Add(buildingEntities[i]);
                else if (buildingData[i].BuildingType == "woodcutter")
                    woodcutters.Add(buildingEntities[i]);
            }

            var random = new Unity.Mathematics.Random(42);
            const int citizenCount = 50;

            for (int i = 0; i < citizenCount; i++)
            {
                var entity = ecb.CreateEntity();
                float age = random.NextFloat(16f, 55f);
                byte sex = (byte)(i % 2); // alternate male/female for even distribution
                var homeEntity = houses[i % houses.Length];

                // Most citizens are gatherers, a few are woodcutters
                Entity workplace;
                if (i < 6 && woodcutters.Length > 0)
                    workplace = woodcutters[i % woodcutters.Length]; // first 6 → woodcutters
                else
                    workplace = gathererHuts[i % gathererHuts.Length]; // rest → gatherers

                var homePos = new int2(10 + (i % 16), 16 + (i / 16));

                ecb.AddComponent(entity, new Citizen
                {
                    Name = $"Citizen {i + 1}",
                    Age = age,
                    Sex = sex,
                    Health = 100f,
                    Happiness = 60f,
                    EducationLevel = 0,
                    HomeBuilding = homeEntity,
                    WorkplaceBuilding = workplace,
                    LastBirthYear = 0,
                });

                ecb.AddComponent(entity, new MapPosition { TileCoordinate = homePos, Rotation = 0 });
                ecb.AddComponent(entity, new CitizenTask
                {
                    TaskType = "idle",
                    TargetEntity = Entity.Null,
                    Progress = 0f,
                });

                if (age < 16f) ecb.AddComponent<Child>(entity);
                else if (age >= 60f) ecb.AddComponent<Elderly>(entity);

                var needs = ecb.AddBuffer<CitizenNeed>(entity);
                needs.Add(new CitizenNeed { NeedType = "food", Urgency = 0.2f });
                needs.Add(new CitizenNeed { NeedType = "warmth", Urgency = 0.1f });

                ecb.AddBuffer<InventorySlot>(entity);
                ecb.AddBuffer<PathFollowing>(entity);
            }

            houses.Dispose();
            gathererHuts.Dispose();
            woodcutters.Dispose();
            buildingEntities.Dispose();
            buildingData.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void AssignInitialPaths(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var buildingPositions = new NativeHashMap<Entity, int2>(20, Allocator.Temp);

            var buildingPosQuery = state.GetEntityQuery(typeof(Building), typeof(MapPosition));
            var buildingEntities = buildingPosQuery.ToEntityArray(Allocator.Temp);
            var positions = buildingPosQuery.ToComponentDataArray<MapPosition>(Allocator.Temp);

            for (int i = 0; i < buildingEntities.Length; i++)
                buildingPositions.Add(buildingEntities[i], positions[i].TileCoordinate);

            var citizenQuery = state.GetEntityQuery(
                typeof(Citizen),
                ComponentType.Exclude<Child>());
            var citizenEntities = citizenQuery.ToEntityArray(Allocator.Temp);
            var citizens = citizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);

            for (int i = 0; i < citizenEntities.Length; i++)
            {
                if (citizens[i].WorkplaceBuilding != Entity.Null
                    && buildingPositions.TryGetValue(citizens[i].WorkplaceBuilding, out var wpPos))
                {
                    ecb.AddComponent<PathRequest>(citizenEntities[i],
                        new PathRequest { Destination = wpPos });
                }
            }

            buildingPositions.Dispose();
            buildingEntities.Dispose();
            positions.Dispose();
            citizenEntities.Dispose();
            citizens.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}