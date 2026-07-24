using NUnit.Framework;
using Unity.Entities;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class CalendarSystemTests
    {
        [Test]
        public void AdvancesDayOfSeason_WhenTickCrossesDayBoundary()
        {
            using var world = new SimulationTestWorld();
            world.SetTick(24);

            world.UpdateSystem<CalendarSystem>();

            var cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.DayOfSeason, Is.EqualTo(1));
        }

        [Test]
        public void DoesNotAdvance_MidDay()
        {
            using var world = new SimulationTestWorld();
            world.SetTick(12);

            world.UpdateSystem<CalendarSystem>();

            var cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.DayOfSeason, Is.EqualTo(0));
        }

        [Test]
        public void RollsOverSeason_AtEndOfSeason()
        {
            using var world = new SimulationTestWorld();

            // Start: DayOfSeason=0, Season=0 (spring)
            var cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.DayOfSeason, Is.EqualTo(0));
            Assert.That(cal.Season, Is.EqualTo(0));

            // Step through 29 days — DayOfSeason goes 1→2→...→29
            for (int day = 1; day <= 29; day++)
            {
                world.SetTick(day * 24);
                world.UpdateSystem<CalendarSystem>();
            }

            cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.DayOfSeason, Is.EqualTo(29));
            Assert.That(cal.Season, Is.EqualTo(0), "Season should still be spring at day 29");

            // Day 30 → rolls over to summer
            world.SetTick(30 * 24);
            world.UpdateSystem<CalendarSystem>();

            cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.DayOfSeason, Is.EqualTo(0));
            Assert.That(cal.Season, Is.EqualTo(1), "Should roll over to summer");
        }

        [Test]
        public void RollsOverYear_WhenAllSeasonsPass()
        {
            using var world = new SimulationTestWorld();

            // Start of year 1, spring, day 0
            var cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.Year, Is.EqualTo(1));

            // Step through 120 days (4 seasons × 30 days)
            for (int day = 1; day <= 120; day++)
            {
                world.SetTick(day * 24);
                world.UpdateSystem<CalendarSystem>();
            }

            cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.Year, Is.EqualTo(2));
            Assert.That(cal.Season, Is.EqualTo(0));
            Assert.That(cal.DayOfSeason, Is.EqualTo(0));
        }

        [Test]
        public void SetsSeasonData_Correctly()
        {
            using var world = new SimulationTestWorld();

            // Spring (day 1)
            world.SetTick(24);
            world.UpdateSystem<CalendarSystem>();
            var cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.GrowingMultiplier, Is.EqualTo(0.5f).Within(0.01f));

            // Advance to summer start (day 30)
            for (int day = 2; day <= 30; day++)
            {
                world.SetTick(day * 24);
                world.UpdateSystem<CalendarSystem>();
            }
            cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.Season, Is.EqualTo(1));
            Assert.That(cal.GrowingMultiplier, Is.EqualTo(1.0f).Within(0.01f));
            Assert.That(cal.Temperature, Is.EqualTo(25f).Within(0.01f));

            // Advance to winter start (day 90)
            for (int day = 31; day <= 90; day++)
            {
                world.SetTick(day * 24);
                world.UpdateSystem<CalendarSystem>();
            }
            cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.Season, Is.EqualTo(3));
            Assert.That(cal.GrowingMultiplier, Is.EqualTo(0.0f).Within(0.01f));
            Assert.That(cal.Temperature, Is.EqualTo(-5f).Within(0.01f));
        }
    }
}
