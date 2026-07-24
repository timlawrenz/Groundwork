using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class BirthSystemTests
    {
        [Test]
        public void CreatesChild_WhenEligibleFemale()
        {
            using var world = new SimulationTestWorld();
            var house = world.CreateBuilding("house", new(10, 10), maxWorkers: 0);

            // Adult female with home and good health
            var mother = world.CreateCitizen(age: 25f, home: house);
            var c = world.EntityManager.GetComponentData<Citizen>(mother);
            c.Sex = 1; // female
            c.Health = 80f;
            c.LastBirthYear = 0;
            world.EntityManager.SetComponentData(mother, c);

            // Advance to day 1 of year 2 (she had no child in year 1)
            var cal = world.GetCalendarSingletonEntity();
            var calData = world.EntityManager.GetComponentData<CalendarSingleton>(cal);
            calData.Year = 2;
            calData.DayOfSeason = 0;
            world.EntityManager.SetComponentData(cal, calData);
            world.SetTick(24);

            // Count citizens before
            int before = world.CountEntities<Citizen>();

            world.UpdateSystem<BirthSystem>();

            // Should have one more citizen (the child)
            int after = world.CountEntities<Citizen>();
            Assert.That(after, Is.EqualTo(before + 1), "Should create one child");

            // The child should have Child tag and be at mother's home
            bool foundChild = false;
            var childQuery = world.EntityManager.CreateEntityQuery(
                typeof(Citizen), typeof(Child));
            var children = childQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
            for (int i = 0; i < children.Length; i++)
            {
                Assert.That(children[i].Age, Is.EqualTo(0f));
                Assert.That(children[i].HomeBuilding, Is.EqualTo(house));
                foundChild = true;
            }
            children.Dispose();
            Assert.That(foundChild, "Should find a child entity");

            // Mother's LastBirthYear should be updated
            var motherAfter = world.EntityManager.GetComponentData<Citizen>(mother);
            Assert.That(motherAfter.LastBirthYear, Is.EqualTo(2));
        }

        [Test]
        public void DoesNotCreateChild_WhenAlreadyHadChildThisYear()
        {
            using var world = new SimulationTestWorld();
            var house = world.CreateBuilding("house", new(10, 10), maxWorkers: 0);

            var mother = world.CreateCitizen(age: 25f, home: house);
            var c = world.EntityManager.GetComponentData<Citizen>(mother);
            c.Sex = 1;
            c.Health = 80f;
            c.LastBirthYear = 2; // already had child this year
            world.EntityManager.SetComponentData(mother, c);

            var cal = world.GetCalendarSingletonEntity();
            var calData = world.EntityManager.GetComponentData<CalendarSingleton>(cal);
            calData.Year = 2;
            calData.DayOfSeason = 0;
            world.EntityManager.SetComponentData(cal, calData);
            world.SetTick(24);

            int before = world.CountEntities<Citizen>();
            world.UpdateSystem<BirthSystem>();
            int after = world.CountEntities<Citizen>();

            Assert.That(after, Is.EqualTo(before), "Should not create child if already gave birth this year");
        }

        [Test]
        public void DoesNotCreateChild_WhenMale()
        {
            using var world = new SimulationTestWorld();
            var house = world.CreateBuilding("house", new(10, 10), maxWorkers: 0);

            var male = world.CreateCitizen(age: 25f, home: house);
            var c = world.EntityManager.GetComponentData<Citizen>(male);
            c.Sex = 0; // male
            c.Health = 80f;
            world.EntityManager.SetComponentData(male, c);

            var cal = world.GetCalendarSingletonEntity();
            var calData = world.EntityManager.GetComponentData<CalendarSingleton>(cal);
            calData.Year = 2;
            calData.DayOfSeason = 0;
            world.EntityManager.SetComponentData(cal, calData);
            world.SetTick(24);

            int before = world.CountEntities<Citizen>();
            world.UpdateSystem<BirthSystem>();
            int after = world.CountEntities<Citizen>();

            Assert.That(after, Is.EqualTo(before), "Males should not give birth");
        }

        [Test]
        public void DoesNotCreateChild_WhenHealthTooLow()
        {
            using var world = new SimulationTestWorld();
            var house = world.CreateBuilding("house", new(10, 10), maxWorkers: 0);

            var mother = world.CreateCitizen(age: 25f, home: house);
            var c = world.EntityManager.GetComponentData<Citizen>(mother);
            c.Sex = 1;
            c.Health = 30f; // unhealthy
            c.LastBirthYear = 0;
            world.EntityManager.SetComponentData(mother, c);

            var cal = world.GetCalendarSingletonEntity();
            var calData = world.EntityManager.GetComponentData<CalendarSingleton>(cal);
            calData.Year = 2;
            calData.DayOfSeason = 0;
            world.EntityManager.SetComponentData(cal, calData);
            world.SetTick(24);

            int before = world.CountEntities<Citizen>();
            world.UpdateSystem<BirthSystem>();
            int after = world.CountEntities<Citizen>();

            Assert.That(after, Is.EqualTo(before), "Unhealthy citizens should not give birth");
        }

        [Test]
        public void DoesNotCreateChild_WhenHomeless()
        {
            using var world = new SimulationTestWorld();

            // No home building
            var mother = world.CreateCitizen(age: 25f);
            var c = world.EntityManager.GetComponentData<Citizen>(mother);
            c.Sex = 1;
            c.Health = 80f;
            c.LastBirthYear = 0;
            c.HomeBuilding = Entity.Null;
            world.EntityManager.SetComponentData(mother, c);

            var cal = world.GetCalendarSingletonEntity();
            var calData = world.EntityManager.GetComponentData<CalendarSingleton>(cal);
            calData.Year = 2;
            calData.DayOfSeason = 0;
            world.EntityManager.SetComponentData(cal, calData);
            world.SetTick(24);

            int before = world.CountEntities<Citizen>();
            world.UpdateSystem<BirthSystem>();
            int after = world.CountEntities<Citizen>();

            Assert.That(after, Is.EqualTo(before), "Homeless citizens should not give birth");
        }

        [Test]
        public void DoesNotCreateChild_WhenTooYoung()
        {
            using var world = new SimulationTestWorld();
            var house = world.CreateBuilding("house", new(10, 10), maxWorkers: 0);

            var child = world.CreateCitizen(age: 14f, home: house);
            var c = world.EntityManager.GetComponentData<Citizen>(child);
            c.Sex = 1;
            c.Health = 80f;
            c.LastBirthYear = 0;
            world.EntityManager.SetComponentData(child, c);

            var cal = world.GetCalendarSingletonEntity();
            var calData = world.EntityManager.GetComponentData<CalendarSingleton>(cal);
            calData.Year = 2;
            calData.DayOfSeason = 0;
            world.EntityManager.SetComponentData(cal, calData);
            world.SetTick(24);

            int before = world.CountEntities<Citizen>();
            world.UpdateSystem<BirthSystem>();
            int after = world.CountEntities<Citizen>();

            Assert.That(after, Is.EqualTo(before), "Children under 16 should not give birth");
        }
    }
}