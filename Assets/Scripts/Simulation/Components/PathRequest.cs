using Unity.Entities;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Marks a citizen that needs a path computed. Systems check for this component,
    /// compute the path, remove this component, and add PathFollowing buffer entries.
    /// </summary>
    public struct PathRequest : IComponentData
    {
        /// <summary>Destination tile coordinate.</summary>
        public int2 Destination;
    }
}
