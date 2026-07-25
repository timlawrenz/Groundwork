using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Removes dead entities from the world. Runs last in the tick group.
    /// Citizens tagged with Dead get destroyed. Buildings with no workers and
    /// no inventory get marked for cleanup (future: ruins/deconstruction).
    /// </summary>
    [BurstCompile]
    public partial struct DeathSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Get event buffer for death events
            if (!SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
                return;
            var eventBuffer = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

            // Destroy dead citizens
            foreach (var (_, position, entity) in SystemAPI.Query<RefRO<Dead>, RefRO<MapPosition>>()
                         .WithAll<Citizen>()
                         .WithEntityAccess())
            {
                eventBuffer.Add(new SimulationEvent
                {
                    Type = EventType.CitizenDied,
                    EntityId = entity.Index,
                    Data0 = position.ValueRO.TileCoordinate.x,
                    Data1 = position.ValueRO.TileCoordinate.y,
                });
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
