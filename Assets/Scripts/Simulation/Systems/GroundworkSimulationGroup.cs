using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Custom system group for Groundwork simulation systems.
    /// Ensures correct execution order: Tick → Calendar → Age → Needs → Pathfinding → Movement → Production → Death.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GroundworkSimulationGroup : ComponentSystemGroup
    {
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
    public partial struct PathfindingSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    public partial struct CitizenMovementSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(CitizenMovementSystem))]
    public partial struct BuildingProductionSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(BuildingProductionSystem))]
    public partial struct DeathSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct SimulationStatsSystem { }
}
