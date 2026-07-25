using NUnit.Framework;
using Unity.Entities;
using Groundwork.Simulation;
using Groundwork.TestHelpers;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class TickDispatchSystemTests
    {
        [Test]
        public void AdvancesCurrentTick_EachUpdate()
        {
            using var world = new SimulationTestWorld();
            world.SetTick(0);

            world.AdvanceTicks(1);

            var config = world.EntityManager.CreateEntityQuery(typeof(SimulationConfig))
                .GetSingleton<SimulationConfig>();
            Assert.That(config.CurrentTick, Is.EqualTo(1));
        }

        [Test]
        public void DoesNotAdvance_WhenPaused()
        {
            using var world = new SimulationTestWorld();

            // Set paused
            var configEntity = world.EntityManager.CreateEntityQuery(typeof(SimulationConfig))
                .GetSingletonEntity();
            var config = world.EntityManager.GetComponentData<SimulationConfig>(configEntity);
            config.TickSpeed = 0f;
            world.EntityManager.SetComponentData(configEntity, config);

            world.AdvanceTicks(1);

            var result = world.EntityManager.CreateEntityQuery(typeof(SimulationConfig))
                .GetSingleton<SimulationConfig>();
            Assert.That(result.CurrentTick, Is.EqualTo(0));
        }

        [Test]
        public void AdvancesMultipleTicks()
        {
            using var world = new SimulationTestWorld();
            world.SetTick(0);

            world.AdvanceTicks(10);

            var config = world.EntityManager.CreateEntityQuery(typeof(SimulationConfig))
                .GetSingleton<SimulationConfig>();
            Assert.That(config.CurrentTick, Is.EqualTo(10));
        }
    }
}