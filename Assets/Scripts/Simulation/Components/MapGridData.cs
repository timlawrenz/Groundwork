using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Read-only walkability grid stored as a blob asset. Shared across all pathfinding queries.
    /// </summary>
    public struct MapGridBlob
    {
        public int Width;
        public int Height;
        /// <summary>Flattened 2D array: index = z * Width + x. 1 = walkable, 0 = blocked.</summary>
        public BlobArray<byte> Walkable;
    }

    /// <summary>
    /// Singleton component holding a reference to the map grid blob asset.
    /// </summary>
    public struct MapGridData : IComponentData
    {
        public BlobAssetReference<MapGridBlob> Grid;

        public bool IsWalkable(int2 tile)
        {
            ref var blob = ref Grid.Value;
            if (tile.x < 0 || tile.x >= blob.Width || tile.y < 0 || tile.y >= blob.Height)
                return false;
            return blob.Walkable[tile.y * blob.Width + tile.x] == 1;
        }
    }
}
