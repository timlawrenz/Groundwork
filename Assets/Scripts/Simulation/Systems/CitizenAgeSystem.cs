using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Ages citizens each game-day, tags children and elderly, and checks for natural death.
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
                return; // only run once per game-day

            float yearFraction = 1f / (config.TicksPerDay * config.DaysPerSeason * 4); // 4 seasons

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (citizen, entity) in SystemAPI.Query<RefRW<Citizen>>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                citizen.ValueRW.Age += yearFraction;

                // Tag children (< 16) — can't work
                if (citizen.ValueRO.Age < 16f && !SystemAPI.HasComponent<Child>(entity))
                    ecb.AddComponent<Child>(entity);

                // Remove child tag when they come of age
                if (citizen.ValueRO.Age >= 16f && SystemAPI.HasComponent<Child>(entity))
                    ecb.RemoveComponent<Child>(entity);

                // Tag elderly (> 60) — reduced work capacity
                if (citizen.ValueRO.Age >= 60f && !SystemAPI.HasComponent<Elderly>(entity))
                    ecb.AddComponent<Elderly>(entity);

                // Natural death at extreme old age
                if (citizen.ValueRO.Age >= 90f && !SystemAPI.HasComponent<Dead>(entity))
                    ecb.AddComponent<Dead>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
