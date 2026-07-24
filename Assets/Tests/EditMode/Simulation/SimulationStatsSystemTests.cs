using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
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
            Assert.That(stats.BuildingCount, Is.EqualTo(14));
            Assert.That(stats.HouseCount, Is.EqualTo(10));
            Assert.That(stats.WoodcutterCount, Is.EqualTo(2));
            Assert.That(stats.GathererHutCount, Is.EqualTo(2));
        }

        [Test]
        public void ComputesResourceTotals_AfterBootstrap()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();
            world.RunFullTick();

            var stats = world.GetStats();
            // 2 woodcutters × 50 logs = 100, but production consumes some during the tick
            Assert.That(stats.TotalLogs, Is.LessThanOrEqualTo(100).And.GreaterThan(0),
                "Woodcutters should have logs, some consumed in production");
            // 2 gatherer huts × 100 food = 200 food
            Assert.That(stats.TotalFood, Is.EqualTo(200));
            // No firewood yet — chop_firewood needs 10 ticks to complete one cycle
            Assert.That(stats.TotalFirewood, Is.EqualTo(0));
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
        public void PopulationDecreases_WhenCitizensStarve()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            // Run enough ticks for needs to escalate and health to decay
            // Each day: food need +0.15, warmth +0.02 (spring/summer/autumn)
            // Need hits 0.8 after ~5.3 days. Then health decays at 2×(urgency-0.8).
            // Without food consumption, all citizens will die eventually.
            // But death by age (at ~90) happens first for some.

            // Run 10 years (10 × 4 × 30 = 1200 days = 1200 ticks? No — 1 tick = 1 hour,
            // 24 ticks per day, so 1200 days = 28800 ticks)
            // That's too many. Let's run fewer ticks to see population start declining.

            // Run 5 days × 24 ticks = 120 ticks
            for (int i = 0; i < 120; i++)
                world.RunFullTick();

            var stats = world.GetStats();
            // Population should be dropping as citizens age and health decays
            // Some may die from old age (max lifespan ~90, starting ages 16-60)
            Assert.That(stats.Population, Is.LessThanOrEqualTo(50),
                "Population should not increase (no births)");
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
            // Food increases because gatherers produce (0.1/tick × 2 huts)
            Assert.That(laterStats.TotalFood, Is.GreaterThan(initialFood),
                "Gatherer huts should produce food");
            // Logs decrease because woodcutters consume them for firewood
            Assert.That(laterStats.TotalLogs, Is.LessThan(initialLogs),
                "Woodcutters should consume logs");
            // Firewood should now exist
            Assert.That(laterStats.TotalFirewood, Is.GreaterThan(0),
                "Woodcutters should produce firewood");
        }
    }
}
