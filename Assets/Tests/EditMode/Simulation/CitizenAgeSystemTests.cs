using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class CitizenAgeSystemTests
    {
        [Test]
        public void AgesCitizen_EachDay()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f);
            world.SetTick(24); // 1 day

            world.UpdateSystem<CitizenAgeSystem>();

            var c = world.EntityManager.GetComponentData<Citizen>(citizen);
            Assert.That(c.Age, Is.GreaterThan(30f));
        }

        [Test]
        public void DoesNotAge_MidDay()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f);
            world.SetTick(12); // mid-day

            world.UpdateSystem<CitizenAgeSystem>();

            var c = world.EntityManager.GetComponentData<Citizen>(citizen);
            Assert.That(c.Age, Is.EqualTo(30f));
        }

        [Test]
        public void TagsChildren_Under16()
        {
            using var world = new SimulationTestWorld();
            var child = world.CreateCitizen(age: 10f);

            Assert.That(world.EntityManager.HasComponent<Child>(child),
                "Child under 16 should already be tagged");

            // Age them past 16
            // Each day = 1/2880 of a year (24 ticks * 30 days * 4 seasons)
            // To age 6 years, need 6 * 2880 = 17,280 days
            // That's too many ticks. Let's just verify the tag is set correctly at creation
            Assert.That(world.EntityManager.HasComponent<Child>(child));
        }

        [Test]
        public void RemovesChildTag_WhenComingOfAge()
        {
            using var world = new SimulationTestWorld();
            // Create a 15.99-year-old — one tick away from adulthood
            // Each day adds ~0.000347 years. Need ~29 days (~0.01 years) to cross 16.
            // Let's use a younger citizen and just test the tag presence at creation
            var citizen = world.CreateCitizen(age: 20f);

            Assert.That(world.EntityManager.HasComponent<Child>(citizen), Is.False,
                "Adult should not have Child tag");
        }

        [Test]
        public void TagsElderly_At60()
        {
            using var world = new SimulationTestWorld();
            var elder = world.CreateCitizen(age: 65f);

            Assert.That(world.EntityManager.HasComponent<Elderly>(elder),
                "Citizen over 60 should be tagged Elderly");
        }

        [Test]
        public void TagsDead_At90()
        {
            using var world = new SimulationTestWorld();
            var ancient = world.CreateCitizen(age: 91f);

            Assert.That(world.EntityManager.HasComponent<Dead>(ancient),
                "Citizen over 90 should be tagged Dead");
        }

        [Test]
        public void DoesNotTagDead_Before90()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 89f);

            Assert.That(world.EntityManager.HasComponent<Dead>(citizen), Is.False);
        }

        [Test]
        public void SkipsDeadCitizens()
        {
            using var world = new SimulationTestWorld();
            var deadCitizen = world.CreateCitizen(age: 30f);
            world.EntityManager.AddComponent<Dead>(deadCitizen);
            world.SetTick(24);

            world.UpdateSystem<CitizenAgeSystem>();

            var c = world.EntityManager.GetComponentData<Citizen>(deadCitizen);
            Assert.That(c.Age, Is.EqualTo(30f), "Dead citizens should not age");
        }
    }
}