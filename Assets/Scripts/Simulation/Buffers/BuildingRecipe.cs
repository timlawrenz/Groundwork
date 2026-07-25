using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    public struct BuildingRecipe : IBufferElementData
    {
        public FixedString32Bytes RecipeId;
    }
}
