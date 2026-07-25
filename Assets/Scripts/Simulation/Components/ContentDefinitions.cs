using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Definition of a recipe: what inputs are consumed, what outputs are produced,
    /// and how many ticks it takes to complete one cycle.
    /// Stored as an entity with RecipeInput and RecipeOutput buffers.
    /// </summary>
    public struct RecipeDefinitionData : IComponentData
    {
        public FixedString32Bytes RecipeId;
        public int TicksPerCycle;       // how many ticks for one complete cycle
    }

    /// <summary>
    /// Buffer element: one input requirement for a recipe.
    /// </summary>
    public struct RecipeInput : IBufferElementData
    {
        public FixedString32Bytes ItemId;
        public int Quantity;            // consumed per cycle
    }

    /// <summary>
    /// Buffer element: one output product of a recipe.
    /// </summary>
    public struct RecipeOutput : IBufferElementData
    {
        public FixedString32Bytes ItemId;
        public int Quantity;            // produced per cycle
    }

    /// <summary>
    /// Definition of a building type: how many workers it can have,
    /// whether workers are required, and which recipes it can run.
    /// Stored as an entity with BuildingRecipe buffers.
    /// </summary>
    public struct BuildingDefinitionData : IComponentData
    {
        public FixedString32Bytes BuildingType;
        public int MaxWorkers;          // 0 = autonomous (well, windmill)
        public bool RequiresWorkers;    // if true, needs at least 1 worker when MaxWorkers > 0
    }

    /// <summary>
    /// Buffer element: links a building definition to one of its recipes.
    /// </summary>
    public struct BuildingRecipe : IBufferElementData
    {
        public FixedString32Bytes RecipeId;
    }
}
