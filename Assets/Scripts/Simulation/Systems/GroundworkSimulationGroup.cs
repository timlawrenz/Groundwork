using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Custom system group for Groundwork simulation systems.
    /// Execution order: ContentLoader → Bootstrap → Tick → Calendar → Births → Age → Needs → Pathfinding → Movement → HaulCompletion → Production → Haul → Death → DebugViz → Stats → EventDispatch.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GroundworkSimulationGroup : ComponentSystemGroup
    {
    }

    // === System Ordering Attributes ===

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    public partial struct ContentLoaderSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(ContentLoaderSystem))]
    public partial struct SimulationBootstrap { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(SimulationBootstrap))]
    public partial struct TickDispatchSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(TickDispatchSystem))]
    public partial struct CalendarSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(CalendarSystem))]
    public partial struct BirthSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(BirthSystem))]
    public partial struct AgeSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(AgeSystem))]
    public partial struct NeedSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(NeedSystem))]
    public partial struct PathfindingSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    public partial struct CitizenMovementSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(CitizenMovementSystem))]
    public partial struct HaulCompletionSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(HaulCompletionSystem))]
    public partial struct BuildingProductionSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(BuildingProductionSystem))]
    public partial struct CitizenHaulSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(CitizenHaulSystem))]
    public partial struct DeathSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct DebugVizSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(DebugVizSystem))]
    public partial struct SimulationStatsSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(SimulationStatsSystem))]
    public partial struct EventDispatchSystem { }

    [UpdateInGroup(typeof(GroundworkSimulationGroup))]
    [UpdateAfter(typeof(EventDispatchSystem))]
    public partial struct LuaModSystem { }
}