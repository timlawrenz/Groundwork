using Unity.Entities;
using Unity.Burst;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Processes production orders in buildings. Consumes inputs from building inventory,
    /// advances recipe progress, and outputs products when complete.
    /// MVP: logs → firewood at woodcutter; food at gatherer's hut (no inputs needed).
    /// </summary>
    [BurstCompile]
    public partial struct BuildingProductionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();

            foreach (var (building, inventory, productionQueue) in
                     SystemAPI.Query<RefRO<Building>, DynamicBuffer<InventorySlot>, DynamicBuffer<ProductionOrder>>()
                         .WithNone<UnderConstruction>())
            {
                if (!building.ValueRO.IsOperational)
                    continue;

                for (int i = 0; i < productionQueue.Length; i++)
                {
                    var order = productionQueue[i];
                    if (order.Progress >= 1f)
                        continue; // already complete, waiting to be collected

                    // MVP recipes are simple:
                    // "gather_food" — no inputs, produces "food"
                    // "chop_firewood" — consumes "logs", produces "firewood"

                    if (order.RecipeId == "gather_food")
                    {
                        order.Progress += 0.1f; // 10 ticks to complete
                        if (order.Progress >= 1f)
                        {
                            AddToInventory(inventory, "food", 1);
                        }
                    }
                    else if (order.RecipeId == "chop_firewood")
                    {
                        if (TryRemoveFromInventory(inventory, "logs", 1))
                        {
                            order.Progress += 0.1f; // 10 ticks to complete
                            if (order.Progress >= 1f)
                            {
                                AddToInventory(inventory, "firewood", 1);
                            }
                        }
                        // else: no logs available, stalls
                    }

                    productionQueue[i] = order;
                }
            }
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

        private static bool TryRemoveFromInventory(DynamicBuffer<InventorySlot> inventory, FixedString32Bytes itemId, int quantity)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemId == itemId && inventory[i].Quantity >= quantity)
                {
                    var slot = inventory[i];
                    slot.Quantity -= quantity;
                    inventory[i] = slot;
                    return true;
                }
            }
            return false;
        }
    }
}
