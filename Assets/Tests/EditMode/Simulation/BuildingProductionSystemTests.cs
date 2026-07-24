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
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10));
            world.AddProductionOrder(hut, "gather_food");

            // Run production for 10 ticks to complete one cycle
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(1), "Should produce 1 food per 10 ticks");
        }

        [Test]
        public void ChopFirewood_ConsumesLogs_ProducesFirewood()
        {
            using var world = new SimulationTestWorld();
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10));
            world.AddToInventory(woodcutter, "logs", 10);
            world.AddProductionOrder(woodcutter, "chop_firewood");

            // Run 10 ticks for one complete cycle (0.1 progress per tick → 1.0)
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(woodcutter);
            int logsLeft = GetItemCount(inventory, "logs");
            int firewood = GetItemCount(inventory, "firewood");

            Assert.That(logsLeft, Is.EqualTo(0), "Should consume all 10 logs (1 per tick × 10 ticks = 1 cycle)");
            Assert.That(firewood, Is.EqualTo(1), "Should produce 1 firewood");
        }

        [Test]
        public void ChopFirewood_Stalls_WhenNoLogs()
        {
            using var world = new SimulationTestWorld();
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10));
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
        public void ProductionStops_WhenAlreadyComplete()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10));
            var queue = world.EntityManager.GetBuffer<ProductionOrder>(hut);
            queue.Add(new ProductionOrder { RecipeId = "gather_food", Progress = 1f });

            world.UpdateSystem<BuildingProductionSystem>();

            // Progress should still be 1.0 (complete orders are skipped)
            var updated = world.EntityManager.GetBuffer<ProductionOrder>(hut);
            Assert.That(updated[0].Progress, Is.GreaterThanOrEqualTo(1f));
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

        private static int GetItemCount(DynamicBuffer<InventorySlot> inventory, FixedString32Bytes itemId)
        {
            for (int i = 0; i < inventory.Length; i++)
                if (inventory[i].ItemId == itemId)
                    return inventory[i].Quantity;
            return 0;
        }
    }
}