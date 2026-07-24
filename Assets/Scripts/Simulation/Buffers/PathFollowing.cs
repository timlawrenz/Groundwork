using Unity.Entities;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Computed path waypoints for a citizen. The citizen moves one waypoint per tick
    /// until the buffer is empty (arrived at destination).
    /// First element is the next tile to move to.
    /// Used as DynamicBuffer&lt;PathFollowing&gt; on Citizen entities.
    /// </summary>
    public struct PathFollowing : IBufferElementData
    {
        public int2 TileCoordinate;
    }
}
