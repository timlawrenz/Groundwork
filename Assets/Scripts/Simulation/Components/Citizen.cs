using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// A citizen of the settlement. Every citizen is an entity with this component.
    /// </summary>
    public struct Citizen : IComponentData
    {
        public FixedString64Bytes Name;
        public float Age;            // years, increments each season
        public byte Sex;             // 0 = male, 1 = female
        public float Health;         // 0–100, below 20 = sick, 0 = dead
        public float Happiness;      // 0–100
        public byte EducationLevel;  // 0 = none, 1 = basic, 2 = advanced

        // Relationships (Entity references)
        public Entity HomeBuilding;
        public Entity WorkplaceBuilding;
    }
}
