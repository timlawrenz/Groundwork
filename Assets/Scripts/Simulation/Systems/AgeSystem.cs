using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Ages all living beings each game-day, tags children and elderly citizens,
    /// and checks for age-based natural death. Works on LivingBeing component —
    /// citizens, animals, any living creature.
    /// Per ADR 2026-07-25 — LivingBeing Abstraction.
    /// </summary>
    [BurstCompile]
    public partial struct AgeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            if (config.CurrentTick % config.TicksPerDay != 0)
                return;

            float yearFraction = 1f / (config.TicksPerDay * config.DaysPerSeason * 4);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity);
            var eventBuffer = eventEntity != Entity.Null
                ? state.EntityManager.GetBuffer<SimulationEvent>(eventEntity)
                : default;

            // Age all living beings (citizens, future animals)
            foreach (var (lb, entity) in SystemAPI.Query<RefRW<LivingBeing>>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                lb.ValueRW.Age += yearFraction;
                float age = lb.ValueRO.Age;

                // Tag children (< 16) and elderly (> 60) — citizen-specific tags
                if (SystemAPI.HasComponent<Citizen>(entity))
                {
                    if (age < 16f && !SystemAPI.HasComponent<Child>(entity))
                        ecb.AddComponent<Child>(entity);
                    if (age >= 16f && SystemAPI.HasComponent<Child>(entity))
                        ecb.RemoveComponent<Child>(entity);
                    if (age >= 60f && !SystemAPI.HasComponent<Elderly>(entity))
                        ecb.AddComponent<Elderly>(entity);
                }

                // ─── Age-based natural death (probability ramps 60→90+) ───

                if (age >= 60f)
                {
                    float t = math.saturate((age - 55f) / 35f);
                    float yearlyDeathProb = t * t;
                    float dailyDeathProb = yearlyDeathProb / 365f;

                    uint seed = (uint)entity.Index ^ (uint)(config.CurrentTick / config.TicksPerDay);
                    var rng = new Random(seed);
                    if (rng.NextFloat() < dailyDeathProb)
                    {
                        ecb.AddComponent<Dead>(entity);
                        if (eventBuffer.IsCreated)
                            eventBuffer.Add(new SimulationEvent
                            {
                                Type = EventType.CitizenDied, EntityId = entity.Index,
                                Data0 = -1f, Data1 = age,
                            });
                    }
                }

                // Hard cutoff at 95
                if (age >= 95f && !SystemAPI.HasComponent<Dead>(entity))
                {
                    ecb.AddComponent<Dead>(entity);
                    if (eventBuffer.IsCreated)
                        eventBuffer.Add(new SimulationEvent
                        {
                            Type = EventType.CitizenDied, EntityId = entity.Index,
                            Data0 = -1f, Data1 = age,
                        });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}