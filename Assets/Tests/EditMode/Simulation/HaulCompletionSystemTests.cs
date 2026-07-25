using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Groundwork.TestHelpers;
using Groundwork.Simulation;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class HaulCompletionSystemTests
    {
        // ─── Phase 0: Pickup at source ───

        [Test]
        public void PicksUpGoodsFromSource_WhenArrivedAtSource()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var housePos = new int2(5, 5);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 1);
            var house = world.CreateBuilding("house", housePos, maxWorkers: 0);

            world.EntityManager.GetBuffer<OutputSlot>(hut).Add(
                new OutputSlot { ItemId = "food", Quantity = 30 });

            var citizen = world.CreateCitizen(age: 30f, position: hutPos);
            world.EntityManager.GetBuffer<PathFollowing>(citizen).Clear();

            world.EntityManager.AddComponent<HaulTask>(citizen);
            world.EntityManager.SetComponentData(citizen, new HaulTask
            {
                SourceBuilding = hut,
                DestinationBuilding = house,
                ItemId = "food",
                Quantity = 10,
                Phase = 0,
            });
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "hauling",
                TargetEntity = hut,
                Progress = 0f,
            });

            world.UpdateSystem<HaulCompletionSystem>();

            var hutInv = world.EntityManager.GetBuffer<OutputSlot>(hut);
            int foodLeft = 0;
            for (int i = 0; i < hutInv.Length; i++)
                if (hutInv[i].ItemId == "food")
                    foodLeft = hutInv[i].Quantity;
            Assert.That(foodLeft, Is.EqualTo(20), "10 food should be taken from source");

            var haul = world.EntityManager.GetComponentData<HaulTask>(citizen);
            Assert.That(haul.Phase, Is.EqualTo(1), "Phase should advance to 1 after pickup");
        }

        [Test]
        public void IssuesPathToDestination_AfterPickup()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var housePos = new int2(5, 5);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 1);
            var house = world.CreateBuilding("house", housePos, maxWorkers: 0);

            world.EntityManager.GetBuffer<OutputSlot>(hut).Add(
                new OutputSlot { ItemId = "food", Quantity = 30 });

            var citizen = world.CreateCitizen(age: 30f, position: hutPos);
            world.EntityManager.GetBuffer<PathFollowing>(citizen).Clear();

            world.EntityManager.AddComponent<HaulTask>(citizen);
            world.EntityManager.SetComponentData(citizen, new HaulTask
            {
                SourceBuilding = hut,
                DestinationBuilding = house,
                ItemId = "food",
                Quantity = 10,
                Phase = 0,
            });
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "hauling",
                TargetEntity = hut,
                Progress = 0f,
            });

            world.UpdateSystem<HaulCompletionSystem>();

            Assert.That(world.EntityManager.HasComponent<PathRequest>(citizen),
                "Should have PathRequest to destination after pickup");
            var pathReq = world.EntityManager.GetComponentData<PathRequest>(citizen);
            Assert.That(pathReq.Destination, Is.EqualTo(housePos));
        }

        [Test]
        public void GoesIdle_WhenSourceHasNoGoods()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var housePos = new int2(5, 5);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 1);
            var house = world.CreateBuilding("house", housePos, maxWorkers: 0);

            var citizen = world.CreateCitizen(age: 30f, position: hutPos);
            world.EntityManager.GetBuffer<PathFollowing>(citizen).Clear();

            world.EntityManager.AddComponent<HaulTask>(citizen);
            world.EntityManager.SetComponentData(citizen, new HaulTask
            {
                SourceBuilding = hut,
                DestinationBuilding = house,
                ItemId = "food",
                Quantity = 10,
                Phase = 0,
            });
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "hauling",
                TargetEntity = hut,
                Progress = 0f,
            });

            world.UpdateSystem<HaulCompletionSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False,
                "HaulTask should be removed when source has no goods");
            var task = world.EntityManager.GetComponentData<CitizenTask>(citizen);
            Assert.That(task.TaskType.ToString(), Is.EqualTo("idle"));
        }

        // ─── Phase 1: Dropoff at destination ───

        [Test]
        public void DeliversGoodsToDestination_WhenArrived()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var housePos = new int2(5, 5);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 1);
            var house = world.CreateBuilding("house", housePos, maxWorkers: 0);

            world.EntityManager.GetBuffer<OutputSlot>(house).Add(
                new OutputSlot { ItemId = "food", Quantity = 5 });

            var citizen = world.CreateCitizen(age: 30f, position: housePos);
            world.EntityManager.GetBuffer<PathFollowing>(citizen).Clear();

            world.EntityManager.AddComponent<HaulTask>(citizen);
            world.EntityManager.SetComponentData(citizen, new HaulTask
            {
                SourceBuilding = hut,
                DestinationBuilding = house,
                ItemId = "food",
                Quantity = 10,
                Phase = 1,
            });
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "hauling",
                TargetEntity = house,
                Progress = 0f,
            });

            world.UpdateSystem<HaulCompletionSystem>();

            var houseInv = world.EntityManager.GetBuffer<OutputSlot>(house);
            int foodCount = 0;
            for (int i = 0; i < houseInv.Length; i++)
                if (houseInv[i].ItemId == "food")
                    foodCount = houseInv[i].Quantity;
            Assert.That(foodCount, Is.EqualTo(15), "10 food should be delivered to destination");

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False,
                "HaulTask should be removed after delivery");

            var task = world.EntityManager.GetComponentData<CitizenTask>(citizen);
            Assert.That(task.TaskType.ToString(), Is.EqualTo("idle"));
        }

        [Test]
        public void DoesNotDeliver_WhenDestinationFull()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var housePos = new int2(5, 5);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 1);
            var house = world.CreateBuilding("house", housePos, maxWorkers: 0);

            var houseInv = world.EntityManager.GetBuffer<OutputSlot>(house);
            houseInv.Add(new OutputSlot { ItemId = "food", Quantity = 200 });

            var citizen = world.CreateCitizen(age: 30f, position: housePos);
            world.EntityManager.GetBuffer<PathFollowing>(citizen).Clear();

            world.EntityManager.AddComponent<HaulTask>(citizen);
            world.EntityManager.SetComponentData(citizen, new HaulTask
            {
                SourceBuilding = hut,
                DestinationBuilding = house,
                ItemId = "food",
                Quantity = 10,
                Phase = 1,
            });
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "hauling",
                TargetEntity = house,
                Progress = 0f,
            });

            world.UpdateSystem<HaulCompletionSystem>();

            // Re-get buffer after system run to avoid stale handle
            var houseInvAfter = world.EntityManager.GetBuffer<OutputSlot>(house);
            int foodCount = 0;
            for (int i = 0; i < houseInvAfter.Length; i++)
                if (houseInvAfter[i].ItemId == "food")
                    foodCount = houseInvAfter[i].Quantity;
            Assert.That(foodCount, Is.EqualTo(200), "Food should not increase when destination is full");

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.False);
            var task = world.EntityManager.GetComponentData<CitizenTask>(citizen);
            Assert.That(task.TaskType.ToString(), Is.EqualTo("idle"));
        }

        [Test]
        public void DoesNotDeliver_WhenHaulerStillMoving()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var housePos = new int2(5, 5);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 1);
            var house = world.CreateBuilding("house", housePos, maxWorkers: 0);

            var citizen = world.CreateCitizen(age: 30f, position: new int2(7, 7));
            var pathBuffer = world.EntityManager.GetBuffer<PathFollowing>(citizen);
            pathBuffer.Add(new PathFollowing { TileCoordinate = new int2(6, 7) });

            world.EntityManager.AddComponent<HaulTask>(citizen);
            world.EntityManager.SetComponentData(citizen, new HaulTask
            {
                SourceBuilding = hut,
                DestinationBuilding = house,
                ItemId = "food",
                Quantity = 10,
                Phase = 0,
            });
            world.EntityManager.SetComponentData(citizen, new CitizenTask
            {
                TaskType = "hauling",
                TargetEntity = hut,
                Progress = 0f,
            });

            world.UpdateSystem<HaulCompletionSystem>();

            Assert.That(world.EntityManager.HasComponent<HaulTask>(citizen), Is.True,
                "HaulTask should remain when citizen is still en route");
        }
    }
}