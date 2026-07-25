using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Defines a citizen need type — what item satisfies it, how urgency grows,
    /// seasonal climate modifiers, and what happens at critical urgency.
    /// Created by ContentLoaderSystem at startup. Read by NeedSystem.
    /// Part of ADR 2026-07-25 §3 — Needs Generalization.
    /// </summary>
    public struct NeedDefinition : IComponentData
    {
        /// <summary>The need type this definition governs (e.g. "food", "warmth").</summary>
        public FixedString32Bytes NeedType;

        /// <summary>The item that satisfies this need when consumed (e.g. "food", "firewood").
        /// Empty if this is a condition-based need (shelter, health) rather than commodity-based.</summary>
        public FixedString32Bytes SatisfyingItem;

        /// <summary>Base urgency growth per game-day.</summary>
        public float UrgencyGrowthPerDay;

        /// <summary>Multiplier applied to growth rate during cold seasons (fall/winter).
        /// 1.0 = no seasonal effect. 10.0 = 10x faster urgency growth in winter.</summary>
        public float ColdSeasonGrowthMultiplier;

        /// <summary>Urgency level above which health decay begins.</summary>
        public float CriticalThreshold;

        /// <summary>Health lost per day when urgency exceeds CriticalThreshold.
        /// Formula: health_loss = (urgency - threshold) * rate</summary>
        public float HealthDecayRate;

        /// <summary>How much urgency is reduced when the need is satisfied (consuming an item).</summary>
        public float SatisfactionReduction;

        /// <summary>Urgency value when a citizen is first created. Commodity needs
        /// (food, warmth) start above 0. Condition-based needs (shelter, health)
        /// start at 0 and are triggered by runtime conditions.</summary>
        public float InitialUrgency;
    }
}