using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Handles pickup/dropoff when a hauling citizen arrives at their destination.
    /// Phase 0 → arrived at source: pick up goods, issue path to destination.
    /// Phase 1 → arrived at destination: drop off goods, remove HaulTask, set idle.
    /// Respects storage capacity — won't deliver to full buildings.
    /// </summary>
    public partial struct HaulCompletionSystem : ISystem
    {
        private EntityQuery _buildingDefQuery;

        public void OnCreate(ref SystemState state)
        {
            _buildingDefQuery = state.GetEntityQuery(typeof(BuildingDefinitionData));
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Build lookup for building capacity
            var bDefs = _buildingDefQuery.ToComponentDataArray<BuildingDefinitionData>(Allocator.Temp);
            var bDefLookup = new NativeHashMap<FixedString32Bytes, int>(bDefs.Length, Allocator.Temp);
            for (int i = 0; i < bDefs.Length; i++)
                if (bDefs[i].OutputCapacity > 0)
                    bDefLookup.TryAdd(bDefs[i].BuildingType, bDefs[i].OutputCapacity);
            bDefs.Dispose();

            foreach (var (task, haul, pathBuffer, entity) in
                     SystemAPI.Query<RefRW<CitizenTask>, RefRW<HaulTask>, DynamicBuffer<PathFollowing>>()
                         .WithAll<Citizen>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                if (pathBuffer.Length > 0)
                    continue;

                if (haul.ValueRO.Phase == 0)
                {
                    // Arrived at source — pick up goods
                    var srcInv = state.EntityManager.GetBuffer<OutputSlot>(haul.ValueRO.SourceBuilding);
                    int taken = TakeFromInventory(srcInv, haul.ValueRO.ItemId, haul.ValueRO.Quantity);
                    if (taken > 0)
                    {
                        haul.ValueRW.Phase = 1;

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
                        ecb.RemoveComponent<HaulTask>(entity);
                        task.ValueRW.TaskType = "idle";
                        task.ValueRW.TargetEntity = Entity.Null;
                    }
                }
                else
                {
                    // Arrived at destination — drop off goods (respect capacity)
                    var destInv = state.EntityManager.GetBuffer<OutputSlot>(haul.ValueRO.DestinationBuilding);

                    bool destFull = false;
                    if (state.EntityManager.HasComponent<Building>(haul.ValueRO.DestinationBuilding))
                    {
                        var destBldg = state.EntityManager.GetComponentData<Building>(haul.ValueRO.DestinationBuilding);
                        if (bDefLookup.TryGetValue(destBldg.BuildingType, out int maxCapacity))
                        {
                            if (CountItems(destInv) >= maxCapacity)
                                destFull = true;
                        }
                    }

                    if (!destFull)
                        AddToInventory(destInv, haul.ValueRO.ItemId, haul.ValueRO.Quantity);

                    ecb.RemoveComponent<HaulTask>(entity);
                    task.ValueRW.TaskType = "idle";
                    task.ValueRW.TargetEntity = Entity.Null;
                }
            }

            bDefLookup.Dispose();
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

        private static int CountItems(DynamicBuffer<OutputSlot> inventory)
        {
            int total = 0;
            for (int i = 0; i < inventory.Length; i++)
                total += inventory[i].Quantity;
            return total;
        }
    }
}