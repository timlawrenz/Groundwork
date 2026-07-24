using NUnit.Framework;
using Unity.Entities;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class InventoryDiagnosticTests
    {
        [Test]
        public void AddAndRead_InventoryBuffer()
        {
            using var world = new SimulationTestWorld();
            var building = world.CreateBuilding("woodcutter", new(10, 10));
            world.AddToInventory(building, "logs", 10);

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(building);
            Assert.That(inventory.Length, Is.EqualTo(1));
            Assert.That(inventory[0].ItemId.ToString(), Is.EqualTo("logs"));
            Assert.That(inventory[0].Quantity, Is.EqualTo(10));
        }

        [Test]
        public void Remove_ModifiesBufferCorrectly()
        {
            using var world = new SimulationTestWorld();
            var building = world.CreateBuilding("woodcutter", new(10, 10));
            world.AddToInventory(building, "logs", 10);

            var inventory = world.EntityManager.GetBuffer<InventorySlot>(building);

            // Simulate TryRemoveFromInventory
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemId == new FixedString32Bytes("logs") && inventory[i].Quantity >= 1)
                {
                    var slot = inventory[i];
                    slot.Quantity -= 1;
                    inventory[i] = slot;
                    break;
                }
            }

            Assert.That(inventory[0].Quantity, Is.EqualTo(9));
        }
    }
}
