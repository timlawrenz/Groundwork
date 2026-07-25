using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Defines a gathering zone around a building. Workers must be within
    /// this radius to harvest resources. Zone covers a square of side
    /// (2*Radius + 1) centered on the building's tile.
    /// Per ADR 2026-07-25 — Building Abstraction & Production Archetypes.
    /// </summary>
    public struct GatheringZone : IComponentData
    {
        public int Radius;
    }
}