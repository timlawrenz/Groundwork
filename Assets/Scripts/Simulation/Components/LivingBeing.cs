using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Shared biological traits for any living entity (citizens, animals, etc.).
    /// Systems that operate on age, health, sex, or happiness query for LivingBeing
    /// rather than Citizen — making them reusable for non-human creatures.
    /// Per ADR 2026-07-25 — LivingBeing Abstraction.
    /// </summary>
    public struct LivingBeing : IComponentData
    {
        public float Age;
        public byte Sex;          // 0=male, 1=female
        public float Health;
        public float Happiness;
    }
}