using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Minimal resource definition. These are templates, not per-entity data.
    /// Stored in a blob asset or ScriptableObject at runtime.
    /// </summary>
    public struct ItemDefinition
    {
        public FixedString32Bytes ItemId;
        public FixedString64Bytes DisplayName;
        public FixedString32Bytes Category;    // "raw", "processed", "food", "tool", "luxury"
        public float Weight;
        public float SpoilRate;                // days until spoiled (0 = non-perishable)
        public float BaseValue;
    }

    /// <summary>
    /// A recipe that transforms inputs into outputs at a building.
    /// Stored in a blob asset or ScriptableObject at runtime.
    /// </summary>
    public struct RecipeDefinition
    {
        public FixedString32Bytes RecipeId;
        public FixedString32Bytes ProducedInBuilding;  // which building type can run this
        public float DurationTicks;                     // how many ticks to complete
    }

    /// <summary>
    /// A single input requirement for a recipe. BlobArray element inside RecipeDefinition.
    /// </summary>
    public struct RecipeIngredient
    {
        public FixedString32Bytes ItemId;
        public int Quantity;
    }

    /// <summary>
    /// A single output product of a recipe. BlobArray element inside RecipeDefinition.
    /// </summary>
    public struct RecipeProduct
    {
        public FixedString32Bytes ItemId;
        public int Quantity;
    }
}
