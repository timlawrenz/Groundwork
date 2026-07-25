using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Headless simulation runner. Invoked via:
    /// Unity -batchmode -nographics -executeMethod Groundwork.Simulation.HeadlessRunner.Run
    /// Runs bootstrap + N ticks, writes per-season stats CSV, then exits.
    /// </summary>
    public static class HeadlessRunner
    {
        private const int TICKS_PER_DAY = 24;
        private const int DAYS_PER_SEASON = 30;
        private const int TICKS_PER_SEASON = TICKS_PER_DAY * DAYS_PER_SEASON;
        private const int TICKS_PER_YEAR = TICKS_PER_SEASON * 4;
        private const int TOTAL_TICKS = TICKS_PER_YEAR * 10; // 10-year sim
        private const string CSV_PATH = "/tmp/groundwork_stats.csv";

        public static void Run()
        {
            using var world = new World("HeadlessWorld");
            var simGroup = world.GetOrCreateSystemManaged<GroundworkSimulationGroup>();

            // Bootstrap
            var contentHandle = world.CreateSystem<ContentLoaderSystem>();
            simGroup.AddSystemToUpdateList(contentHandle);
            simGroup.Update();
            simGroup.RemoveSystemFromUpdateList(contentHandle);

            var bootstrapHandle = world.CreateSystem<SimulationBootstrap>();
            simGroup.AddSystemToUpdateList(bootstrapHandle);
            simGroup.Update();
            simGroup.RemoveSystemFromUpdateList(bootstrapHandle);
            UnityEngine.Debug.Log($"[HeadlessRunner] Bootstrap complete. Running {TOTAL_TICKS} ticks ({TOTAL_TICKS / TICKS_PER_YEAR} years)...");

            // All systems in pipeline order
            var allSystems = new SystemHandle[]
            {
                world.CreateSystem<TickDispatchSystem>(),
                world.CreateSystem<CalendarSystem>(),
                world.CreateSystem<BirthSystem>(),
                world.CreateSystem<AgeSystem>(),
                world.CreateSystem<NeedSystem>(),
                world.CreateSystem<PathfindingSystem>(),
                world.CreateSystem<CitizenMovementSystem>(),
                world.CreateSystem<HaulCompletionSystem>(),
                world.CreateSystem<BuildingProductionSystem>(),
                world.CreateSystem<CitizenHaulSystem>(),
                world.CreateSystem<DeathSystem>(),
                world.CreateSystem<DebugVizSystem>(),
                world.CreateSystem<SimulationStatsSystem>(),
                world.CreateSystem<EventDispatchSystem>(),
            };

            foreach (var handle in allSystems)
                simGroup.AddSystemToUpdateList(handle);

            // Write CSV header
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("tick,year,season,population,food,firewood,buildings,births_season,deaths_season,avg_health");

            var seasonNames = new string[] { "spring", "summer", "fall", "winter" };
            int prevBirths = 0;
            int prevDeaths = 0;

            for (int i = 0; i < TOTAL_TICKS; i++)
            {
                simGroup.Update();

                // Log stats at every season boundary
                if (i > 0 && i % TICKS_PER_SEASON == 0)
                {
                    var statsQuery = world.EntityManager.CreateEntityQuery(typeof(SimulationStats));
                    if (!statsQuery.IsEmpty)
                    {
                        var stats = statsQuery.GetSingleton<SimulationStats>();
                        int year = i / TICKS_PER_YEAR + 1;
                        int seasonIdx = (i / TICKS_PER_SEASON) % 4;
                        int birthsThisSeason = stats.CumulativeBirths - prevBirths;
                        int deathsThisSeason = stats.CumulativeDeaths - prevDeaths;
                        prevBirths = stats.CumulativeBirths;
                        prevDeaths = stats.CumulativeDeaths;

                        csv.AppendLine($"{i},{year},{seasonNames[seasonIdx]}," +
                            $"{stats.Population},{stats.TotalFood},{stats.TotalFirewood}," +
                            $"{stats.BuildingCount},{birthsThisSeason},{deathsThisSeason}," +
                            $"{stats.AverageHealth:F1}");

                        UnityEngine.Debug.Log($"[HeadlessRunner] Y{year} {seasonNames[seasonIdx]}: " +
                            $"Pop={stats.Population} Food={stats.TotalFood} Firewood={stats.TotalFirewood}");
                    }
                }
            }

            foreach (var handle in allSystems)
                simGroup.RemoveSystemFromUpdateList(handle);

            // Write CSV to file
            try
            {
                System.IO.File.WriteAllText(CSV_PATH, csv.ToString());
                UnityEngine.Debug.Log($"[HeadlessRunner] Stats written to {CSV_PATH} ({csv.Length} bytes, {csv.ToString().Split('\n').Length - 1} rows)");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.Log($"[HeadlessRunner] CSV write failed: {ex.Message}");
            }

            // Final stats
            var finalStatsQuery = world.EntityManager.CreateEntityQuery(typeof(SimulationStats));
            if (!finalStatsQuery.IsEmpty)
            {
                var stats = finalStatsQuery.GetSingleton<SimulationStats>();
                UnityEngine.Debug.Log($"[HeadlessRunner] Complete. " +
                    $"Pop: {stats.Population}, Food: {stats.TotalFood}, " +
                    $"Firewood: {stats.TotalFirewood}, Buildings: {stats.BuildingCount}, " +
                    $"Births: {stats.CumulativeBirths}, Deaths: {stats.CumulativeDeaths}");
            }

            UnityEngine.Debug.Log("[HeadlessRunner] Done.");
        }
    }
}