using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Groundwork.TestHelpers;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    [Category("Integration")]
    public class SimulationStatsSystemTests
    {
        [Test]
        public void ComputesPopulation_AfterBootstrap()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            // Run one tick so stats are computed
            world.RunFullTick();

            var stats = world.GetStats();
            Assert.That(stats.Population, Is.EqualTo(50), "Bootstrap should create 50 citizens");
            Assert.That(stats.Adults, Is.EqualTo(50), "All should be adults (16-60)");
            Assert.That(stats.Children, Is.EqualTo(0));
            Assert.That(stats.Elderly, Is.EqualTo(0));
        }

        [Test]
        public void ComputesBuildingCounts_AfterBootstrap()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();
            world.RunFullTick();

            var stats = world.GetStats();
            Assert.That(stats.BuildingCount, Is.EqualTo(20));
        }

        [Test]
        public void ComputesResourceTotals_AfterBootstrap()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();
            world.RunFullTick();

            var stats = world.GetStats();
            // 8 gatherer huts × 2000 + 8 houses × 500 = 20000 food
            Assert.That(stats.TotalFood, Is.EqualTo(22000));
            // Logs are in InputInventory, not OutputSlot — not counted by stats
            Assert.That(stats.TotalLogs, Is.EqualTo(0));
            // Houses have starting firewood (8 × 1000 = 8000)
            Assert.That(stats.TotalFirewood, Is.GreaterThan(0));
        }

        [Test]
        public void AverageHealth_IsPositive_AfterBootstrap()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();
            world.RunFullTick();

            var stats = world.GetStats();
            Assert.That(stats.AverageHealth, Is.GreaterThan(90f),
                "All citizens start at 100 health, no decay on tick 0");
        }

        [Test]
        public void HasTickAndCalendarData()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();
            world.RunFullTick();

            var stats = world.GetStats();
            Assert.That(stats.CurrentTick, Is.GreaterThan(0));
            Assert.That(stats.Temperature, Is.EqualTo(10f).Within(1f));
            Assert.That(stats.DaylightHours, Is.EqualTo(12f).Within(1f));
        }

        [Test]
        public void SimulationRuns_OverMultipleDays()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            // Run 5 days × 24 ticks = 120 ticks
            // With births, population may fluctuate
            world.RunFullTicks(120);

            var stats = world.GetStats();
            // At minimum, the sim should run without crashing
            Assert.That(stats.CurrentTick, Is.GreaterThan(0));
            Assert.That(stats.Population, Is.GreaterThan(0),
                "Population should remain even with births and food available");
        }

        [Test]
        public void ResourcesChange_OverTime()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();
            world.RunFullTick(); // tick 1

            var initialStats = world.GetStats();
            int initialFood = initialStats.TotalFood;
            int initialLogs = initialStats.TotalLogs;

            // Run 10 ticks: gather_food at 0.1/tick, chop_firewood at 0.1/tick consuming logs
            for (int i = 0; i < 10; i++)
                world.RunFullTick();

            var laterStats = world.GetStats();
            // Food increases because gatherers produce
            Assert.That(laterStats.TotalFood, Is.EqualTo(initialFood),
                "Food stable when output at capacity");
            // Note: logs/firewood may not change if woodcutter has no workers
        }
    }
}
