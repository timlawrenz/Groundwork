using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Singleton entity carrying per-season simulation statistics.
    /// Updated once per season by SimulationStatsSystem.
    /// </summary>
    public struct SimulationStats : IComponentData
    {
        // Population
        public int Population;
        public int Children;          // age < 16
        public int Adults;            // 16 ≤ age < 60
        public int Elderly;           // age ≥ 60
        public int BirthsThisSeason;
        public int DeathsThisSeason;
        public float AverageHealth;
        public float AverageHappiness;

        // Resources (across all building inventories)
        public int TotalFood;
        public int TotalLogs;
        public int TotalFirewood;

        // Buildings
        public int BuildingCount;
        public int HouseCount;
        public int WoodcutterCount;
        public int GathererHutCount;

        // Environment
        public float Temperature;
        public float DaylightHours;
        public long CurrentTick;

        // Cumulative (life-of-sim)
        public int CumulativeBirths;
        public int CumulativeDeaths;

        // Internal tracking (not meaningful to external readers)
        internal int _lastLoggedSeason;
    }
}