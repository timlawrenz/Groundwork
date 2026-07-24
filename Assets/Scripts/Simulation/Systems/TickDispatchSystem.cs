using Unity.Entities;
using Unity.Burst;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Advances CurrentTick on the SimulationConfig singleton each simulation step.
    /// This is the heartbeat of the simulation. Runs first in the tick group.
    /// </summary>
    [BurstCompile]
    public partial struct TickDispatchSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // In headless mode, this runs every Unity frame and advances one tick.
            // In full game mode, we'd accumulate deltaTime and only tick when enough
            // real time has passed for the current TickSpeed.

            var config = SystemAPI.GetSingletonRW<SimulationConfig>();
            if (config.ValueRO.TickSpeed <= 0f)
                return; // paused

            config.ValueRW.CurrentTick++;
        }
    }
}
