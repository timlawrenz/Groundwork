using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    public struct RecipeOutput : IBufferElementData
    {
        public FixedString32Bytes ItemId;
        public int Quantity;
    }
}
