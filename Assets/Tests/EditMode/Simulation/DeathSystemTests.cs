using NUnit.Framework;
using Unity.Entities;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class DeathSystemTests
    {
        [Test]
        public void DestroysDeadCitizens()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f);
            world.EntityManager.AddComponent<Dead>(citizen);

            Assert.That(world.EntityManager.Exists(citizen), Is.True,
                "Should exist before DeathSystem runs");

            world.UpdateSystem<DeathSystem>();

            Assert.That(world.EntityManager.Exists(citizen), Is.False,
                "Dead citizens should be destroyed");
        }

        [Test]
        public void DoesNotDestroy_LivingCitizens()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f);
            Assert.That(world.EntityManager.HasComponent<Dead>(citizen), Is.False);

            world.UpdateSystem<DeathSystem>();

            Assert.That(world.EntityManager.Exists(citizen), Is.True,
                "Living citizens should not be destroyed");
        }

        [Test]
        public void DestroysMultipleDeadCitizens()
        {
            using var world = new SimulationTestWorld();
            var c1 = world.CreateCitizen(age: 30f);
            var c2 = world.CreateCitizen(age: 40f);
            world.EntityManager.AddComponent<Dead>(c1);
            world.EntityManager.AddComponent<Dead>(c2);

            world.UpdateSystem<DeathSystem>();

            Assert.That(world.EntityManager.Exists(c1), Is.False);
            Assert.That(world.EntityManager.Exists(c2), Is.False);
        }
    }
}