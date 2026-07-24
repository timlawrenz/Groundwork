using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Creates the initial MVP world state. Runs once at startup.
    /// World: 100x100 flat temperate map, 50 citizens, 14 buildings, initial resources.
    /// </summary>
    public partial struct SimulationBootstrap : ISystem
    {
        private bool _initialized;

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
                return;
            _initialized = true;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // === Simulation Config singleton ===
            var configEntity = ecb.CreateEntity();
            ecb.AddComponent(configEntity, SimulationConfig.Default);

            // === Calendar singleton ===
            var calendarEntity = ecb.CreateEntity();
            ecb.AddComponent(calendarEntity, new CalendarSingleton
            {
                Year = 1,
                Season = 0,         // spring
                DayOfSeason = 0,
                Temperature = 10f,
                Precipitation = 0.5f,
                DaylightHours = 12f,
                GrowingMultiplier = 0.5f,
            });

            // === Buildings ===
            // 10 houses
            for (int i = 0; i < 10; i++)
            {
                CreateBuilding(ecb, "house", new int2(10 + i, 15));
            }
            // 2 woodcutters
            CreateBuilding(ecb, "woodcutter", new int2(20, 10));
            CreateBuilding(ecb, "woodcutter", new int2(25, 10));
            // 2 gatherer's huts
            CreateBuilding(ecb, "gatherer_hut", new int2(30, 10));
            CreateBuilding(ecb, "gatherer_hut", new int2(35, 10));

            // Add initial resources to woodcutters and gatherer's huts
            // We need to collect the building entities — use a query after playback
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Second pass: add initial resources and assign workers
            AddInitialResources(ref state);
            CreateCitizens(ref state);
        }

        private static void CreateBuilding(EntityCommandBuffer ecb, FixedString32Bytes buildingType, int2 position)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new Building
            {
                BuildingType = buildingType,
                ConstructionProgress = 1f, // fully built
                IsOperational = true,
                MaxWorkers = buildingType == "house" ? 5 : 3, // houses hold 5, workplaces 3
            });
            ecb.AddComponent(entity, new MapPosition
            {
                TileCoordinate = position,
                Rotation = 0,
            });
            ecb.AddBuffer<InventorySlot>(entity);
            ecb.AddBuffer<ProductionOrder>(entity);
        }

        private static void AddInitialResources(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (building, inventory, productionQueue, entity) in
                     SystemAPI.Query<RefRO<Building>, DynamicBuffer<InventorySlot>, DynamicBuffer<ProductionOrder>>()
                         .WithEntityAccess())
            {
                if (building.ValueRO.BuildingType == "woodcutter")
                {
                    // Start with 50 logs and an active firewood order
                    inventory.Add(new InventorySlot { ItemId = "logs", Quantity = 50 });
                    productionQueue.Add(new ProductionOrder { RecipeId = "chop_firewood", Progress = 0f });
                }
                else if (building.ValueRO.BuildingType == "gatherer_hut")
                {
                    // Start with 100 food and an active gathering order
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

            // Collect building entities for assignment
            var houses = new NativeList<Entity>(Allocator.Temp);
            var woodcutters = new NativeList<Entity>(Allocator.Temp);
            var gathererHuts = new NativeList<Entity>(Allocator.Temp);

            foreach (var (building, entity) in SystemAPI.Query<RefRO<Building>>().WithEntityAccess())
            {
                if (building.ValueRO.BuildingType == "house")
                    houses.Add(entity);
                else if (building.ValueRO.BuildingType == "woodcutter")
                    woodcutters.Add(entity);
                else if (building.ValueRO.BuildingType == "gatherer_hut")
                    gathererHuts.Add(entity);
            }

            var random = new Random(42); // deterministic seed

            for (int i = 0; i < 50; i++)
            {
                var entity = ecb.CreateEntity();

                float age = random.NextFloat(16f, 60f); // adults only in starting town
                byte sex = (byte)random.NextInt(0, 2);
                var homeIdx = i % houses.Length;
                var homeEntity = houses[homeIdx];

                // Assign workplace: ~40% woodcutters, ~40% gatherers, 20% unemployed
                Entity workplace = Entity.Null;
                float roll = random.NextFloat();
                if (roll < 0.4f && woodcutters.Length > 0)
                    workplace = woodcutters[i % woodcutters.Length];
                else if (roll < 0.8f && gathererHuts.Length > 0)
                    workplace = gathererHuts[i % gathererHuts.Length];

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

                ecb.AddComponent(entity, new MapPosition
                {
                    TileCoordinate = new int2(10 + (i % 20), 16 + (i / 20)),
                });

                ecb.AddComponent(entity, new CitizenTask
                {
                    TaskType = "idle",
                    TargetEntity = Entity.Null,
                    Progress = 0f,
                });

                // Age-based tags
                if (age < 16f)
                    ecb.AddComponent<Child>(entity);
                else if (age >= 60f)
                    ecb.AddComponent<Elderly>(entity);

                var needs = ecb.AddBuffer<CitizenNeed>(entity);
                needs.Add(new CitizenNeed { NeedType = "food", Urgency = 0.3f });
                needs.Add(new CitizenNeed { NeedType = "warmth", Urgency = 0.1f });
                needs.Add(new CitizenNeed { NeedType = "social", Urgency = 0.1f });

                ecb.AddBuffer<InventorySlot>(entity);
            }

            houses.Dispose();
            woodcutters.Dispose();
            gathererHuts.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}