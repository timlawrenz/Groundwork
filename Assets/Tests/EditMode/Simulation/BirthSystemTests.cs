using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;

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

            var mother = world.CreateCitizen(age: 25f, home: house);
            var c = world.EntityManager.GetComponentData<Citizen>(mother);
            var lb = world.EntityManager.GetComponentData<LivingBeing>(mother);
            lb.Sex = 1;
            lb.Health = 80f;
            c.LastBirthYear = 0;
            world.EntityManager.SetComponentData(mother, c);
            world.EntityManager.SetComponentData(mother, lb);

            var cal = world.GetCalendarSingletonEntity();
            var calData = world.EntityManager.GetComponentData<CalendarSingleton>(cal);
            calData.Year = 2;
            calData.DayOfSeason = 0;
            world.EntityManager.SetComponentData(cal, calData);
            world.SetTick(24);

            int before = world.CountEntities<Citizen>();
            world.UpdateSystem<BirthSystem>();
            int after = world.CountEntities<Citizen>();
            Assert.That(after, Is.EqualTo(before + 1), "Should create one child");

            // The child's age lives on LivingBeing now
            bool foundChild = false;
            var childQuery = world.EntityManager.CreateEntityQuery(
                typeof(Citizen), typeof(LivingBeing), typeof(Child));
            var childCitizens = childQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
            var childLBs = childQuery.ToComponentDataArray<LivingBeing>(Allocator.Temp);
            for (int i = 0; i < childCitizens.Length; i++)
            {
                Assert.That(childLBs[i].Age, Is.EqualTo(0f));
                Assert.That(childCitizens[i].HomeBuilding, Is.EqualTo(house));
                foundChild = true;
            }
            childCitizens.Dispose();
            childLBs.Dispose();
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
            var lb = world.EntityManager.GetComponentData<LivingBeing>(mother);
            lb.Sex = 1;
            lb.Health = 80f;
            c.LastBirthYear = 2; // already had child this year
            world.EntityManager.SetComponentData(mother, c);
            world.EntityManager.SetComponentData(mother, lb);

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
            var lb = world.EntityManager.GetComponentData<LivingBeing>(male);
            lb.Sex = 0; // male
            lb.Health = 80f;
            world.EntityManager.SetComponentData(male, c);
            world.EntityManager.SetComponentData(male, lb);

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
            var lb = world.EntityManager.GetComponentData<LivingBeing>(mother);
            lb.Sex = 1;
            lb.Health = 30f; // unhealthy
            c.LastBirthYear = 0;
            world.EntityManager.SetComponentData(mother, c);
            world.EntityManager.SetComponentData(mother, lb);

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
            var lb = world.EntityManager.GetComponentData<LivingBeing>(mother);
            lb.Sex = 1;
            lb.Health = 80f;
            c.LastBirthYear = 0;
            c.HomeBuilding = Entity.Null;
            world.EntityManager.SetComponentData(mother, c);
            world.EntityManager.SetComponentData(mother, lb);

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

            var childEntity = world.CreateCitizen(age: 14f, home: house);
            var c = world.EntityManager.GetComponentData<Citizen>(childEntity);
            var lb = world.EntityManager.GetComponentData<LivingBeing>(childEntity);
            lb.Sex = 1;
            lb.Health = 80f;
            c.LastBirthYear = 0;
            world.EntityManager.SetComponentData(childEntity, c);
            world.EntityManager.SetComponentData(childEntity, lb);

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

        [Test]
        public void EmitsCitizenBornEvent()
        {
            using var world = new SimulationTestWorld();
            var house = world.CreateBuilding("house", new(10, 10), maxWorkers: 0);

            var mother = world.CreateCitizen(age: 25f, home: house);
            var c = world.EntityManager.GetComponentData<Citizen>(mother);
            var lb = world.EntityManager.GetComponentData<LivingBeing>(mother);
            lb.Sex = 1;
            lb.Health = 80f;
            c.LastBirthYear = 0;
            world.EntityManager.SetComponentData(mother, c);
            world.EntityManager.SetComponentData(mother, lb);

            var cal = world.GetCalendarSingletonEntity();
            var calData = world.EntityManager.GetComponentData<CalendarSingleton>(cal);
            calData.Year = 2;
            calData.DayOfSeason = 0;
            world.EntityManager.SetComponentData(cal, calData);
            world.SetTick(24);

            world.UpdateSystem<BirthSystem>();

            // Verify CitizenBorn event was emitted
            var eventEntity = world.GetOrCreateEventBufferEntity();
            var events = world.EntityManager.GetBuffer<SimulationEvent>(eventEntity);
            Assert.That(events.Length, Is.EqualTo(1));
            Assert.That(events[0].Type, Is.EqualTo(EventType.CitizenBorn));
            Assert.That(events[0].Data0, Is.EqualTo(0f));
            Assert.That(events[0].Data1, Is.EqualTo(0f));
        }
    }
}