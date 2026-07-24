using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Moves citizens along their PathFollowing buffer. Each tick, the citizen moves one
    /// waypoint closer to their destination. When the buffer is empty, the citizen
    /// has arrived and their task is set to idle.
    /// </summary>
    [BurstCompile]
    public partial struct CitizenMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (position, task, pathBuffer, entity) in
                     SystemAPI.Query<RefRW<MapPosition>, RefRW<CitizenTask>, DynamicBuffer<PathFollowing>>()
                         .WithAll<Citizen>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                if (pathBuffer.Length == 0)
                {
                    // Arrived — clear task
                    if (task.ValueRO.TaskType == "walking")
                    {
                        task.ValueRW.TaskType = "idle";
                        task.ValueRW.TargetEntity = Entity.Null;
                    }
                    continue;
                }

                // Move to next waypoint
                int2 nextTile = pathBuffer[0].TileCoordinate;
                position.ValueRW.TileCoordinate = nextTile;
                pathBuffer.RemoveAt(0);

                // Update task
                task.ValueRW.TaskType = "walking";
                task.ValueRW.Progress = 0f;

                // If this was the last waypoint, we've arrived
                if (pathBuffer.Length == 0)
                {
                    task.ValueRW.TaskType = "idle";
                    task.ValueRW.TargetEntity = Entity.Null;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
