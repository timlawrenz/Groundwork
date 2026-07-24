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
            world.SetTick(24); // 1 day at 24 ticks/day

            world.UpdateSystem<CalendarSystem>();

            var cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.DayOfSeason, Is.EqualTo(1));
        }

        [Test]
        public void DoesNotAdvance_MidDay()
        {
            using var world = new SimulationTestWorld();
            world.SetTick(12); // half a day

            world.UpdateSystem<CalendarSystem>();

            var cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.DayOfSeason, Is.EqualTo(0), "Should not advance mid-day");
        }

        [Test]
        public void RollsOverSeason_AtEndOfSeason()
        {
            using var world = new SimulationTestWorld();

            // Advance to the last tick of the last day of spring (day 29)
            long lastTickOfSpring = 24 * 30 - 1; // days 0-29, so tick 24*29 is start of day 29
            world.SetTick(lastTickOfSpring);

            var cal1 = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal1.Season, Is.EqualTo(0), "Should still be spring");

            // Advance to first tick of day 30 (rollover)
            world.SetTick(24 * 30);
            world.UpdateSystem<CalendarSystem>();

            var cal2 = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal2.Season, Is.EqualTo(1), "Should roll over to summer");
            Assert.That(cal2.DayOfSeason, Is.EqualTo(0));
        }

        [Test]
        public void RollsOverYear_WhenAllSeasonsPass()
        {
            using var world = new SimulationTestWorld();

            // Start of year 1, spring
            var cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.Year, Is.EqualTo(1));
            Assert.That(cal.Season, Is.EqualTo(0));

            // Advance to first day of year 2 (tick = 24 * 30 * 4 = 2880)
            world.SetTick(24 * 30 * 4);
            world.UpdateSystem<CalendarSystem>();

            var cal2 = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal2.Year, Is.EqualTo(2));
            Assert.That(cal2.Season, Is.EqualTo(0), "Should be spring of year 2");
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

            // Summer
            world.SetTick(24 * 30);
            world.UpdateSystem<CalendarSystem>();
            cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.GrowingMultiplier, Is.EqualTo(1.0f).Within(0.01f));
            Assert.That(cal.Temperature, Is.EqualTo(25f).Within(0.01f));

            // Winter
            world.SetTick(24 * 30 * 3);
            world.UpdateSystem<CalendarSystem>();
            cal = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();
            Assert.That(cal.GrowingMultiplier, Is.EqualTo(0.0f).Within(0.01f));
            Assert.That(cal.Temperature, Is.EqualTo(-5f).Within(0.01f));
        }
    }
}