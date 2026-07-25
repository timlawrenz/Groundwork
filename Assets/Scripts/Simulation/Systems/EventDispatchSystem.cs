using Unity.Entities;
using Unity.Burst;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Processes the SimulationEvent buffer each tick. Reads all events in emission
    /// order, invokes Lua mod hooks for subscribed event types, then clears the buffer.
    /// Runs after DeathSystem (all state changes complete), before SimulationStatsSystem
    /// (stats capture includes event outcomes).
    /// </summary>
    [BurstCompile]
    public partial struct EventDispatchSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Find the unique entity with DynamicBuffer<SimulationEvent>
            foreach (var events in SystemAPI.Query<DynamicBuffer<SimulationEvent>>()
                         .WithAll<SimulationEventSingleton>())
            {
                // TODO: Lua mod hook dispatch will iterate events here,
                // calling registered callbacks for each event type.
                // For now, we just clear the buffer.

                events.Clear();
                return; // only one singleton entity
            }
        }
    }
}