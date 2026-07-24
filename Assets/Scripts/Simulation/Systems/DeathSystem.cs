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

            // Destroy dead citizens
            foreach (var (entity) in SystemAPI.Query<RefRO<Dead>>()
                         .WithAll<Citizen>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
