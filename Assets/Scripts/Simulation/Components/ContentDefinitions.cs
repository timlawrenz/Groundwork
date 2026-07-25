using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Definition of a recipe: ticks per cycle. Inputs and outputs are stored
    /// as RecipeInput and RecipeOutput buffers on the same entity.
    /// </summary>
    public struct RecipeDefinitionData : IComponentData
    {
        public FixedString32Bytes RecipeId;
        public int TicksPerCycle;
    }

    /// <summary>
    /// Definition of a building type: worker configuration.
    /// Recipes are stored as BuildingRecipe buffers on the same entity.
    /// </summary>
    public struct BuildingDefinitionData : IComponentData
    {
        public FixedString32Bytes BuildingType;
        public int MaxWorkers;
        public bool RequiresWorkers;
        public int InputCapacity;   // max total items in input inventory
        public int OutputCapacity;  // max total items in output inventory
    }
}
