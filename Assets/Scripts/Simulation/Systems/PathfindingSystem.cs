using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// A* pathfinding on the tile grid. Processes PathRequest components on citizens,
    /// computes the shortest walkable path, and stores waypoints in a PathFollowing buffer.
    /// Supports 8-directional movement with diagonal cost (√2 vs 1.0).
    /// </summary>
    [BurstCompile]
    public partial struct PathfindingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gridData = SystemAPI.GetSingleton<MapGridData>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int maxNodes = gridData.Grid.Value.Width * gridData.Grid.Value.Height;

            foreach (var (position, request, entity) in
                     SystemAPI.Query<RefRO<MapPosition>, RefRO<PathRequest>>()
                         .WithAll<Citizen>()
                         .WithEntityAccess())
            {
                int2 start = position.ValueRO.TileCoordinate;
                int2 goal = request.ValueRO.Destination;

                // Compute A*
                var path = ComputePath(in gridData, start, goal, maxNodes);

                // Write path to buffer
                var pathBuffer = ecb.AddBuffer<PathFollowing>(entity);
                for (int i = 0; i < path.Length; i++)
                {
                    pathBuffer.Add(new PathFollowing { TileCoordinate = path[i] });
                }

                path.Dispose();
                ecb.RemoveComponent<PathRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// A* search on the 2D tile grid. Returns a list of waypoints from start (exclusive) to goal.
        /// If no path exists, returns an empty list.
        /// </summary>
        private static NativeList<int2> ComputePath(
            in MapGridData gridData,
            int2 start,
            int2 goal,
            int maxNodes)
        {
            var result = new NativeList<int2>(256, Allocator.Temp);

            // Early exit: same tile
            if (start.Equals(goal))
                return result;

            // Early exit: goal not walkable
            if (!gridData.IsWalkable(goal))
                return result;

            var openSet = new NativeList<int2>(maxNodes, Allocator.Temp);
            var cameFrom = new NativeHashMap<int2, int2>(maxNodes, Allocator.Temp);
            var gScore = new NativeHashMap<int2, float>(maxNodes, Allocator.Temp);
            var fScore = new NativeHashMap<int2, float>(maxNodes, Allocator.Temp);
            var inOpenSet = new NativeHashMap<int2, bool>(maxNodes, Allocator.Temp);

            // Initialize
            openSet.Add(start);
            gScore.Add(start, 0f);
            fScore.Add(start, Heuristic(start, goal));
            inOpenSet.Add(start, true);

            while (openSet.Length > 0)
            {
                // Find node with lowest fScore
                int currentIdx = FindLowestFScore(openSet, fScore);
                int2 current = openSet[currentIdx];
                openSet.RemoveAtSwapBack(currentIdx);
                inOpenSet.Remove(current);

                // Goal reached
                if (current.Equals(goal))
                {
                    ReconstructPath(cameFrom, current, start, result);
                    break;
                }

                // Explore 8-directional neighbors
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int2 neighbor = new int2(current.x + dx, current.y + dy);

                        if (!gridData.IsWalkable(neighbor))
                            continue;

                        // Diagonal moves cost √2 ≈ 1.414, cardinal cost 1.0
                        float moveCost = (dx != 0 && dy != 0) ? 1.41421356f : 1.0f;
                        float tentativeG = GetOrDefault(gScore, current, float.MaxValue) + moveCost;
                        float currentG = GetOrDefault(gScore, neighbor, float.MaxValue);

                        if (tentativeG < currentG)
                        {
                            AddOrSet(cameFrom, neighbor, current);
                            AddOrSet(gScore, neighbor, tentativeG);
                            AddOrSet(fScore, neighbor, tentativeG + Heuristic(neighbor, goal));

                            if (!inOpenSet.ContainsKey(neighbor))
                            {
                                openSet.Add(neighbor);
                                inOpenSet.Add(neighbor, true);
                            }
                        }
                    }
                }
            }

            // Cleanup
            openSet.Dispose();
            cameFrom.Dispose();
            gScore.Dispose();
            fScore.Dispose();
            inOpenSet.Dispose();

            return result;
        }

        /// <summary>
        /// Octile distance heuristic for 8-directional grid movement.
        /// </summary>
        private static float Heuristic(int2 a, int2 b)
        {
            int dx = math.abs(a.x - b.x);
            int dy = math.abs(a.y - b.y);
            float sqrt2minus1 = 0.41421356f; // √2 - 1
            return dx + dy + sqrt2minus1 * math.min(dx, dy);
        }

        private static int FindLowestFScore(NativeList<int2> openSet, NativeHashMap<int2, float> fScore)
        {
            int bestIdx = 0;
            float bestF = float.MaxValue;
            for (int i = 0; i < openSet.Length; i++)
            {
                if (fScore.TryGetValue(openSet[i], out float f) && f < bestF)
                {
                    bestF = f;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private static float GetOrDefault(NativeHashMap<int2, float> map, int2 key, float defaultValue)
        {
            return map.TryGetValue(key, out float val) ? val : defaultValue;
        }

        private static void AddOrSet(NativeHashMap<int2, float> map, int2 key, float value)
        {
            if (map.ContainsKey(key))
                map[key] = value;
            else
                map.Add(key, value);
        }

        private static void AddOrSet(NativeHashMap<int2, int2> map, int2 key, int2 value)
        {
            if (map.ContainsKey(key))
                map[key] = value;
            else
                map.Add(key, value);
        }

        /// <summary>
        /// Follow cameFrom links from goal back to start, writing waypoints in forward order.
        /// The start tile is excluded from the path.
        /// </summary>
        private static void ReconstructPath(
            NativeHashMap<int2, int2> cameFrom,
            int2 current,
            int2 start,
            NativeList<int2> outPath)
        {
            var reversePath = new NativeList<int2>(256, Allocator.Temp);

            while (!current.Equals(start))
            {
                reversePath.Add(current);
                current = cameFrom[current];
            }

            // Write in forward order (goal first in reversePath → last to be written)
            for (int i = reversePath.Length - 1; i >= 0; i--)
            {
                outPath.Add(reversePath[i]);
            }

            reversePath.Dispose();
        }
    }
}
