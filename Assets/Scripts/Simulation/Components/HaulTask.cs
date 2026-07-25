using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Attached to a citizen when they are hauling goods between buildings.
    /// Phase 0 = en route to source building (to pick up).
    /// Phase 1 = en route to destination building (to drop off).
    /// Removed when delivery is complete.
    /// Part of ADR 2026-07-25 §2 — Citizen-Driven Goods Transport.
    /// </summary>
    public struct HaulTask : IComponentData
    {
        /// <summary>Building to pick up goods from.</summary>
        public Entity SourceBuilding;

        /// <summary>Building to deliver goods to.</summary>
        public Entity DestinationBuilding;

        /// <summary>Item being hauled (e.g. "firewood", "food").</summary>
        public FixedString32Bytes ItemId;

        /// <summary>How many units to transport.</summary>
        public int Quantity;

        /// <summary>0 = going to source, 1 = going to destination.</summary>
        public byte Phase;
    }
}