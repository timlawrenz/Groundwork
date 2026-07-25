using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Human-specific traits for a citizen entity. Biological traits (Age, Sex,
    /// Health, Happiness) live on the LivingBeing component — shared with animals.
    /// Per ADR 2026-07-25 — LivingBeing Abstraction.
    /// </summary>
    public struct Citizen : IComponentData
    {
        public FixedString64Bytes Name;
        public byte EducationLevel;  // 0 = none, 1 = basic, 2 = advanced

        // Relationships (Entity references)
        public Entity HomeBuilding;
        public Entity WorkplaceBuilding;

        // Reproduction
        public int LastBirthYear;    // game year of last childbirth (0 = never)
    }
}