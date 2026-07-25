using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Moves citizens along their PathFollowing buffer. Each tick, the citizen moves one
    /// waypoint closer to their destination. After each step, remaining waypoints are
    /// cleared and a fresh PathRequest is issued — ensuring the path is always optimal
    /// from the current position. When the buffer is empty and no PathRequest exists,
    /// the citizen has arrived and their task is set to idle.
    /// </summary>
    [BurstCompile]
    public partial struct CitizenMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Get the event buffer singleton for emitting tile events
            if (!SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
                return;
            var eventBuffer = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

            foreach (var (position, task, pathBuffer, entity) in
                     SystemAPI.Query<RefRW<MapPosition>, RefRW<CitizenTask>, DynamicBuffer<PathFollowing>>()
                         .WithAll<Citizen>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                if (pathBuffer.Length == 0)
                {
                    if (task.ValueRO.TaskType == "walking" || task.ValueRO.TaskType == "hauling" || task.ValueRO.TaskType == "idle")
                    {
                        task.ValueRW.TaskType = "idle";
                        task.ValueRW.TargetEntity = Entity.Null;
                    }
                    continue;
                }

                // Remember old position for tile events
                int2 oldPos = position.ValueRO.TileCoordinate;
                int2 nextTile = pathBuffer[0].TileCoordinate;

                // Move to next waypoint
                position.ValueRW.TileCoordinate = nextTile;

                // Emit tile transition events
                eventBuffer.Add(new SimulationEvent
                {
                    Type = EventType.TileLeave,
                    EntityId = entity.Index,
                    Data0 = oldPos.x,
                    Data1 = oldPos.y,
                });
                eventBuffer.Add(new SimulationEvent
                {
                    Type = EventType.TileEnter,
                    EntityId = entity.Index,
                    Data0 = nextTile.x,
                    Data1 = nextTile.y,
                });

                // Update task
                task.ValueRW.TaskType = "walking";
                task.ValueRW.Progress = 0f;

                // Consume the waypoint we just moved to
                pathBuffer.RemoveAt(0);

                if (pathBuffer.Length == 0)
                {
                    // Arrived at destination
                    task.ValueRW.TaskType = "idle";
                    task.ValueRW.TargetEntity = Entity.Null;
                }
                else
                {
                    // Re-path: clear remaining stale waypoints and request fresh A* path
                    int2 destination = pathBuffer[pathBuffer.Length - 1].TileCoordinate;
                    pathBuffer.Clear();
                    ecb.AddComponent(entity, new PathRequest { Destination = destination });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}