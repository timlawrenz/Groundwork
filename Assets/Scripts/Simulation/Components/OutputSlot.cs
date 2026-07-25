using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Output inventory slot for a building. Holds finished goods available
    /// for pickup by hauling citizens or for direct consumption by citizens
    /// (food, firewood, water). Production systems deposit here.
    /// Hauling citizens pick up from here. NeedSystem consumes from here.
    /// Separate from InventorySlot (input inventory) to prevent citizens
    /// from stealing raw materials before processing.
    /// Per ADR 2026-07-25 — Building Abstraction & Production Archetypes.
    /// </summary>
    public struct OutputSlot : IBufferElementData
    {
        public FixedString32Bytes ItemId;
        public int Quantity;
    }
}