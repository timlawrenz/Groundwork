using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Handles pickup/dropoff when a hauling citizen arrives at their destination.
    /// Phase 0 → arrived at source: pick up goods, issue path to destination.
    /// Phase 1 → arrived at destination: drop off goods, remove HaulTask, set idle.
    /// Runs after CitizenMovementSystem, before BuildingProductionSystem (or Death).
    /// Part of ADR 2026-07-25 §2 — Citizen-Driven Goods Transport.
    /// </summary>
    public partial struct HaulCompletionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (task, haul, pathBuffer, entity) in
                     SystemAPI.Query<RefRW<CitizenTask>, RefRW<HaulTask>, DynamicBuffer<PathFollowing>>()
                         .WithAll<Citizen>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                // Only process when citizen has arrived (no waypoints remaining)
                if (pathBuffer.Length > 0)
                    continue;

                if (haul.ValueRO.Phase == 0)
                {
                    // ─── Arrived at source — pick up goods ───
                    var srcInv = state.EntityManager.GetBuffer<OutputSlot>(haul.ValueRO.SourceBuilding);
                    int taken = TakeFromInventory(srcInv, haul.ValueRO.ItemId, haul.ValueRO.Quantity);
                    if (taken > 0)
                    {
                        haul.ValueRW.Phase = 1;

                        // Issue path to destination
                        if (state.EntityManager.HasComponent<MapPosition>(haul.ValueRO.DestinationBuilding))
                        {
                            var destPos = state.EntityManager.GetComponentData<MapPosition>(
                                haul.ValueRO.DestinationBuilding);
                            ecb.AddComponent(entity, new PathRequest { Destination = destPos.TileCoordinate });
                        }

                        task.ValueRW.TargetEntity = haul.ValueRO.DestinationBuilding;
                    }
                    else
                    {
                        // Source ran out — cancel haul
                        ecb.RemoveComponent<HaulTask>(entity);
                        task.ValueRW.TaskType = "idle";
                        task.ValueRW.TargetEntity = Entity.Null;
                    }
                }
                else
                {
                    // ─── Arrived at destination — drop off goods ───
                    var destInv = state.EntityManager.GetBuffer<OutputSlot>(haul.ValueRO.DestinationBuilding);
                    AddToInventory(destInv, haul.ValueRO.ItemId, haul.ValueRO.Quantity);

                    // Haul complete
                    ecb.RemoveComponent<HaulTask>(entity);
                    task.ValueRW.TaskType = "idle";
                    task.ValueRW.TargetEntity = Entity.Null;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static int TakeFromInventory(DynamicBuffer<OutputSlot> inventory, FixedString32Bytes itemId, int amount)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemId != itemId || inventory[i].Quantity <= 0)
                    continue;
                int taken = math.min(inventory[i].Quantity, amount);
                var slot = inventory[i];
                slot.Quantity -= taken;
                inventory[i] = slot;
                return taken;
            }
            return 0;
        }

        private static void AddToInventory(DynamicBuffer<OutputSlot> inventory, FixedString32Bytes itemId, int quantity)
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
            inventory.Add(new OutputSlot { ItemId = itemId, Quantity = quantity });
        }
    }
}