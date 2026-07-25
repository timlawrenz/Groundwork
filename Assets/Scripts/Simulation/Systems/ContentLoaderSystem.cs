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
                TicksPerCycle = 10,
            });
            // No inputs
            ecb.AddBuffer<RecipeInput>(gatherFood);
            var gatherOut = ecb.AddBuffer<RecipeOutput>(gatherFood);
            gatherOut.Add(new RecipeOutput { ItemId = "food", Quantity = 1 });

            var chopFirewood = ecb.CreateEntity();
            ecb.AddComponent(chopFirewood, new RecipeDefinitionData
            {
                RecipeId = "chop_firewood",
                TicksPerCycle = 10,
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
            });
            ecb.AddBuffer<BuildingRecipe>(house); // houses have no recipes

            var gathererHut = ecb.CreateEntity();
            ecb.AddComponent(gathererHut, new BuildingDefinitionData
            {
                BuildingType = "gatherer_hut",
                MaxWorkers = 4,
                RequiresWorkers = true,
            });
            var ghRecipes = ecb.AddBuffer<BuildingRecipe>(gathererHut);
            ghRecipes.Add(new BuildingRecipe { RecipeId = "gather_food" });

            var woodcutter = ecb.CreateEntity();
            ecb.AddComponent(woodcutter, new BuildingDefinitionData
            {
                BuildingType = "woodcutter",
                MaxWorkers = 4,
                RequiresWorkers = true,
            });
            var wcRecipes = ecb.AddBuffer<BuildingRecipe>(woodcutter);
            wcRecipes.Add(new BuildingRecipe { RecipeId = "chop_firewood" });

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}