using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// A need that a citizen must satisfy (food, warmth, shelter, health, social).
    /// Used as DynamicBuffer&lt;CitizenNeed&gt; on Citizen entities.
    /// </summary>
    public struct CitizenNeed : IBufferElementData
    {
        public FixedString32Bytes NeedType;  // "food", "warmth", "shelter", "health", "social"
        public float Urgency;                // 0–1, higher = more urgent. Above 0.8 = critical.
    }
}
