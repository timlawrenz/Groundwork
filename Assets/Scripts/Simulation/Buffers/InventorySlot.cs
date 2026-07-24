using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// A single slot in an entity's inventory. Used as DynamicBuffer&lt;InventorySlot&gt; on Citizen and Building.
    /// </summary>
    public struct InventorySlot : IBufferElementData
    {
        public FixedString32Bytes ItemId;
        public int Quantity;
        public int Capacity;  // per-slot capacity (for building storage), 0 = unlimited for citizen pockets
    }
}
