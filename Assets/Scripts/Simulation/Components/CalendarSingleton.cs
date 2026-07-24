using Unity.Entities;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Singleton entity component. One entity in the world holds the calendar state.
    /// </summary>
    public struct CalendarSingleton : IComponentData
    {
        public int Year;
        public byte Season;          // 0 = spring, 1 = summer, 2 = autumn, 3 = winter
        public byte DayOfSeason;     // 0–29 (30-day seasons)
        public float Temperature;    // °C
        public float Precipitation;  // 0–1
        public float DaylightHours;  // hours of daylight
        public float GrowingMultiplier;  // crop yield modifier (0 in winter, 1 in summer)
    }
}
