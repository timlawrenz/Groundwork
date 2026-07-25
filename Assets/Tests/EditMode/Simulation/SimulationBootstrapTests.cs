using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class SimulationBootstrapTests
    {
        [Test]
        public void CreatesCorrectNumberOfCitizens()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            int citizenCount = world.EntityManager
                .CreateEntityQuery(typeof(Citizen))
                .CalculateEntityCount();

            Assert.That(citizenCount, Is.EqualTo(50));
        }

        [Test]
        public void CreatesCorrectNumberOfBuildings()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            int buildingCount = world.EntityManager
                .CreateEntityQuery(typeof(Building))
                .CalculateEntityCount();

            Assert.That(buildingCount, Is.EqualTo(20), "8 houses + 9 gatherer huts + 3 woodcutters");
        }

        [Test]
        public void CreatesCalendarSingleton()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            var cal = world.EntityManager
                .CreateEntityQuery(typeof(CalendarSingleton))
                .GetSingleton<CalendarSingleton>();

            Assert.That(cal.Year, Is.EqualTo(1));
            Assert.That(cal.Season, Is.EqualTo(0)); // spring
        }

        [Test]
        public void CreatesSimulationConfig()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            var config = world.EntityManager
                .CreateEntityQuery(typeof(SimulationConfig))
                .GetSingleton<SimulationConfig>();

            Assert.That(config.TicksPerDay, Is.EqualTo(24));
            Assert.That(config.TickSpeed, Is.EqualTo(1f));
        }

        [Test]
        public void CreatesMapGrid()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            var gridData = world.EntityManager
                .CreateEntityQuery(typeof(MapGridData))
                .GetSingleton<MapGridData>();

            Assert.That(gridData.Grid.IsCreated, Is.True);
            Assert.That(gridData.Grid.Value.Width, Is.EqualTo(100));
            Assert.That(gridData.Grid.Value.Height, Is.EqualTo(100));
        }

        [Test]
        public void CitizensHaveNeeds()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            var citizens = world.EntityManager
                .CreateEntityQuery(typeof(Citizen), typeof(CitizenNeed))
                .ToEntityArray(Allocator.Temp);

            foreach (var citizen in citizens)
            {
                var needs = world.EntityManager.GetBuffer<CitizenNeed>(citizen);
                Assert.That(needs.Length, Is.GreaterThan(0),
                    $"Citizen should have initialized needs");
            }

            citizens.Dispose();
        }

        [Test]
        public void CitizensHavePathBuffers()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            var citizens = world.EntityManager
                .CreateEntityQuery(typeof(Citizen), typeof(PathFollowing))
                .ToEntityArray(Allocator.Temp);

            Assert.That(citizens.Length, Is.EqualTo(50),
                "All citizens should have PathFollowing buffer");

            citizens.Dispose();
        }

        [Test]
        public void WoodcuttersStartWithLogs()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            int woodcuttersWithLogs = 0;
            var query = world.EntityManager.CreateEntityQuery(
                typeof(Building), typeof(InventorySlot));
            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (buildings[i].BuildingType == "woodcutter")
                {
                    var inventory = world.EntityManager.GetBuffer<InventorySlot>(entities[i]);
                    for (int j = 0; j < inventory.Length; j++)
                        if (inventory[j].ItemId == "logs" && inventory[j].Quantity > 0)
                            woodcuttersWithLogs++;
                }
            }

            buildings.Dispose();
            entities.Dispose();

            Assert.That(woodcuttersWithLogs, Is.EqualTo(3),
                "Both woodcutters should start with logs");
        }

        [Test]
        public void SimulationRuns_WithoutErrors()
        {
            using var world = new SimulationTestWorld();
            world.RunBootstrap();

            // Run 10 full ticks — should not throw
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10; i++)
                    world.RunFullTick();
            });
        }
    }
}