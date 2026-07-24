using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Advances the calendar each game-day (when CurrentTick crosses a day boundary).
    /// Handles season and year rollover. Runs after TickDispatchSystem.
    /// </summary>
    [BurstCompile]
    public partial struct CalendarSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            long tick = config.CurrentTick;

            // Only advance if a full day has passed since last check
            if (tick % config.TicksPerDay != 0)
                return;

            long day = tick / config.TicksPerDay;

            var calendar = SystemAPI.GetSingletonRW<CalendarSingleton>();

            // Advance day within season
            calendar.ValueRW.DayOfSeason = (byte)((calendar.ValueRO.DayOfSeason + 1) % config.DaysPerSeason);

            // Season rollover
            if (calendar.ValueRW.DayOfSeason == 0)
            {
                calendar.ValueRW.Season = (byte)((calendar.ValueRO.Season + 1) % 4);

                // Year rollover
                if (calendar.ValueRW.Season == 0)
                    calendar.ValueRW.Year++;

                // Update season-dependent values
                UpdateSeasonData(ref calendar.ValueRW);
            }
        }

        private static void UpdateSeasonData(ref CalendarSingleton cal)
        {
            // Simple seasonal model for temperate climate
            switch (cal.Season)
            {
                case 0: // Spring
                    cal.Temperature = 10f;
                    cal.Precipitation = 0.5f;
                    cal.DaylightHours = 12f;
                    cal.GrowingMultiplier = 0.5f;
                    break;
                case 1: // Summer
                    cal.Temperature = 25f;
                    cal.Precipitation = 0.3f;
                    cal.DaylightHours = 16f;
                    cal.GrowingMultiplier = 1.0f;
                    break;
                case 2: // Autumn
                    cal.Temperature = 12f;
                    cal.Precipitation = 0.6f;
                    cal.DaylightHours = 10f;
                    cal.GrowingMultiplier = 0.5f;
                    break;
                case 3: // Winter
                    cal.Temperature = -5f;
                    cal.Precipitation = 0.2f; // snow
                    cal.DaylightHours = 8f;
                    cal.GrowingMultiplier = 0.0f;
                    break;
            }
        }
    }
}
