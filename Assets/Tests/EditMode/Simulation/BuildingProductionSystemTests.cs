using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class BuildingProductionSystemTests
    {
        [Test]
        public void GatherFood_ProducesFood_WithoutInputs()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 0); // autonomous for test
            world.AddProductionOrder(hut, "gather_food");

            // Run production for 10 ticks to complete one cycle
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(10), "Should produce 10 food per 10 ticks (TicksPerCycle=1)");
        }

        [Test]
        public void ChopFirewood_ConsumesLogs_ProducesFirewood()
        {
            using var world = new SimulationTestWorld();
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            world.AddToInventory(woodcutter, "logs", 10);
            world.AddProductionOrder(woodcutter, "chop_firewood");

            // Run 10 ticks for one complete cycle (0.1 progress per tick → 1.0)
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(woodcutter);
            int logsLeft = GetItemCount(inventory, "logs");
            int firewood = GetItemCount(inventory, "firewood");

            Assert.That(logsLeft, Is.EqualTo(0), "Should consume all 10 logs (1 per tick × 10 ticks = 1 cycle)");
            Assert.That(firewood, Is.EqualTo(10), "Should produce 10 firewood (TicksPerCycle=1)");
        }

        [Test]
        public void ChopFirewood_Stalls_WhenNoLogs()
        {
            using var world = new SimulationTestWorld();
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            // No logs in inventory
            world.AddProductionOrder(woodcutter, "chop_firewood");

            // Run 10 ticks
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(woodcutter);
            int firewood = GetItemCount(inventory, "firewood");
            Assert.That(firewood, Is.EqualTo(0), "Should not produce without logs");
        }

        [Test]
        public void CompletedOrder_ResetsProgress_ForNextCycle()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 0);
            var queue = world.EntityManager.GetBuffer<ProductionOrder>(hut);
            queue.Add(new ProductionOrder { RecipeId = "gather_food", Progress = 1f });

            world.UpdateSystem<BuildingProductionSystem>();

            // Progress resets to 0 after completion so the recipe can cycle again
            var updated = world.EntityManager.GetBuffer<ProductionOrder>(hut);
            Assert.That(updated[0].Progress, Is.EqualTo(0f));
        }

        // ─── Regression: continuous production ───

        [Test]
        public void ChopFirewood_ProducesMultipleCycles_WhenEnoughLogs()
        {
            using var world = new SimulationTestWorld();
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            world.AddToInventory(woodcutter, "logs", 20);  // enough for 2 cycles
            world.AddProductionOrder(woodcutter, "chop_firewood");

            // Run 20 ticks (2 complete cycles: 10 ticks each)
            for (int i = 0; i < 20; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(woodcutter);
            int logsLeft = GetItemCount(inventory, "logs");
            int firewood = GetItemCount(inventory, "firewood");

            Assert.That(logsLeft, Is.EqualTo(0), "Should consume all 20 logs");
            Assert.That(firewood, Is.EqualTo(20), "Should produce 20 firewood over 20 ticks (TicksPerCycle=1)");
        }

        [Test]
        public void DoesNotOperate_UnderConstruction()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), operational: false);
            world.EntityManager.AddComponent<UnderConstruction>(hut);
            world.AddProductionOrder(hut, "gather_food");

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(0), "Under construction should not produce");
        }

        // ─── Regression: worker requirement ───

        [Test]
        public void RequiresWorkers_WhenMaxWorkersGreaterThanZero()
        {
            using var world = new SimulationTestWorld();

            // Building with MaxWorkers=3 but no citizens assigned
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 3);
            world.AddProductionOrder(hut, "gather_food");

            // Run 10 ticks
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(0),
                "Should not produce without workers when MaxWorkers > 0");
        }

        [Test]
        public void Produces_WhenWorkersAssigned()
        {
            using var world = new SimulationTestWorld();

            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 3);
            world.AddProductionOrder(hut, "gather_food");

            // Assign a citizen as worker
            var citizen = world.CreateCitizen(age: 30f, workplace: hut);

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.GreaterThan(0),
                "Should produce when at least one worker is assigned");
        }

        [Test]
        public void AutonomousBuilding_ProducesWithoutWorkers_WhenMaxWorkersZero()
        {
            using var world = new SimulationTestWorld();

            // MaxWorkers=0 means autonomous (e.g. a well, windmill)
            var well = world.CreateBuilding("well", new(10, 10), maxWorkers: 0);
            world.AddProductionOrder(well, "gather_food"); // well produces water

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(well);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.GreaterThan(0),
                "Autonomous buildings (MaxWorkers=0) should produce without workers");
        }

        [Test]
        public void EmitsProductionCompleteEvent()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 0);
            world.AddProductionOrder(hut, "gather_food");

            // Run enough ticks to complete one cycle (10 ticks at 0.1/tick = 1.0)
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var eventEntity = world.GetOrCreateEventBufferEntity();
            var events = world.EntityManager.GetBuffer<SimulationEvent>(eventEntity);
            Assert.That(events.Length, Is.EqualTo(10));  // TicksPerCycle=1 produces 10 events in 10 ticks
            Assert.That(events[0].Type, Is.EqualTo(EventType.ProductionComplete));
            Assert.That(events[0].EntityId, Is.EqualTo(hut.Index));
        }

        private static int GetItemCount(DynamicBuffer<InventorySlot> inventory, FixedString32Bytes itemId)
        {
            for (int i = 0; i < inventory.Length; i++)
                if (inventory[i].ItemId == itemId)
                    return inventory[i].Quantity;
            return 0;
        }
    }
}