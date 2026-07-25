using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Groundwork.TestHelpers;
using Groundwork.Simulation;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class CitizenHaulSystemTests
    {
        [Test]
        public void AssignsHaulJob_WhenSurplusAndDeficitExist()
        {
            using var world = new SimulationTestWorld();

            // Woodcutter with surplus firewood (above 50 threshold)
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            var wcInv = world.EntityManager.GetBuffer<OutputSlot>(woodcutter);
            wcInv.Add(new OutputSlot { ItemId = "firewood", Quantity = 80 });

            // House with deficit (below 20 threshold)
            var house = world.CreateBuilding("house", new(5, 5), maxWorkers: 0);
            var houseInv = world.EntityManager.GetBuffer<OutputSlot>(house);
            houseInv.Add(new OutputSlot { ItemId = "firewood", Quantity = 5 });

            // Idle citizen
            var citizen = world.CreateCitizen(age: 30f, position: new int2(10, 10));
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "idle",
                TargetEntity = Entity.Null,
                Progress = 0f,
            });

            world.UpdateSystem<CitizenHaulSystem>();

            // Should have HaulTask assigned
            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen),
                "Should assign HaulTask when surplus and deficit exist");
            var haul = world.EntityManager.GetComponentData<HaulTask>(citizen);
            Assert.That(haul.ItemId.ToString(), Is.EqualTo("firewood"));
            Assert.That(haul.SourceBuilding, Is.EqualTo(woodcutter));
            Assert.That(haul.DestinationBuilding, Is.EqualTo(house));
            Assert.That(haul.Phase, Is.EqualTo(0));

            // Should have PathRequest to source
            Assert.That(world.EntityManager.HasComponent<PathRequest>(citizen),
                "Should issue PathRequest to source building");

            // Task should be "hauling"
            var task = world.EntityManager.GetComponentData<CitizenTask>(citizen);
            Assert.That(task.TaskType.ToString(), Is.EqualTo("hauling"));
        }

        [Test]
        public void DoesNotAssign_WhenNoSurplus()
        {
            using var world = new SimulationTestWorld();

            // Woodcutter with low firewood (below 10 threshold = no surplus)
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            var wcInv = world.EntityManager.GetBuffer<OutputSlot>(woodcutter);
            wcInv.Add(new OutputSlot { ItemId = "firewood", Quantity = 5 });

            // House with deficit
            var house = world.CreateBuilding("house", new(5, 5), maxWorkers: 0);
            var houseInv = world.EntityManager.GetBuffer<OutputSlot>(house);
            houseInv.Add(new OutputSlot { ItemId = "firewood", Quantity = 5 });

            var citizen = world.CreateCitizen(age: 30f, position: new int2(10, 10));
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "idle",
                TargetEntity = Entity.Null,
                Progress = 0f,
            });

            world.UpdateSystem<CitizenHaulSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False,
                "Should not assign HaulTask when no building has surplus");
        }

        [Test]
        public void DoesNotAssign_WhenNoDeficit()
        {
            using var world = new SimulationTestWorld();

            // Woodcutter with surplus
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            var wcInv = world.EntityManager.GetBuffer<OutputSlot>(woodcutter);
            wcInv.Add(new OutputSlot { ItemId = "firewood", Quantity = 80 });

            // House with plenty (above 20 threshold = no deficit)
            var house = world.CreateBuilding("house", new(5, 5), maxWorkers: 0);
            var houseInv = world.EntityManager.GetBuffer<OutputSlot>(house);
            houseInv.Add(new OutputSlot { ItemId = "firewood", Quantity = 40 });

            var citizen = world.CreateCitizen(age: 30f, position: new int2(10, 10));
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "idle",
                TargetEntity = Entity.Null,
                Progress = 0f,
            });

            world.UpdateSystem<CitizenHaulSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False,
                "Should not assign HaulTask when no building has deficit");
        }

        [Test]
        public void DoesNotAssign_WhenCitizenIsChild()
        {
            using var world = new SimulationTestWorld();

            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(woodcutter).Add(
                new OutputSlot { ItemId = "firewood", Quantity = 80 });
            var house = world.CreateBuilding("house", new(5, 5), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(house).Add(
                new OutputSlot { ItemId = "firewood", Quantity = 5 });

            // Child citizen (age 10)
            var child = world.CreateCitizen(age: 10f, position: new int2(10, 10));
            world.EntityManager.SetComponentData(child, new CitizenTask
            {
                TaskType = "idle",
                TargetEntity = Entity.Null,
                Progress = 0f,
            });

            world.UpdateSystem<CitizenHaulSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(child), Is.False,
                "Should not assign HaulTask to children");
        }

        [Test]
        public void DoesNotAssign_WhenCitizenHasUrgentNeeds()
        {
            using var world = new SimulationTestWorld();

            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(woodcutter).Add(
                new OutputSlot { ItemId = "firewood", Quantity = 80 });
            var house = world.CreateBuilding("house", new(5, 5), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(house).Add(
                new OutputSlot { ItemId = "firewood", Quantity = 5 });

            var citizen = world.CreateCitizen(age: 30f, position: new int2(10, 10));
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "idle",
                TargetEntity = Entity.Null,
                Progress = 0f,
            });

            // Set urgent needs
            var needs = world.EntityManager.GetBuffer<CitizenNeed>(citizen);
            for (int i = 0; i < needs.Length; i++)
            {
                var need = needs[i];
                need.Urgency = 0.9f; // critical
                needs[i] = need;
            }

            world.UpdateSystem<CitizenHaulSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False,
                "Should not assign HaulTask when citizen has urgent needs");
        }

        [Test]
        public void DoesNotAssign_WhenCitizenAlreadyBusy()
        {
            using var world = new SimulationTestWorld();

            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(woodcutter).Add(
                new OutputSlot { ItemId = "firewood", Quantity = 80 });
            var house = world.CreateBuilding("house", new(5, 5), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(house).Add(
                new OutputSlot { ItemId = "firewood", Quantity = 5 });

            // Citizen with active task (not idle)
            var citizen = world.CreateCitizen(age: 30f, position: new int2(10, 10));
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "walking",
                TargetEntity = house,
                Progress = 0f,
            });

            world.UpdateSystem<CitizenHaulSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False,
                "Should not assign HaulTask when citizen is not idle");
        }

        [Test]
        public void DoesNotAssign_WhenSurplusAndDeficitAreDifferentItems()
        {
            using var world = new SimulationTestWorld();

            // Surplus of food, but deficit of firewood — no match
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(hut).Add(
                new OutputSlot { ItemId = "food", Quantity = 80 });

            var house = world.CreateBuilding("house", new(5, 5), maxWorkers: 0);
            var houseInv = world.EntityManager.GetBuffer<OutputSlot>(house);
            houseInv.Add(new OutputSlot { ItemId = "firewood", Quantity = 5 });

            var citizen = world.CreateCitizen(age: 30f, position: new int2(10, 10));
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "idle",
                TargetEntity = Entity.Null,
                Progress = 0f,
            });

            world.UpdateSystem<CitizenHaulSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False,
                "Should not assign HaulTask when surplus and deficit are different items");
        }

        [Test]
        public void DoesNotAssign_WhenCitizenAlreadyHasPathRequest()
        {
            using var world = new SimulationTestWorld();

            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(woodcutter).Add(
                new OutputSlot { ItemId = "firewood", Quantity = 80 });
            var house = world.CreateBuilding("house", new(5, 5), maxWorkers: 0);
            world.EntityManager.GetBuffer<OutputSlot>(house).Add(
                new OutputSlot { ItemId = "firewood", Quantity = 5 });

            // Citizen with existing PathRequest (already walking somewhere)
            var citizen = world.CreateCitizen(age: 30f, position: new int2(10, 10));
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "idle",
                TargetEntity = Entity.Null,
                Progress = 0f,
            });
            world.EntityManager.AddComponent<PathRequest>(citizen);
            world.EntityManager.SetComponentData(citizen,
                new PathRequest { Destination = new int2(15, 15) });

            world.UpdateSystem<CitizenHaulSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False,
                "Should not assign HaulTask when citizen already has PathRequest");
        }
    }
}