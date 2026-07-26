using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Creates the initial world state in abundance mode — enough food, housing,
    /// and production to sustain a growing population. Runs once at startup.
    /// World: 100x100 flat temperate map, 50 citizens (25M/25F), 8 houses,
    /// 9 gatherer huts, 3 woodcutters, 3 forester huts.
    /// Multi-tile buildings (2x2) mark their footprint as blocked in the map grid.
    /// </summary>
    public partial struct SimulationBootstrap : ISystem
    {
        private bool _initialized;
        private BlobAssetReference<MapGridBlob> _mapGridBlob;

        private struct BuildingPlacement
        {
            public FixedString32Bytes BuildingType;
            public int2 Position;
            public int MaxWorkers;
            public byte FootprintSize;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
                return;
            _initialized = true;

            // ─── Phase 1: Collect building placements (before grid creation) ───

            var bDefQuery = state.GetEntityQuery(typeof(BuildingDefinitionData));
            var bDefs = bDefQuery.ToComponentDataArray<BuildingDefinitionData>(Allocator.Temp);
            var bDefMap = new NativeHashMap<FixedString32Bytes, BuildingDefinitionData>(bDefs.Length, Allocator.Temp);
            for (int i = 0; i < bDefs.Length; i++)
                bDefMap.TryAdd(bDefs[i].BuildingType, bDefs[i]);
            bDefs.Dispose();

            int GetMaxWorkers(FixedString32Bytes type) =>
                bDefMap.TryGetValue(type, out var d) ? d.MaxWorkers : 0;
            byte GetFootprintSize(FixedString32Bytes type) =>
                bDefMap.TryGetValue(type, out var d) ? d.FootprintSize : (byte)1;

            var placements = new NativeList<BuildingPlacement>(Allocator.Temp);

            // 8 houses (1x1)
            for (int i = 0; i < 8; i++)
                placements.Add(new BuildingPlacement { BuildingType = "house", Position = new int2(10 + i, 15), MaxWorkers = GetMaxWorkers("house"), FootprintSize = GetFootprintSize("house") });

            // 9 gatherer huts (2x2)
            for (int i = 0; i < 9; i++)
                placements.Add(new BuildingPlacement { BuildingType = "gatherer_hut", Position = new int2(20 + i * 3, 10), MaxWorkers = GetMaxWorkers("gatherer_hut"), FootprintSize = GetFootprintSize("gatherer_hut") });

            // 3 woodcutters (1x1)
            for (int i = 0; i < 3; i++)
                placements.Add(new BuildingPlacement { BuildingType = "woodcutter", Position = new int2(40 + i * 2, 10), MaxWorkers = GetMaxWorkers("woodcutter"), FootprintSize = GetFootprintSize("woodcutter") });

            // 3 forester huts (2x2)
            for (int i = 0; i < 3; i++)
                placements.Add(new BuildingPlacement { BuildingType = "forester_hut", Position = new int2(25 + i * 3, 20), MaxWorkers = GetMaxWorkers("forester_hut"), FootprintSize = GetFootprintSize("forester_hut") });

            bDefMap.Dispose();

            // ─── Phase 2: Create singletons + grid + buildings ───

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            CreateMapGrid(ecb, placements);

            var configEntity = ecb.CreateEntity();
            ecb.AddComponent(configEntity, SimulationConfig.Default);

            var calendarEntity = ecb.CreateEntity();
            ecb.AddComponent(calendarEntity, new CalendarSingleton
            {
                Year = 1, Season = 0, DayOfSeason = 0,
                Temperature = 10f, Precipitation = 0.5f,
                DaylightHours = 12f, GrowingMultiplier = 0.5f,
            });

            var eventEntity = ecb.CreateEntity();
            ecb.AddComponent<SimulationEventSingleton>(eventEntity);
            ecb.AddBuffer<SimulationEvent>(eventEntity);

            for (int i = 0; i < placements.Length; i++)
                CreateBuilding(ecb, placements[i]);

            placements.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // ─── Phase 3: Resources + citizens ───

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
            var eventQuery = state.GetEntityQuery(typeof(SimulationEventSingleton));
            if (eventQuery.IsEmpty) return;
            var eventEntity = eventQuery.GetSingletonEntity();
            var eventBuffer = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

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

        private void CreateMapGrid(EntityCommandBuffer ecb, NativeList<BuildingPlacement> placements)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MapGridBlob>();
            root.Width = 100;
            root.Height = 100;

            var walkable = builder.Allocate(ref root.Walkable, root.Width * root.Height);
            for (int i = 0; i < walkable.Length; i++)
                walkable[i] = 1;

            // Mark building tiles as blocked (skip the origin tile — it's the entrance)
            for (int i = 0; i < placements.Length; i++)
            {
                var p = placements[i];
                for (int dx = 0; dx < p.FootprintSize; dx++)
                {
                    for (int dy = 0; dy < p.FootprintSize; dy++)
                    {
                        if (dx == 0 && dy == 0) continue; // entrance tile stays walkable
                        int tx = p.Position.x + dx;
                        int ty = p.Position.y + dy;
                        if (tx >= 0 && tx < root.Width && ty >= 0 && ty < root.Height)
                            walkable[ty * root.Width + tx] = 0;
                    }
                }
            }

            _mapGridBlob = builder.CreateBlobAssetReference<MapGridBlob>(Allocator.Persistent);
            builder.Dispose();

            var mapEntity = ecb.CreateEntity();
            ecb.AddComponent(mapEntity, new MapGridData { Grid = _mapGridBlob });
        }

        private static void CreateBuilding(EntityCommandBuffer ecb, BuildingPlacement placement)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new Building
            {
                BuildingType = placement.BuildingType,
                ConstructionProgress = 1f,
                IsOperational = true,
                MaxWorkers = placement.MaxWorkers,
                FootprintSize = placement.FootprintSize,
            });
            ecb.AddComponent(entity, new MapPosition { TileCoordinate = placement.Position, Rotation = 0 });
            ecb.AddBuffer<OutputSlot>(entity);
            ecb.AddBuffer<ProductionOrder>(entity);

            if (placement.BuildingType == "woodcutter")
                ecb.AddBuffer<InventorySlot>(entity);

            if (placement.BuildingType == "gatherer_hut" || placement.BuildingType == "forester_hut")
                ecb.AddComponent(entity, new GatheringZone { Radius = 5 });
        }

        private void AddInitialResources(ref SystemState state)
        {
            var query = state.GetEntityQuery(
                typeof(Building),
                typeof(OutputSlot),
                typeof(ProductionOrder));

            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var outputInv = state.EntityManager.GetBuffer<OutputSlot>(entities[i]);
                var productionQueue = state.EntityManager.GetBuffer<ProductionOrder>(entities[i]);

                if (buildings[i].BuildingType == "woodcutter")
                {
                    if (state.EntityManager.HasBuffer<InventorySlot>(entities[i]))
                    {
                        var inputInv = state.EntityManager.GetBuffer<InventorySlot>(entities[i]);
                        inputInv.Add(new InventorySlot { ItemId = "logs", Quantity = 0 });
                    }
                    productionQueue.Add(new ProductionOrder { RecipeId = "chop_firewood", Progress = 0f });
                }
                else if (buildings[i].BuildingType == "gatherer_hut")
                {
                    outputInv.Add(new OutputSlot { ItemId = "food", Quantity = 0 });
                    productionQueue.Add(new ProductionOrder { RecipeId = "gather_food", Progress = 0f });
                }
                else if (buildings[i].BuildingType == "forester_hut")
                {
                    outputInv.Add(new OutputSlot { ItemId = "logs", Quantity = 0 });
                    productionQueue.Add(new ProductionOrder { RecipeId = "gather_logs", Progress = 0f });
                }
                else if (buildings[i].BuildingType == "house")
                {
                    outputInv.Add(new OutputSlot { ItemId = "food", Quantity = 0 });
                    outputInv.Add(new OutputSlot { ItemId = "firewood", Quantity = 0 });
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
            var foresterHuts = new NativeList<Entity>(Allocator.Temp);
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
                else if (buildingData[i].BuildingType == "forester_hut")
                    foresterHuts.Add(buildingEntities[i]);
                else if (buildingData[i].BuildingType == "woodcutter")
                    woodcutters.Add(buildingEntities[i]);
            }

            var random = new Unity.Mathematics.Random(42);
            const int citizenCount = 50;

            for (int i = 0; i < citizenCount; i++)
            {
                var entity = ecb.CreateEntity();
                float age = random.NextFloat(16f, 55f);
                byte sex = (byte)(i % 2);
                var homeEntity = houses[i % houses.Length];

                Entity workplace;
                if (i < 4 && woodcutters.Length > 0)
                    workplace = woodcutters[i % woodcutters.Length];
                else if (i < 8 && foresterHuts.Length > 0)
                    workplace = foresterHuts[i % foresterHuts.Length];
                else if (i < 42 && gathererHuts.Length > 0)
                    workplace = gathererHuts[i % gathererHuts.Length];
                else
                    workplace = Entity.Null;

                var homePos = new int2(10 + (i % 16), 16 + (i / 16));

                ecb.AddComponent(entity, new LivingBeing
                {
                    Age = age, Sex = sex, Health = 100f, Happiness = 60f,
                });
                ecb.AddComponent(entity, new Citizen
                {
                    Name = $"Citizen {i + 1}", EducationLevel = 0,
                    HomeBuilding = homeEntity, WorkplaceBuilding = workplace,
                    LastBirthYear = 0,
                });

                ecb.AddComponent(entity, new MapPosition { TileCoordinate = homePos, Rotation = 0 });
                ecb.AddComponent(entity, new CitizenTask
                {
                    TaskType = "idle", TargetEntity = Entity.Null, Progress = 0f,
                });

                if (age < 16f) ecb.AddComponent<Child>(entity);
                else if (age >= 60f) ecb.AddComponent<Elderly>(entity);

                var needs = ecb.AddBuffer<CitizenNeed>(entity);
                var needDefQuery = state.GetEntityQuery(typeof(NeedDefinition));
                var needDefs = needDefQuery.ToComponentDataArray<NeedDefinition>(Allocator.Temp);
                for (int n = 0; n < needDefs.Length; n++)
                {
                    if (needDefs[n].InitialUrgency > 0f)
                    {
                        needs.Add(new CitizenNeed
                        {
                            NeedType = needDefs[n].NeedType,
                            Urgency = needDefs[n].InitialUrgency,
                        });
                    }
                }
                needDefs.Dispose();

                ecb.AddBuffer<InventorySlot>(entity);
                ecb.AddBuffer<PathFollowing>(entity);
            }

            houses.Dispose();
            gathererHuts.Dispose();
            foresterHuts.Dispose();
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