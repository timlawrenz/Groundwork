using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Ages citizens each game-day, tags children and elderly, and checks for natural death.
    /// Death probability increases with age: 0% at 60, ramping to ~100%/year at 90+.
    /// Deterministic per citizen — same seed always produces same outcome.
    /// Runs after CalendarSystem.
    /// </summary>
    [BurstCompile]
    public partial struct CitizenAgeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            if (config.CurrentTick % config.TicksPerDay != 0)
                return;

            float yearFraction = 1f / (config.TicksPerDay * config.DaysPerSeason * 4);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Get event buffer for death events
            SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity);
            var eventBuffer = eventEntity != Entity.Null
                ? state.EntityManager.GetBuffer<SimulationEvent>(eventEntity)
                : default;

            foreach (var (citizen, entity) in SystemAPI.Query<RefRW<Citizen>>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                citizen.ValueRW.Age += yearFraction;
                float age = citizen.ValueRO.Age;

                // Tag children (< 16) — can't work
                if (age < 16f && !SystemAPI.HasComponent<Child>(entity))
                    ecb.AddComponent<Child>(entity);
                if (age >= 16f && SystemAPI.HasComponent<Child>(entity))
                    ecb.RemoveComponent<Child>(entity);

                // Tag elderly (> 60) — reduced work capacity
                if (age >= 60f && !SystemAPI.HasComponent<Elderly>(entity))
                    ecb.AddComponent<Elderly>(entity);

                // ─── Age-based natural death (probability ramps 60→90+) ───

                if (age >= 60f)
                {
                    // Yearly death probability: quadratic ramp from ~2% at 60 to ~100% at 90
                    // Per-day probability: yearly / 365 (approximate)
                    float t = math.saturate((age - 55f) / 35f); // 0→1 from 55→90
                    float yearlyDeathProb = t * t;                // quadratic: 2% at 60, 18% at 70, 51% at 80
                    float dailyDeathProb = yearlyDeathProb / 365f;

                    // Deterministic random: seed = entity index XOR current day
                    uint seed = (uint)entity.Index ^ (uint)(config.CurrentTick / config.TicksPerDay);
                    var rng = new Random(seed);
                    float roll = rng.NextFloat();

                    if (roll < dailyDeathProb)
                    {
                        ecb.AddComponent<Dead>(entity);

                        if (eventBuffer.IsCreated)
                        {
                            eventBuffer.Add(new SimulationEvent
                            {
                                Type = EventType.CitizenDied,
                                EntityId = entity.Index,
                                Data0 = -1f, // natural death marker
                                Data1 = age,
                            });
                        }
                    }
                }

                // Hard cutoff at 95 — nobody lives past this
                if (age >= 95f && !SystemAPI.HasComponent<Dead>(entity))
                {
                    ecb.AddComponent<Dead>(entity);
                    if (eventBuffer.IsCreated)
                    {
                        eventBuffer.Add(new SimulationEvent
                        {
                            Type = EventType.CitizenDied,
                            EntityId = entity.Index,
                            Data0 = -1f,
                            Data1 = age,
                        });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}