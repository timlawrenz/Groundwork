using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Singleton entity holding simulation-wide configuration.
    /// One entity in the world carries this component.
    /// </summary>
    public struct SimulationConfig : IComponentData
    {
        /// <summary>How many ticks make one game day.</summary>
        public int TicksPerDay;

        /// <summary>How many days per season (30 = one-month seasons).</summary>
        public int DaysPerSeason;

        /// <summary>Current tick number. Monotonically increasing.</summary>
        public long CurrentTick;

        /// <summary>Simulation speed multiplier. 0 = paused, 1 = normal, 2 = 2x, 4 = 4x.</summary>
        public float TickSpeed;

        /// <summary>Map width in tiles.</summary>
        public int MapWidth;

        /// <summary>Map depth in tiles.</summary>
        public int MapHeight;

        public static SimulationConfig Default => new SimulationConfig
        {
            TicksPerDay = 24,       // 1 tick = 1 game-hour
            DaysPerSeason = 30,     // 30-day months
            CurrentTick = 0,
            TickSpeed = 1f,
            MapWidth = 100,
            MapHeight = 100,
        };
    }
}
