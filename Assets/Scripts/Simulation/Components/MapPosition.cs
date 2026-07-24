using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Position of an entity on the tile grid. Citizen or Building.
    /// </summary>
    public struct MapPosition : IComponentData
    {
        public int2 TileCoordinate;  // x, z on the grid
        public byte Rotation;        // 0–3 (0, 90, 180, 270 degrees)
    }
}
