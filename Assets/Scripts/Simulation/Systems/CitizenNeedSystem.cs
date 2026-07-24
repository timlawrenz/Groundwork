using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Updates citizen needs each game-day. Decays health when critical needs go unmet.
    /// Needs are satisfied by production systems (e.g., eating food removes "food" need).
    /// Runs after CalendarSystem, before DeathSystem.
    /// </summary>
    [BurstCompile]
    public partial struct CitizenNeedSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            if (config.CurrentTick % config.TicksPerDay != 0)
                return;

            var calendar = SystemAPI.GetSingleton<CalendarSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (citizen, needsBuffer, entity) in SystemAPI.Query<RefRW<Citizen>, DynamicBuffer<CitizenNeed>>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                // Food need — always present, grows daily
                UpsertNeed(needsBuffer, "food", 0.15f);

                // Warmth need
                if (calendar.Season >= 2)
                    UpsertNeed(needsBuffer, "warmth", 0.2f);
                else
                    UpsertNeed(needsBuffer, "warmth", 0.02f);

                // Shelter need — homeless citizens
                if (citizen.ValueRO.HomeBuilding == Entity.Null)
                    UpsertNeed(needsBuffer, "shelter", 0.25f);

                // Health need — escalates when health is low
                if (citizen.ValueRO.Health < 50f)
                    UpsertNeed(needsBuffer, "health", 0.1f);

                // Social need
                UpsertNeed(needsBuffer, "social", 0.05f);

                // Apply critical needs → health decay
                for (int i = 0; i < needsBuffer.Length; i++)
                {
                    var need = needsBuffer[i];
                    if (need.Urgency > 0.8f)
                    {
                        float decay = (need.Urgency - 0.8f) * 2f;
                        citizen.ValueRW.Health -= decay;
                        if (citizen.ValueRW.Health < 0f)
                            citizen.ValueRW.Health = 0f;
                    }
                }

                // Death by health
                if (citizen.ValueRO.Health <= 0f && !SystemAPI.HasComponent<Dead>(entity))
                    ecb.AddComponent<Dead>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void UpsertNeed(DynamicBuffer<CitizenNeed> needs, FixedString32Bytes needType, float urgencyIncrease)
        {
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i].NeedType == needType)
                {
                    var need = needs[i];
                    need.Urgency = math.min(1f, need.Urgency + urgencyIncrease);
                    needs[i] = need;
                    return;
                }
            }
            needs.Add(new CitizenNeed { NeedType = needType, Urgency = urgencyIncrease });
        }
    }
}