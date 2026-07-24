using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Processes production orders in buildings. Consumes inputs from building inventory,
    /// advances recipe progress, and outputs products when complete.
    /// MVP: logs → firewood at woodcutter; food at gatherer's hut (no inputs needed).
    /// </summary>
    public partial struct BuildingProductionSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadWrite<InventorySlot>(),
                ComponentType.ReadWrite<ProductionOrder>(),
                ComponentType.Exclude<UnderConstruction>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var buildings = _query.ToComponentDataArray<Building>(Allocator.Temp);
            var entities = _query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (!buildings[i].IsOperational)
                    continue;

                var inventory = state.EntityManager.GetBuffer<InventorySlot>(entities[i]);
                var productionQueue = state.EntityManager.GetBuffer<ProductionOrder>(entities[i]);

                for (int j = 0; j < productionQueue.Length; j++)
                {
                    var order = productionQueue[j];

                    if (order.RecipeId == "gather_food")
                    {
                        order.Progress += 0.1f;
                        if (order.Progress >= 1f)
                        {
                            AddToInventory(inventory, "food", 1);
                            order.Progress = 0f;  // reset for next cycle
                        }
                    }
                    else if (order.RecipeId == "chop_firewood")
                    {
                        bool consumed = false;
                        for (int k = 0; k < inventory.Length; k++)
                        {
                            var slot = inventory[k];
                            if (slot.ItemId == new FixedString32Bytes("logs") && slot.Quantity >= 1)
                            {
                                slot.Quantity -= 1;
                                inventory[k] = slot;
                                consumed = true;
                                break;
                            }
                        }
                        if (consumed)
                        {
                            order.Progress += 0.1f;
                            if (order.Progress >= 1f)
                            {
                                AddToInventory(inventory, "firewood", 1);
                                order.Progress = 0f;  // reset for next cycle
                            }
                        }
                    }

                    productionQueue[j] = order;
                }
            }

            buildings.Dispose();
            entities.Dispose();
        }

        private static void AddToInventory(DynamicBuffer<InventorySlot> inventory, FixedString32Bytes itemId, int quantity)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemId == itemId)
                {
                    var slot = inventory[i];
                    slot.Quantity += quantity;
                    inventory[i] = slot;
                    return;
                }
            }
            inventory.Add(new InventorySlot { ItemId = itemId, Quantity = quantity });
        }
    }
}
