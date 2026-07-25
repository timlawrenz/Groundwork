using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Creates recipe and building definition entities at startup.
    /// All game content is registered here — no other system hardcodes
    /// recipe logic or building types. Runs once before SimulationBootstrap.
    /// </summary>
    public partial struct ContentLoaderSystem : ISystem
    {
        private bool _initialized;

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
                return;
            _initialized = true;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // ─── Recipe definitions ───

            var gatherFood = ecb.CreateEntity();
            ecb.AddComponent(gatherFood, new RecipeDefinitionData
            {
                RecipeId = "gather_food",
                TicksPerCycle = 1,
            });
            // No inputs
            ecb.AddBuffer<RecipeInput>(gatherFood);
            var gatherOut = ecb.AddBuffer<RecipeOutput>(gatherFood);
            gatherOut.Add(new RecipeOutput { ItemId = "food", Quantity = 1 });

            var chopFirewood = ecb.CreateEntity();
            ecb.AddComponent(chopFirewood, new RecipeDefinitionData
            {
                RecipeId = "chop_firewood",
                TicksPerCycle = 1,
            });
            var chopIn = ecb.AddBuffer<RecipeInput>(chopFirewood);
            chopIn.Add(new RecipeInput { ItemId = "logs", Quantity = 1 });
            var chopOut = ecb.AddBuffer<RecipeOutput>(chopFirewood);
            chopOut.Add(new RecipeOutput { ItemId = "firewood", Quantity = 1 });

            // ─── Building definitions ───

            var house = ecb.CreateEntity();
            ecb.AddComponent(house, new BuildingDefinitionData
            {
                BuildingType = "house",
                MaxWorkers = 0,
                RequiresWorkers = false,
                InputCapacity = 0,
                OutputCapacity = 200,
                Archetype = ProductionArchetype.Service,
                GatheringRadius = 0,
            });
            ecb.AddBuffer<BuildingRecipe>(house); // houses have no recipes

            var gathererHut = ecb.CreateEntity();
            ecb.AddComponent(gathererHut, new BuildingDefinitionData
            {
                BuildingType = "gatherer_hut",
                MaxWorkers = 4,
                RequiresWorkers = true,
                InputCapacity = 0,
                OutputCapacity = 200,
                Archetype = ProductionArchetype.Gathering,
                GatheringRadius = 5,
            });
            var ghRecipes = ecb.AddBuffer<BuildingRecipe>(gathererHut);
            ghRecipes.Add(new BuildingRecipe { RecipeId = "gather_food" });

            var woodcutter = ecb.CreateEntity();
            ecb.AddComponent(woodcutter, new BuildingDefinitionData
            {
                BuildingType = "woodcutter",
                MaxWorkers = 4,
                RequiresWorkers = true,
                InputCapacity = 200,
                OutputCapacity = 200,
                Archetype = ProductionArchetype.Workshop,
                GatheringRadius = 0,
            });
            var wcRecipes = ecb.AddBuffer<BuildingRecipe>(woodcutter);
            wcRecipes.Add(new BuildingRecipe { RecipeId = "chop_firewood" });

            
            // ─── Need definitions (ADR 2026-07-25 §3 — Needs Generalization) ───

            var foodNeed = ecb.CreateEntity();
            ecb.AddComponent(foodNeed, new NeedDefinition
            {
                NeedType = "food",
                SatisfyingItem = "food",
                UrgencyGrowthPerDay = 0.15f,
                ColdSeasonGrowthMultiplier = 1.0f,
                CriticalThreshold = 0.8f,
                HealthDecayRate = 2.0f,
                SatisfactionReduction = 0.5f,
            });

            var warmthNeed = ecb.CreateEntity();
            ecb.AddComponent(warmthNeed, new NeedDefinition
            {
                NeedType = "warmth",
                SatisfyingItem = "firewood",
                UrgencyGrowthPerDay = 0.01f,
                ColdSeasonGrowthMultiplier = 10.0f,
                CriticalThreshold = 0.8f,
                HealthDecayRate = 2.0f,
                SatisfactionReduction = 0.5f,
            });
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}