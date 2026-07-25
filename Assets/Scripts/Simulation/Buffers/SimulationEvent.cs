using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Event types emitted by simulation systems and consumed by EventDispatchSystem.
    /// Mod hooks subscribe to these types to receive notifications.
    /// </summary>
    public enum EventType : byte
    {
        None = 0,
        CitizenBorn,
        CitizenDied,
        CitizenAged,
        BuildingComplete,
        BuildingDeconstructed,
        SeasonChanged,
        DayChanged,
        ResourceDepleted,
        NeedCritical,
        PopulationMilestone,
        ProductionComplete,
        CitizenSpawned,
        BuildingPlaced,
        TileEnter,
        TileLeave,
    }

    /// <summary>
    /// A simulation event emitted during a tick. Stored in a singleton
    /// DynamicBuffer&lt;SimulationEvent&gt; and processed by EventDispatchSystem.
    /// EntityId references the relevant entity (citizen, building, etc.).
    /// Data0–Data3 carry generic payload values.
    /// </summary>
    public struct SimulationEvent : IBufferElementData
    {
        public EventType Type;
        public int EntityId;
        public float Data0;
        public float Data1;
        public float Data2;
        public float Data3;
    }
}