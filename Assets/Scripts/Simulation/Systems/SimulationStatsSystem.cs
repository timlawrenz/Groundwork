using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Collects simulation-wide statistics into the SimulationStats singleton.
    /// Runs after DeathSystem (end of pipeline). Computes stats every tick
    /// so any system can query them. Logs a summary at season boundaries.
    /// </summary>
    public partial struct SimulationStatsSystem : ISystem
    {
        private EntityQuery _citizenQuery;
        private EntityQuery _buildingQuery;
        private EntityQuery _statsQuery;

        public void OnCreate(ref SystemState state)
        {
            _citizenQuery = state.GetEntityQuery(
                typeof(Citizen), typeof(MapPosition));
            _buildingQuery = state.GetEntityQuery(
                typeof(Building), typeof(InventorySlot));
            _statsQuery = state.GetEntityQuery(typeof(SimulationStats));

            // Ensure stats singleton exists (idempotent — may be recreated after bootstrap)
            EnsureStatsSingleton(ref state);
        }

        private void EnsureStatsSingleton(ref SystemState state)
        {
            if (!_statsQuery.IsEmpty)
                return;
            state.EntityManager.CreateSingleton<SimulationStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Recreate if destroyed (e.g. by RunBootstrap in tests)
            EnsureStatsSingleton(ref state);

            var calendar = SystemAPI.GetSingleton<CalendarSingleton>();
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            ref var stats = ref SystemAPI.GetSingletonRW<SimulationStats>().ValueRW;

            // Collect population stats
            var citizens = _citizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
            int children = 0, adults = 0, elderly = 0, pop = citizens.Length;
            float totalHealth = 0f, totalHappiness = 0f;

            for (int i = 0; i < pop; i++)
            {
                var c = citizens[i];
                totalHealth += c.Health;
                totalHappiness += c.Happiness;
                if (c.Age < 16f) children++;
                else if (c.Age >= 60f) elderly++;
                else adults++;
            }
            citizens.Dispose();

            stats.Population = pop;
            stats.Children = children;
            stats.Adults = adults;
            stats.Elderly = elderly;
            stats.AverageHealth = pop > 0 ? totalHealth / pop : 0f;
            stats.AverageHappiness = pop > 0 ? totalHappiness / pop : 0f;
            stats.CurrentTick = config.CurrentTick;
            stats.Temperature = calendar.Temperature;
            stats.DaylightHours = calendar.DaylightHours;

            // Count buildings and resources
            var buildingEntities = _buildingQuery.ToEntityArray(Allocator.Temp);
            var buildingTypes = _buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            int totalFood = 0, totalLogs = 0, totalFirewood = 0;

            for (int i = 0; i < buildingTypes.Length; i++)
            {
                var inv = state.EntityManager.GetBuffer<InventorySlot>(buildingEntities[i]);
                for (int j = 0; j < inv.Length; j++)
                {
                    var slot = inv[j];
                    if (slot.ItemId == "food") totalFood += slot.Quantity;
                    else if (slot.ItemId == "logs") totalLogs += slot.Quantity;
                    else if (slot.ItemId == "firewood") totalFirewood += slot.Quantity;
                }
            }
            buildingEntities.Dispose();
            buildingTypes.Dispose();

            stats.BuildingCount = buildingTypes.Length;
            stats.TotalFood = totalFood;
            stats.TotalLogs = totalLogs;
            stats.TotalFirewood = totalFirewood;

            // Count births and deaths from the event buffer
            if (SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
            {
                var events = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);
                for (int i = 0; i < events.Length; i++)
                {
                    if (events[i].Type == EventType.CitizenBorn)
                    {
                        stats.CumulativeBirths++;
                        stats.BirthsThisSeason++;
                    }
                    else if (events[i].Type == EventType.CitizenDied)
                    {
                        stats.CumulativeDeaths++;
                        stats.DeathsThisSeason++;
                    }
                }
            }

            // Log at season boundaries — only once per season change
            int seasonId = calendar.Year * 4 + calendar.Season;
            if (seasonId != stats._lastLoggedSeason)
            {
                stats._lastLoggedSeason = seasonId;
                stats.BirthsThisSeason = 0;
                stats.DeathsThisSeason = 0;
                var seasonNames = new FixedString32Bytes[] { "Spring", "Summer", "Autumn", "Winter" };
                var seasonName = seasonNames[calendar.Season];
                UnityEngine.Debug.Log(
                    $"[Groundwork] Year {calendar.Year} {seasonName} | " +
                    $"Pop: {stats.Population} (C:{stats.Children} A:{stats.Adults} E:{stats.Elderly}) | " +
                    $"Health: {stats.AverageHealth:F1} Happy: {stats.AverageHappiness:F1} | " +
                    $"Food: {stats.TotalFood} Logs: {stats.TotalLogs} Firewood: {stats.TotalFirewood} | " +
                    $"Buildings: {stats.BuildingCount} | " +
                    $"Births: {stats.BirthsThisSeason} Deaths: {stats.DeathsThisSeason} | " +
                    $"Temp: {stats.Temperature:F1}°C Tick: {stats.CurrentTick}");
            }
        }
    }
}
