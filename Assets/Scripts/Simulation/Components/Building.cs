using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// A building placed on the map. Buildings are entities with this component.
    /// </summary>
    public struct Building : IComponentData
    {
        public FixedString32Bytes BuildingType;   // "house", "woodcutter", "gatherer_hut"
        public float ConstructionProgress;        // 0–1, 1 = fully built
        public bool IsOperational;
        public int MaxWorkers;
        public byte FootprintSize;                // 1 = 1x1, 2 = 2x2, etc.
    }
}
