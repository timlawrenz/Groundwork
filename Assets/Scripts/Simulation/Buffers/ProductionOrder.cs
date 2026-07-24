using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// A recipe being produced in a building. Used as DynamicBuffer&lt;ProductionOrder&gt; on Building entities.
    /// </summary>
    public struct ProductionOrder : IBufferElementData
    {
        public FixedString32Bytes RecipeId;   // e.g. "logs_to_firewood"
        public float Progress;                // 0–1
        public Entity AssignedWorker;          // citizen working on this, or Entity.Null
    }
}
