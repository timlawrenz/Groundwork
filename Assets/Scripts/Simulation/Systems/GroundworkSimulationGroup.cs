using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Custom system group for Groundwork simulation systems.
    /// Ensures correct execution order: Tick → Calendar → Age → Needs → Production → Death.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GroundworkSimulationGroup : ComponentSystemGroup
    {
        // Unity automatically discovers systems with [UpdateInGroup(typeof(GroundworkSimulationGroup))]
        // and orders them based on [UpdateBefore] / [UpdateAfter] attributes.
        // Fallback: systems are sorted by type name.
    }

    // === System Ordering Attributes ===
    // These partial structs define the dependency graph.

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    public partial struct SimulationBootstrap { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(SimulationBootstrap))]
    public partial struct TickDispatchSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(TickDispatchSystem))]
    public partial struct CalendarSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(CalendarSystem))]
    public partial struct CitizenAgeSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(CitizenAgeSystem))]
    public partial struct CitizenNeedSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(CitizenNeedSystem))]
    public partial struct BuildingProductionSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(BuildingProductionSystem))]
    public partial struct DeathSystem { }
}
