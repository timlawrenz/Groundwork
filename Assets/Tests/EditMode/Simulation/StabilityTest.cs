using NUnit.Framework;
using UnityEngine;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    [Category("Integration")]
    public class StabilityTest
    {
        /// <summary>
        /// Runs the simulation for a target number of game-years and logs
        /// population stats at each season boundary via SimulationStatsSystem.
        /// Also measures tick throughput for benchmarking.
        /// </summary>
        [Test]
        public void RunSim_WithSeasonalLogging()
        {
            const int yearsToRun = 10; // increase to 100 for full test
            const int ticksPerYear = 4 * 30 * 24; // 2880

            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int y = 0; y < yearsToRun; y++)
            {
                world.RunFullTicks(ticksPerYear);

                var stats = world.GetStats();
                var cal = world.GetCalendar();
                UnityEngine.Debug.Log(
                    $"[BENCH] Year {cal.Year} complete | Pop: {stats.Population} " +
                    $"(C:{stats.Children} A:{stats.Adults} E:{stats.Elderly}) | " +
                    $"Health: {stats.AverageHealth:F1} | " +
                    $"Food: {stats.TotalFood} Firewood: {stats.TotalFirewood} | " +
                    $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s");
            }

            stopwatch.Stop();
            long totalTicks = yearsToRun * ticksPerYear;
            var finalStats = world.GetStats();

            UnityEngine.Debug.Log(
                $"[BENCH] Done. {yearsToRun} years, {totalTicks} ticks, " +
                $"{stopwatch.Elapsed.TotalSeconds:F2}s total, " +
                $"{totalTicks / stopwatch.Elapsed.TotalSeconds:F0} ticks/s avg. " +
                $"Final pop: {finalStats.Population}");

            // At minimum, verify the sim runs without crashing
            Assert.That(finalStats.CurrentTick, Is.GreaterThan(0));
        }
    }
}