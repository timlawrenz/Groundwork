using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

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
            foreach (var (building, entity) in
                     SystemAPI.Query<RefRO<Building>>()
                         .WithNone<UnderConstruction>()
                         .WithAll<InventorySlot>()
                         .WithAll<ProductionOrder>()
                         .WithEntityAccess())
            {
                if (!building.ValueRO.IsOperational)
                    continue;

                var inventory = SystemAPI.GetBuffer<InventorySlot>(entity);
                var productionQueue = SystemAPI.GetBuffer<ProductionOrder>(entity);

                for (int i = 0; i < productionQueue.Length; i++)
                {
                    var order = productionQueue[i];
                    if (order.Progress >= 1f)
                        continue;

                    if (order.RecipeId == "gather_food")
                    {
                        order.Progress += 0.1f;
                        if (order.Progress >= 1f)
                            AddToInventory(inventory, "food", 1);
                    }
                    else if (order.RecipeId == "chop_firewood")
                    {
                        if (TryRemoveFromInventory(inventory, "logs", 1))
                        {
                            order.Progress += 0.1f;
                            if (order.Progress >= 1f)
                                AddToInventory(inventory, "firewood", 1);
                        }
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