using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// The citizen's current task. Drives pathfinding, animation, and production.
    /// </summary>
    public struct CitizenTask : IComponentData
    {
        public FixedString32Bytes TaskType;  // "idle", "gather", "haul", "walk", "build", "produce"
        public Entity TargetEntity;          // building, resource, or Entity.Null
        public float Progress;               // 0–1
    }
}
