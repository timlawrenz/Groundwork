using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Production archetype for a building. Determines how goods are produced
    /// and what worker requirements apply. Per ADR 2026-07-25.
    /// </summary>
    public enum ProductionArchetype : byte
    {
        Workshop,   // Worker at building tile, input→output transform
        Gathering,  // Worker in zone, harvests from map tiles
        Source,     // No worker, infinite supply to output
        Service,    // No goods, need relief only
    }

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
        public ProductionArchetype Archetype;
        public int GatheringRadius; // for Gathering archetype only, tiles from building center
        public byte FootprintSize;  // 1 = 1x1, 2 = 2x2, etc.
    }
}
