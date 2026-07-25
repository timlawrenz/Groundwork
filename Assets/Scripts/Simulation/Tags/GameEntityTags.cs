using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Tag component: marks a citizen as a child (age &lt; 16). Can't work.
    /// </summary>
    public struct Child : IComponentData { }

    /// <summary>
    /// Tag component: marks a citizen as elderly (age &gt; 60). Reduced work capacity.
    /// </summary>
    public struct Elderly : IComponentData { }

    /// <summary>
    /// Tag component: marks a citizen as dead. Systems check this to trigger removal.
    /// </summary>
    public struct Dead : IComponentData { }

    /// <summary>
    /// Tag component: marks a citizen as homeless. Triggers shelter need and health decay.
    /// </summary>
    public struct Homeless : IComponentData { }

    /// <summary>
    /// Tag component: marks a building as under construction. Can't operate yet.
    /// </summary>
    public struct UnderConstruction : IComponentData { }

    /// <summary>
    /// Tag component: marks the singleton entity holding the SimulationEvent buffer.
    /// </summary>
    public struct SimulationEventSingleton : IComponentData { }
}
